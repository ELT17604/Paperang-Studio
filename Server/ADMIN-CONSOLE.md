# Local terminal administration

`sudo python3 /home/elt/paperang/manage.py` opens the Python curses interface over an ordinary SSH terminal. No browser, X11, desktop, HTTP listener or extra administrator token is used. Arrow keys select, Enter confirms, Escape/Q returns; Page Up/Down scroll. Run SSH with a TTY (`ssh -t LA-VPS`).

The UI must start as root. It uses the installed venv, then delegates database operations through a private pipe to a worker under `paperang-relay`. This keeps SQLite/WAL ownership compatible with the live service. The local root actor is written to audit events, never inserted as a credential or user. The console creates no persistent daemon. Closing it stops the worker, not the relay.

Functions: service/database/receiver status; A/B/C/M random tokens; A/B/C passphrases; new paired owner JSON files; existing public PEM registration; credential metadata/rename/rotation/revocation; session revocation; identities; quota controls; paged complete print history, text/raster/metadata/events and JSONL export; audit browsing. Server authorization invariants, including protection of the last active C credential, remain in force.

New secrets are shown only when created and saved under `/home/elt/paperang/admin-exports/` (directory 0700, files 0600, root-owned). Transfer these files privately to the intended user/client, then remove the distribution copy when no longer needed. Never put them in Git/chat. Existing token/passphrase secrets cannot be recovered from hashes. Existing owner private keys remain on the client. Newly registered public keys cannot execute until the client proves possession. A generated owner JSON is saved before registering its public key, so registration failure does not lose the private material.

The console does not print automatically, replay jobs or mark `sent` as physical completion. History is the current online database, governed by configured retention; exports are independent private copies. New exports require explicit operator cleanup.

## Data-preserving upgrades

Use [the v0.3.2 upgrade and rollback procedure](../doc/UPGRADE-v0.3.2.md). Upgrades preserve all users, credentials, configuration, history and templates. Never use a factory reset or owner cutover for a normal upgrade. The one-time opt1 reset implementation has been removed from the shipping tree; its history remains in Git.

## Service configuration

Open **Service configuration / apply changes** to edit anonymous access, maximum text/row units, hourly limits, queue capacity, retention, ordinary-session lifetime, global ceilings and trusted proxy networks. Edits stay in a draft until you review the changes and confirm Apply. Class/stable-identity quota overrides remain in the Quota page; existing overrides are not erased by changing service defaults. Credential creation can attach a new credential to an existing identity UUID; the credential menu links directly to that identity's quotas.

The controller validates types/ranges/networks and rejects changes to deployment paths, bind/port and the device ID. It refuses an active print, checks for a concurrent configuration change, stops the receiver endpoint, rechecks claims, writes a 0640 config atomically with the original owner/group, starts the service and checks health. Startup failure restores the original config and service; it never rolls back or deletes the database. The local database worker reloads the new config so its displayed quotas match the running relay. Successful edits write an audit event containing field names, not secret values. The ordinary owner session remains ten minutes; `session_days` affects only non-owner sessions.

The terminal console may be updated independently of the immutable relay release; deployment records track its source separately. Close open console instances before installing an update. A console-only update does not reset data or require a relay restart.

`Server/update_console.py --package <pinned-console-package>` updates only the root terminal program/documentation. It verifies hashes, refuses an open console/reset lock, checks root config-write permissions using identical bytes and verifies the service-account worker; failed installation restores the prior terminal program. It does not restart the relay, change policy, rotate credentials or touch task data.
