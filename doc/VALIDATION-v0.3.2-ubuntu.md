# Paperang Studio v0.3.2 — Ubuntu integration acceptance

**PASS — macOS, Windows and Ubuntu accepted.** Product version 0.3.2 retains each distribution's architecture/platform suffix. The final main commit, tag commit, CI URL and package checksums are recorded in the Release's generated Final Release Report and SHA256SUMS.

## Platform evidence

| Platform | Result | Evidence |
|---|---|---|
| macOS / 0.3.2-macclient | PASS | [Native build, local API, template/configuration and confirmed device records](VALIDATION-v0.3.2-macos.md); Stage 3 macOS arm64 CI rebuilt/signed the app, passed 47 actual local HTTP requests and the actual-source owner/Keychain/recovery harness including product Release discovery |
| Windows / 0.3.2-winlite | PASS | [Native build/install/upgrade, DPAPI, Relay integration and confirmed device records](VALIDATION-v0.3.2-windows.md); Stage 3 Windows CI rebuilt setup/portable/lite packages and reran native and isolated HTTP integration checks including product Release discovery |
| Ubuntu / 0.3.2-linuxrelayserver | PASS | Real protected deployment, unchanged persistent rows/schema rehearsal, backup integrity/readability, failure rollback, local/public HTTPS, retired API rejection, source hashes and authenticated Mac/task/preview acceptance below |
| RelaySkill / 0.3.2-relayskill | PASS | 12 rendering/protocol regressions and actual HTTP sender-to-Relay integration, short-ID status and exclusive submission protection |

The owner's final integration criterion combines the established confirmed device workflow with a fresh authenticated Mac connection and visible pending task on the upgraded Ubuntu Relay. That criterion passed, and the owner confirmed the complete task and preview as normal. Platform/device evidence is retained in the two linked platform records.

## Production deployment

- Existing source workspace and production release were kept separate under `/home/elt/paperang`. The development branch received both earlier stages at `a68a245`; neither platform's commits were replaced.
- Before upgrade: Ubuntu 24.04/Python 3.12, `paperang-relay.service`, user `paperang-relay`, loopback `127.0.0.1:28177`, source `9fcf22f05a3c514c76f5f8da968dd3e7d40af202`, runtime `0.3.1-linuxrelayserver-opt1`.
- Configuration: `paperang/config/config.json`; SQLite: `paperang/data/relay.sqlite3`; dedicated existing venv and `paperang/current/relay.py`. Nginx/systemd definitions remain in their original system directories. TLS, Cloudflare and other services were preserved.
- Deployed source: `a13e0198d86900fdf8466a0e9b562bca9ff97368`; runtime `0.3.2-linuxrelayserver`; deployment time 2026-11-10 08:46:02 UTC. Final release-candidate core files were independently compared with the live source hashes.
- Protected consistent backup: `/home/elt/paperang/backups/20261110T084602Z-1794300362907463452`. SQLite `Connection.backup`, `integrity_check`, new/old Store startup and complete persistent row/schema comparison passed before switching code. No schema change was needed.
- Original three identities and credentials preserved; database quick_check=ok; configuration, quotas, history, credential files, templates and earlier backups retained. There was no active print at cutover. The updater holds the SQLite writer lock across the idle check and stop to exclude a racing claim.
- New program start, local HTTP and public HTTPS return the new version; `/` includes the new web version. `/v1` and `/v1/jobs` return HTTP410. The v1 guards do not dispatch business operations.
- New Mac heartbeat: `0.3.2-macclient`, observed after the successful deployment, age 0.6 seconds, idle. The root inspection recorded `fresh_mac_heartbeat=true`.
- One authorized acceptance task, short ID `40886D0E599D`, was submitted once through public HTTPS API v2. Server state queued; owner confirmed “看到了完整任务和预览，确认正常”. The owner then rejected the test task; state `rejected` was verified and its dedicated anonymous browser session logged out.

## Upgrade and rollback evidence

The initial cutover's local health passed, but Cloudflare rejected the default Python User-Agent with HTTP403. Automatic program-only rollback restored and health-checked the old service while retaining the current database. A follow-up probe isolated the User-Agent behavior; the same public URL returned 200 with the project identity. The updater now sends that identity and still rejects actual 403 failures. No Cloudflare rule or authorization policy was relaxed.

Real SQLite regression simulations additionally verify that post-upgrade writes survive both startup failure and explicit rollback; racing/active claims and corrupt packages stop deployment safely. Backup files are 0600 inside protected 0700 directories. Previous releases/environments and both production backup attempts remain available. See [executable upgrade/rollback](UPGRADE-v0.3.2.md).

## Reproducible regression and protocol audit

[Tests](../tests/README.md) cover identity scopes, owner proof, last-C protection, concurrent UUID submission and quota reservation, stable identity rotation, paging/export, migration/restart, active-claim content, timeout to unknown, late report-only recovery, rejection and C-only paper confirmation. Actual RelaySkill and experimental ChatGPT core clients exercise real isolated Relay HTTP. The ChatGPT gateway remains an explicitly experimental integration.

Python, JavaScript and shell syntax, installer dry-run, shared route/OpenAPI inventory and version guards passed. Ubuntu CJK rendering used a real Noto CJK font in the external development workspace. CI reruns these tests and native builds for the final main publication baseline.

The Mac protocol/print libraries, Bluetooth/UUID/CRC and actual Windows BLE/framing/raster transport implementations are unchanged across the release. Windows Core diffs are version/assembly metadata. Template/content/configuration schema 1 and the 576-dot / 72-byte physical row format remain unchanged.

## Repository maintenance

- Removed the obsolete one-time destructive `Server/fresh_install.py` and its routine deployment recommendations; Git retains its history.
- Restored reusable server/security/console/adapter tests to `tests/server/`; release validation no longer depends on private external test archives.
- Consolidated current deployment, version, API and documentation entry points. Historical protocol/security/migration/rollback records remain available.
- Integrated both handoffs' durable facts in platform records, this report, release notes and CHANGELOG. Temporary `xdev/032-handoff/` communication files are removed only after acceptance.
- Final publication is gated by Ubuntu/macOS/Windows CI and checked artifacts; it creates an ordinary tag pointing at final main and refuses to overwrite any existing tag or Release.
