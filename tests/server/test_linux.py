"""Linux upgrade regression: real HTTP, maximum raster and durable state."""

import base64
import hashlib
import json
import tempfile
import threading
import unittest
import urllib.request
import uuid
from pathlib import Path
from relay import Store, Server, Problem


class LinuxTests(unittest.TestCase):
	def setUp(self):
		self.tmp = tempfile.TemporaryDirectory()
		self.config = {"database": str(Path(self.tmp.name) / "db"), "port": 0}
		# Store validation requires positive configured port; Server binds ephemeral below.
		self.config["port"] = 8177
		self.s = Store(self.config)
		self.entry = self.s.api.auth.create("B", "B")
		self.key = self.s.api.auth.authenticate("Bearer " + self.entry["token"])

	def tearDown(self):
		self.tmp.cleanup()

	def test_max_raster_long_text_restart_and_export(self):
		raw = b"\x80" + bytes(72 * 1400 - 1)
		content = {
			"schemaVersion": 1,
			"type": "raster1",
			"width": 576,
			"height": 1400,
			"encoding": "base64",
			"bitOrder": "msb-first",
			"blackIsOne": True,
			"data": base64.b64encode(raw).decode(),
			"sha256": hashlib.sha256(raw).hexdigest(),
		}
		rid = str(uuid.uuid4())
		body = {"request_id": rid, "text": "x" * 8600, "content": content}
		s = self.s
		s.c["port"] = 0
		server = Server(s)
		thread = threading.Thread(target=server.serve_forever, daemon=True)
		thread.start()
		try:
			req = urllib.request.Request(
				"http://127.0.0.1:" + str(server.server_port) + "/v2/jobs",
				data=json.dumps(body).encode(),
				headers={
					"Content-Type": "application/json",
					"Authorization": "Bearer " + self.entry["token"],
				},
			)
			with urllib.request.urlopen(req) as response:
				job = json.load(response)
		finally:
			server.shutdown()
			server.server_close()
			thread.join()
		restarted = Store(self.config)
		key = restarted.api.auth.authenticate("Bearer " + self.entry["token"])
		replay = restarted.route("POST", "/v2/jobs", body, key, "127.0.0.1", {})[1]
		self.assertEqual(replay["id"], job["id"])
		owner = restarted.api.auth.create("C", "C")
		c = restarted.api.auth.authenticate("Bearer " + owner["token"])
		exported = restarted.route("POST", "/v2/admin/jobs/export", {}, c, "127.0.0.1", {})[1]["jobs"][0]
		self.assertEqual(exported["content"]["data"], content["data"])
		self.assertEqual(len(exported["text"]), 8600)
		self.assertTrue(exported["events"])

	def test_queue_and_stable_identity_quota(self):
		self.s.c["queue_limit"] = 1
		body = {"text": "test", "request_id": str(uuid.uuid4())}
		self.s.route("POST", "/v2/jobs", body, self.key, "127.0.0.1", {})
		with self.assertRaises(Problem) as exc:
			self.s.route(
				"POST", "/v2/jobs", {**body, "request_id": str(uuid.uuid4())}, self.key, "127.0.0.1", {}
			)
		self.assertEqual(exc.exception.status, 503)
		self.s.c["queue_limit"] = 200
		self.s.c["authenticated_hourly_limit"] = 1
		rotated = self.s.api.auth.create("B", "B", user_id=self.key["user_id"])
		key = self.s.api.auth.authenticate("Bearer " + rotated["token"])
		with self.assertRaises(Problem) as exc:
			self.s.route("POST", "/v2/jobs", {**body, "request_id": str(uuid.uuid4())}, key, "127.0.0.2", {})
		self.assertEqual(exc.exception.status, 429)
