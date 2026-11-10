# Relay API v2 — v0.3.2

[简体中文](AUTH-v2.zh-CN.md) · [Upgrade and rollback](UPGRADE-v0.3.2.md) · [Final contract](API-v2-032.md)

Relay and Mac local HTTP APIs use v2 with separate responsibilities. Relay v1 was retired during the historical v0.3.0 migration; raster1 schema 1, template schema 1 and BLE framing remain separate contracts. Upgrade the relay, receivers and senders together; all `/v1/*` relay routes return HTTP 410. Never restore old receipt/content routes for compatibility.

| Role | Submit | Read status | Read historical body/delivery metadata | Manage |
|---|---|---|---|---|
| A key | Yes | No (403 `status_read_forbidden`) | No | No |
| Anonymous browser | Yes | Own identity only | No | No |
| B | Yes | Own stable identity only | No | No |
| C | Yes | All | All, paginated/filterable/exportable | Credentials, names, sessions, identities, trusted keys |
| M | No | Active device queue only | Claimed active payload only, with claim secret | Report its claim results |

An accepted A submission returns `accepted`, `id`, `shortID`, `statusReadable:false`, never a print state or private receipt. B/C status routes return only id/shortID/state/created/updated, never text, raster, delivery metadata or free-form device detail. A printed ID is a locator, not authority. Public status advertises acceptance, policy and content capabilities, not printer telemetry.

## Identity and secrets

Stable `user_id` belongs to the sender identity; credentials rotate without changing it. Shared passphrases represent a shared identity, not proven individuals. Names are mutable; submitted metadata retains its original name snapshot. `browser_id` is a random instance label, not a fingerprint. IP/UA are delivery/rate metadata, not authenticated hardware identity. Clearing cookies/incognito can create a new anonymous identity.

Random keys use `psr_` and SHA-256 storage. Human phrases use `psp_<credential UUID>.<phrase>`, Argon2id (19 MiB, t=2, p=1, random salt), no custom length or character-complexity policy; blank generates a random phrase (HTTP request-body bounds still apply). Login and anonymous registration are rate limited. No API reveals an old secret. Newly created/rotated credentials are shown once.

Web login creates a persistent Secure/HttpOnly/SameSite=Strict cookie, normally 180 days (`session_days`, bounded 1–180); C sessions last ten minutes. JavaScript stores only the nonsecret `psb_` + 32 hex browser ID and the last twenty task IDs. `GET auth/me` confirms the server identity before submission. Cookie-authenticated POST requires same-origin Origin and X-CSRF-Token. Browser login does not return the session token in JSON. The native header `X-Paperang-Native: 1` without Origin returns a token instead of setting a cookie. Production web access requires HTTPS.

## Fixed owner file

RSA 3072/4096, RSA-PSS SHA-256, MGF1 SHA-256, salt length 32. The client signs an opaque server challenge valid 60 seconds and consumed once. A registered public key is pending until possession is proven. A successful proof issues a ten-minute C session; revocation invalidates it immediately. This is an HTTPS challenge protocol, not SSH transport or complete RFC 9421 request signing. The private JSON contains `server`, credential `id`, and `privateKeyPem` (PKCS8 or PKCS1); the server sees only the public PEM/signature. Windows writes owner-only ACLs before secret bytes; macOS uses mode 0600. Keep backups private. No arbitrary local file grants privilege.

First trust: `python Server/relay.py --config CONFIG trust-public-key --name Owner --file PUBLIC.pem`; put the returned id in the local owner JSON. Existing C clients may register a new public key after login; raw bearer tokens must first exchange for a short-lived management session. Background printing continues under an M token; closing the owner panel clears C state.

## Routes (root-relative; JSON; no query strings)

- `GET /v2/public/status`; `POST /v2/auth/anonymous {browser_id}`; `POST /v2/auth/login {credential}`; `GET /v2/auth/me`; `POST /v2/auth/logout {}`.
- `POST /v2/auth/challenge {id}` → challenge/message; `POST /v2/auth/prove {challenge,signature}` → native C session token.
- `POST /v2/jobs {text?,content?,request_id}`. UUID request_id required; duplicates within retained history return the original acceptance and never create another job. Do not replay uncertain physical output.
- `GET /v2/jobs/{id}` or `/v2/jobs/lookup/{shortID}`: B-own/C fixed status only. Legacy X-Job-Receipt has no authority.
- `POST /v2/device/status`; `GET /v2/device/jobs`: M queue descriptors only; `POST /v2/device/jobs/{id}/claim`; `GET /v2/device/jobs/{id}` with X-Job-Claim: active matching claim only; `POST .../result` with claim/state; `POST .../reject` for queued jobs. M cannot read completed history or manually confirm paper.
- `POST /v2/admin/keys/search`, `/identities/search`, `/sessions/search`: C, `{before?}`. At most 100 metadata rows/page; cursor `next`. GET collections are bounded legacy UI conveniences.
- `POST /v2/admin/keys {name,role,kind,device?,user_id?,password?}`; kind token/passphrase. M requires token. Existing user_id creates another credential for the same identity.
- `POST /v2/admin/keys/{id}/rename {name}`, `/rotate {password?}`, `/revoke {}`. Rotation revokes old sessions and keeps user_id; queued work continues. Current admin credential cannot revoke/rotate itself. Last proven C is protected. Deletion retains an audit tombstone.
- `POST /v2/admin/identities/{user_id}/revoke {}`: blocks all credentials and rejects queued jobs. Admin identities require individual credential revocation. Already buffered printer data cannot be recalled.
- `POST /v2/admin/sessions/revoke {credential_id}`: invalidates all sessions for that credential.
- `POST /v2/admin/public-keys {name,public_key}`: recent C login required.
- `POST /v2/admin/jobs/search` or `/export {before?,user_id?,job_id?}`: five full jobs/page, cursor next is an opaque SQLite row-order integer, not a timestamp. Full body/raster data, identity links, delivery metadata and job events; no claim/receipt/auth secrets. Native clients stream JSONL pages, including base64 attachments. JSONL avoids CSV formula execution. Do not request huge pages.
- `POST /v2/admin/jobs/{id}/confirm {}`: C manual paper confirmation of sent jobs.
- `POST /v2/admin/audit {before?}`: 100 records/page, 90-day retention. Content retention defaults to seven days. Session cleanup and expired-history event cleanup run during queue/submission activity.

Limits include IP, stable identity, credential and aggregate hourly buckets, plus bounded queue/HTTP workers/bodies/responses. These are abuse controls, not proof of a unique physical person. SQLite content remains plaintext at rest; use OS/disk encryption. Optional application-level content encryption and CSV/attachment-folder export are not implemented in this release. HTTPS protects transport; the relay necessarily sees plaintext.

## Validation boundaries

Windows build and local v2 integration are validated on Windows. Swift/AppKit/Security source and Linux deployment files are updated but require their native agents to build and validate before deployment. See the release checklist and handoff prompts. No live database or production service has been migrated by this Windows work.

Primary implementation references: [cryptography RSA](https://cryptography.io/en/latest/hazmat/primitives/asymmetric/rsa/), [argon2-cffi](https://argon2-cffi.readthedocs.io/en/stable/api.html), [OWASP sessions](https://cheatsheetseries.owasp.org/cheatsheets/Session_Management_Cheat_Sheet.html).

A late valid result from the same claim can move a timeout-generated unknown to sent when the client already durably recorded transport completion. This updates a report only; it never reclaims or physically resends a job. Stale claims cannot fetch payload after the five-minute activity window.

## v0.3.1 correction

Anonymous browser identities may read only their own fixed state/progress responses. Explicit A tokens remain mailbox-only. The active payload rendering whitelist includes IP and User-Agent for local template fields; customer-facing routes still never return body/content/metadata. WinLite defaults to a paired RSA owner file for both management and execution, automatically renewing short-lived native proof sessions. C bearer/passphrase credentials alone cannot enter device execution routes; only a proven public-key C session can, still scoped to its registered device. Existing M execution routes remain for other compatible receivers. An owner-only retire-others action refuses active claims, preserves the current proven owner, invalidates other credentials/sessions and audits the cutover.

## Per-identity quotas (relay fix1)

C-only `POST /v2/admin/quotas/get {scope}` and `/set {scope,limits}` accept scopes `anonymous`, `authenticated`, `user:<UUID>`. Fields: `hourly_limit` jobs, `hourly_chars` text characters + raster rows, `max_length` units/submission (hard maximum 10000). Null inherits; zero blocks. Config defaults → class defaults → individual identity. SQLite persistence and transactional acceptance counters; stable identity usage survives rotation/additional credentials. Idempotent retries do not consume quota twice. Independent credential, IP and global limits still apply. Class usage is aggregate information; class limits apply separately per identity. Counters reset at the next epoch-hour boundary (`resetAt`). `auth/me` returns only the caller's effective quota/usage.

WinLite: select a credential/identity and “用量与限额…”. Swift source adds a quota editor; native macOS verification pending. Online server deployment is required for enforcement; old servers return unsupported rather than silently saving locally.
