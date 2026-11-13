# v0.3.0 upgrade and release record

[完整中文操作手册](UPGRADE-v0.3.0.zh-CN.md) · [Authentication contract](AUTH-v2.md)

The release replaces relay API v1 with v2 and A/B/C/M authorization, stable identities, Argon2id passphrases, persistent secure browser sessions, registered owner public keys and single-use RSA-PSS challenges. WinLite settings include native C management, full text/image history, identity filtering and streaming JSONL export. SwiftUI management, Keychain pending claims, persistent M credentials and continuous retry are updated in Mac source. RelaySkill requires A/B keys and distinguishes acceptance from status-read authority. Templates/raster1/BLE and the Mac local HTTP API v1 retain their independent formats.

## Coordinated upgrade

1. Pin the current origin/main commit. Pause new submissions and claims; drain active claimed/printing work. Never reset uncertain work to queued.
2. Stop the old service. Back up source/binaries/config, SQLite via its backup API (including WAL state), private credentials and proxy config. Keep an independent rollback copy.
3. Install Server/requirements.txt into a dedicated venv. Test on a database copy first. Startup refuses a first security migration while active claims exist and creates a private pre-v0.3.0 backup.
4. Legacy submit/admin/device hashes migrate to B/C/M. Named jobs map to stable identities; old anonymous jobs remain unassigned. Old receipt/private-content access is retired; all relay /v1 routes return 410.
5. Validate HTTPS, proxy Host/Origin, trusted forwarded addresses, Cookie and CSRF. Upgrade all receivers and senders together. M handles unattended execution; C is loaded only for management.
6. Resume submissions and claims.

Rollback restores both the old service and its database backup, together with matching clients. Do not let an old binary write the migrated database. Preserve new jobs using a private C export and decide their fate manually; do not automatically turn them into new print jobs.
