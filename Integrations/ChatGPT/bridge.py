"""Small, testable Paperang Relay API v2 client for ChatGPT integrations.

Network transport and validation live here; MCP is only a thin presentation
layer. Never print without an explicit user request; never retry a POST.
"""
from __future__ import annotations

import base64
import binascii
import hashlib
import ipaddress
import re
import uuid
from urllib.parse import urlsplit

import httpx

WIDTH = 576
ROW_BYTES = 72
MAX_ROWS = 1400
MAX_JSON_BYTES = 163840
KEY_RE = re.compile(r"psr_[A-Za-z0-9_-]{40,100}\Z")
SHORT_RE = re.compile(r"[a-fA-F0-9]{12}\Z")
UUID_RE = re.compile(r"[a-f0-9-]{36}\Z")


class BridgeError(Exception):
    """Stable, nonsecret error safe for presentation to a user."""


def validate_endpoint(endpoint: str) -> str:
    if not isinstance(endpoint, str):
        raise BridgeError("Relay root URL is required")
    u = urlsplit(endpoint)
    if (u.scheme != "https" or not u.hostname or u.username is not None
            or u.password is not None or u.query or u.fragment
            or u.path not in ("", "/")):
        raise BridgeError("Relay URL must be an HTTPS root without userinfo, query or path")
    try:
        if ipaddress.ip_address(u.hostname).is_loopback:
            raise BridgeError("Remote bridge requires HTTPS DNS endpoint; use local tests for loopback")
    except ValueError:
        pass
    return endpoint.rstrip("/")


def validate_key(key: str) -> str:
    if not isinstance(key, str) or not KEY_RE.fullmatch(key):
        raise BridgeError("A dedicated A/B random psr_ submit token is required")
    return key


def job_id(value: str) -> str:
    if not isinstance(value, str):
        raise BridgeError("Invalid job identifier")
    if SHORT_RE.fullmatch(value):
        return "lookup/" + value.upper()
    try:
        return str(uuid.UUID(value))
    except (ValueError, AttributeError):
        raise BridgeError("Expected full UUID or 12-character short ID") from None


def request_id(value: str) -> str:
    try:
        if not isinstance(value, str):
            raise ValueError()
        result = str(uuid.UUID(value))
        if result != value.lower():
            raise ValueError()
        return result
    except (ValueError, AttributeError):
        raise BridgeError("request_id must be a canonical UUID") from None


def clean_text(text: str, optional: bool = False) -> str:
    if not isinstance(text, str) or len(text) > 10000:
        raise BridgeError("Text must be UTF-8-compatible string of at most 10000 characters")
    if any(ord(c) < 32 and c not in "\r\n\t" for c in text):
        raise BridgeError("Unsupported text control character")
    if not optional and not text.strip():
        raise BridgeError("No printable text provided")
    return text


def raster1(data: str, height: int, checksum: str = "") -> dict:
    """Validate already packed 576-wide MSB-first black=1 raster1 bytes."""
    if type(height) is not int or not 1 <= height <= MAX_ROWS:
        raise BridgeError("Raster height must be 1..1400 rows")
    if not isinstance(data, str) or len(data) != 4 * ((ROW_BYTES * height + 2) // 3):
        raise BridgeError("Invalid raster1 base64 length")
    try:
        raw = base64.b64decode(data, validate=True)
    except (binascii.Error, ValueError):
        raise BridgeError("Invalid raster1 base64") from None
    if len(raw) != ROW_BYTES * height:
        raise BridgeError("Raster1 must contain exactly 72 bytes per row")
    sha = hashlib.sha256(raw).hexdigest()
    if checksum and checksum != sha:
        raise BridgeError("Raster1 SHA-256 mismatch")
    return dict(schemaVersion=1, type="raster1", width=WIDTH, height=height,
                encoding="base64", bitOrder="msb-first", blackIsOne=True,
                data=data, sha256=sha)


class RelayBridge:
    def __init__(self, endpoint: str, key: str, transport: httpx.AsyncBaseTransport | None = None):
        self.endpoint = validate_endpoint(endpoint)
        self.key = validate_key(key)
        self.transport = transport

    async def _call(self, method: str, path: str, payload: dict | None = None) -> dict:
        body = None if payload is None else __import__("json").dumps(
            payload, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
        if body is not None and len(body) > MAX_JSON_BYTES:
            raise BridgeError("Relay request exceeds 160 KiB")
        headers = {"Authorization": "Bearer " + self.key,
                   "Accept": "application/json", "Cache-Control": "no-store"}
        if body is not None:
            headers["Content-Type"] = "application/json"
        try:
            async with httpx.AsyncClient(transport=self.transport, follow_redirects=False,
                                         trust_env=False, timeout=10) as client:
                res = await client.request(method, self.endpoint + path,
                                           content=body, headers=headers)
        except httpx.RequestError:
            if method == "POST":
                raise BridgeError("Submission outcome unknown: do not resend with a new request_id") from None
            raise BridgeError("Relay status request unavailable") from None
        if res.is_redirect:
            raise BridgeError("Unexpected redirect: verify the relay HTTPS URL")
        if res.status_code in (401, 403):
            raise BridgeError("Relay denied this sender credential or role")
        if res.status_code == 404:
            raise BridgeError("Job not found or not visible to this identity")
        if res.status_code == 410:
            raise BridgeError("Relay API version mismatch; requires v0.3.0 API v2")
        if res.status_code == 429:
            raise BridgeError("Relay quota exceeded; never automatically resubmit")
        if res.status_code >= 400:
            raise BridgeError(f"Relay HTTP {res.status_code}; do not automatically retry printing")
        try:
            value = res.json()
        except ValueError:
            raise BridgeError("Invalid JSON response from relay") from None
        if not isinstance(value, dict):
            raise BridgeError("Relay response must be a JSON object")
        return value

    async def capabilities(self) -> dict:
        return await self._call("GET", "/v2/public/status")

    async def submit_text(self, text: str, rid: str) -> dict:
        return await self._call("POST", "/v2/jobs", dict(text=clean_text(text), request_id=request_id(rid)))

    async def submit_raster(self, data: str, height: int, rid: str,
                            caption: str = "", sha256: str = "") -> dict:
        payload = dict(text=clean_text(caption, optional=True),
                       content=raster1(data, height, sha256),
                       request_id=request_id(rid))
        return await self._call("POST", "/v2/jobs", payload)

    async def status(self, identifier: str) -> dict:
        return await self._call("GET", "/v2/jobs/" + job_id(identifier))
