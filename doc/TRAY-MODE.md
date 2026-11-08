# Menu bar / tray mode · v0.2.3

> v0.3.0: historical observations/screenshots below remain recorded evidence. Current relay authentication, history access, dependencies and validation status are defined by [API v2](AUTH-v2.md), [upgrade](UPGRADE-v0.3.0.zh-CN.md) and [matrix](FEATURE-MATRIX.md). Local API v1 and BLE/template records are separate contracts.

[简体中文](TRAY-MODE.zh-CN.md) · [User guide](USER-GUIDE.md)

Choose **进入托盘模式** at the bottom of the sidebar or in File. The main window is hidden and the Dock icon disappears; a printer icon remains in the macOS menu bar. Click it to see Bluetooth, print progress, actual local-listener state, relay online/retry/error state, pause/resume network printing, copy the local API token, restore the main interface, or quit.

Entering the mode explicitly enables unattended **network jobs for this session**. It starts the configured authenticated loopback API if necessary, preserves the current Bluetooth connection and relay polling, and enables automatic processing on an already running relay. Connect/configure the printer and relay before entering; tray mode does not scan for other devices, start a stopped relay with missing credentials, alter Bluetooth system settings, or persist keys. Printer/device readiness checks, media and density validation, bounded job data and the five-retry relay policy remain active. A failed relay connection stays disconnected until manually restarted from the main interface; a warning appears in the menu and its icon.

The PTY shell and its idle timer are stopped. Workspace preview and selected-task fetch tasks are cancelled; new automatic workspace previews are suppressed while hidden. Incoming network jobs are still rendered and validated because that is required for printing. No shell output or engineering command is started by entering tray mode. A print already underway may finish; bytes already buffered by the printer cannot be recalled. This is an active user-session app, not a launch daemon, login item or sleep-prevention service; quitting, logging out or system sleep can stop communication.

## Consent and API

Network print consent is separate from the workspace's sidebar paper switch. `GET /v1/status` reports `appVersion`, `trayMode`, `networkPrintAllowed`, `automaticNetworkConsent`, `workspacePrintingEnabled`, and `printingEnabled` (effective network permission). These flags authorize jobs; they do not assert printer readiness or paper completion. `apiRunning` in the menu comes from the listener's actual startup callback; port conflicts are shown as listener failure rather than a false connected state.

| State | POST /v1/jobs | Relay inbox |
|---|---|---|
| Normal | Global paper switch enabled **and** `allowPaper:true` | Existing manual/automatic preference |
| Tray, enabled | May omit `allowPaper`; explicit `true` accepted, explicit `false` or wrong type rejected | New queued jobs automatically handled when ready |
| Tray, paused | New jobs rejected with 403 | Automatic handling paused |

Bearer authentication, allowed origins and 127.0.0.1 binding still apply. Raw feed requests require effective network permission **and explicit `allowPaper:true`**. Print-job feed/density settings apply normally. Pausing prevents new submissions/claims, but does not interrupt data already sending or buffered. Unknown jobs are never automatically replayed. Sending complete and manual paper confirmation remain distinct.

Restoring the main interface exits tray mode, removes the menu icon, restores the pre-entry relay automatic-print preference, and stops the local API only if tray mode started it. An API that was already active stays active. The workspace paper switch is never automatically changed. PTY shells are not restarted. Tray mode/consent is not persisted across launches.

## Version and rollout

v0.2.3 synchronizes the bundle/About/sidebar/menu, repository VERSION, server version/status, website title/footer/meta, SDK package and OpenAPI/current documentation. Historical changelog entries and third-party dependency versions retain their original values. Restart the app and updated server; static web files must be deployed together. Public relay status returns `version:"0.2.3"` and `status.automaticPrinting` based on fresh device telemetry; the website identifies automatic mode when this flag is true.

Reopening/activating the app while in tray mode does not end its background session. Use the explicit Restore action in the menu bar to restore the window and prior permissions.

`relayAutomaticPrinting` reports whether the relay consumer is actually enabled. The webpage uses this when available; network API consent alone does not imply that a relay paused after a print error is automatically consuming tasks.

Python/JavaScript SDK print helpers omit per-request consent by default so tray mode can authorize the request. Explicit false is retained as a veto; normal mode still requires explicit true.
