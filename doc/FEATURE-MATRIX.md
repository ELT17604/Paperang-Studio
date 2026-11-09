# Version Feature Implementation Matrix — baseline 0.3.1

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Version policy](VERSIONING.md) · [Registry](../VERSIONS.json) · [Upgrade](UPGRADE-v0.3.0.zh-CN.md)

Reviewed 2026-11-09. **N** native macOS software build/local HTTP integration verified; physical printing not re-tested. **W** Windows-tested implementation; **T** tested on Windows with real local HTTP/SQLite (not Linux deployment); **S** source updated, native target validation pending; **O** outside scope; **P** planned/unimplemented. These labels distinguish code from release/deployment evidence.

| Feature | MacClient | LinuxRelayServer | WinLite | RelaySkill | WinClient / LinuxClient |
|---|---|---|---|---|---|
| Distribution | v0.3.1-macclient-fix3 | v0.3.1-linuxrelayserver | v0.3.1-winlite | v0.3.1-relayskill | P / P |
| API v2, A/B/C/M separation | N | T | W | W (A/B sender) | P |
| Claim before payload, active claim only | N | T | W | O | P |
| Text/raster1, SHA-256, 576 dots | N; prior BLE evidence | T | W; prior real P2 evidence | W renderer | P |
| Stable identity / passphrase / secure cookies | N consumer | T | W C management | O browser; A/B token | P |
| Native C keys/identities/sessions/history panel | N SwiftUI | T endpoints | W WinForms | O | P |
| RSA-PSS local owner file | N Security.framework | T verifier | W .NET ↔ Python | O | P |
| User-filtered full history / JSONL with raster | N | T paged | W bounded pages/export | O | P |
| Durable receiver credential / pending result | N Keychain | T SQLite | W DPAPI | W exclusive attempt file | P |
| Automatic print / tray / block templates | N | O | W | O receiver; sender raster options | P |
| Continuous reconnect / cancellation | N (default on) | O client loop | W 5 attempts + 60 s | O no POST retry | P |
| Windows sign-in startup | O (Mac platform difference) | S systemd install | W registration; no reboot test | O | P |
| Full text/image workspace, terminal, local API | N existing Mac | O | O Lite scope | O | P |
| Optional at-rest app encryption / CSV folders | P | P | P | O | P |

**Experimental non-distribution:** The [ChatGPT adapter](CHATGPT.md) reuses A/B Relay API v2 for text, validated pre-rendered raster1, and B-only own-status reads. Implementation reviewed at source level with mock-transport tests provided; no deployed MCP/OAuth gateway, ChatGPT account connection, or P2 paper verification is claimed. This is not an additional release column or shared compatibility contract.

API v2 breaks relay v1; local Mac API v1, template JSON v1, raster1 schema1 and legacy BLE framing remain separate. A cannot read state. B state is own-identity only. C manages complete history. M is scoped execution, not full historical access. A short ID/old receipt never conveys content authority. `sent` is transport only; physical confirmation requires owner action.

Windows evidence: regression suite (51 checks), Chinese GDI/template rendering, DPAPI atomic persistence/restart, retry cancellation, concurrent lifecycle and 1,200-request bounded offline stress. Real local v2 HTTP/SQLite integration covers text+raster, C login, identity-preserving rotation, Windows/Python RSA-PSS interoperability, C export and private-file ACL. Native C panel layout inspected at Windows display scaling. Server permission tests cover role boundaries, legacy bypass denial, browser Cookie/CSRF, anonymous registration/revocation, owner replay, rotation and idempotency. RelaySkill has twelve local tests. See tests and release verification record for current results.

Previous physical P2 Chinese print and two reconnects were user-confirmed on 2026-11-06. This update does not claim a new physical paper confirmation, actual Windows reboot, indefinite resource stability, Linux production deployment. Those require target-platform evidence. No planned full client is represented as released. Future features must update this matrix and shared contract before claiming parity.

Linux: isolated security/HTTPS/browser/max-raster/restart tests and production migration/systemd/public HTTPS checks passed; native receiver/paper checks remain pending. See [Linux validation](LINUX-v0.3.0-VALIDATION.md).

## v0.3.1 correction

Anonymous browser sessions track their own fixed progress only; ordinary A tokens stay submit-only. WinLite defaults to a paired owner private file, with renewable proof sessions for C management and device-scoped execution. IP/UA are restored to active rendering metadata. Server regression: 25 tests; Windows regression: 51 checks plus native owner integration. Historical Linux v0.3.0 deployment evidence remains separate from the new owner cutover.


See [v0.3.1 live validation and browser metadata follow-up](VALIDATION-v0.3.1.zh-CN.md) for verified deployment scope and remaining platform checks.


WinLite `v0.3.1-winlite-fix1`: native management layout, automatic credential/identity IDs, hide-revoked and 25/50/100-row paging. Legacy relay compatibility is retained; server summary/filter pagination is additive. See [Windows guide](WINDOWS.md).

### 2026-11-09 / v0.3.1 revisions

| Feature | LinuxRelayServer fix1 | WinLite fix3 | MacClient fix1 |
| --- | --- | --- | --- |
| Unrestricted custom passphrases | Enforced; 39 tests | Native input updated | Source updated; verified in MacClient fix3 below |
| Class and per-identity quotas | SQLite policy + atomic counters | Native management dialog | Native editor; verified in MacClient fix3 below |
| Ntandard installer and uninstall | systemd deployment | Per-user setup, optional data purge | Existing distribution |
| Manual WinLite release check | N/A | Stable GitHub WinLite tags | N/A |

Server source ready; live deployment pending. Compatible API v2 baseline remains 0.3.1.

## MacClient fix3 validation

Native build, SwiftUI window review, isolated owner/C/M HTTP integration and 39 server tests passed. See [upgrade and exact scope](MACCLIENT-v0.3.1.md). No new physical print or production deployment.

## opt1

[Current source cleanup, per-platform validation and regression archive](OPTIMIZATION-opt1.md). All implemented distribution runtime versions now end in opt1; this does not assert new Windows/hardware/deployment validation.

Linux opt1 follow-up: local root-only curses console for credential distribution/revocation, identities, quotas and full paged history/export; pinned fresh reset preserves one selected Windows owner. Isolated 39 regressions, 4 console checks, reset/rollback and terminal navigation passed; host reset and retained Windows reconnection verified.

Local console follow-up: service configuration drafts with validation/atomic apply/health and rollback; identity-bound credential creation and direct identity quota controls. Configuration tests cover six save/failure/concurrency/active-claim paths; configuration editor installed; root atomic write/worker checks and retained Windows reconnection verified.
