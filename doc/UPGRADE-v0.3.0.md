# v0.3.0 upgrade and release record

[完整中文操作手册](UPGRADE-v0.3.0.zh-CN.md) · [Authentication contract](AUTH-v2.md) · [Native agent handoff](HANDOFF-v0.3.0.zh-CN.md)

Repository: **ELT17604/Paperang-Studio**, https://github.com/ELT17604/Paperang-Studio . All implemented distribution source versions now share baseline 0.3.0 (macclient, linuxrelayserver, winlite, relayskill). Planned full Windows/Linux clients remain unimplemented. The owner explicitly approved a coordinated breaking minor release before 1.0.0; fix revisions cannot change the shared contract.

The release replaces relay API v1 with v2 and A/B/C/M authorization, stable identities, Argon2id passphrases, persistent secure browser sessions, registered owner public keys and single-use RSA-PSS challenges. WinLite settings include native C management, full text/image history, identity filtering and streaming JSONL export. SwiftUI management, Keychain pending claims, persistent M credentials and continuous retry are updated in Mac source. RelaySkill requires A/B keys and distinguishes acceptance from status-read authority. Templates/raster1/BLE and the Mac local HTTP API v1 retain their independent formats.

## Coordinated upgrade

1. Pin the current origin/main commit. Pause new submissions and claims; drain active claimed/printing work. Never reset uncertain work to queued.
2. Stop the old service. Back up source/binaries/config, SQLite via its backup API (including WAL state), private credentials and proxy config. Keep an independent rollback copy.
3. Install Server/requirements.txt into a dedicated venv. Test on a database copy first. Startup refuses a first security migration while active claims exist and creates a private pre-v0.3.0 backup.
4. Legacy submit/admin/device hashes migrate to B/C/M. Named jobs map to stable identities; old anonymous jobs remain unassigned. Old receipt/private-content access is retired; all relay /v1 routes return 410.
5. Validate HTTPS, proxy Host/Origin, trusted forwarded addresses, Cookie and CSRF. Upgrade all receivers and senders together. M handles unattended execution; C is loaded only for management.
6. Run security tests, native client integration and platform deployment checks. Check rotation/revocation, B ownership, M active claims, no secret export, no physical replay, persistence and resources.
7. Re-enable production traffic only under the host owner's deployment authorization and after target-platform checks. This Windows work does not migrate production.

Rollback restores both the old service and its database backup, together with matching clients. Do not let an old binary write the migrated database. Preserve new jobs using a private C export and decide their fate manually; do not automatically turn them into new print jobs.

## Evidence and limits

Windows regression, local real HTTP/SQLite text+raster integration, C login/rotation/export, .NET↔Python RSA-PSS and private-file ACL checks are covered. Mac compilation/Keychain/BLE and Linux systemd/TLS/production migration require their agents; complete handoff prompts are included. The original Chinese P2 paper confirmation is historical, not a new hardware test. No actual Windows reboot or indefinite stability claim is made.

RSA-PSS was chosen instead of the draft Ed25519 suggestion to retain native Windows cryptography without an extra crypto runtime. It is not SSH transport or a complete RFC 9421 implementation. Full content remains plaintext in SQLite; OS disk encryption is recommended. Application-level content encryption, CSV and separate attachment-folder exports are not implemented; JSONL includes raster1/base64 attachments.
