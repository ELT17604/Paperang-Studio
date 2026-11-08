#!/usr/bin/env python3
"""Paperang Studio MCP adapter for Relay API v2.

Run behind a trusted OAuth-capable access gateway. Never expose the bare
MCP server on the public Internet.
"""
import asyncio
import os
import re
import uuid
from urllib.parse import urlsplit

import httpx
from mcp.server.fastmcp import FastMCP

RELAY_URL = os.environ.get("PAPERANG_RELAY_URL", "").rstrip("/")
SUBMIT_KEY = os.environ.get("PAPERANG_RELAY_SUBMIT_KEY", "")
HOST = os.environ.get("PAPERANG_MCP_HOST", "127.0.0.1")
PORT = int(os.environ.get("PAPERANG_MCP_PORT", "8188"))

mcp = FastMCP("Paperang Studio", host=HOST, port=PORT)


def config() -> None:
    u = urlsplit(RELAY_URL)
    if u.scheme != "https" or not u.hostname or u.username or u.password or u.query or u.fragment or u.path not in ("", "/"):
        raise ValueError("PAPERANG_RELAY_URL must be a trusted HTTPS relay root")
    if not re.fullmatch(r"psr_[A-Za-z0-9_-]{40,100}", SUBMIT_KEY):
        raise ValueError("Configure a dedicated A/B random submit key, never M/C")


async def call(method: str, path: str, body: dict | None = None) -> dict:
    config()
    headers = {"Authorization": "Bearer " + SUBMIT_KEY, "Accept": "application/json"}
    async with httpx.AsyncClient(timeout=12, follow_redirects=False, trust_env=False) as client:
        response = await client.request(method, RELAY_URL + path, json=body, headers=headers)
        response.raise_for_status()
        return response.json()


@mcp.tool()
async def relay_capabilities() -> dict:
    """Read public policy/capabilities. This does NOT prove physical printer readiness."""
    return await call("GET", "/v2/public/status")


@mcp.tool()
async def print_text(text: str, request_id: str) -> dict:
    """Print exactly one explicitly requested text note. Physical side effect!
    
    The caller must get user authorization for the content before invoking.
    request_id must be a UUID, unique for each intentional submission; reuse
    the SAME UUID after an uncertain timeout to avoid duplicate output.
    Never generate another request_id to retry uncertain printing.
    """
    if not isinstance(text, str) or not text.strip() or len(text) > 10000:
        raise ValueError("Text must be nonempty, at most 10000 characters")
    try:
        rid = str(uuid.UUID(request_id))
    except (ValueError, TypeError, AttributeError):
        raise ValueError("request_id must be a UUID") from None
    return await call("POST", "/v2/jobs", {"text": text, "request_id": rid})


@mcp.tool()
async def print_status(job_id: str) -> dict:
    """Read a job's state with a B credential. A credentials cannot read status.
    
    'sent' means transport finished, NOT confirmed physical output.
    """
    if not re.fullmatch(r"[a-fA-F0-9-]{36}", job_id):
        raise ValueError("Invalid job id")
    return await call("GET", "/v2/jobs/" + job_id)


if __name__ == "__main__":
    config()
    mcp.run(transport="streamable-http")
