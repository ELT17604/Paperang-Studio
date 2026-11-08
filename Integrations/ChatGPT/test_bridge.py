"""No-network tests for the Paperang ChatGPT relay bridge.

Run: python -m unittest discover -s Integrations/ChatGPT -p 'test_*.py' -v
Requires httpx only; no MCP or real printer.
"""
import asyncio
import base64
import hashlib
import sys
import unittest
import uuid
from pathlib import Path
from unittest.mock import patch

import httpx

sys.path.insert(0, str(Path(__file__).resolve().parent))
from bridge import BridgeError, RelayBridge, raster1, request_id, job_id, validate_endpoint

KEY = "psr_" + "A" * 43
ENDPOINT = "https://relay.example.test"


class RelayTests(unittest.TestCase):
    def run_async(self, coro):
        return asyncio.run(coro)

    def bridge(self, callback):
        return RelayBridge(ENDPOINT, KEY, httpx.MockTransport(callback))

    def test_url_and_key_rejected(self):
        for url in ["http://relay.example", "https://user@relay.example",
                    "https://relay.example/path", "https://relay.example?q=1",
                    "https://relay.example/#foo", "https://127.0.0.1"]:
            with self.subTest(url=url), self.assertRaises(BridgeError):
                validate_endpoint(url)
        with self.assertRaises(BridgeError):
            RelayBridge(ENDPOINT, "psp_admin")
        with self.assertRaises(BridgeError):
            request_id("hello")

    def test_text_submission_schema_and_idempotency(self):
        rid = str(uuid.uuid4())
        seen = []

        def respond(req):
            seen.append(req)
            self.assertEqual(req.headers["authorization"], "Bearer " + KEY)
            self.assertEqual(req.url.path, "/v2/jobs")
            data = __import__("json").loads(req.content)
            self.assertEqual(data, {"text": "你好, Paperang!", "request_id": rid})
            return httpx.Response(202, json={"id": rid, "accepted": True, "statusReadable": True})

        api = self.bridge(respond)
        result = self.run_async(api.submit_text("你好, Paperang!", rid))
        self.assertTrue(result["accepted"])
        self.assertEqual(len(seen), 1)
        with self.assertRaises(BridgeError):
            self.run_async(api.submit_text("\x00unsafe", rid))
        self.assertEqual(len(seen), 1)

    def test_raster_validation_and_quota_safe_pack(self):
        pixels = bytes([0x80] + [0] * 71) * 4
        encoded = base64.b64encode(pixels).decode()
        sha = hashlib.sha256(pixels).hexdigest()
        expected = raster1(encoded, 4, sha)
        self.assertEqual(expected["width"], 576)
        self.assertTrue(expected["blackIsOne"])
        self.assertEqual(expected["sha256"], sha)
        for bad in [(encoded, 5, ""), (encoded, 4, "00"), ("not base64!", 4, "")]:
            with self.subTest(bad=bad), self.assertRaises(BridgeError):
                raster1(*bad)
        with self.assertRaises(BridgeError):
            raster1(encoded, True)

        def respond(req):
            content = __import__("json").loads(req.content)
            self.assertEqual(content["text"], "示例")
            self.assertEqual(content["content"], expected)
            return httpx.Response(202, json={"accepted": True, "statusReadable": False})
        api = self.bridge(respond)
        self.assertTrue(self.run_async(api.submit_raster(encoded, 4, str(uuid.uuid4()), "示例", sha))["accepted"])

    def test_status_scope_and_404(self):
        self.assertEqual(job_id("abcdef123456"), "lookup/ABCDEF123456")
        ident = str(uuid.uuid4())
        self.assertEqual(job_id(ident), ident)
        def respond(req):
            self.assertEqual(req.url.path, "/v2/jobs/lookup/ABCDEF123456")
            return httpx.Response(403, json={"error": "status_read_forbidden"})
        with self.assertRaisesRegex(BridgeError, "credential or role"):
            self.run_async(self.bridge(respond).status("abcdef123456"))

    def test_redirects_and_uncertain_post_not_retried(self):
        n = 0
        def fail(req):
            nonlocal n
            n += 1
            raise httpx.ConnectError("simulated outage", request=req)
        api = self.bridge(fail)
        with self.assertRaisesRegex(BridgeError, "outcome unknown"):
            self.run_async(api.submit_text("test", str(uuid.uuid4())))
        self.assertEqual(n, 1)

        def redirect(req):
            return httpx.Response(307, headers={"location": "https://other.example/v2/jobs"})
        with self.assertRaisesRegex(BridgeError, "redirect"):
            self.run_async(self.bridge(redirect).submit_text("test", str(uuid.uuid4())))

    def test_status_sent_is_not_confirmed(self):
        def respond(req):
            return httpx.Response(200, json={"state": "sent", "id": str(uuid.uuid4())})
        self.assertEqual(self.run_async(self.bridge(respond).status("ABCDEF123456"))["state"], "sent")


if __name__ == "__main__":
    unittest.main()
