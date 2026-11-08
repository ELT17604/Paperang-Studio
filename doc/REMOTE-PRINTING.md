# Remote printing and deployment — v0.3.0

Read the [API v2 authentication contract](AUTH-v2.md), [server deployment parameters](../Server/README.md), [coordinated migration/rollback](UPGRADE-v0.3.0.zh-CN.md) and [platform handoff prompts](HANDOFF-v0.3.0.zh-CN.md).

WinLite settings → Connection saves the relay URL, M credential and P2. Settings → Management opens native C administration. Mac Remote printing configures M; its C panel owns history, preview and administration. Clients poll outward, need no public inbound port, and retain local template/density/feed control. Both text and raster1 are supported.

Persistent secure browser sessions replace tab-local receipt capabilities. An anonymous browser must register before posting. A gets acceptance only; B can read its stable identity’s status; content/delivery metadata are C-only history. M fetches payload only after claiming, with the active claim secret. Public web status does not expose printer telemetry. Unknown output is never replayed automatically; sent is distinct from C manual paper confirmation.

Earlier 0.2.x /v1, receipt-content and standard-library-only deployment instructions are retired. Do not re-enable old content routes for compatibility.
