# Linux Relay v0.3.0 validation

Baseline: `4f8d96a64f2f8d51d406c195d44f31bb98f6650d`.

Validated on Ubuntu 24.04, Python 3.12, isolated SQLite databases. This record does not claim a native Mac/Windows client or physical paper test.

- 22 tests: `python -m unittest discover -s Server -p 'test_*.py'`. Includes A/B/C/M boundaries, stable identity rotation, one-time RSA proofs, old API retirement with stale bearer keys, quotas, concurrent idempotency, paging, retention, maximum 1400-row raster plus long text, and state persistence across Store restart.
- Chromium over local HTTPS: anonymous identity survives reload; B login survives reload; Secure/HttpOnly/SameSite=Strict cookies; CSRF rejection; one intentional isolated submission; no credentials or note content in localStorage; browser RSA proof has no JSON token even with the native header; revoked C session rejected.
- `bash Server/install.sh --dry-run`, shell syntax, and Python syntax checks pass.
- Dependencies installed in an isolated venv: argon2-cffi 25.1.0, argon2-cffi-bindings 26.1.0, cryptography 46.0.7, cffi 2.1.1, pycparser 3.12. Production provisioning uses an offline wheelhouse and hash manifest.

Corrections: raw credentials cannot authenticate through a session cookie; public-key browser login follows the same cookie rules as password login; v1 routes retire before authentication; events record state transitions transactionally rather than repeated print heartbeats; expiration removes associated rows in the same prune pass. Installer uses the service identity for bootstrap and disables runtime bytecode writes.

Production runtime source: `771c14c1732644a2e754ffe50ab9fb0cd213b9d5`. The user executed the host-specific sudo updater successfully. It rehearses migration twice on a stopped-database snapshot and old-code rollback on another copy, checks unchanged job fields and legacy role/hash mapping, then migrates as the service account. Ingress remains in maintenance through backend checks and cold restart. Startup failures restore code, database and the old unit together. After reopening ingress, public-check failure must not automatically discard newly accepted jobs.

Legacy admin/device/submit tokens map to C/M/B without regenerating secrets. Old Relay API v1 clients require upgrading to v0.3.0. Seven-day job retention remains configured on the deployment; audit events expire after 90 days when pruning runs. This is application-level retention, not deletion of operator backups.

Production verification: snapshot migration/repeat and old-code rollback rehearsal passed before switching the live DB. Hardened non-root systemd start and cold restart passed; no automatic service restarts observed. The 18 release-file hashes match the tested package and venv `pip check` passes. Backend listens only on loopback. Public HTTPS returns v0.3.0 with no-store/DYNAMIC; v1 with a stale bearer returns 410; HTTP redirects to HTTPS. Real public Chromium verifies valid TLS, webpage initialization, secure anonymous session persistence across reload and no JavaScript exceptions. No production print was submitted.

The installer recorded a failed initial public probe; that original record is preserved. Independent follow-up origin and public curl checks, then the public browser check, passed. Post-publication failure never triggered automatic database rollback.

The updater's isolated fixture test also injected a restart failure and verified restoration of code, DB, unit and ingress. This simulation is distinct from the actual production cold restart.

Remaining verification: native v2 receiver cross-test; physical paper; extended unattended soak. These must not be inferred from the completed server tests.
