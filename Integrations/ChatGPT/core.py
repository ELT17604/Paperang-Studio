"""Paperang Studio ChatGPT sender core (Relay API v2, 0.3.0 baseline).

The adapter never owns the printer or receiver settings. MCP transport and
authentication are separate layers. No POST retries or redirects.
"""
import base64
import binascii
import hashlib
import ipaddress
import json
import os
import re
import uuid
from urllib.parse import urlsplit

import httpx

TOKEN_RE = re.compile(r"psr_[A-Za-z0-9_-]{40,100}\Z")
JOB_ID_RE = re.compile(r"(?:[a-fA-F0-9]{40}|[a-fA-F0-9-]{36})\Z")
SHORT_ID_RE = re.compile(r"[a-fA-F0-9]{12}\Z")
MAX_BODY_BYTES = 163840
MAX_TEXT = 10000
MAX_ROWS = 1400


class PaperangError(Exception):
    """Safe, credential-free error for the caller."""


def relay_url(value: str) -> str:
    """Reject URL credentials, fragments, paths and unsafe remote HTTP."""
    url = urlsplit(value or "")
    try:
        _ = url.port
    except ValueError:
        raise PaperangError("Invalid relay port") from None
    try:
        loopback = url.hostname == "localhost" or ipaddress.ip_address(url.hostname or "").is_loopback
    except ValueError:
        loopback = url.hostname == "localhost"
    if (url.scheme not in ("http", "https") or not url.hostname
            or url.username is not None or url.password is not None
            or url.query or url.fragment or url.path not in ("", "/")
            or (url.scheme == "http" and not loopback)):
        raise PaperangError("Use the HTTPS root of a trusted Relay API v2 server (HTTP loopback only)")
    return value.rstrip("/")


def validate_text(text: str) -> int:
    if not isinstance(text, str) or not text.strip() or len(text) > MAX_TEXT:
        raise PaperangError("Printable text must contain 1..10000 characters")
    if any(ord(c) < 32 and c not in "\n\r\t" for c in text):
        raise PaperangError("Unsupported control character in text")
    return len(text)


def validate_raster(content: dict) -> int:
    """The existing RelaySkill raster1 contract: 576 pixels, 72 bytes per row."""
    if not isinstance(content, dict) or set(content) - {
        "schemaVersion", "type", "width", "height", "encoding", "bitOrder",
        "blackIsOne", "data", "sha256"}:
        raise PaperangError("Unsupported raster1 field")
    height = content.get("height")
    if (type(content.get("schemaVersion")) is not int or content["schemaVersion"] != 1
            or content.get("type") != "raster1"
            or type(content.get("width")) is not int or content["width"] != 576
            or type(height) is not int or not 1 <= height <= MAX_ROWS
            or content.get("encoding") != "base64" or content.get("bitOrder") != "msb-first"
            or content.get("blackIsOne") is not True):
        raise PaperangError("Unsupported raster1 schema or dimensions")
    data = content.get("data")
    if not isinstance(data, str) or len(data) != 4 * ((72 * height + 2) // 3):
        raise PaperangError("Invalid raster1 base64 size")
    try:
        raw = base64.b64decode(data, validate=True)
    except (binascii.Error, ValueError):
        raise PaperangError("Invalid raster1 base64") from None
    if len(raw) != 72 * height or hashlib.sha256(raw).hexdigest() != content.get("sha256"):
        raise PaperangError("Raster1 length or SHA-256 mismatch")
    return height


def validated_job(text: str = "", raster: dict | None = None) -> tuple[dict, int]:
    if not isinstance(text, str):
        raise PaperangError("Text must be a string")
    units = validate_text(text) if text else 0
    job = {"text": text}
    if raster is not None:
        units += validate_raster(raster)
        job["content"] = raster
    if not text.strip() and raster is None:
        raise PaperangError("No printable content")
    if units > MAX_TEXT:
        raise PaperangError("Job exceeds maximum text+row units")
    if len(json.dumps(job, ensure_ascii=False, separators=(",", ":")).encode("utf-8")) > MAX_BODY_BYTES:
        raise PaperangError("Relay request exceeds 160 KiB")
    return job, units


def normalize_request_id(value: str) -> str:
    try:
        parsed = uuid.UUID(value)
    except (ValueError, AttributeError, TypeError):
        raise PaperangError("Provide a stable UUID request_id for one intended print") from None
    if str(parsed) != value:
        raise PaperangError("Use a lowercase canonical UUID request_id")
    return value


class RelayClient:
    """A single configured A/B identity, never M or C.

    Keys are provided through the process environment by the operator; callers
    cannot choose a target URL or supply credentials in tool arguments.
    """
    def __init__(self, server: str, key: str, *, transport=None):
        self.server = relay_url(server)
        if not isinstance(key, str) or not TOKEN_RE.fullmatch(key):
            raise PaperangError("Configure a dedicated A/B random submit token")
        self.key = key
        self.transport = transport

    async def request(self, method: str, path: str, body: dict | None = None) -> dict:
        headers = {"Authorization": "Bearer " + self.key, "Accept": "application/json",
                   "Cache-Control": "no-store"}
        try:
            async with httpx.AsyncClient(timeout=httpx.Timeout(12.0), follow_redirects=False,
                                         trust_env=False, transport=self.transport) as client:
                response = await client.request(method, self.server + path, headers=headers, json=body)
                if 300 <= response.status_code < 400:
                    raise PaperangError("Relay redirected request; use final trusted HTTPS URL")
                if response.status_code >= 400:
                    raise PaperangError(f"Relay HTTP {response.status_code} (check permissions and policy)")
                if len(response.content) > 1048576:
                    raise PaperangError("Relay response too large")
                result = response.json()
                if not isinstance(result, dict):
                    raise PaperangError("Relay returned invalid JSON object")
                return result
        except (httpx.TimeoutException, httpx.NetworkError, httpx.ProtocolError) as exc:
            detail = "POST outcome may be uncertain; never create a new request_id to retry" if method == "POST" else "read failed; no print submitted"
            raise PaperangError("Relay unavailable; " + detail) from None
        except ValueError:
            raise PaperangError("Relay returned invalid JSON") from None

    async def identity(self) -> dict:
        """Verify A/B role from the server, not token format."""
        response = await self.request("GET", "/v2/auth/me")
        if response.get("role") not in ("A", "B"):
            raise PaperangError("ChatGPT adapter requires a dedicated A/B sender identity")
        return {"role": response["role"]}

    async def capabilities(self) -> dict:
        return await self.request("GET", "/v2/public/status")

    async def submit(self, job: dict, request_id: str) -> dict:
        """Call only after explicit print authorization; never retry POST."""
        rid = normalize_request_id(request_id)
        await self.identity()  # Reject C/M before a physical submission.
        validated, units = validated_job(job.get("text", ""), job.get("content"))
        policy = await self.capabilities()
        if policy.get("capabilities", {}).get("authVersion") != 2:
            raise PaperangError("Relay API v2 identity support not advertised")
        if not policy.get("status", {}).get("accepting"):
            raise PaperangError("Relay not accepting new jobs")
        if validated.get("content") and (
                "raster1" not in policy.get("capabilities", {}).get("contentTypes", [])
                or policy.get("capabilities", {}).get("contentSchemaVersion") != 1):
            raise PaperangError("Relay does not advertise raster1 schema 1")
        limit = policy.get("policy", {}).get("authenticated_max_length")
        if type(limit) is not int or units > limit:
            raise PaperangError("Job exceeds relay configured sender allowance")
        result = await self.request("POST", "/v2/jobs", {**validated, "request_id": rid})
        if result.get("accepted") is not True or not JOB_ID_RE.fullmatch(result.get("id", "")):
            raise PaperangError("Uncertain submission result; do not retry with a new request_id")
        return {k: result[k] for k in ("id", "shortID", "accepted", "statusReadable") if k in result}

    async def status(self, job_id: str) -> dict:
        if not isinstance(job_id, str) or not (JOB_ID_RE.fullmatch(job_id) or SHORT_ID_RE.fullmatch(job_id)):
            raise PaperangError("Invalid job identifier")
        identity = await self.identity()
        if identity["role"] != "B":
            raise PaperangError("Job status requires a B sender identity")
        path = "/v2/jobs/lookup/" + job_id.upper() if SHORT_ID_RE.fullmatch(job_id) else "/v2/jobs/" + job_id
        result = await self.request("GET", path)
        return {k: result[k] for k in ("id", "shortID", "state", "created", "updated") if k in result}


def configured_client() -> RelayClient:
    return RelayClient(os.environ.get("PAPERANG_RELAY_URL", ""),
                       os.environ.get("PAPERANG_RELAY_SUBMIT_KEY", ""))
