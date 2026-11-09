# v0.3.2-winlite — Windows native validation

Date: 2026-11-10 (Asia/Shanghai). Stage 2 native acceptance passed with the production-network limitations below; not a final Release or Linux deployment acceptance.

## Environment and inheritance

Windows 11 Pro for Workstations x64, build 26200; .NET SDK 10.0.403 / Desktop runtime 10.0.14; Inno Setup 6.7.3; Python 3.14 with Server requirements in a disposable development venv. P2 via Windows BLE, 576-dot / 72-byte raster rows. These are the actual observed environment versions, not a claim to have tested every supported Windows release.

Accepted remote HEAD: `51f9ec8bcc1bd6ac0b089a924541e9fc3750f780` on `xdev/032task`, matching Stage 1 handoff commit and its implementation parent `d71380b537772587d9c3fac0ffb9c1e10ec6050b`. Reviewed actual `origin/main...HEAD` changes, the shared API/auth/version/schema contract, Windows source/build/installer and historical external tests independently. Outer workspace was an unborn repository containing untracked historical work; it was left intact. Work occurred only in its existing clean `upstream` checkout, fast-forwarded from origin. No macOS commits were reset/reverted.

## Shared contract mapping and code review

| Concern | Actual Windows implementation | Result |
|---|---|---|
| HTTP client | `RelayClient.cs`, `OwnerAuthentication.cs`, `AdminForm.cs`, `QuotaForm.cs`, `Program.cs` | All actual business requests use Relay `/v2`; no HTTP v1 execution adapter. Windows has no Mac loopback HTTP service. Local and Relay error formats remain distinct. |
| Owner C / execution M | `OwnerAuthentication.cs`, `Models.cs`, `RelayClient.cs` | Bound owner JSON, RSA-PSS SHA256, PKCS8/PKCS1, ten-minute proof sessions; M has no C management. Persisted authentication mode `legacy` means existing Relay-v2 bearer credentials, not HTTP v1. Retained for valid migration. |
| Jobs and uncertainty | `RelayEngine.cs`, `LocalStore.cs` | Persist unknown before fetching/rendering/sending; active heartbeat; sent after transport; restart only reports persisted result. Claim and credential affinity preserved. No silent print replay; sent never means paper confirmed. |
| History / confirmation | `AdminForm.cs` | C paging/filter/export and explicit paper-inspection dialog; M cannot manually confirm. Shared reject behavior remains queued -> rejected; no invented cancelled Relay state. |
| Template/config | `Templates.cs`, `NoteRenderer.cs`, `LocalStore.cs` | Template/content/config schema 1 retained, original font names preserved; Windows maps fonts locally. Old templates/settings/owner path remain readable. |
| BLE/P2 | `BlePrinter.cs`, `P2Session.cs`, `Protocol.cs` | No protocol/UUID/CRC/framing/density/feed/576-dot implementation changes. |
| Versions | VERSION, AppVersion, both csproj, installer, registry, comparer | Runtime 0.3.2-winlite; numeric package/assembly/file 0.3.2 / 0.3.2.0; same baseline with documented distribution suffix. 0.3.2 sorts above 0.3.1 fix99/opt99. |
| Update robustness | `ReleaseUpdates.cs` | Reject other platform tags, absent suffix, invalid/overflow revision values; preserve old fix/opt ordering. Still reads actual published GitHub records, no fabricated 0.3.2 download URL. |
| Native template editor | `TemplateEditor.cs` | Initial DPI baseline/layout suspension now matches other native windows. Before: editor shrank and controls overlapped/clipped on this desktop. After: full preview, fields, save/cancel visible and cancel usable; no user template overwritten. |

The existing Windows runtime already used v2. No artificial bulk migration or schema-1 removal was needed. Removed stale external-only test directions in current Windows docs; promoted the usable historical test harness and corrected its obsolete v1 mock/detail/auth defaults. Preserved historical release evidence, necessary v2 pagination compatibility, config backup/migration, DPAPI, ACL handling and installer rollback/data-retention behavior. No server or macOS implementation changed; no new shared wire contract was introduced.

## Reproducible checks

Commands and explicit physical/installer opt-ins: [tests/windows/README.md](../tests/windows/README.md).

| Check | Result |
|---|---|
| Native Windows Release build, self-contained x64 portable publish, framework-dependent x64 lite publish | Passed; real .NET/Windows SDK compilation, no simulated build |
| Inno Setup and SHA256 sidecars | Passed; per-user x64 self-contained installer, stable AppId/install/data locations |
| Native regressions | 55 checks passed: CRC/golden frames, fragmentation/resync, model/media/density/credits guards, raster/hash/bounds, CJK rendering, schema-1 presets/composition, version ordering, DPAPI/storage/draft/backup, durable pending results, pause, cancellation and report retry without reprint |
| Native isolated Relay integration | 12 checks passed: PKCS8/PKCS1 actual RSA proof, forced expired-session renewal, C history/quota access, proven owner execution, M history denial, real renderer/engine text+raster1 and server DB sent states (fake printer) |
| Shared real HTTP Relay tests | 5 tests passed: public schema, retired v1, A/B/C/M/own scope, UUID dedup/claim isolation, five-minute timeout -> unknown, denied stale body/reclaim, late sent report, C-only explicit manual confirmation, queued rejection |
| Cross-platform inventory/version guards | Passed; Mac route/OpenAPI still agree, Windows runtime contains no v1 calls; independent version domains and all Windows build metadata checked |
| Installer upgrade | Installed 0.3.1-winlite-opt2 -> 0.3.2-winlite passed; running old client stopped gracefully; payload replaced, uninstall version registered; private settings/templates/keys byte-identical |
| Overlay/uninstall/reinstall | Passed repeatedly, including final DPI repair payload: running 0.3.2 overlay; silent uninstall removes startup and executable while retaining all data; reinstall and restart succeed; original startup preference restored |
| GUI | Installed app settings/templates restored; editor full preview/save/cancel verified after DPI repair; network error/countdown visible; settings close leaves tray running; native receiver later recovered online and connected P2 |

The installer test initially observed the short-lived uninstall launcher exit before registry cleanup. The test now waits for actual cleanup; repeated tests passed. This was a test synchronization issue, not a product change or ignored failure. A full clean Windows VM/user and real logoff/reboot were not available; this was a clean-payload reinstall with existing user data preserved. Startup command/registration and actual tray launches were tested, not a real sign-in cycle.

## Actual paper — separate from sent reports

All test sheets used final `0.3.2-winlite` rendering/printer/engine code with density60/feed60 overrides confined to test memory. Existing user settings were not changed. Each intended physical attempt wrote an exclusive record, with no automatic resubmission. GUI DPI/update/version-only changes do not alter raster/transport behavior and did not trigger repeat paper tests.

| Label / job ID | Path | Result |
|---|---|---|
| PS032-W-T1 (local label; no Relay ID) | Windows -> BLE P2; 576x100, short Chinese/English text | Transport sent once; owner replied “两张均正常” for T1 and IT1 |
| PS032-W-IT1 (local label; no Relay ID) | Windows -> BLE P2; 576x272, image + schema-1 template | Transport sent once; same explicit owner paper confirmation |
| PS032-W-R1 / `6c0b6979746bab9e354867ec2b5dc311f8fd5ce4` | Temporary isolated Relay -> actual Windows owner client/engine -> BLE P2 | Transport sent once, temporary DB sent; owner replied “纸面正常” |

One P2 discovered during scan. Separate connect/disconnect plus two reconnect cycles passed, model P2, battery100%, paper present and lid closed. None of these three sheets passed through the existing Ubuntu Server. Paper confirmation above is user evidence; no fabricated production C confirmation was posted. No physical paper-out, power-cut or mid-cache cancellation was injected; safe recovery paths were tested with a fake printer and real isolated HTTP server.

## Existing Ubuntu compatibility and limitations

Production source, service, database, schema, historical data and global permissions were not changed; no reset/restart/deployment was performed. The installed owner receiver initially showed network errors/timeouts and retry countdown. Later the same installed app, original owner file and original settings visibly recovered to “中转在线 · 待处理 0 项” plus connected P2/battery100%. The real loop only sets Online after successful authenticated v2 device status and job-list responses, so this is evidence of existing owner login/execution polling and automatic recovery, not a simulated status label.

Separate Python public-status reads failed with an 8-second timeout, two .NET public probes failed at 30 seconds, and curl direct TLS failed its handshake. The observed successful GUI receiver recovery does not turn these failed probes into passed tests. Production connectivity remains intermittent from this environment; exact cause and production runtime version were not established. No new production print job, credential, quota or manual confirmation was created. Full live submit/claim/print/confirm remains unverified; real remote printing used isolation. Ubuntu must recheck endpoint/TLS/network health and production end-to-end behavior before release. This does not prove a server outage or a WinLite API incompatibility.

Mac and Windows use the same unchanged Relay v2 and schema-1 contract, exercised against the same actual server implementation. Mac GUI was not run on Windows; Stage 1 native evidence is inherited. Ubuntu/RelaySkill validation remains pending. No final release claim, main merge or tag is made.
