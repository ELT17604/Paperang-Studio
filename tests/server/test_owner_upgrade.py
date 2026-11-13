"""Operator-cutover failure boundaries without systemd/root/network side effects."""

import base64
import hashlib
import io
import json
import os
from pathlib import Path
import sqlite3
import subprocess
import tarfile
import tempfile
import time
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import upgrade_owner


class OwnerUpgradeTests(unittest.TestCase):
	def exercise(self, active=False, fail_start=False, bad_hash=False, paused=False, locked=False):
		with tempfile.TemporaryDirectory() as folder:
			root = Path(folder)
			source = root / "source"
			source.mkdir()
			(source / "web").mkdir()
			config = root / "config.json"
			database = root / "db.sqlite3"
			original = json.dumps(
				{"database": str(database), "port": 8177, **({"execution_paused": True} if paused else {})}
			).encode()
			config.write_bytes(original)
			files = ["relay.py", "security.py", "api_v2.py", "web/app.js", "web/index.html", "web/style.css"]
			for name in files:
				(source / name).write_text("original " + name)
			legacy = root / "initial-credentials.jsonl"
			legacy.write_text("fixture retired secret")
			unrelated = root / "unrelated.txt"
			unrelated.write_text("keep")
			identifier = "a" * 8 + "-aaaa-aaaa-aaaa-" + "a" * 12
			db = sqlite3.connect(database)
			db.executescript(
				"CREATE TABLE jobs(state TEXT);CREATE TABLE credentials(id TEXT,kind TEXT,revoked INTEGER);CREATE TABLE audit_events(action TEXT,target TEXT,at REAL);"
			)
			if active:
				db.execute("INSERT INTO jobs VALUES('printing')")
			db.commit()
			db.close()
			payload = io.BytesIO()
			with tarfile.open(fileobj=payload, mode="w:gz") as tar:
				for name in files:
					content = ("new " + name).encode()
					member = tarfile.TarInfo("repo/Server/" + name)
					member.size = len(content)
					tar.addfile(member, io.BytesIO(content))
			archive = payload.getvalue()
			events = []
			starts = 0

			def check_output(args, **kw):
				field = next(x for x in args if x.startswith("--property="))[11:]
				return {
					"WorkingDirectory": str(source),
					"User": "paperang-relay",
					"ExecStart": f'{{ path=python ; argv[]=python relay.py --config "{config.as_posix()}" serve ; }}',
				}[field] + "\n"

			def run(args, **kw):
				nonlocal starts
				events.append(args)
				if args[:2] == ["systemctl", "start"]:
					starts += 1
					if fail_start and starts == 1:
						raise subprocess.CalledProcessError(1, args)
				if args[0] == "runuser":
					with sqlite3.connect(database) as db:
						db.execute("INSERT INTO credentials VALUES(?,'publickey',0)", (identifier,))
						db.execute(
							"INSERT INTO audit_events VALUES('retire_all_other_credentials',?,?)",
							(identifier, time.time()),
						)
					db.close()
				return SimpleNamespace(returncode=0)

			argv = [
				"upgrade_owner.py",
				"--commit",
				"1" * 40,
				"--archive-sha256",
				"0" * 64 if bad_hash else hashlib.sha256(archive).hexdigest(),
				"--credential-id",
				identifier,
				"--public-key-b64",
				base64.b64encode(b"-----BEGIN PUBLIC KEY-----\nfixture").decode(),
			]
			with patch.dict(
				"sys.modules",
				{
					"fcntl": SimpleNamespace(
						LOCK_EX=2,
						LOCK_NB=4,
						flock=lambda *a: (_ for _ in ()).throw(BlockingIOError()) if locked else None,
					)
				},
			), patch.object(
				upgrade_owner,
				"os",
				SimpleNamespace(
					name="posix",
					geteuid=lambda: 0,
					environ={},
					open=lambda *a: 123,
					close=lambda *a: None,
					O_CREAT=os.O_CREAT,
					O_RDWR=os.O_RDWR,
				),
			), patch(
				"sys.argv", argv
			), patch.object(
				upgrade_owner.subprocess, "check_output", side_effect=check_output
			), patch.object(
				upgrade_owner.subprocess, "run", side_effect=run
			), patch.object(
				upgrade_owner.urllib.request, "urlopen", return_value=io.BytesIO(archive)
			), patch.object(
				upgrade_owner.time, "sleep"
			):
				if active or fail_start or bad_hash or locked:
					with self.assertRaises((RuntimeError, SystemExit, subprocess.CalledProcessError)):
						upgrade_owner.main()
					self.assertEqual((source / "relay.py").read_text(), "original relay.py")
					self.assertEqual(config.read_bytes(), original)
					self.assertTrue(legacy.exists())
					if bad_hash or locked:
						self.assertFalse(any(e[:2] == ["systemctl", "stop"] for e in events))
					else:
						self.assertGreaterEqual(starts, 1)
				else:
					upgrade_owner.main()
					self.assertEqual((source / "relay.py").read_text(), "new relay.py")
					expected = json.loads(original)
					expected.pop("execution_paused", None)
					self.assertEqual(json.loads(config.read_bytes()), expected)
					self.assertFalse(legacy.exists())
					self.assertTrue(unrelated.exists())
					backups = list(root.glob("owner-cutover-*/database.sqlite3"))
					self.assertEqual(len(backups), 1)
					with sqlite3.connect(backups[0]) as saved:
						self.assertEqual(saved.execute("SELECT count(*) FROM credentials").fetchone()[0], 0)
					saved.close()
					self.assertTrue(any(e[:2] == ["systemctl", "restart"] for e in events))

	def test_success_waits_for_owner_retirement_and_removes_only_known_exports(self):
		self.exercise()

	def test_active_print_aborts_and_restores_running_service(self):
		self.exercise(active=True)

	def test_new_service_start_failure_restores_original_code_and_config(self):
		self.exercise(fail_start=True)

	def test_prior_pause_is_cleared_after_verified_repeat(self):
		self.exercise(paused=True)

	def test_concurrent_updater_aborts_before_service_side_effect(self):
		self.exercise(locked=True)

	def test_bad_archive_hash_has_no_service_side_effect(self):
		self.exercise(bad_hash=True)


if __name__ == "__main__":
	unittest.main()
