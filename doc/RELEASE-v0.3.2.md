## Paperang Studio v0.3.2

Application HTTP APIs are unified on **v2**. MacClient, WinLite, LinuxRelayServer and RelaySkill share product version **0.3.2**, retaining runtime suffixes `-macclient`, `-winlite`, `-linuxrelayserver`, `-relayskill`.

The clients share the same Relay v2 authentication, permissions and task contract. Local Mac API v2 remains a separate loopback interface. Template/content/configuration schema 1, BLE UUIDs/framing/CRC, 576-dot raster format, density and feed protocol are preserved.

## Compatibility and upgrade

HTTP API v1 is retired; obsolete requests receive explicit rejection and cannot execute business operations. Update old Local API consumers to v2 and coordinate clients/server upgrades. This release explicitly permits the scoped PATCH breaking-change exception documented in the version policy; it does not imply generic compatibility with API v1.

Ubuntu upgrades retain users, identities, credentials, sessions, quotas, history, configuration and templates. Consistent SQLite backups and new/old-code rehearsals precede the release switch. Program-only rollback retains the current database and all post-upgrade writes. No factory reset or key rotation is part of this upgrade.

## Platform validation

| Component | Result | Evidence |
|---|---|---|
| macOS 0.3.2-macclient | PASS | Native arm64 build/ad-hoc signing, 47 local HTTP checks, actual-source owner/Keychain/recovery/template/configuration and update checks; confirmed P2 text/image device records |
| Windows 0.3.2-winlite | PASS | Native x64 build, DPAPI, permissions/recovery/template tests, isolated actual-client Relay integration, setup/upgrade/uninstall/reinstall paths and confirmed P2 device records |
| Ubuntu 0.3.2-linuxrelayserver | PASS | Real preserving deployment, backup/readability, migration/rollback, API v2, v1 rejection, HTTPS/web/source hashes, authenticated Mac connection and owner-confirmed complete pending task/preview |
| RelaySkill 0.3.2-relayskill | PASS | Protocol/rendering and actual HTTP integration, explicit one-shot submission, own status and short-ID tracking |

The owner-approved integration acceptance uses the existing confirmed device workflow plus the new Ubuntu connection/task/preview verification. All three platform sides were accepted. Test task `40886D0E599D` was rejected after acceptance and its browser session closed.

## Maintenance and release files

Current documentation links API v2, authentication, templates, installation, preserving upgrades, rollback, troubleshooting, development/tests and platform validation. The obsolete one-time factory-reset installer and temporary stage communication files are removed; useful security, hardware, migration and historical change records remain.

Release files are built by native CI from the final main baseline, with SHA-256 in `SHA256SUMS`: Mac arm64 app archive; WinLite x64 setup, self-contained portable and lite ZIPs; Ubuntu Server and RelaySkill source archives; Final Release Report. The Mac app uses ad-hoc signing, the existing distribution method. The lite Windows package requires .NET 10 Desktop Runtime. Planned full WinClient/LinuxClient remain planned; ChatGPT remains experimental.

See [Ubuntu acceptance](VALIDATION-v0.3.2-ubuntu.md), [macOS evidence](VALIDATION-v0.3.2-macos.md), [Windows evidence](VALIDATION-v0.3.2-windows.md), [API v2](API-v2-032.md), [upgrade/rollback](UPGRADE-v0.3.2.md), [authentication](AUTH-v2.md) and [CHANGELOG](../CHANGELOG.md).
