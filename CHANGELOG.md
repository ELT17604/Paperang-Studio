# Changelog

## `v0.3.1-<distribution>-opt1` — 2026-11-09

- Remove unused Mac management state/cache, unreachable retired relay handlers and redundant preference writes; move regression sources outside the shipping tree.
- Group Mac remote printing into tasks/connection/style/management; fix preview/active-job refresh and improve responsive web identity/status UI.
- Support optN update tags; restore macOS CJK font discovery in RelaySkill. Preserve authentication and no-replay boundaries.
- [Review and validation / 审阅与验证](doc/OPTIMIZATION-opt1.md). Native Windows/hardware/deployment validation not claimed.

## Unreleased · ChatGPT sender integration (experimental)

- Add loopback MCP sender adapter and optional GPT Actions OpenAPI schema targeting existing Relay API v2.
- Support explicitly authorized text and pre-rendered raster1, server-side A/B identity verification, B own-status and UUID idempotency; never retry ambiguous POST.
- Add offline HTTP contract tests, bilingual guides and security/deployment boundaries. OAuth gateway, live ChatGPT connection and paper testing remain pending; no new released distribution or shared protocol change.

## v0.3.1-macclient-fix3 — 2026-11-09

- Native macOS build restored; paired owner-file execution, renewable RSA-PSS proof, stable recovery identity and legacy M compatibility.
- Native paginated C management, private history/raster details, scoped quotas, safe owner creation and streamed JSONL export.
- Autosaved settings/drafts, credential-free configuration export, optional startup/printer reconnect and immutable active-job layout. Known sent results survive disconnects without replay.
- Native arm64 build/GUI/local relay integration and 39 server tests passed. No new physical print or production deployment. [Upgrade notes](doc/MACCLIENT-v0.3.1.md).

## v0.3.1-macclient-fix2 — 2026-11-09

- macOS 应用菜单和托盘加入手动检查 GitHub 更新，只比较最近 100 条中的正式 MacClient 标签，发现新版后打开下载页面。无发行版、网络失败均提示；不自动安装。按要求未运行 macOS 验证。

## v0.3.1-winlite-fix3 / linuxrelayserver-fix1 / macclient-fix1 — 2026-11-09

- Windows 标准按用户安装包，内置 .NET、系统卸载入口和可选数据清除，托盘手动检查 GitHub 正式 WinLite 发行版。
- 自定义口令移除长度/复杂度规则，留空自动生成；保留传输边界和登录限流。
- C 管理匿名/注册默认及单身份的小时任务数、内容量和单次大小限额；SQLite 持久化与原子计数，轮换不清零。网页、WinLite 界面及 Swift 管理源代码同步。
- Windows 61 项、服务器 39 项测试通过。macOS 原生编译与线上服务器部署待验证，见交接文档。


## v0.3.1-winlite-fix2 — 2026-11-09

Autosave all settings, including the paired owner-file path, startup preferences, remembered printer and reconnect options. Encrypt unfinished connection drafts separately from active recovery settings. Remember settings/management tabs, page size, revoked-row visibility and identity filters. Add versioned JSON configuration export with templates and UI preferences; raw credentials, private-key contents, sessions and pending tasks are excluded. Valid settings are restored on startup; live connection changes still use Save and Connect.

## v0.3.1-winlite-fix1 — 2026-11-09

Redesign the native management center with category navigation, contextual actions, readable tables, 25/50/100-row pages, previous/next navigation and optional hiding of revoked credentials/identities. Credential creation uses a dedicated form and server-generated IDs; existing identities are selected rather than typed. Key generation requires an unlocked owner and writes the registered ID automatically, without placeholder JSON. Add backwards-compatible relay pagination/filtering and content-free C history summaries with selected-record details. Legacy relays remain usable through bounded client paging.

## v0.3.1 — 2026-11-08

Fix anonymous own-job progress without content disclosure; restore IP/UA to active render payloads; default WinLite to paired owner private-key authentication and renewable sessions. Merge verified Linux security corrections and add safe owner cutover tooling.

## v0.3.0 — 2026-11-08

Coordinated API v2 identity/authentication update, A/B/C/M permissions, persistent browser sessions, RSA owner files, native WinLite/SwiftUI C administration, filtered JSONL history/export and migration safeguards. RelaySkill upgraded; local Mac API v1 retained. Windows tested; native Mac/Linux deployment validation pending. [Complete upgrade record](doc/UPGRADE-v0.3.0.zh-CN.md).

## v0.2.3-relayskill — 2026-11-08 (independent skill distribution)

- Add `Skills/paperang-relay-print` as a peer distribution: native text and print-ready raster1 submission, CJK typography, image transforms/dithering, previews and bounded receipt tracking.
- Reuse relay HTTP v1 with explicit Content-Length, capability/quota preflight, explicit intended copies and exclusive attempt records; never automatically replay ambiguous submissions.
- Keep submit credentials separate from device keys and document density/feed/template as receiver-local controls. No client/server protocol or runtime changes are needed.
- Extend version policy, registry, feature matrix and bilingual docs. Validate 12 local tests, original relay/SQLite integration and read-only live status.

## v0.2.3-winlite-fix1 — 2026-11-08 (Windows client)

- Keep the tray label fixed at “开启自动打印”; indicate state with its check mark only. Add live status in settings and save/connect on the Connection tab.
- Autosave the selected template/style and print options, flush on close, and restore them at launch without changing active-job layout snapshots.
- Enable continuous recovery by default: after five retries, wait 60 seconds, try another five times and repeat until successful. Disconnect/quit cancels waiting immediately; optional opt-out retains the original stop behavior.
- Repair enabled startup paths after upgrades, suppress settings on repeated background launch, serialize atomic local writes and coalesce UI refreshes.
- Add persistence/migration, retry/cancellation, concurrency and 1,200-request outage checks. Short-term verification is not an indefinite stability or actual-reboot claim.
- Add distribution version naming, the release registry and the cross-platform feature implementation matrix; preserve Mac/server runtime versions as documented legacy aliases.

## 0.2.3-win_lite — 2026-11-06 (Windows client)

- Add native Windows tray-only relay reception, settings, encrypted credentials and pending-result recovery, optional sign-in startup and remembered-printer reconnection. The existing macOS defaults, server and webpage are unchanged.
- Add the macOS-compatible v1 block-template editor with four presets, drag/reorder, paper selection, margins/gaps, variables, JSON import/export and shared drawing/text composition.
- Fix actual P2 discovery on the tested Windows stack by enumerating all GATT services before selecting FF00; filtered discovery incorrectly returned no entries. Preserve FF03 credits, key-zero CRC, media checks and density readback.
- Verify complete Chinese paper output with user confirmation, 150-row feed, two reconnect cycles, 39 automated checks and original-relay integration. Keep build output and credentials outside published source; document adapter/firmware and font-rendering limits.

## 0.2.3-fix1 — 2026-11-04

- Fix remote task previews being invalidated by unstable template serialization; fetch full task content on demand and show loading/errors.
- Client-only release; relay server, webpage and SDK remain at their existing versions.

- Fix raster jobs bypassing custom layouts: Body blocks now compose image/caption alongside existing metadata blocks; editor and print share the same parts. Preserve legacy text rendering and template JSON.

## 0.2.3 — 2026-11-04

- Menu bar mode retains Bluetooth/network, authorizes network jobs, pauses unrelated work and restores prior settings.
- Synchronize app, web, server, SDK and current documentation to v0.2.3.

- Invalidate stale lookups on submission; preserve the last accepted/sending stage for unknown jobs.
- Add versioned raster1 drawing jobs, touch canvas, capability negotiation, row quotas, private full-content fetch and native previews.
- [Content API and limits](doc/RICH-MEDIA.md).

## 0.2.2 — 2026-11-04

- Authenticated 2-second heartbeat, fresh/stale status, five retries followed by disconnect and alert.
- Server-backed task feedback and explicit manual paper confirmation.
- Native administrator key creation, rename, revoke and deletion.
- Faster flow-controlled defaults: 10 ms BLE writes and 0 ms image-frame pause.
- Full status/API details in [remote printing](doc/REMOTE-PRINTING.md).


[English](CHANGELOG.md) · [简体中文](doc/CHANGELOG.zh-CN.md)

### Remote-printing features included in 0.2.2

- Dedicated macOS temporary-relay folder, Python3.10+ discovery, executable Finder launcher, and complete server parameter tables.

- Active/recent remote task list, full details, status/search filters,40-hex internal IDs and separately unique12-hex optional printable IDs, with migration of existing tasks.
- Saved default automatic-print preference and graphical block-layout editor with live composition, presets, drag/reorder, and JSON import/export.
- Persistent systemd installation and temporary foreground relay mode with Ctrl-C/SIGHUP cleanup.
- Bluetooth scan timer cancellation, live/cache distinction, discovery counters and per-app central-manager recovery.


- Ubuntu relay installer with configurable port/address/quotas, local/offline generation of revocable role-based keys, SQLite hourly IP limits, and a metadata-disclosing submission website.
- Native remote-print inbox with telemetry, manual/automatic handling, four local styles, selectable metadata fields, and uncertain-outcome protection.

- English README and default documentation, with Chinese translations and reciprocal links under `doc/`.
- Updated build/startup instructions and removed the known-limitations section from the project landing pages.

## 0.2.1 — 2026-11-04

- Single global paper-use switch in the sidebar; footer version/copyright and File → About with English project statements and contact email.
- CC BY-NC-SA 4.0 license and attribution NOTICE.
- Paperang candidate filtering with Show all Bluetooth devices.
- Newest-first engineering logs with chronological export.
- Real controlling PTY, interactive zsh/control keys, batched paper output, private input, and output guards.
- Local HTTP v1 API with Bearer/Origin checks, offline rendering, jobs/feed, and client examples.
- Reusable Swift libraries, OpenAPI, protocol reference, and acquisition records.
- ESP32 extended-command research distinguished from real hardware results.
- Original logo, ICNS/ICO assets, clean source layout, and external development archive.

## 0.2

Native print workspace, image preview/rendering, presets, and tasks. The user physically confirmed image direction/completeness and 150-line final feed.

## 0.1

Verified legacy P2 BLE handshake, read-only fields, raw raster printing, feed, and engineering tools.

Linux Relay v0.3.0: enforce session-only cookies and browser owner-proof cookies; retire v1 before authentication; bound job history to state transitions; validate Ubuntu migration, cold restart and public HTTPS. Native receiver/paper/extended soak remain pending.
