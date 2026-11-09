#!/usr/bin/env python3
"""Local-only Streamable HTTP MCP tools for Paperang Studio Relay API v2.

Requires a separate authenticated HTTPS/OAuth gateway for remote access.
Do not bind directly to 0.0.0.0 or expose the endpoint publicly.
"""
import ipaddress
import os

from mcp.server.fastmcp import FastMCP
from core import PaperangError, configured_client, validated_job

HOST = os.environ.get("PAPERANG_MCP_HOST", "127.0.0.1")
PORT = int(os.environ.get("PAPERANG_MCP_PORT", "8188"))
# This service holds a single sender key and has no inbound user authentication.
# Prevent accidental public exposure even if the environment is misconfigured.
if not ipaddress.ip_address(HOST).is_loopback:
    raise RuntimeError("MCP backend must bind to a loopback IP; configure an authenticated gateway")
if not 1 <= PORT <= 65535:
    raise RuntimeError("Invalid PAPERANG_MCP_PORT")

mcp = FastMCP("Paperang Studio", host=HOST, port=PORT)


@mcp.tool()
async def relay_capabilities() -> dict:
    """Read Relay v2 acceptance policy and supported content formats.

    Public acceptance does not prove receiver online, printer connected, or paper present.
    """
    return await configured_client().capabilities()


@mcp.tool()
async def print_text(text: str, request_id: str) -> dict:
    """Print one explicitly authorized text note (physical paper side effect).

    Call only after the user explicitly requests printing this exact content.
    request_id is one canonical UUID for this intended print. On an uncertain
    timeout NEVER create a new UUID or automatically submit again. One copy only.
    Receiver controls density, feed and layout.
    """
    return await configured_client().submit({"text": text}, request_id)


@mcp.tool()
async def print_raster1(content: dict, request_id: str, caption: str = "") -> dict:
    """Print one explicitly authorized RelaySkill-compatible 576-dot raster1.

    content is the exact 'content' object from RelaySkill's prepare output.
    Images must be rendered by a trusted sender into raster1 schema 1; raw
    URLs and arbitrary filesystem paths are intentionally unsupported.
    A caption is optional and uses the receiver template. No auto-resubmit.
    """
    job, _ = validated_job(caption, content)
    return await configured_client().submit(job, request_id)


@mcp.tool()
async def print_status(job_id: str) -> dict:
    """Read own job status (requires B sender key; A gets HTTP 403).

    'accepted' means queued by relay; 'sent' is transport completion only;
    'confirmed' is manual paper confirmation. Never infer paper from 'sent'.
    """
    return await configured_client().status(job_id)


if __name__ == "__main__":
    configured_client()  # Validate endpoint/token before accepting requests.
    mcp.run(transport="streamable-http")
