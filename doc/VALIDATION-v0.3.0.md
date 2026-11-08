# v0.3.0 validation record — Windows host

Repository: ELT17604/Paperang-Studio, https://github.com/ELT17604/Paperang-Studio . Date: 2026-11-08. This record describes source/build validation, not a production migration.

| Check | Result |
|---|---|
| .NET 10 Release build | PASS, zero warnings/errors |
| Windows regression harness | PASS, 51 checks: BLE protocol, rendering, templates, journal, DPAPI, retry/cancellation, concurrency and bounded 1,200-request offline stress |
| Real local v2 HTTP/SQLite ↔ WinLite | PASS: long Chinese text and raster1, claim-before-payload and final sent states using simulated printer |
| Native C protocol integration | PASS: login/session, stable-identity rotation, .NET RSA-PSS ↔ Python proof, full export and owner-only file ACL |
| Server security tests | PASS, 17 tests: role isolation, old-route denial, Cookie/CSRF, migration, anonymous quotas, revoke/rotation, owner replay/expiry, last C, concurrent idempotency, paging/name snapshots and late durable reports |
| RelaySkill local tests | PASS, 12 protocol/rendering/no-resubmit tests |
| Version registry / JSON / Python syntax / document file links | PASS |
| Native manager UI | Layout/accessibility inspected on Windows; C controls disabled before authentication. Window isolated from user settings. Full details provide record/text/image tabs. |
| Packaged executable | --version returns 0.3.0-winlite |

WinLite archive: V0.3.0-winlite-win-x64-lite.zip, SHA-256 `841b07f9ac4172083b9730993c64866c82c13aefa2cd6bf5b2d3b707793e85e9`. Framework-dependent; requires .NET 10 Desktop Runtime. Build command: Windows/scripts/build.ps1. Build outputs stay outside Git.

RelaySkill archive: PaperangStudio-v0.3.0-relayskill.zip, SHA-256 `1aa11a50a1f84f26213005d332d6f6b917dcca7527130b1af5c43901abe158a5`. Archive excludes caches, credentials, previews and jobs.

Not executed: new physical-paper confirmation, actual Windows reboot, indefinite stability soak, macOS compilation/Keychain/native Security/BLE, Linux systemd/reverse-proxy TLS/production migration. Historical user-confirmed Chinese paper from 2026-11-06 is retained as earlier hardware evidence. No production server/database or existing active tray process was replaced.

The complete remaining native validation and migration/rollback prompts are in [handoff](HANDOFF-v0.3.0.zh-CN.md). Application-level at-rest encryption and CSV/attachment-folder export are not implemented; JSONL includes raster1/base64. RSA-PSS replaces draft Ed25519 to keep Windows cryptography native; this is not SSH transport or full RFC 9421 message signing.
