# Version Feature Implementation Matrix

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Version policy](VERSIONING.md) · [Release registry](../VERSIONS.json)

Reviewed **2026-11-08**, baseline **0.2.3**. This is the parity/release checklist, not a promise that every distribution already has every feature. Same-baseline common contracts must be compatible; full-client requirements and WinLite exclusions are recorded separately. Update this matrix with each release.

## Distribution inventory

| Distribution | Canonical release | Actual runtime/source | Scope |
| --- | --- | --- | --- |
| MacClient / `macclient` | v0.2.3-macclient-fix1 | `VERSION`: 0.2.3-fix1 (legacy alias) | Full native macOS client |
| LinuxRelayServer / `linuxrelayserver` | v0.2.3-linuxrelayserver | `Server/relay.py`: 0.2.3 (legacy alias) | Relay, database and submission web UI; no local printer transport |
| WinLite / `winlite` | v0.2.3-winlite-fix1 | `Windows/VERSION` | Windows tray-only receiver and template editor |
| WinClient / `winclient` | Not released | Planned; no implementation | Future full Windows client |
| LinuxClient / `linuxclient` | Not released | Planned; no implementation | Future local Linux client |

**I** = implemented, with evidence below; **P** = planned/not implemented; **O** = intentionally out of this distribution's scope; **—** = not applicable. I does not claim every platform/firmware has been physically verified. No feature is called technically impossible without evidence. Full-client P cells are parity targets, not release commitments.

## Shared baseline and interoperability

These are mandatory for clients that receive relay jobs and for the server where applicable. A failure blocks a shared-baseline compatibility claim.

| Feature / contract | MacClient | LinuxRelayServer | WinLite | WinClient | LinuxClient |
| --- | --- | --- | --- | --- | --- |
| Relay HTTP v1 and Bearer role isolation | I | I (provider) | I | P | P |
| Text and raster1 schema 1 capability negotiation | I | I | I | P | P |
| Full job retrieval before print; reject corrupt size/SHA-256 | I | I (validation/storage) | I | P | P |
| queued/claimed/printing/sent/unknown/rejected state contract | I | I (authority) | I | P | P |
| Separate transport sent from manual paper confirmed | I (confirmation UI) | I (endpoint) | I (sent only; no confirmation UI) | P | P |
| No automatic replay of uncertain/failed sends | I | I (timeout to unknown) | I | P | P |
| Partition progress remains transport-only | I | I (parse/store/web) | I | P | P |
| Template JSON v1, ten block kinds and one-pass variables | I | — (does not render templates) | I | P | P |
| Drawing/caption composition and 1,400-row limit | I | I (input limits) | I | P | P |
| Legacy P2 576 dots, 72-byte rows, MSB-first, black=1 | I | I (raster input) | I | P | P |
| BLE FF03 credits, key-zero CRC, media/density checks | I (CoreBluetooth) | — | I (Windows GATT) | P | P |

## Client capabilities and parity targets

| Feature | MacClient | LinuxRelayServer | WinLite | WinClient | LinuxClient | Difference / requirement |
| --- | --- | --- | --- | --- | --- | --- |
| Text/image workspace, image transformations/dithering | I | — | O | P | P | Full-client parity target; WinLite intentionally has no main workspace |
| Live block-template editor, presets, JSON import/export | I | — | I | P | P | Same schema; font substitution and line wrapping can differ |
| Full inbox/history, reject and manual confirm UI | I | — | O | P | P | WinLite automatically receives queued jobs; server endpoints still exist |
| Tray receiver with pause/automatic print permission | I | — | I | P | P | WinLite always starts in tray; Mac explicitly enters tray mode |
| Credentials persist across process restart | O (memory-only) | I (hashed keys) | I (DPAPI) | P | P | Not equivalent security stores; Mac must add explicit secure storage before parity is claimed |
| Template definitions/selected style persist | I | — | I (autosave in fix1) | P | P | Mac editor saves explicitly; Windows also autosaves selected print options |
| Durable local pending-result recovery | O (session-only) | I (SQLite job states) | I (encrypted journal) | P | P | Windows recovery reports unknown; never reprints old claim |
| Continuous 60-second cooldown retry cycles | O (stops after five retries) | — | I (default on, configurable) | P | P | Documented policy difference, not a relay protocol change |
| User sign-in startup option | O | I (systemd deployment) | I (HKCU Run) | P | P | Server service and desktop login startup serve different lifecycles |
| Local authenticated HTTP API / SDK integration | I | — (different relay API) | O | P | P | Existing Python/JS Clients target Mac local API, not WinLite |
| Interactive physical-paper terminal | I (zsh PTY) | — | O | P | P | Windows shell/PTY needs platform-specific implementation; no shell execution in WinLite |
| Readings, protocol catalog and guarded engineering tools | I | — | O (only printing prerequisites) | P | P | Firmware/unknown command writes remain restricted, not a parity goal |
| Native administrator key-management UI | I | I (admin endpoints/CLI) | O | P | P | Device key remains sufficient for WinLite reception |

## Server-only responsibilities

LinuxRelayServer implements the submission webpage/drawing canvas, anonymous/authenticated quotas, multiple key roles and revocation, receipt/short-ID access control, SQLite persistence and retention, and systemd/temporary deployment tools. Clients consume these services; they do not need separate copies. Server rendering of template JSON, USB printing, IPP/MCP, real-time charging and LED controls are **not implemented baseline capabilities**. Their appearance in research references is not evidence of support.

## Evidence and verification boundaries

| Area | Source / evidence | Verification scope |
| --- | --- | --- |
| Mac protocol/printing | [Bluetooth.swift](../Sources/PaperangLab/Bluetooth.swift), [Workspace.swift](../Sources/PaperangLab/Workspace.swift), [protocol record](PROTOCOL.md) | Historical P2 observations in the original docs; no new macOS run in this Windows update |
| Mac templates/tray | [RelayLayout.swift](../Sources/PaperangLab/RelayLayout.swift), [TrayMode.swift](../Sources/PaperangLab/TrayMode.swift) | Source-level comparison; template structure/semantics compared with Windows tests |
| Relay contract | [relay.py](../Server/relay.py), [rich media](RICH-MEDIA.md) | Original real HTTP server and SQLite integration tested with Windows mock transport; server deployment not changed |
| Windows protocol/template/queue | [Core](../Windows/src/Paperang.Core), [WinLite](../Windows/src/Paperang.WinLite), [tests](../Windows/tests) | Automated regression, DPAPI restart/atomic-write tests, retry/cancellation/concurrency and 1,200-request offline stress |
| Windows actual P2 | [porting record](WINDOWS-PORTING.md) | 2026-11-06 complete Chinese paper confirmed by user, 150-row feed and two reconnects; not a guarantee for every adapter |
| Windows UI/startup/resources | [Windows guide](WINDOWS.md) | Live UI inspection, quoted background startup and single-instance checks, bounded short soak; no actual Windows reboot or indefinite stability guarantee |

Every new full client must fill its P cells with source and verification, or document an accepted O exception with a concrete reason before claiming parity. A future technical limitation needs reproducible platform evidence, its user impact and a fallback; “not implemented yet” is not a technical impossibility.
