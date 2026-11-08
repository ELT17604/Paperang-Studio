"""Offline unit/HTTP contract tests. Never contacts a live relay or printer."""
import asyncio
import base64
import hashlib
import json
import unittest
import uuid

import httpx

from core import PaperangError, RelayClient, relay_url, validate_raster, validated_job

KEY = "psr_" + "A" * 43


class CoreTests(unittest.TestCase):
    def test_relay_url(self):
        self.assertEqual(relay_url("https://print.example:8177/"), "https://print.example:8177")
        self.assertEqual(relay_url("http://127.0.0.1:8177"), "http://127.0.0.1:8177")
        for url in ("http://example.com", "https://user:pass@example.com",
                    "https://example.com/path", "https://example.com?token=x",
                    "https://example.com#secret", "file:///tmp/local"):
            with self.subTest(url=url), self.assertRaises(PaperangError):
                relay_url(url)

    def test_raster1(self):
        raw = b"\x80" + b"\x00" * 71
        content = {"schemaVersion": 1, "type": "raster1", "width": 576,
                   "height": 1, "encoding": "base64", "bitOrder": "msb-first",
                   "blackIsOne": True, "data": base64.b64encode(raw).decode(),
                   "sha256": hashlib.sha256(raw).hexdigest()}
        self.assertEqual(validate_raster(content), 1)
        self.assertEqual(validated_job("caption", content)[1], 8)
        for change in ({"width": 384}, {"sha256": "0"*64}, {"height": 1401},
                       {"bitOrder": "lsb-first"}, {"unknown": 1}):
            with self.subTest(change=change), self.assertRaises(PaperangError):
                validate_raster({**content, **change})

    def test_text_validation(self):
        for bad in ("", "   ", "x"*10001, "a\x00b"):
            with self.subTest(bad=bad[:10]), self.assertRaises(PaperangError):
                validated_job(bad)
        self.assertEqual(validated_job("你好\nWorld")[1], 8)

    def test_http_contract_idempotency_and_status(self):
        events = []
        request_id = str(uuid.uuid4())
        job_id = str(uuid.uuid4())
        def handler(request):
            assert request.headers.get("authorization") == "Bearer " + KEY
            events.append((request.method, request.url.path))
            if request.url.path == "/v2/public/status":
                return httpx.Response(200, json={
                    "status": {"accepting": True},
                    "policy": {"authenticated_max_length": 10000},
                    "capabilities": {"authVersion": 2, "contentTypes": ["text", "raster1"],
                                     "contentSchemaVersion": 1}})
            if request.url.path == "/v2/jobs" and request.method == "POST":
                payload = json.loads(request.content)
                assert payload == {"text": "test note", "request_id": request_id}
                return httpx.Response(202, json={"accepted": True, "id": job_id,
                                                   "statusReadable": True})
            if request.url.path == "/v2/jobs/" + job_id:
                return httpx.Response(200, json={"id": job_id, "state": "sent",
                                                   "created": 1, "updated": 2})
            return httpx.Response(404)
        sender = RelayClient("https://print.example", KEY, transport=httpx.MockTransport(handler))
        result = asyncio.run(sender.submit({"text": "test note"}, request_id))
        self.assertTrue(result["accepted"])
        self.assertEqual(asyncio.run(sender.status(job_id))["state"], "sent")
        self.assertEqual(events, [("GET", "/v2/public/status"),
                                  ("POST", "/v2/jobs"), ("GET", "/v2/jobs/" + job_id)])

    def test_no_retry_on_timeout(self):
        events = []
        def handler(request):
            events.append(request.url.path)
            if request.method == "GET":
                return httpx.Response(200, json={
                    "status": {"accepting": True},
                    "policy": {"authenticated_max_length": 10000},
                    "capabilities": {"authVersion": 2}})
            raise httpx.ReadTimeout("ambiguous")
        sender = RelayClient("https://print.example", KEY, transport=httpx.MockTransport(handler))
        with self.assertRaisesRegex(PaperangError, "never create a new request_id"):
            asyncio.run(sender.submit({"text": "test"}, str(uuid.uuid4())))
        self.assertEqual(events, ["/v2/public/status", "/v2/jobs"])

    def test_redirect_and_auth_rejected(self):
        for status in (302, 403):
            sender = RelayClient("https://print.example", KEY,
                                 transport=httpx.MockTransport(lambda r: httpx.Response(status)))
            with self.subTest(status=status), self.assertRaises(PaperangError):
                asyncio.run(sender.capabilities())


if __name__ == "__main__":
    unittest.main()
