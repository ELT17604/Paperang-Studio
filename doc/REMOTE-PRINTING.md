# Remote printing and deployment

Read the [API v2 authentication contract](AUTH-v2.md), [server deployment parameters](../Server/README.md) and [upgrade and rollback](UPGRADE-v0.3.2.md).

WinClient → Remote printing saves the Relay URL and paired owner file or scoped M execution credential. Printer selection is on the Printer page; C administration has its own Management page. See the [Windows guide](WINDOWS.md). Mac Remote printing selects its paired owner file or scoped M credential; its C panel owns history, preview and administration. Clients poll outward, need no public inbound port, and retain local template/density/feed control. Both text and raster1 are supported.

Persistent secure browser sessions replace tab-local receipt capabilities. An anonymous browser must register before posting. A gets acceptance only; B can read its stable identity’s status; content/delivery metadata are C-only history. M fetches payload only after claiming, with the active claim secret. Public web status does not expose printer telemetry. Unknown output is never replayed automatically; sent is distinct from C manual paper confirmation.
