#!/usr/bin/env python3
"""Paperang Studio ChatGPT MCP adapter (experimental, local backend only).

This is NOT an authentication gateway. Keep it on loopback and place an
OAuth-capable HTTPS MCP gateway in front of it before remote installation.
"""
from __future__ import annotations

import os

from bridge import BridgeError, RelayBridge

try:
    from mcp.server.fastmcp import FastMCP
except ImportError:
    raise SystemExit("Install optional MCP runtime: pip install -r requirements.txt")

APP_VERSION = "0.3.0-chatgpt-dev"
HOST = "127.0.0.1"
PORT = 8188

# Publicly exposed Streamable HTTP without authorization is unsafe: this
# server deliberately always binds to loopback.
mcp = FastMCP("Paperang Studio", host=HOST, port=PORT)


def client() -> RelayBridge:
    endpoint = os.environ.get("PAPERANG_RELAY_URL", "")
    key = os.environ.get("PAPERANG_RELAY_SUBMIT_KEY", "")
    return RelayBridge(endpoint, key)


@mcp.tool()
async def relay_capabilities() -> dict:
    """Read relay policy, payload limits and capabilities.

    Public status does not establish real-time Bluetooth/paper readiness.
    """
    return await client().capabilities()


@mcp.tool()
async def print_text(text: str, request_id: str) -> dict:
    """Submit ONE explicitly user-authorized note for real physical printing.

    Consumes paper. Do not invoke after mere preview/layout requests.
    request_id is a canonical UUID for one intentional print; preserve it
    across an uncertain timeout. Never create a new UUID to retry.
    Accepted means queued at relay, NOT actually printed.
    """
    return await client().submit_text(text, request_id)


@mcp.tool()
async def print_raster1(data: str, height: int, request_id: str,
                        caption: str = "", sha256: str = "") -> dict:
    """Print a user-authorized pre-rendered one-bit raster; ONE physical copy.

    Advanced: packed 576px rows, MSB-first, black=1, base64 of 72*height
    bytes; SHA-256 optional. 1..1400 rows subject to relay quota/receiver
    footer. This tool does NOT convert images or change physical density,
    feed, template, or BLE settings. No automatic retries.
    """
    return await client().submit_raster(data, height, request_id, caption, sha256)


@mcp.tool()
async def print_status(job_id: str) -> dict:
    """Read job state (B only; A credential returns 403).

    Use a full UUID or 12-hex short ID. sent means transport completion,
    not physical-paper confirmation; only C can manually confirm.
    """
    return await client().status(job_id)


if __name__ == "__main__":
    # Fail closed before listening if the relay or token is misconfigured.
    client()
    # RelayBridge constructor validates without making a print request.
    mcp.run(transport="streamable-http")
