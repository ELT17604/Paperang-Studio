> v0.3.2-winlite（未发布）：Relay API v2；模板/配置 schema 1 不变。当前测试与限制见 [Windows 验证](VALIDATION-v0.3.2-windows.md)，下文 fix/opt 验收数字是历史记录。

> v0.3.1: [所有者私钥与匿名进度修正](OWNER-CUTOVER-v0.3.1.zh-CN.md)。WinLite 默认使用已配对私钥，不再把 M 密钥作为管理凭证。


# Windows tray client · V0.3.0-winlite

> **v0.3.0:** [API v2 authentication](AUTH-v2.md) · [Upgrade](UPGRADE-v0.3.0.zh-CN.md) · [Handoff](HANDOFF-v0.3.0.zh-CN.md). WinLite includes a C management panel; web receipts no longer grant content access.

[简体中文](WINDOWS.zh-CN.md) · [Source and build](../Windows/README.md) · [Documentation](README.md)

Native C#/.NET 10, WinForms and Windows BLE GATT. Requires Windows 10 2004 or Windows 11 with a BLE adapter. The lightweight package needs .NET 10 Desktop Runtime; the self-contained package includes it. No Python, Node, WebView or printer driver is needed at runtime.

## Setup and tray behavior

1. Extract to a stable directory and run `PaperangStudio.exe`. It starts in the tray without a main window.
2. Double-click the tray icon, enter the relay root URL and select the **paired owner private-key JSON file**. Old M keys cannot open C management.
3. Turn on Bluetooth and the P2, scan, select your printer, then save and connect. Avoid simultaneous Mac/phone connections to the same printer.
4. Closing settings keeps the app running. The tray label “开启自动打印” stays fixed; a left check mark means enabled. Settings show live relay/printer status, and the Connection tab provides save/connect directly.
5. Startup settings offer current-user Windows sign-in startup and automatic connection. Move the executable to its permanent location before enabling startup. If disabled by Windows Task Manager, re-enable it there.

Unlike the full macOS workspace, this user-requested tray-only edition defaults to automatic connection and print consent after configuration. Both are configurable. The full macOS app's defaults remain unchanged. Pausing prevents new claims; buffered printer data cannot be recalled.

## Template editor

Settings → Print → graphical template editor. Four presets match the macOS block definitions: note, receipt, retro teletype and keepsake.

- Ten block kinds: title, body, sender, time, job ID, IP, UA, browser information, separator and custom text.
- Rename/copy/load/delete templates; add/delete/enable blocks; drag to reorder or use up/down buttons.
- Font, size 8–96, bold, left/center/right alignment, horizontal margin 0–120 dots, upper gap 0–200 rows.
- Click the paper to select a block; horizontal dragging changes its margin, vertical dragging changes its upper gap. Edit sample text or switch to a drawing sample.
- Import/export the same single-template **v1 JSON** used by macOS. Up to 30 templates, 24 blocks each, 64 KiB per import. Template text allows 2,000 characters per block.
- Variables `{{text}} {{sender}} {{id}} {{time}} {{ip}} {{ua}} {{device}}` are expanded once. Tokens inside visitor content are not recursively expanded.

The body block places the drawing before its caption. Nearest-neighbor fitting preserves one-bit pixels; zero margin preserves original raster bytes. Disabling body hides both drawing and caption. All blocks flow vertically, with a maximum composite height of 1,400 rows. Preview and printing share the same renderer. Saving applies templates to later jobs and persists them; active jobs keep their layout snapshot. Cancel discards the draft.

JSON retains original font names. Windows maps PingFang SC to Microsoft YaHei, Menlo to Consolas, and Helvetica to Arial. Glyphs and line breaks may differ between operating systems; the template structure and image composition remain compatible.

## Relay and local persistence

The current style, selected template and printing options autosave after a short debounce and flush on closing settings. They restore on the next launch/login without save/connect. Connection credentials change only through save/connect.

The client polls and reports every two seconds, claims and durably records uncertainty before fetching the active payload, validates raster1 geometry and SHA-256, and processes queued jobs in creation order. Requests include `text,raster1` capability negotiation and explicit Content-Length for the original relay's HTTP parser. Printing progress uses the existing partition-report format.

Claim information is atomically persisted **before** any printer bytes. Successful sends persist `sent` before reporting. Interrupted claims recover as `unknown` and are never automatically printed again. Failed final reports retry reporting only. An outstanding result requires the same server and device key until resolved.

Credentials, preferences, templates and pending results are encrypted with current-user Windows DPAPI in `%LOCALAPPDATA%\ELT\PaperangStudioWinLite`. No note body or plaintext credential log is retained. These files cannot be transferred to another Windows account for decryption.

Continuous reconnect defaults on, including migrated settings: after the initial five retries fail, wait 60 seconds, try another five times, and repeat until successful. Disable the option to retain the original stop behavior. Disconnect/quit cancels waiting immediately. The selected printer retries every 30 seconds. Repeated background launches remain silent; explicit repeated launches open settings. Enabled startup registrations repair their executable path after upgrades; user-disabled Windows startup approval is not overridden. This is a user-session app, not a service or sleep-prevention tool.

The first Windows edition includes relay reception, tray settings and template editing. It does not include the macOS workspace, PTY, engineering writes, local HTTP API or macOS workspace task window; C administration and Relay history are available. `sent` is transport completion, not an automatic physical-paper confirmation.

## Build and verification

Fix1 adds persistence/restart, old-setting defaults, exact one-minute cooldown configuration, cancellation and concurrent-start/stop checks. A 1,200-request offline stress test retained bounded managed memory with one request in flight. A 90-second live UI/relay observation showed private memory about 39–45 MB and handles falling overall; this is a short sample, not a guarantee of indefinite stability. Startup was verified by the registered quoted executable/`--background` command, silent duplicate launch and restored preferences; Windows was not rebooted.

From the repository root:

```powershell
dotnet build Windows/src/Paperang.WinLite -c Release
./Windows/scripts/build.ps1
./Windows/scripts/build.ps1 -SelfContained
```

Python 3.10+ with Server/requirements.txt is needed only for the integration test, which runs the included v0.3.0 relay with a simulated printer. It checks long-text detail retrieval, raster1, authentication, claims and final database states without using paper. Build output defaults outside the source tree, under sibling `PaperangStudio-Windows-Development/Release/`.

The 51 checks cover CRC golden vectors, notification fragmentation, invalid CRC rejection, flow control, media/density guards, templates and non-recursive variables, exact image composition, interrupted-result recovery, report retry without reprinting, pause/heartbeats, Chinese rendering and DPAPI.

On 2026-11-06, the actual Windows/P2 combination completed model/CRC handshake, FF01/FF03 subscriptions, density write/readback, Chinese raster printing and 150-row feed. The user confirmed complete paper output. Two subsequent disconnect/reconnect cycles also succeeded. Other adapters/firmware require their own validation.

The observed Windows stack returned `Success` with no results for UUID-filtered FF00 discovery, while full uncached discovery returned FF00 among five services. The client therefore enumerates all services and selects FF00 locally. Detailed connection errors are retained in tray/settings status. This is a local interoperability observation, not a claim about every Windows Bluetooth stack.

To reproduce hardware diagnostics, first quit the tray app:

```powershell
# Add --print only when paper use is authorized.
```

The diagnostic uses the saved selected address, validates two reconnections, and does not print server keys. Normal app operation does not enable raw diagnostics.

Research compared [paperang-p2-lib](https://github.com/mdj2812/paperang-p2-lib), [paperang-p1-connect](https://github.com/Orangray-MDJX/paperang-p1-connect), and Microsoft's [GATT write API](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattcharacteristic.writevaluewithresultasync). The implementation preserves this repository's verified 576-dot legacy BLE protocol, not classic SPP or the P1 A5 protocol. See the [protocol record](PROTOCOL.md).

© 2026 ELT Group. Retains the repository [license](../LICENSE), [NOTICE](../NOTICE), original icon, and independent-project statement. This is the Windows adaptation of macOS revision `b1788b07aacb8c0cff7f86a884e58fb4ad8afb32`.

## C management panel

Settings → Management provides owner-private-file login, credential creation/rename/rotation/revocation, identities and browser sessions, trusted public keys, complete historical JSON records with readable text/image tabs, user-ID filtering, streaming JSONL export and manual paper confirmation. A ten-minute local timeout locks management; the owner-authenticated background loop continues. New owner files get user-only ACLs. The relay must support v0.3.1 owner authentication.


### v0.3.1-winlite-fix1 management center

The redesigned window separates category navigation, contextual actions, the table and record details. Create credentials in a dedicated form: the server generates credential and identity IDs; choose an existing identity from the list when needed. Random tokens and optional empty passphrases are generated by the server. Generating an owner file requires an unlocked owner; registration fills its ID automatically and existing private files cannot be overwritten.

Credential and identity lists hide revoked rows by default. Toggle the checkbox to inspect them. Choose 25, 50 or 100 rows per page and navigate previous/next pages. The client retains at most 12 recent pages, resetting pagination when the category, filter or size changes. It can consume legacy fixed-size pages without dropping overflow records. Updated relays filter before pagination and return content-free job summaries; full text/images load only for the selected task. Legacy relays still send their existing five-job detail pages, which the client reduces to summaries in its page cache. The new relay enhancements require server deployment; the native layout/filter/paging already works with the deployed v0.3.1 relay.


### v0.3.1-winlite-fix2 autosave and configuration export

All settings now autosave after a 350 ms debounce and flush on close: server, owner-file path, HTTP permission, printer, startup/reconnection/printing preferences and templates. Settings/management categories, page size, revoked visibility, identity filter and scan display preference persist too. A successfully verified private-file switch in management updates the remembered path.

DPAPI-encrypted `settings.dat` retains recoverable active settings, `settings-draft.dat` preserves incomplete connection edits, and `ui.dat` holds UI preferences. Invalid drafts or credentials conflicting with a pending report do not replace the recovery connection. Valid edits are used on the next launch; use Save and Connect to switch the currently running connection. Autosaving does not interrupt active printing.

Startup and Status → Export all configuration writes versioned JSON containing settings, templates, UI preferences and the owner-file path. Raw credentials, private-key contents, sessions and pending tasks are excluded. Export cannot overwrite the selected private file. Moving to another computer still requires transferring the private file separately and updating its path. This version adds export, not graphical import. Windows regression: 60 checks pass; no relay changes required.
