# Contributing

Releases follow the [distribution version policy](doc/VERSIONING.md). Update [VERSIONS.json](VERSIONS.json) and the [feature implementation matrix](doc/FEATURE-MATRIX.md) for every relevant change; run `python scripts/check-versions.py`. Same-baseline compatibility is mandatory for shared contracts; document distribution-specific omissions rather than claiming unverified parity.

[English](CONTRIBUTING.md) · [简体中文](doc/CONTRIBUTING.zh-CN.md)

Read the [protocol](doc/PROTOCOL.md), [architecture](doc/ARCHITECTURE.md), and [acquisition record](doc/ACQUISITION.md) first. Protocol changes should include pinned source links or redacted captures, model/firmware, CRC and ACK/data distinctions, and physical observations. Do not present emulator constants as device measurements. Preserve576-column whole-row packing and the shared device lock.

Connect every paper-using feature to the sidebar's single global switch. Default to no automatic connection/printing and no automatic replay of failed jobs. Keep API routes, [API documentation](doc/API.md), [OpenAPI](doc/openapi.json), and clients synchronized. Keep actual Chinese wire enum values unchanged when translating documentation.

Validate UI changes in the app and record hardware validation scope. Keep build/test output, credentials, raw device identifiers, sessions, and third-party checkouts outside the published source. English pages use default filenames; Chinese translations live under `doc/*.zh-CN.md`, with reciprocal language links.

Contributions/adaptations follow CC BY-NC-SA 4.0: retain ELT Group attribution, [LICENSE](LICENSE), and [NOTICE](NOTICE). Distribution to other Macs may require Developer ID signing and notarization.


### ChatGPT integration (experimental)

Keep `Integrations/ChatGPT/` isolated from the Mac-local API v2 and existing relay API v2 contract. Mirror new integration behavior in `doc/CHATGPT.md` and `doc/CHATGPT.zh-CN.md`; mark live gateway/MCP/hardware validation separately from source and mock tests. Never publish a bare token-bearing MCP endpoint or use M/C credentials for submission. Preserve UUID idempotency and the no-automatic-reprint rule.
