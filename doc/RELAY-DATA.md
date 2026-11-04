# Relay data and retention

[简体中文](RELAY-DATA.zh-CN.md) · [Remote printing](REMOTE-PRINTING.md)

## Live server data

The configuration's `database` field selects one SQLite database. The default
installer uses `/var/lib/paperang-relay/relay.sqlite3`; a home-based deployment can
keep it under its dedicated `data/` directory. WAL/SHM files, when present, are
adjacent to the database. The file is mode 0600 for the service account.

| Table | Stored data | Removal |
|---|---|---|
| jobs | Body, sender/IP/browser metadata, long/short IDs, state, timestamps, partition counts, receipt/claim hashes and key IDs | Default seven days since `updated`, except active claimed/printing tasks |
| keys | Key ID/name/role/device, token hash, creation/revocation state | No automatic expiration; revocation invalidates, deletion removes the row |
| usage | Per-IP fixed UTC-hour job/character counters | Older than the previous 48 hour windows, during cleanup |
| devices | Latest status/telemetry and heartbeat time per device | Replaced by new status; no time-based row deletion |

`policy.retention_days` in `GET /v1/public/status` reflects the actual configured
task retention. Public status stops calling a device online after 15 seconds,
but this does not delete its stored telemetry row.

Cleanup runs on submissions, device status/list polling and task lookups, not on
an independent timer. With no activity, expired data may remain until a cleanup
request. Queued/final tasks older than the threshold are deleted. Claimed or
printing tasks that have not updated for five minutes first become `unknown`,
with a new `updated` timestamp; that starts their final-state retention window.
Manual paper confirmation also updates the timestamp. Merely viewing a task
does not refresh its age.

SQLite deletion is logical deletion, not a promise of secure erasure from disks
or snapshots. Database files need not shrink immediately. Back up via SQLite's
backup API, or stop the service before copying its entire data directory; do not
copy a live main file while ignoring its WAL.

## Browser receipts

The current tab's sessionStorage holds the most recent tracking state and up to
20 original receipt capabilities. It does not store note text, visitor metadata
or access tokens. Reloads can reuse these receipts. Closing the tab normally
ends that storage session, though browser session-restoration behavior varies.
It is not permanent archival storage or an account history system.

The server stores receipt hashes, so an anonymous author who loses the original
receipt cannot recover private text with a public 12-character ID alone. An
authorized administrator/device key can still retrieve the existing task.

## Exported secrets, backups and logs

The standard installer writes raw initial keys to a separate root-only
`initial-credentials.jsonl`. Recovery tooling may also export replacement keys.
These exports are separate from the hashes stored in SQLite and require private
handling. There is no automatic credential-file expiration.

Backups are independent copies: the seven-day live-task rule does not delete
their contents. The supplied base installer does not schedule backups or expire
them. Any external maintenance scripts need their own backup retention policy.
Restoring an old snapshot restores its task and key states, potentially making a
key active again if it was revoked after the snapshot.

Application/Nginx logs are separate from task history. Their location and rotation
depend on deployment; shared systemd/Cloudflare logs or external snapshots are
outside this database's cleanup policy.
