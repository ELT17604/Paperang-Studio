# Changelog

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
