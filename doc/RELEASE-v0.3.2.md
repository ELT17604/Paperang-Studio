# Paperang Studio v0.3.2

Application HTTP APIs are unified on **v2**. MacClient, WinLite, LinuxRelayServer and RelaySkill share product version **0.3.2**, retaining runtime suffixes `-macclient`, `-winlite`, `-linuxrelayserver`, `-relayskill`.

The clients share the same Relay v2 authentication, permissions and task contract. Local Mac API v2 remains a separate loopback interface. Template/content/configuration schema 1, BLE UUIDs/framing/CRC, 576-dot raster format, density and feed protocol are preserved.

## Compatibility and upgrade

HTTP API v1 is retired; obsolete requests receive explicit rejection and cannot execute business operations. Update old Local API consumers to v2 and coordinate clients/server upgrades.

Ubuntu upgrades retain users, identities, credentials, sessions, quotas, history, configuration and templates. Consistent SQLite backups and new/old-code rehearsals precede the release switch. Program-only rollback retains the current database and all post-upgrade writes. No factory reset or key rotation is part of this upgrade.

## Downloads

[GitHub release](https://github.com/ELT17604/Paperang-Studio/releases/tag/v0.3.2) · [Upgrade and rollback](UPGRADE-v0.3.2.md)
