# Local terminal administration and opt1 reset

`sudo python3 /home/elt/paperang/manage.py` opens the Python curses interface over an ordinary SSH terminal. No browser, X11, desktop, HTTP listener or extra administrator token is used. Arrow keys select, Enter confirms, Escape/Q returns; Page Up/Down scroll. Run SSH with a TTY (`ssh -t LA-VPS`).

The UI must start as root. It uses the installed venv, then delegates database operations through a private pipe to a worker under `paperang-relay`. This keeps SQLite/WAL ownership compatible with the live service. The local root actor is written to audit events, never inserted as a credential or user. The console creates no persistent daemon. Closing it stops the worker, not the relay.

Functions: service/database/receiver status; A/B/C/M random tokens; A/B/C passphrases; new paired owner JSON files; existing public PEM registration; credential metadata/rename/rotation/revocation; session revocation; identities; quota controls; paged complete print history, text/raster/metadata/events and JSONL export; audit browsing. Server authorization invariants, including protection of the last active C credential, remain in force.

New secrets are shown only when created and saved under `/home/elt/paperang/admin-exports/` (directory 0700, files 0600, root-owned). Transfer these files privately to the intended user/client, then remove the distribution copy when no longer needed. Never put them in Git/chat. Existing token/passphrase secrets cannot be recovered from hashes. Existing owner private keys remain on the client. Newly registered public keys cannot execute until the client proves possession. A generated owner JSON is saved before registering its public key, so registration failure does not lose the private material.

The console does not print automatically, replay jobs or mark `sent` as physical completion. History is the current online database, governed by configured retention; exports are independent private copies. New exports require explicit operator cleanup. This reset intentionally removes prior history: those deleted jobs are not recoverable through the new GUI.

## Opt1 deployment reset on this host

`Server/fresh_install.py` is the host-specific, explicit factory-reset installer for `/home/elt/paperang`. It preserves one operator-selected active Windows public-key C credential, its stable identity ID/public key/device, and only its still-valid short sessions. It does not need the Windows private file. All other users/credentials, task history, quota overrides, audit history, expired/other sessions, old application logs/backups, old code/venvs and legacy credential exports are removed after successful checks. New sessions, audits, users or logs created after reopening are current data, not leftovers.

TLS certificates, domain/Nginx settings, Cloudflare operational resource IDs and home ACL restoration metadata are infrastructure needed for the existing endpoint; they are retained. Shared Nginx, other sites, other services, shared system journals and provider/external snapshots are not deleted. Cleanup is filesystem/database deletion, not a disk-overwrite claim.

The installer pins package hashes, refuses active claims or a mismatched public-key fingerprint, pauses ingress, stops the old unit, builds a genuinely empty DB and imports only the selected Windows identity. It replaces the unit/current release/venv and verifies health, restart and public HTTPS. During the private maintenance phase failures restore the old deployment. Once ingress reopens, failures keep current data and postpone deletion, instead of blindly restoring old rows. After success the temporary recovery tree and all historical application snapshots are deleted as requested. There is then no old-history rollback.

The current source checkout/development artifacts live under `paperang/workspace`; live immutable code is under `paperang/releases`, dependencies under `paperang/venvs`, state under `paperang/data`, configuration under `paperang/config`, and current logs under `paperang/logs`. Only the unit and Nginx site remain outside. Historical unrelated development checkouts outside paperang are not removed automatically.

New reset/runtime verification is recorded in `paperang/docs/deployment.json`. A Windows heartbeat after reset confirms the retained key still executes; public acceptance alone is not evidence that the receiver is connected. No physical test print is submitted by the installer or console.

## Service configuration

Open **Service configuration / apply changes** to edit anonymous access, maximum text/row units, hourly limits, queue capacity, retention, ordinary-session lifetime, global ceilings and trusted proxy networks. Edits stay in a draft until you review the changes and confirm Apply. Class/stable-identity quota overrides remain in the Quota page; existing overrides are not erased by changing service defaults. Credential creation can attach a new credential to an existing identity UUID; the credential menu links directly to that identity's quotas.

The controller validates types/ranges/networks and rejects changes to deployment paths, bind/port and the device ID. It refuses an active print, checks for a concurrent configuration change, stops the receiver endpoint, rechecks claims, writes a 0640 config atomically with the original owner/group, starts the service and checks health. Startup failure restores the original config and service; it never rolls back or deletes the database. The local database worker reloads the new config so its displayed quotas match the running relay. Successful edits write an audit event containing field names, not secret values. The ordinary owner session remains ten minutes; `session_days` affects only non-owner sessions.

The terminal console may be updated independently of the immutable relay release; deployment records track its source separately. Close open console instances before installing an update. A console-only update does not reset data or require a relay restart.

`Server/update_console.py --package <pinned-console-package>` updates only the root terminal program/documentation. It verifies hashes, refuses an open console/reset lock, checks root config-write permissions using identical bytes and verifies the service-account worker; failed installation restores the prior terminal program. It does not restart the relay, change policy, rotate credentials or touch task data.
