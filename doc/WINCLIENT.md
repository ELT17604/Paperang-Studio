# Windows Client · 0.3.3-winclient-dev5

[简体中文](WINCLIENT.zh-CN.md) · [User guide](WINDOWS.md) · [Build](../Windows/README.md)

WinClient is the active Windows development target. WinLite feature development is paused. The desktop application uses WinUI 3, Windows App SDK 2.7.0 and .NET 10, with separate Core and Windows libraries. It does not reference or ship the WinLite executable project. Native printer, templates, administration, Relay, API, engineering and terminal pages replace the old settings windows.

## Run, build and package

Run `Paperang.WinClient.exe` from the published directory with its adjacent runtime files. These libraries include the self-contained .NET and Windows App SDK runtimes; multiple DLLs are expected. Old WinLite release folders are historical artifacts and are not loaded by this client.

From the repository root:

```powershell
./Windows/scripts/build-client.ps1
./Windows/scripts/build-client-installer.ps1
```

The installer script requires Inno Setup 6. Both scripts use `PAPERANG_WIN_WORK_DIR` when set; otherwise output goes to the sibling `PaperangStudio-Windows-Development/Release` directory. The portable directory is `v0.3.3-winclient-dev5-win-x64`; the installer is `PaperangStudio-0.3.3-winclient-dev5-win-x64-setup.exe`. ARM64 portable builds use `build-client.ps1 -Runtime win-arm64`; ARM64 packaging and execution are not validated here.

The per-user installer installs WinClient into `Programs/Paperang Studio`, separately from legacy WinLite. It preserves encrypted configuration and pending results during update/uninstall. It requests a graceful shutdown before replacing an existing WinClient. The published app implements `--version`, `--background` and `--shutdown`; opening it again restores the existing workspace. WinClient and WinLite share a printer-owner mutex and compatible encrypted storage identifiers to avoid two simultaneous printer owners. A running WinLite must be exited before installation/launch.

The build first runs a separate verification application compiled from `tests/windows/WinClientChecks`, then publishes a production application without those fixtures. It checks PRI/XBF resources, rejects legacy/test payloads, and runs the production app's offline `--smoke-test`. Earlier development releases omitted required PRI/XBF resources. Verification uses test storage and fake transports/HTTP fixtures; it does not print, connect to a real Relay or overwrite saved configuration. Production `--self-test` is intentionally unavailable.

## Pages and controls

The workspace places editing and print settings in the main column, with an auxiliary paper preview and a separate enlarged view. Content import and file drop automatically distinguish Mac/Android raster1 JSON from complete schema 1 parameter presets, including portable font aliases. Empty drafts can accept presets without a failed render; rejected JSON leaves the current content intact. The About page contains product/version information and user preferences.

Bluetooth scanning runs independently from the application operation lock. Rows appear during discovery, retain selection while signal values update, and can be selected/connected before the scan finishes. Stop/restart and window closure cancel scanning. The connection card distinguishes selected, connecting, connected, cancelled, disconnected and failed states. Scan UI tests use an injected discovery stream and real WinUI row bindings; physical Bluetooth discovery still requires hardware acceptance.

| Area | Windows implementation | Verification |
|---|---|---|
| Workspace | NavigationView, command bar, paper canvas, adaptive properties pane, zoom, Ctrl+O/S/P, file drop, automatic preview, PNG/RAW/raster1 import/export | Real WinUI layout and navigation; Chinese raster generation and exact export round-trips |
| Text and image | Installed fonts, alignment, margins, width, fit/native/crop, rotation, threshold/Floyd/Bayer, brightness/contrast/Gamma/inversion, Mac schema 1 presets | Synthetic image geometry, alpha-on-white, tone golden vectors and complete control/preset round-trips |
| QR | Text/URL/email, correction levels, square/rounded/dot modules, border and quiet zone | 18 format/shape/border combinations decoded independently by Java ZXing |
| Advanced layout | Drag/position/resize/reorder/delete; editable text, mixed rich typography, labels, QR sources and original image gray levels; independent object tone; confirm/cancel draft | Native rich-text rendering, geometry, label border, image re-toning and editable/legacy source restoration |
| Printer | USB-first/BLE-only selection, scan/address, connection reuse, disconnect, live battery/media status, density with readback, copies, before/after feed, progress and cancellation | Fake BLE/USB wire tests, exclusive ownership and cancellation/reconnect checks; physical hardware not exercised |
| Materials/history | Up to ten materials and ten opt-in successful sends, bounded storage, Windows user encryption, exact raster/layout/parameter restoration | Storage, raster and editable source round-trips; no automatic recording of document contents |
| Relay receiver | Owner/M credentials, HTTP consent, reconnect/auto-connect, native paper preferences, metadata switches, queue filter/search, full preview/load, reject and selected manual claim/print | Fake server tests for claim authority, persistent uncertainty, report recovery, no replay, manual/automatic separation, queue search and summary rejection |
| Templates | Presets, clone/create/delete, ordered blocks, font/style/size/margins/gaps, sample preview, JSON import/export, save/apply | Template render equality after editing and JSON round-trip |
| Management | Independent server/owner or C token/passphrase login without a pre-existing receiver; bounded session expiry, lock, keys/identities/sessions/jobs/audit, filters/pages, rename/rotate/revoke, paper confirmation, quotas, public key registration/pairing, JSONL export | Fake C bootstrap, role rejection, expiry, pagination and full-job requests; live administrative mutations and pairing not exercised |
| Engineering | 37-command Mac research catalog, 17 known GET commands, sample fields, bounded TX/RX logs, session JSON, actual transfer knobs, default-locked single-frame writes using the Mac whitelist | Query/response mapping, framing, rejection before transmission, custom packet sizes, density readback and trace tests |
| Local API v2 | Explicit loopback listener, token/CORS/Host checks, status/devices/protocol/settings/logs/OpenAPI, scan/connect/query/render/jobs/feed/cancel | Real loopback HTTP authentication, routing, consent and render checks; no physical printing |
| Paper terminal | Manual text buffer; real cmd/PowerShell ConPTY; profile/workdir/rows/columns, private input, control keys, bounded print buffer, idle batching, export/discard/flush | Real cmd input/output and shutdown; split ANSI/CR parsing, privacy, buffer commit and shared stop completion |
| Preferences | Native sign-in startup control, sanitized configuration export, version/source/release links, background receiver | Compiled/native navigation; startup registry writes and installed-app lifecycle not exercised automatically |

The [455-declaration Mac control inventory](MAC-WINCLIENT-CONTROLS.zh-CN.md) links each source element to its migration area or known difference. Module mapping and passing tests do not establish that every button, label, dynamic menu entry and accessibility behavior has received manual acceptance.

Source links in that inventory are relative to a repository checkout; application packages do not include the Swift source tree.

## Defaults and paper ownership

- BLE defaults match Android: 1008-byte raster payloads, 180-byte maximum writes bounded by negotiated limits, minimum 10 ms between write starts, zero additional frame pause, 6 s command timeout and 2 s post-data wait. FF03 credits and device/media checks can still delay transmission. USB keeps its separate framing/countdown semantics.
- Normal print batches default to 160 lines of final feed on Mac, Android and Windows. Previously saved explicit choices remain intact. Continuous paper-terminal batches intentionally default to zero extra feed.
- All page margins default to 0 px and content width to 576 printable dots. Explicit content whitespace, QR quiet zones and post-print feed are independent.
- Engineering transport controls affect actual workspace/API jobs, including reused connections and pre-feed commands. Smaller packets are rejected before sending when they would exceed the protocol's 256-frame index space.
- Paper permission starts off. Turning it off cancels active local/Relay sends; uncertain claimed work remains durable and is not replayed. Pausing automatic Relay selection alone allows an already active send to settle. Manual selection does not turn on automatic queue printing.
- Each print captures its parameters before sending. Printer ownership is exclusive across workspace, API, terminal and Relay. Closing the desktop leaves the receiver in the tray but stops its window-owned API and Shell. Exiting cancels sends and awaits receiver/Shell shutdown.

## Remaining differences and acceptance work

- Full Mac equivalence is not yet signed off. Dynamic UI items, keyboard focus, screen-reader behavior, high-DPI/small-window states and every physical printer operation still require interactive/hardware acceptance.
- Windows uses cmd/PowerShell and ConPTY instead of zsh/PTTY. This is a text paper terminal, not a full-screen VT emulator. PowerShell interactive applications have not been comprehensively tested.
- Image import currently covers PNG/JPEG/BMP/GIF/TIFF and RAW/raster1. HEIC/HEIF import is not implemented. Editable image objects have a 48×24 minimum frame and a 2 MB normalized gray source limit. Older raster-only materials remain flattened.
- The native font picker supports installed family selection; it does not reproduce every macOS system font-panel capability. Template block edits use an explicit apply action.
- The API has the principal v2 operations, complete protocol catalog, device filtering and bounded logs. Exact Mac response-field/status parity and detailed OpenAPI request schemas still need reconciliation. API/Shell lifetime currently follows the workspace window.
- Management pagination uses the server's page cursor directly; old servers that cap pages at 25 do not aggregate a requested 100-row page. Live administrative mutations and owner pairing require dedicated acceptance.
- The tray has workspace/quit; Mac's additional API-token and automatic-print shortcuts remain desktop actions. GitHub release links are present; automatic WinClient release comparison is not implemented.
- The new installer is a development package. Installer compilation and published-app self-tests are separate from installation, update/uninstall and ARM64 smoke tests. Touch-only drawing remains intentionally excluded.

Design references: [NavigationView](https://learn.microsoft.com/windows/apps/develop/ui/controls/navigationview), [CommandBar](https://learn.microsoft.com/windows/apps/develop/ui/controls/command-bar), [Windows app design](https://learn.microsoft.com/windows/apps/design/) and [ConPTY](https://learn.microsoft.com/windows/console/creating-a-pseudoconsole-session).
