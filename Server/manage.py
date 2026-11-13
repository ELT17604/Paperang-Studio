#!/usr/bin/env python3
"""Local sudo-only curses administration. No HTTP listener or console credentials."""

import argparse
import curses
import datetime
import fcntl
import json
import ipaddress
import os
from pathlib import Path
import pwd
import subprocess
import sys
import textwrap
import time
import tempfile
import urllib.request
import uuid


class AdminBackend:
	"""Trusted local administrator; no additional identity/token is inserted."""

	def __init__(self, store):
		self.store = store
		self.actor = dict(
			id="local-root-console",
			role="C",
			device=store.c["device_id"],
			session=True,
			kind="publickey",
			user_id="local-root-console",
		)

	def call(self, action, body):
		s = self.store
		if action == "restart_check":
			with s.connect() as db:
				if db.execute("SELECT 1 FROM jobs WHERE state IN ('claimed','printing') LIMIT 1").fetchone():
					raise ValueError("An active print must finish before changing service configuration")
			return dict(ready=True)
		if action == "configuration_saved":
			s.api.auth.audit(self.actor, "local_configuration_update", ",".join(body["fields"]))
			return dict(saved=True)
		if action == "summary":
			with s.connect() as db:
				counts = {
					name: db.execute("SELECT count(*) FROM " + name).fetchone()[0]
					for name in ("identities", "credentials", "sessions", "jobs")
				}
				device = db.execute("SELECT * FROM devices WHERE id=?", (s.c["device_id"],)).fetchone()
			counts.update(version=s.version, retention_days=s.c["retention_days"])
			if device:
				value = json.loads(device["status"])
				counts["device"] = {
					k: value.get(k) for k in ("appVersion", "connected", "busy", "_relay_key_id")
				}
				counts["device"]["seconds_since_seen"] = round(time.time() - device["seen"], 1)
			return counts
		if action == "credentials":
			with s.connect() as db:
				rows = db.execute(
					"SELECT c.id,c.user_id,i.name,c.role,c.kind,c.device,c.revoked,c.created "
					"FROM credentials c JOIN identities i ON i.id=c.user_id ORDER BY c.created DESC"
				).fetchall()
			return [dict(r) for r in rows]
		if action == "identities":
			with s.connect() as db:
				return [
					dict(r)
					for r in db.execute(
						"SELECT id,name,anonymous,revoked,created FROM identities ORDER BY created DESC"
					)
				]
		if action == "public_key":
			result = s.api.auth.public_key_add(body["name"], body["public_key"], body.get("id"))
			s.api.auth.audit(self.actor, "local_trust_public_key", result["id"])
			return result
		if action == "history":
			with s.connect() as db:
				rows = db.execute(
					"SELECT rowid AS cursor,id,short_id,state,created,updated FROM jobs "
					"WHERE rowid<? AND (?='' OR id=? OR short_id=?) "
					"ORDER BY rowid DESC LIMIT 25",
					(
						body.get("before", 1e20),
						body.get("id", ""),
						body.get("id", ""),
						body.get("id", "").upper(),
					),
				).fetchall()
			return [dict(r) for r in rows]
		if action == "detail":
			return s.route("POST", "/v2/admin/jobs/export", dict(job_id=body["id"]), self.actor, "local", {})[
				1
			]["jobs"]
		if action == "api":
			path = body["path"]
			if not path.startswith("/v2/admin/"):
				raise ValueError("Only local administrative actions are supported")
			return s.route("POST", path, body.get("body", {}), self.actor, "local", {})[1]
		raise ValueError("Unknown console action")


class Worker:
	def __init__(self, root, config):
		self.root, self.config = root, config
		self.process = subprocess.Popen(
			[
				"runuser",
				"-u",
				"paperang-relay",
				"--",
				sys.executable,
				"-B",
				str(Path(__file__).resolve()),
				"--worker",
				"--root",
				str(root),
				"--config",
				str(config),
			],
			stdin=subprocess.PIPE,
			stdout=subprocess.PIPE,
			text=True,
			bufsize=1,
		)

	def call(self, action, **body):
		self.process.stdin.write(json.dumps(dict(action=action, body=body)) + "\n")
		self.process.stdin.flush()
		line = self.process.stdout.readline()
		if not line:
			raise RuntimeError("Database worker stopped; close and reopen the console")
		result = json.loads(line)
		if "error" in result:
			raise ValueError(result["error"])
		return result["result"]

	def reload(self):
		self.close()
		self.__init__(self.root, self.config)

	def close(self):
		self.process.stdin.close()
		try:
			self.process.wait(timeout=12)
		except subprocess.TimeoutExpired:
			self.process.kill()
			self.process.wait()


class Configuration:
	FIELDS = {
		"allow_anonymous": ("Allow anonymous submissions", bool),
		"anonymous_max_length": ("Anonymous maximum text/row units", int),
		"anonymous_hourly_limit": ("Anonymous hourly jobs per IP", int),
		"anonymous_hourly_chars": ("Anonymous hourly text/row units per IP", int),
		"authenticated_max_length": ("Authenticated maximum text/row units", int),
		"authenticated_hourly_limit": ("Authenticated hourly jobs per IP", int),
		"authenticated_hourly_chars": ("Authenticated hourly units per IP", int),
		"queue_limit": ("Maximum queued/active jobs", int),
		"retention_days": ("History retention days", int),
		"session_days": ("Non-owner session lifetime days", int),
		"global_hourly_limit": ("Global hourly jobs", int),
		"global_hourly_chars": ("Global hourly text/row units", int),
		"trusted_proxies": ("Trusted proxy IP networks", list),
	}

	def __init__(self, path, worker):
		self.path, self.worker = path, worker

	def load(self):
		original = self.path.read_bytes()
		return original, json.loads(original)

	def validate(self, original, draft):
		old = json.loads(original)
		if set(old) != set(draft) or any(draft[k] != old[k] for k in old if k not in self.FIELDS):
			raise ValueError(
				"Deployment paths, bind/port and device identity require coordinated deployment changes"
			)
		for field, (label, kind) in self.FIELDS.items():
			if field not in draft:
				continue
			value = draft[field]
			if type(value) is not kind:
				raise ValueError(label + ": incorrect type")
			if kind is int:
				ceiling = (
					10000
					if field.endswith("max_length")
					else (180 if field == "session_days" else 1000000000)
				)
				if not 1 <= value <= ceiling:
					raise ValueError(label + f": expected 1..{ceiling}")
			if kind is list:
				for network in value:
					if not isinstance(network, str):
						raise ValueError(label + ": expected CIDR strings")
					ipaddress.ip_network(network)

	def write(self, content):
		stat = self.path.stat()
		fd, temporary = tempfile.mkstemp(dir=self.path.parent, prefix=".config-")
		try:
			os.fchown(fd, stat.st_uid, stat.st_gid)
			os.fchmod(fd, 0o640)
			with os.fdopen(fd, "wb") as file:
				file.write(content)
				file.flush()
				os.fsync(file.fileno())
			os.replace(temporary, self.path)
			directory = os.open(self.path.parent, os.O_RDONLY | os.O_DIRECTORY)
			try:
				os.fsync(directory)
			finally:
				os.close(directory)
		finally:
			Path(temporary).unlink(missing_ok=True)

	def control(self, action):
		subprocess.run(["systemctl", action, "paperang-relay"], capture_output=True, text=True, check=True)

	def healthy(self):
		opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
		for _ in range(30):
			try:
				with opener.open("http://127.0.0.1:28177/v2/public/status", timeout=2) as response:
					if response.status == 200:
						return
			except (OSError, ValueError):
				pass
			time.sleep(0.2)
		raise RuntimeError("Relay did not become healthy")

	def apply(self, original, draft):
		self.validate(original, draft)
		old = json.loads(original)
		changed = [k for k in draft if old[k] != draft[k]]
		if not changed:
			return "No changes."
		with self.path.with_name(".config-edit.lock").open("a") as lock:
			fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
			if self.path.read_bytes() != original:
				raise ValueError("Configuration changed elsewhere; reopen the configuration page")
			self.worker.call("restart_check")
			try:
				self.control("stop")
				# Recheck after stopping the receiver endpoint; no new claim can race the write.
				self.worker.call("restart_check")
				self.write((json.dumps(draft, indent=2) + "\n").encode())
				self.control("start")
				self.healthy()
				self.worker.reload()
			except Exception:
				self.write(original)
				self.control("restart")
				self.healthy()
				self.worker.reload()
				raise
		message = "Saved, service restarted and health verified. Windows keys and task data are unchanged."
		try:
			self.worker.call("configuration_saved", fields=changed)
		except ValueError as error:
			message += " Audit entry failed: " + str(error)
		return message


class Console:
	def __init__(self, worker, root, server, config=None):
		self.worker, self.root, self.server = worker, root, server
		self.configuration = Configuration(config or root / "config/config.json", worker)
		self.screen = None

	def draw(self, lines, selected=None, footer="Arrows: select   Enter: open   Esc/Q: back"):
		screen = self.screen
		screen.erase()
		height, width = screen.getmaxyx()
		for index, line in enumerate(lines[: max(0, height - 2)]):
			try:
				screen.addnstr(
					index,
					1,
					str(line),
					max(0, width - 3),
					curses.A_REVERSE if index == selected else curses.A_NORMAL,
				)
			except curses.error:
				pass  # Resizing/wide text can consume the final terminal cell.
		try:
			screen.addnstr(height - 1, 1, footer, max(0, width - 3), curses.A_DIM)
		except curses.error:
			pass
		screen.refresh()

	def menu(self, title, options):
		if not options:
			self.view(title, "No records.")
			return None
		selected = 0
		while True:
			rows = max(1, self.screen.getmaxyx()[0] - 5)
			first = selected // rows * rows
			self.draw(
				[title, ""] + ["  " + str(x) for x in options[first : first + rows]], selected - first + 2
			)
			key = self.screen.getch()
			if key in (27, ord("q")):
				return None
			if key in (curses.KEY_UP, ord("k")):
				selected = (selected - 1) % len(options)
			if key in (curses.KEY_DOWN, ord("j")):
				selected = (selected + 1) % len(options)
			if key == curses.KEY_NPAGE:
				selected = min(len(options) - 1, selected + rows)
			if key == curses.KEY_PPAGE:
				selected = max(0, selected - rows)
			if key in (10, 13, curses.KEY_ENTER):
				return selected

	def view(self, title, value):
		text = value if isinstance(value, str) else json.dumps(value, ensure_ascii=False, indent=2)
		top = 0
		while True:
			height, width = self.screen.getmaxyx()
			lines = [
				chunk
				for line in text.splitlines()
				for chunk in (textwrap.wrap(line, max(1, width - 4), replace_whitespace=False) or [""])
			]
			self.draw(
				[title, ""] + lines[top : top + max(1, height - 5)],
				footer="Up/Down/PgUp/PgDn: scroll   Enter/Esc/Q: back",
			)
			key = self.screen.getch()
			if key in (27, ord("q"), 10, 13, curses.KEY_ENTER):
				return
			if key == curses.KEY_UP:
				top = max(0, top - 1)
			if key == curses.KEY_DOWN:
				top = min(max(0, len(lines) - 1), top + 1)
			if key == curses.KEY_NPAGE:
				top = min(max(0, len(lines) - 1), top + max(1, height - 5))
			if key == curses.KEY_PPAGE:
				top = max(0, top - max(1, height - 5))

	def ask(self, prompt, default="", secret=False):
		value = ""
		curses.curs_set(1)
		try:
			while True:
				self.draw(
					[prompt, "Default: " + default if default else "", "*" * len(value) if secret else value],
					footer="Enter: confirm   Esc: cancel   Backspace: edit",
				)
				key = self.screen.get_wch()
				if key == "\x1b":
					return None
				if key in ("\n", "\r"):
					return value or default
				if key in ("\b", "\x7f", curses.KEY_BACKSPACE):
					value = value[:-1]
				elif isinstance(key, str) and key.isprintable() and len(value) < 4096:
					value += key
		finally:
			curses.curs_set(0)

	def api(self, path, **body):
		return self.worker.call("api", path="/v2/admin/" + path, body=body)

	def save(self, name, data):
		folder = self.root / "admin-exports"
		folder.mkdir(mode=0o700, exist_ok=True)
		path = folder / (name + "-" + uuid.uuid4().hex[:10] + ".json")
		fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
		with os.fdopen(fd, "w") as file:
			json.dump(data, file, ensure_ascii=False, indent=2)
			file.write("\n")
		return path

	def create(self):
		index = self.menu(
			"Create / distribute",
			[
				"Random A/B/C/M key",
				"A/B/C passphrase",
				"Owner private-key connection file",
				"Register existing public PEM",
			],
		)
		if index is None:
			return
		name = self.ask("Display name")
		if not name:
			return
		if index in (2, 3):
			if index == 3:
				path = self.ask("Public PEM file path (never upload a private key)")
				if not path:
					return
				with open(path, "rb") as file:
					pem = file.read(8193).decode()
				if len(pem) > 8192 or "PRIVATE" in pem:
					raise ValueError("Expected a public PEM only")
				result = self.worker.call("public_key", name=name, public_key=pem)
				self.view("Public key registered; client must prove possession", result)
				return
			from cryptography.hazmat.primitives import serialization
			from cryptography.hazmat.primitives.asymmetric import rsa

			key = rsa.generate_private_key(public_exponent=65537, key_size=3072)
			identifier = str(uuid.uuid4())
			bundle = dict(
				server=self.server,
				id=identifier,
				privateKeyPem=key.private_bytes(
					serialization.Encoding.PEM,
					serialization.PrivateFormat.PKCS8,
					serialization.NoEncryption(),
				).decode(),
			)
			# Save the private material before registering the matching public key.
			path = self.save("owner", bundle)
			pem = (
				key.public_key()
				.public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo)
				.decode()
			)
			self.worker.call("public_key", name=name, public_key=pem, id=identifier)
			self.view(
				"Owner connection file created",
				f"{path}\n\nTransfer this 0600 file privately to the client. The key remains pending until the client proves it. Never paste it into chat or Git.",
			)
			return
		roles = ["A", "B", "C", "M"] if index == 0 else ["A", "B", "C"]
		choice = self.menu("Role: A submit; B own status; C management; M execution", roles)
		if choice is None:
			return
		body = dict(name=name, role=roles[choice], kind="token" if index == 0 else "passphrase")
		if index == 1:
			phrase = self.ask("Passphrase (blank: generate)", secret=True)
			if phrase is None:
				return
			if phrase:
				body["password"] = phrase
		existing = self.ask("Existing identity UUID (blank: create new identity)")
		if existing is None:
			return
		if existing:
			body["user_id"] = existing
		result = self.api("keys", **body)
		path = self.save("credential", dict(server=self.server, **result))
		self.view(
			"New secret / distribution file",
			f"Saved root-only: {path}\n\n"
			+ json.dumps(result, indent=2)
			+ "\n\nExisting secrets cannot be recovered. Transfer privately; rotate if lost.",
		)

	def credentials(self):
		while True:
			rows = self.worker.call("credentials")
			choice = self.menu(
				"Credentials (no raw secrets in listings)",
				[
					f'{r["role"]} {r["kind"]} {"REVOKED" if r["revoked"] else "pending proof" if r["kind"]=="pendingkey" else "active"} {r["name"]}  {r["id"]}'
					for r in rows
				],
			)
			if choice is None:
				return
			row = rows[choice]
			action = self.menu(
				row["name"],
				[
					"Details",
					"Rename",
					"Revoke",
					"Rotate / distribute replacement",
					"Revoke sessions",
					"Configure identity quota",
				],
			)
			if action is None:
				continue
			identifier = row["id"]
			if action == 0:
				self.view("Credential metadata", row)
			elif action == 1:
				name = self.ask("New display name", row["name"])
				if name:
					self.api("keys/" + identifier + "/rename", name=name)
			elif action == 5:
				self.quotas("user:" + row["user_id"])
			elif action in (2, 3, 4):
				if action == 3 and row["kind"] in ("publickey", "pendingkey"):
					self.view(
						"Owner key rotation",
						"Create a replacement owner file, connect and prove it on the client, then revoke this old key. Existing private keys cannot be recovered or rotated as bearer tokens.",
					)
					continue
				approved = self.menu("Confirm action for " + identifier, ["Cancel", "Confirm"])
				if approved != 1:
					continue
				if action == 2:
					self.api("keys/" + identifier + "/revoke")
				elif action == 4:
					self.api("sessions/revoke", credential_id=identifier)
				else:
					result = self.api("keys/" + identifier + "/rotate")
					self.view(
						"Replacement saved root-only",
						str(self.save("credential", dict(server=self.server, **result))),
					)

	def identities(self):
		rows = self.worker.call("identities")
		choice = self.menu("Identities", [f'{r["name"]} {r["id"]} revoked={r["revoked"]}' for r in rows])
		if choice is None:
			return
		row = rows[choice]
		action = self.menu(row["name"], ["Details", "Revoke identity and queued jobs"])
		if action == 0:
			self.view("Identity metadata", row)
		elif action == 1 and self.menu("Confirm identity revocation", ["Cancel", "Confirm"]) == 1:
			self.api("identities/" + row["id"] + "/revoke")

	def history(self):
		query = self.ask("Optional short ID or complete job ID (blank: all)")
		if query is None:
			return
		query = query.strip()
		if len(query.replace("-", "")) == 12:
			query = query.replace("-", "").upper()
		before = 1e20
		while True:
			rows = self.worker.call("history", before=before, id=query)
			options = [
				f'{r["short_id"]}  {r["state"]:10}  '
				+ datetime.datetime.fromtimestamp(r["created"]).strftime("%Y-%m-%d %H:%M:%S")
				for r in rows
			]
			options += ["Next page", "Export all history JSONL"]
			choice = self.menu("Print history / complete private records", options)
			if choice is None:
				return
			if choice < len(rows):
				records = self.worker.call("detail", id=rows[choice]["id"])
				self.view("Full task (text / raster1 / metadata / events)", records)
			elif choice == len(rows):
				if rows:
					before = rows[-1]["cursor"]
			else:
				folder = self.root / "admin-exports"
				folder.mkdir(mode=0o700, exist_ok=True)
				path = folder / ("history-" + uuid.uuid4().hex[:10] + ".jsonl")
				fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
				cursor = 1e20
				count = 0
				with os.fdopen(fd, "w") as file:
					while True:
						result = self.api("jobs/export", before=cursor)
						if not result["jobs"]:
							break
						for job in result["jobs"]:
							file.write(json.dumps(job, ensure_ascii=False) + "\n")
							count += 1
						cursor = result["next"]
				self.view(
					"History exported",
					f"{count} jobs\n{path}\nContains private text, raster and metadata; keep private.",
				)

	def quotas(self, scope=None):
		scope = scope or self.ask("Scope: anonymous / authenticated / user:<id>", "anonymous")
		if scope is None:
			return
		current = self.api("quotas/get", scope=scope)
		self.view("Current quotas", current)
		value = self.ask(
			"Limits JSON (null values reset overrides)", json.dumps(current.get("overrides", {}))
		)
		if value is not None:
			self.view("Quota result", self.api("quotas/set", scope=scope, limits=json.loads(value)))

	def settings(self):
		original, draft = self.configuration.load()
		self.view(
			"Configuration scope",
			"Service policy defaults apply after restart. Class/user overrides are managed under Quota management and are preserved. Deployments paths, ports and device ID are read-only here.",
		)
		fields = list(Configuration.FIELDS)
		while True:
			labels = [
				label + ": " + json.dumps(draft.get(key), ensure_ascii=False)
				for key, (label, kind) in Configuration.FIELDS.items()
			]
			choice = self.menu(
				"Service configuration (edits stay local until applied)",
				labels
				+ [
					"View full configuration / read-only deployment fields",
					"Apply changes and restart Relay",
					"Discard and return",
				],
			)
			if choice is None or choice == len(fields) + 2:
				return
			if choice == len(fields):
				self.view("Deployment fields are read-only: bind / port / database / device_id", draft)
			elif choice == len(fields) + 1:
				changes = {
					key: dict(old=json.loads(original).get(key), new=value)
					for key, value in draft.items()
					if json.loads(original).get(key) != value
				}
				self.view("Pending changes", changes)
				if self.menu("Apply and briefly restart Relay?", ["Cancel", "Apply"]) == 1:
					self.view("Configuration", self.configuration.apply(original, draft))
					original, draft = self.configuration.load()
			else:
				key = fields[choice]
				label, kind = Configuration.FIELDS[key]
				if kind is bool:
					selected = self.menu(label, ["Disabled", "Enabled"])
					if selected is not None:
						draft[key] = bool(selected)
				else:
					value = self.ask(label, json.dumps(draft.get(key), ensure_ascii=False))
					if value is not None:
						updated = dict(draft)
						updated[key] = int(value) if kind is int else json.loads(value)
						self.configuration.validate(original, updated)
						draft = updated

	def run(self, screen):
		self.screen = screen
		curses.curs_set(0)
		screen.keypad(True)
		while True:
			choice = self.menu(
				"Paperang local sudo console",
				[
					"Service / database / Windows receiver status",
					"Create / distribute credential",
					"Manage credentials / revoke / rotate",
					"Print history / export",
					"Quota management",
					"Audit events",
					"Identities",
					"Service configuration / apply changes",
					"Exit",
				],
			)
			if choice in (None, 8):
				return
			try:
				if choice == 0:
					result = self.worker.call("summary")
					result["systemd"] = subprocess.run(
						[
							"systemctl",
							"show",
							"paperang-relay",
							"-p",
							"ActiveState",
							"-p",
							"SubState",
							"-p",
							"NRestarts",
						],
						capture_output=True,
						text=True,
						check=True,
					).stdout
					self.view("Status", result)
				elif choice == 1:
					self.create()
				elif choice == 2:
					self.credentials()
				elif choice == 3:
					self.history()
				elif choice == 4:
					self.quotas()
				elif choice == 6:
					self.identities()
				elif choice == 7:
					self.settings()
				elif choice == 5:
					before = 2**63 - 1
					while True:
						page = self.api("audit", before=before)
						self.view("Audit events", page["events"])
						if not page["next"] or self.menu("Audit pagination", ["Back", "Next page"]) != 1:
							break
						before = page["next"]
			except Exception as error:
				self.view("Action failed", str(error))


def main():
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parent)
	parser.add_argument("--config", type=Path)
	parser.add_argument("--server", default="https://etel.patronum.uk")
	parser.add_argument("--worker", action="store_true", help=argparse.SUPPRESS)
	parser.add_argument(
		"--check", action="store_true", help="Check local worker/database access without opening the UI"
	)
	args = parser.parse_args()
	root = args.root.resolve()
	config = args.config or root / "config/config.json"
	os.umask(0o077)
	if args.worker:
		if os.getuid() != pwd.getpwnam("paperang-relay").pw_uid:
			raise SystemExit("Worker requires the dedicated service identity")
		sys.path.insert(0, str(root / "current"))
		from relay import Store

		backend = AdminBackend(Store(json.loads(config.read_text())))
		for line in sys.stdin:
			try:
				request = json.loads(line)
				result = dict(result=backend.call(request["action"], request["body"]))
			except Exception as error:
				result = dict(error=str(error))
			print(json.dumps(result), flush=True)
		return
	if os.geteuid() != 0:
		raise SystemExit("Start with sudo python3 " + str(root / "manage.py"))
	python = root / "venvs/current/bin/python"
	if Path(sys.prefix).resolve() != python.parent.parent.resolve():
		os.execv(str(python), [str(python), "-B", str(Path(__file__).resolve()), *sys.argv[1:]])
	if not args.check and not sys.stdin.isatty():
		raise SystemExit("An interactive terminal is required (use ssh -t)")
	if not args.check:
		lock = open(root / "workspace/reset.lock", "a")
		try:
			fcntl.flock(lock, fcntl.LOCK_SH | fcntl.LOCK_NB)
		except BlockingIOError:
			raise SystemExit("Deployment is running; reopen the console after it finishes")
	worker = Worker(root, config)
	try:
		if args.check:
			print(json.dumps(worker.call("summary"), indent=2))
		else:
			curses.wrapper(Console(worker, root, args.server, config).run)
	finally:
		worker.close()


if __name__ == "__main__":
	main()
