# Paperang Studio — ChatGPT integration (experimental)

This adapter is derived from Paperang Studio v0.3.0 and its
`Skills/paperang-relay-print/` sender skill. It does not change the relay
or require exposing local Mac HTTP port 8176.

## Two entrypoints

**MCP (preferred direction):** `server.py` offers a small Streamable HTTP
MCP service with tools `relay_capabilities`, `print_text`, and
`print_status`. Requires Python 3.10+, `pip install -r requirements.txt`,
`PAPERANG_RELAY_URL=https://your-relay.example` and a **dedicated A/B**
`PAPERANG_RELAY_SUBMIT_KEY`. Start `python server.py` and access the local
MCP endpoint at `http://127.0.0.1:8188/mcp`.

**SECURITY:** This is a localhost-only backend, **not a complete
public ChatGPT connector**. A ChatGPT-facing deployment requires a
separately configured HTTPS, authenticated, OAuth-capable MCP gateway
with proper user authorization; do not publish the bare endpoint or
put a shared submit key in the URL. The single configured token represents
one sender identity; it is not multiuser OAuth isolation.

**Legacy GPT Actions:** Paste `openapi.yaml` into an existing eligible GPT
Actions editor, replacing the example `servers[0].url` with the **actual
HTTPS root** of your deployed Relay API v2. Set Authentication → API Key →
Bearer and provide a dedicated B (or A) token in the editor. A keys can
submit but cannot read job status. This mode talks directly to the
existing relay; MCP server not needed. Personal accounts may no longer
be able to create a new custom GPT, so this is a compatibility artifact.

## Operational rules

- A print request is a **physical side effect**: only submit on explicit
  authorization. A preview does not authorize print. One job per copy.
- Use UUID `request_id` idempotency per intended job. For network
  ambiguity never retry with a new UUID or auto-print a second copy.
- `accepted` is relay acceptance; `sent` is transport completion;
  only a human-confirmed `confirmed` means paper was checked.
- Restrict to A/B credentials. Never put M/C owner/admin/device keys into
  this integration. Do not expose print body or history through status.
- The relay cannot set receiver-local heat density, feed, or template.
- This minimal adapter supports text, not the RelaySkill's Pillow-based
  image transformations. Preserve raster1 constraints for future tools.
- Deploy the current v0.3.0 relay and online Mac/WinLite receiver first.
- The code is source-level integration only; no live printer, end-to-end
  ChatGPT connection, or OAuth gateway was tested.

See [RelaySkill](../../doc/RELAY-SKILL.md),
[API v2 authorization](../../doc/AUTH-v2.md) and
[relay deployment](../../Server/README.md).

License: CC BY-NC-SA 4.0; © 2026 ELT Group. Retain attribution/NOTICE.
