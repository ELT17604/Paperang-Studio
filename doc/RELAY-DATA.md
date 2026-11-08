# Relay data and retention — v0.3.0

[Authentication and complete schemas](AUTH-v2.md) · [Migration](UPGRADE-v0.3.0.zh-CN.md)

SQLite stores stable identities, credential hashes/Argon2id verifiers, public keys, session hashes, quotas, job text/raster1, delivery metadata, state events and audit events. Browser status responses never include content or metadata. C history pages/export include identity links, original name snapshots, content and delivery records; authentication hashes, claims and receipts are excluded. M receives a rendering whitelist only while its claim is active.

Default job retention is seven days and audit retention ninety days, with activity-driven cleanup. Content remains plaintext at rest; use OS disk encryption and protect exports/backups. The transport is HTTPS, not end-to-end encryption. Anonymous browser IDs are random, not hardware fingerprints; IP/UA cannot prove a physical person. Rotation preserves identity and revokes sessions; identity revocation also rejects queued jobs. Buffered paper data cannot be recalled. Legacy anonymous history is never assigned by IP guesses.
