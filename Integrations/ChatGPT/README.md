# Paperang Studio · ChatGPT integration

[Project](../../README.md) · [English guide](../../doc/CHATGPT.md) · [简体中文](../../doc/CHATGPT.zh-CN.md) · [RelaySkill](../../doc/RELAY-SKILL.md)

Experimental **source integration**, not a released distribution or a connected
ChatGPT account. Uses existing **Relay API v2**, independent of the Mac-local
HTTP API v1, with the same A/B credential permissions and raster1 schema 1.

## Architecture

```text
ChatGPT → authenticated HTTPS OAuth-capable gateway → MCP localhost adapter
        → HTTPS Relay API v2 → MacClient/WinLite (M receiver) → Bluetooth P2
```

The gateway is **not included**. The adapter binds to 127.0.0.1:8188 and
must never be exposed directly to the internet. The bridge has one fixed
A/B sender identity: it is intended for a private single-user integration,
not multi-tenant authorization. Install/connect a ChatGPT plugin only after
the authenticated gateway supports the ChatGPT connection requirements.

## Tools and boundaries

| MCP tool | Relay API | Scope |
|---|---|---|
| `relay_capabilities` | `GET /v2/public/status` | policy/content capabilities, **not** physical readiness |
| `print_text` | `POST /v2/jobs` | explicitly authorized text, exactly one job |
| `print_raster1` | `POST /v2/jobs` | already packed 576-dot binary raster, optional caption |
| `print_status` | `GET /v2/jobs/{id}` or `.../lookup/{shortID}` | B-own only; A cannot read status |

All printing is consequential: request authorization before submitting,
including from content gathered via files/webpages. One explicit copy means
one request_id. Never print for a mere preview, suggestion or installation.
Generate a canonical UUID **once per intentional submission**; preserve it
on any ambiguous network failure. The relay uses UUID idempotency; never
resend with a different UUID to "fix" a timeout.

`accepted` means queued by server. `sent` means transport completion, not
paper confirmation; `confirmed` is manual C-owner verification. This
adapter never confirms prints or fetches C history. A-role tokens return
`statusReadable=false` and status GET is forbidden. B tokens may read only
jobs under their identity.

The adapter does not expose C/M credentials, printer controls, the local
Mac API, thermal density, physical feed or receiver templates. Text uses
the receiver's active template. `print_raster1` takes exact pre-rendered
base64 one-bit bytes, **not** a PNG/JPEG or arbitrary source URL.
It checks 576 columns/72 bytes each row, 1..1400 rows, MSB-first,
black=1 and SHA-256. Combined receiver footer + content must still fit
1400 rows and the active relay quota. For full image/photo/CJK rendering,
use existing `Skills/paperang-relay-print/` sender locally to prepare a
raster payload before invoking this bridge. The MCP tool cannot itself
open local ChatGPT image uploads or change printer heat settings.

## Source installation

Python 3.10+ and `httpx`; the MCP server additionally requires `mcp`.
Start from the repository checkout:

```sh
python3 -m venv /private/path/paperang-chatgpt-venv
/private/path/paperang-chatgpt-venv/bin/pip install -r Integrations/ChatGPT/requirements.txt
export PAPERANG_RELAY_URL=https://relay.example.com
export PAPERANG_RELAY_SUBMIT_KEY=psr_YOUR_DEDICATED_A_OR_B_RANDOM_TOKEN
/private/path/paperang-chatgpt-venv/bin/python Integrations/ChatGPT/server.py
```

Use a **fresh, dedicated, revocable A or B random key** created in the
project's C management UI/CLI. Do not use an M receiver key or C owner key.
Put secrets in a restricted environment file/secret manager, never in
repo commits, public examples, URL query parameters, command-line flags,
or ChatGPT messages. HTTPS is mandatory for the relay root; redirects
are rejected. The MCP backend is locally available at
`http://127.0.0.1:8188/mcp`, not publicly authenticated. Deploy a
trusted HTTPS/OAuth gateway first, configured to forward only the MCP
endpoint and enforce authorization. A generic reverse proxy with TLS
alone does **not** provide OAuth or per-user authorization.

If your ChatGPT plan/workspace has an eligible plugin creation flow,
install the resulting private plugin from that UI and authorize it.
Availability depends on ChatGPT account/workspace policy; code in GitHub
does not install anything into a conversation. Do not publish a
shared-token multiuser plugin.

## Optional compatibility: GPT Actions

`openapi.yaml` is a **minimal legacy Actions schema** for qualifying
existing GPT environments. Set its server URL to the deployed HTTPS
relay root, configure bearer authentication with a **dedicated A/B**
sender key, and review the consequential-print permission. This direct
Actions path does not use `server.py`. It is not a replacement for an
OAuth-authenticated multiuser MCP gateway.

## Validation

```sh
python3 -m unittest discover -s Integrations/ChatGPT -p 'test_*.py' -v
python3 scripts/check-versions.py
```

Unit tests use `httpx.MockTransport` and do **not** send paper or access
a live server. After deployment, run a controlled staging test against
a v0.3.0 relay: capabilities, A read denial, B own-status read, one
approved text print, raster1 render, uncertain timeout/no replay, and
manual paper check. Check target environment's MCP API/auth contract.
**No live ChatGPT integration, Linux deployment or physical P2 print
is claimed by this source-only change.**

© 2026 ELT Group · CC BY-NC-SA 4.0 · [LICENSE](../../LICENSE) · [NOTICE](../../NOTICE).
