# Changelog

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
