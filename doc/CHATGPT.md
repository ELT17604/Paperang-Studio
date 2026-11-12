# ChatGPT integration · experimental v0.3.0

**Status: ChatGPT MCP development is paused and the current version is unavailable for use.** The adapter source is retained. ChatGPT access requires a separate authenticated HTTPS/OAuth gateway and a configured ChatGPT connection. RelaySkill works independently.


> opt1: Regression sources are archived outside the shipping repository. See [validation and reproduction](OPTIMIZATION-opt1.md). Historical test paths below refer to Git revision `4393ae0`.
[简体中文](CHATGPT.zh-CN.md) · [Documentation](README.md) · [Source](../Integrations/ChatGPT/README.md) · [RelaySkill](RELAY-SKILL.md)

Paperang Studio adds a **source-level** ChatGPT bridge. It reuses the v0.3.0 Linux Relay API v2, sender roles A/B and raster1 schema 1. It is **not** a fifth released distribution, an installed ChatGPT plugin, or a deployed public service. No changes to BLE framing, receiver templates, Mac-local API v2 or relay contract.

## Components

ChatGPT → authenticated HTTPS OAuth-capable gateway (not included) → localhost MCP adapter → Relay API v2 → MacClient / WinLite M receiver → physical P2.

The existing `RelaySkill` is a local CLI/agent sender with Pillow rendering; the new `Integrations/ChatGPT/` layer provides a lightweight HTTP/MCP boundary. The MCP server deliberately binds loopback `127.0.0.1:8188`, and **has no built-in OAuth or user sessions**. Never publish this endpoint directly, even behind a TLS-only reverse proxy. Use a supported authenticated gateway, HTTPS, request limits and a single private user identity, or do not connect it to ChatGPT.

## Capabilities

- Read server policy, supported payload types and size limits. Public status is **not live printer readiness**.
- Submit explicitly user-approved text as **one physical print job** through `/v2/jobs`.
- Submit pre-rendered `raster1` bitmaps: exactly 576 columns, 72 bytes/row, MSB-first, black=1, 1–1400 rows; checks base64 length, SHA-256 and request-size ceiling.
- Query own job status under a B sender key (full UUID or 12-hex short ID); A submit key cannot read statuses.
- Explicit per-job UUID `request_id` idempotency. No automatic POST retries. Unknown completion remains unknown; never create a fresh UUID after network ambiguity.

This integration does **not** process PNG/JPEG uploads, render fonts, configure physical feed/density, select local templates, expose admin histories, issue C/M credentials or prove the page printed. Use RelaySkill to prepare images locally. The receiver can add a footer and its own template, subject to combined 1400-row limit.

## Installation and validation

See [the full deployment guide](../Integrations/ChatGPT/README.md). Requirements: Python 3.10+, the repository's compatible v0.3.0 relay, a dedicated revocable A/B `psr_` key, an online MacClient/WinLite receiver and a separately deployed authenticated HTTPS gateway. A dedicated B key is recommended if status reads are desired. ChatGPT plugin creation/connection is a separate account-specific action. For legacy GPT Actions, see the optional `openapi.yaml` compatibility file (direct relay HTTPS path; no MCP server).

Regression sources are in the external archive described above. These tests use a mock transport and do not access hardware. Linux deployment, MCP-to-ChatGPT connection and physical paper verification remain **unvalidated**, not implicitly successful.

## Security and terminology

`accepted` means relay accepted a job; `sent` means receiver transport completed; only C manual confirmation can mark `confirmed`. A timeout may hide an accepted task. Print only content the user intentionally authorized; reading files, generating a preview or enabling the plugin does not authorize printing. Secrets must remain outside the repository and chat. Do not expose M device or C owner keys.

See [API v2](AUTH-v2.md), [remote printing](REMOTE-PRINTING.md), [version policy](VERSIONING.md), [feature matrix](FEATURE-MATRIX.md) and [data retention](RELAY-DATA.md).

© 2026 ELT Group · [LICENSE](../LICENSE) · [NOTICE](../NOTICE) · CC BY-NC-SA 4.0.
