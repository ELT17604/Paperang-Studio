"""Run actual WinLite code against a temporary Relay; never a physical printer."""

import argparse, base64, hashlib, json, socket, subprocess, sys, tempfile, threading, uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Server"))
from relay import Store, Server
from cryptography.hazmat.primitives.asymmetric import rsa
from cryptography.hazmat.primitives import serialization


def main():
	parser = argparse.ArgumentParser()
	parser.add_argument("--dotnet", default="dotnet")
	parser.add_argument(
		"--physical-once",
		type=Path,
		help="Explicit real print authorization; exclusive attempt file, one job",
	)
	args = parser.parse_args()
	if args.physical_once:
		args.physical_once.parent.mkdir(parents=True, exist_ok=True)
		with args.physical_once.open("x", encoding="utf-8") as f:
			json.dump({"state": "attempting", "label": "PS032-W-R1"}, f)
	with tempfile.TemporaryDirectory(prefix="ps032-win-relay-") as directory:
		tmp = Path(directory)
		with socket.socket() as sock:
			sock.bind(("127.0.0.1", 0))
			port = sock.getsockname()[1]
		base = f"http://127.0.0.1:{port}/"
		store = Store({"database": str(tmp / "relay.sqlite3"), "port": port})
		auth = store.api.auth
		private = rsa.generate_private_key(public_exponent=65537, key_size=3072)
		public = (
			private.public_key()
			.public_bytes(serialization.Encoding.PEM, serialization.PublicFormat.SubjectPublicKeyInfo)
			.decode()
		)
		entry = auth.public_key_add("PS032 isolated Windows owner", public)
		owner_paths = []
		for name, fmt in [
			("owner.json", serialization.PrivateFormat.PKCS8),
			("owner-pkcs1.json", serialization.PrivateFormat.TraditionalOpenSSL),
		]:
			path = tmp / name
			path.write_text(
				json.dumps(
					dict(
						server=base,
						id=entry["id"],
						privateKeyPem=private.private_bytes(
							serialization.Encoding.PEM, fmt, serialization.NoEncryption()
						).decode(),
					)
				),
				encoding="utf-8",
			)
			owner_paths.append(str(path))
		m = auth.create("PS032 isolated M", "M")
		b = auth.create("PS032 isolated sender", "B")
		actor = auth.authenticate("Bearer " + b["token"])
		count = 1 if args.physical_once else 2
		job_id = None
		for i in range(count):
			body = {
				"text": (
					"PS032-W-R1\nRemote via isolated Relay"
					if args.physical_once
					else "PS032 Windows isolated caption " * 9
				),
				"request_id": str(uuid.uuid4()),
			}
			if i:
				raw = bytes([0xAA]) * 72 * 20
				body["content"] = dict(
					type="raster1",
					schemaVersion=1,
					width=576,
					height=20,
					bitOrder="msb-first",
					blackIsOne=True,
					encoding="base64",
					data=base64.b64encode(raw).decode(),
					sha256=hashlib.sha256(raw).hexdigest(),
				)
			code, job = store.route("POST", "/v2/jobs", body, actor, "127.0.0.1", {})
			assert code == 202
			job_id = job["id"]
		config = tmp / "fixture.json"
		config.write_text(
			json.dumps(
				dict(
					server=base,
					ownerPath=owner_paths[0],
					pkcs1Path=owner_paths[1],
					mToken=m["token"],
					physical=bool(args.physical_once),
					jobCount=count,
				)
			),
			encoding="utf-8",
		)
		server = Server(store)
		thread = threading.Thread(target=server.serve_forever, daemon=True)
		thread.start()
		try:
			subprocess.run(
				[
					args.dotnet,
					"run",
					"--project",
					str(ROOT / "tests/windows/Paperang.Tests"),
					"-c",
					"Release",
					"--",
					"--integration",
					str(config),
				],
				check=True,
				timeout=90,
			)
			with store.connect() as db:
				states = [row[0] for row in db.execute("SELECT state FROM jobs")]
			assert states == ["sent"] * count, states
			if args.physical_once:
				args.physical_once.write_text(
					json.dumps(
						dict(
							label="PS032-W-R1",
							id=job_id,
							state="sent",
							physicalCompletion="unknown",
							viaUbuntu=False,
							isolatedRelay=True,
						)
					),
					encoding="utf-8",
				)
				print("PS032-W-R1 sent once; job ID=" + job_id + "; requires paper confirmation")
			print("PASS isolated database records sent only; physical completion was never fabricated")
		finally:
			server.shutdown()
			server.server_close()
			thread.join()


if __name__ == "__main__":
	main()
