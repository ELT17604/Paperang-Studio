import importlib.util, json, sys, tempfile, unittest, uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "Server"))
from relay import Store, Problem

spec = importlib.util.spec_from_file_location(
	"console", str(Path(__file__).resolve().parents[2] / "Server/manage.py")
)
m = importlib.util.module_from_spec(spec)
spec.loader.exec_module(m)


class BackendTests(unittest.TestCase):
	def setUp(self):
		self.temp = tempfile.TemporaryDirectory()
		self.store = Store({"database": str(Path(self.temp.name) / "db")})
		self.backend = m.AdminBackend(self.store)

	def tearDown(self):
		self.temp.cleanup()

	def api(self, path, body):
		return self.backend.call("api", {"path": "/v2/admin/" + path, "body": body})

	def test_no_console_identity_and_secret_free_list(self):
		self.assertEqual(self.backend.call("summary", {})["credentials"], 0)
		entry = self.api("keys", {"name": "B", "role": "B"})
		raw = json.dumps(self.backend.call("credentials", {}))
		self.assertNotIn(entry["token"], raw)
		self.assertNotIn("secret_hash", raw)
		with self.store.connect() as db:
			self.assertEqual(
				db.execute("SELECT count(*) FROM identities WHERE id='local-root-console'").fetchone()[0], 0
			)

	def test_create_revoke_rotate_and_last_owner(self):
		owner = self.api("keys", {"name": "owner", "role": "C"})
		with self.assertRaises(Problem):
			self.api("keys/" + owner["id"] + "/revoke", {})
		key = self.api("keys", {"name": "B", "role": "B"})
		replacement = self.api("keys/" + key["id"] + "/rotate", {})
		self.assertEqual(replacement["user_id"], key["user_id"])
		with self.assertRaises(Problem):
			self.store.api.auth.authenticate("Bearer " + key["token"])
		self.api("keys/" + replacement["id"] + "/revoke", {})

	def test_private_history_and_json_export(self):
		entry = self.api("keys", {"name": "B", "role": "B"})
		key = self.store.api.auth.authenticate("Bearer " + entry["token"])
		job = self.store.route(
			"POST",
			"/v2/jobs",
			{"text": "private fixture 中文", "request_id": str(uuid.uuid4())},
			key,
			"127.0.0.1",
			{},
		)[1]
		rows = self.backend.call("history", {"id": job["shortID"]})
		self.assertEqual(len(rows), 1)
		detail = self.backend.call("detail", {"id": job["id"]})[0]
		self.assertIn("中文", detail["text"])
		raw = json.dumps(detail)
		for secret in ["secret_hash", "claim_hash", "receipt"]:
			self.assertNotIn('"' + secret + '"', raw)

	def test_quota_and_private_export_mode(self):
		self.api("quotas/set", {"scope": "anonymous", "limits": {"hourly_limit": 7}})
		self.assertEqual(self.api("quotas/get", {"scope": "anonymous"})["effective"]["hourly_limit"], 7)
		ui = m.Console(None, Path(self.temp.name), "https://example.com")
		path = ui.save("test", {"token": "fixture"})
		self.assertEqual(path.stat().st_mode & 0o777, 0o600)
		self.assertEqual(path.parent.stat().st_mode & 0o777, 0o700)


if __name__ == "__main__":
	unittest.main()
