# Version Feature Implementation Matrix — baseline 0.3.0

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Version policy](VERSIONING.md) · [Registry](../VERSIONS.json) · [Upgrade](UPGRADE-v0.3.0.zh-CN.md)

Reviewed 2026-11-08. **W** Windows-tested implementation; **T** tested on Windows with real local HTTP/SQLite (not Linux deployment); **S** source updated, native target validation pending; **O** outside scope; **P** planned/unimplemented. These labels distinguish code from release/deployment evidence.

| Feature | MacClient | LinuxRelayServer | WinLite | RelaySkill | WinClient / LinuxClient |
|---|---|---|---|---|---|
| Distribution | v0.3.0-macclient | v0.3.0-linuxrelayserver | v0.3.0-winlite | v0.3.0-relayskill | P / P |
| API v2, A/B/C/M separation | S | T | W | W (A/B sender) | P |
| Claim before payload, active claim only | S | T | W | O | P |
| Text/raster1, SHA-256, 576 dots | S; prior BLE evidence | T | W; prior real P2 evidence | W renderer | P |
| Stable identity / passphrase / secure cookies | S consumer | T | W C management | O browser; A/B token | P |
| Native C keys/identities/sessions/history panel | S SwiftUI | T endpoints | W WinForms | O | P |
| RSA-PSS local owner file | S Security.framework | T verifier | W .NET ↔ Python | O | P |
| User-filtered full history / JSONL with raster | S | T paged | W bounded pages/export | O | P |
| Durable receiver credential / pending result | S Keychain | T SQLite | W DPAPI | W exclusive attempt file | P |
| Automatic print / tray / block templates | S | O | W | O receiver; sender raster options | P |
| Continuous reconnect / cancellation | S (default on) | O client loop | W 5 attempts + 60 s | O no POST retry | P |
| Windows sign-in startup | O (Mac platform difference) | S systemd install | W registration; no reboot test | O | P |
| Full text/image workspace, terminal, local API | S existing Mac | O | O Lite scope | O | P |
| Optional at-rest app encryption / CSV folders | P | P | P | O | P |

API v2 breaks relay v1; local Mac API v1, template JSON v1, raster1 schema1 and legacy BLE framing remain separate. A cannot read state. B state is own-identity only. C manages complete history. M is scoped execution, not full historical access. A short ID/old receipt never conveys content authority. `sent` is transport only; physical confirmation requires owner action.

Windows evidence: regression suite (51 checks), Chinese GDI/template rendering, DPAPI atomic persistence/restart, retry cancellation, concurrent lifecycle and 1,200-request bounded offline stress. Real local v2 HTTP/SQLite integration covers text+raster, C login, identity-preserving rotation, Windows/Python RSA-PSS interoperability, C export and private-file ACL. Native C panel layout inspected at Windows display scaling. Server permission tests cover role boundaries, legacy bypass denial, browser Cookie/CSRF, anonymous registration/revocation, owner replay, rotation and idempotency. RelaySkill has twelve local tests. See tests and release verification record for current results.

Previous physical P2 Chinese print and two reconnects were user-confirmed on 2026-11-06. This update does not claim a new physical paper confirmation, actual Windows reboot, indefinite resource stability, Mac native compilation, or Linux production deployment. Those require target-platform evidence. No planned full client is represented as released. Future features must update this matrix and shared contract before claiming parity.
