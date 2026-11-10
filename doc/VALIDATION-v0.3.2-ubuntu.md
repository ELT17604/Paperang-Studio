# Paperang Studio v0.3.2 — Ubuntu integration record

Status: **IN PROGRESS / formal release BLOCKED**. No main merge, final tag or Release has occurred.

## Baseline and production protection

- Received `xdev/032task` at `a68a245`, containing macOS `d71380b` and Windows `c5c04bf` implementation/test evidence. Main at `ada72fa`. Work is in a separate `paperang/workspace/032` worktree; the original clean console worktree and live immutable release are not Git targets.
- Observed production: Ubuntu 24.04/Python 3.12, `paperang-relay.service`, non-root `paperang-relay`, loopback `127.0.0.1:28177`, runtime `0.3.1-linuxrelayserver-opt1`, source `9fcf22f05a3c514c76f5f8da968dd3e7d40af202`. Unit uses the existing dedicated venv and `paperang/current/relay.py`; configuration is `paperang/config/config.json`, database `paperang/data/relay.sqlite3`.
- systemd active/running, restart count 0; public HTTPS capability GET 200. Root read-only inventory: SQLite quick_check=ok, zero jobs, three identities and credentials, no active claim; latest receiver reports `0.3.2-winlite`, idle. Telemetry age was 457 seconds, not proof of a current connection.
- No reset, credential rotation, user/history/log/backup deletion or production mutation performed during preflight. Sensitive inspection output is outside Git.

## Isolation evidence

Reproducible commands are in `tests/README.md`. Ubuntu tests use temporary databases/ports, no real receiver. Tests recovered from Git history are now committed under `tests/server`; they are no longer external-only release prerequisites.

- PASS: shared five real HTTP v2 cases, roles/status visibility/content claims, deduplication, expiry/late sent, C-only paper confirmation and v1 GET/POST retirement, including bare `/v1`.
- PASS: isolated security, SQLite legacy migration/restart, stable-identity quotas, concurrent reservation/UUID submission, paging/export, revocation/last-C protection, console/configuration checks and RelaySkill offline rendering/ambiguous-submission safety.
- PASS: actual RelaySkill and experimental ChatGPT core clients against a real isolated Relay HTTP server; B-own status/short ID, A denial, no duplicate submission. This is not external ChatGPT account/gateway validation.
- PASS: six data-preserving updater simulations, including a claim racing preflight and writer-lock exclusion; with real SQLite; intact rows/schema across new/old code rehearsal; protected backup; failed startup rollback retains post-upgrade writes; busy/checksum failures have no service side effects.
- PASS: Python source syntax, shell syntax, JavaScript syntax and shared inventory/version guards. CJK rendering requires a real CJK font; tests explicitly use the unpacked Ubuntu Noto CJK package outside source, not a glyph substitution.

## Physical protocol audit

Stage 1/2 diffs preserve `Sources/ProtocolCore`, `Sources/PrintCore`, Bluetooth transport and actual Windows BLE/framing/CRC/raster printer implementations. Windows Core changes are assembly/version metadata only. Stage 3 changes are Relay runtime/retirement rejection, console label, deployment/testing/documentation and release-discovery logic; no physical protocol changes. Recheck the final diff before publication.

## Current gates

| Gate | Status |
|---|---|
| macOS Stage 1 native build/local HTTP/physical text and image | PASS per platform record |
| Windows Stage 2 build/install/isolated Relay/three physical sheets | PASS per platform record |
| New product-release discovery native revalidation | NOT RUN; required after Stage 3 code changes |
| Production backup/copy rehearsal/cutover | NOT RUN |
| New Ubuntu authenticated Mac and Windows physical round trips | NOT RUN |
| External ChatGPT MCP/gateway/account connection | NOT RUN; experimental |
| Final artifacts from current source and SHA-256 | BLOCKED on native builds |
| Temporary handoff removal / merge / v0.3.2 tag / Release | BLOCKED until final acceptance |

The temporary handoffs are deliberately retained until final acceptance. No Linux result is represented as native Mac/Windows GUI or physical printing success. Production deployment results, source SHA, timestamps, backup path and final user confirmations will be appended only after execution.

CI baseline `7748f60`: Ubuntu and Windows jobs passed; macOS native build and all 47 local HTTP checks passed, but the recovery harness raced the queue refresh after heartbeat-online and received the correct 409 on a stale job. The test now waits for the reported job to disappear from the queue before selecting the next one; no server protection or product print behavior was weakened. Native CI rerun required.
