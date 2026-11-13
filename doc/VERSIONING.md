# Versioning and compatibility

[简体中文](VERSIONING.zh-CN.md) · [Feature matrix](FEATURE-MATRIX.md) · [Version registry](../VERSIONS.json)

## Product release series

Paperang Studio uses three numeric fields, `MAJOR.MINOR.PATCH`. Before 1.0, the second field identifies a product generation. For `0.3.2`:

| Field | Value | Project meaning |
|---|---:|---|
| MAJOR | 0 | Pre-1.0 development. Installable packages can be published during this phase. |
| MINOR | 3 | Third product generation, with compatibility baseline `0.3.x`. |
| PATCH | 2 | A revision in the third generation. Distributions may increment it independently. |

Distributions preserve shared contracts within a release series. A later generation can retain the same protocol. Incompatible changes require explicit versioning of the affected API or schema and a migration path.

## Current version domains

| Domain | Current value | Scope |
|---|---|---|
| Product compatibility baseline | `0.3.x` | Distributions implementing their applicable shared contracts |
| Published GitHub product release | `v0.3.2` | Published packages and their acceptance record |
| Relay HTTP API | `v2` | LinuxRelayServer, MacClient and WinLite relay integration |
| Mac Local HTTP API | `v2` | MacClient loopback HTTP interface |
| Content, templates and saved configuration | schema 1 | Independently versioned formats, where implemented |
| Printer transport | Legacy P2 576-dot BLE, plus P2 USB on WinLite | Device transport, independent of HTTP APIs |

## Distribution versions and builds

Use `vMAJOR.MINOR.PATCH-distribution[-note]`, for example `v0.3.3-androidclient`. Runtime identifiers may omit the leading `v`. Patch numbers and build metadata are distribution-specific. MacClient uses bundle build `3301`, AndroidClient uses `versionCode 8`, and WinLite uses assembly version `0.3.3.0`. Installers and update checks use their platform metadata. Interoperability uses explicit API versions, schemas and capabilities.

Optional suffixes include `fixN`, `optN`, `alphaN`, `betaN` and `rcN`. Preserve published tags and artifact names. For a distribution update, revise its version source, registry, changelog and feature matrix, then run `python3 scripts/check-versions.py` and the relevant local platform checks.

[v0.3.2 release record](RELEASE-v0.3.2.md) · [API v2 contract](API-v2-032.md)
