# v0.3.2 installation, upgrade and rollback

Product release: **Paperang Studio v0.3.2**. Runtime labels retain distribution suffixes: `0.3.2-macclient`, `0.3.2-winlite`, `0.3.2-linuxrelayserver`, `0.3.2-relayskill`.

## Compatibility

All application HTTP business calls use API v2. Old HTTP v1 paths are rejected with 410, without authentication dispatch, body interpretation or business execution on Relay. This small explicit rejection guard is retained to distinguish retirement from an unknown route; there is no forwarding adapter. Mac loopback authentication/Origin rules apply before its retirement rejection. Bare `/v1` is also rejected. Template/content/configuration schema 1 and the BLE physical protocol are independent version domains and remain unchanged.

This is the explicitly approved PATCH exception to the normal pre-1.0 MINOR breaking-change policy: Mac local v1 consumers must upgrade to v2. Relay v2 remains compatible with existing v0.3.1 v2 receivers. Upgrade native clients before final cross-platform acceptance. Never infer physical paper completion from `sent`.

## Existing Ubuntu host

This procedure targets the existing `/home/elt/paperang` deployment, not a new generic installation. New installations use `Server/install.sh`; do not run that generic installer over this host's managed layout.

1. Prepare a pinned package outside the live release, under `paperang/workspace`. `manifest.json` contains the source commit, runtime version and SHA-256 of every packaged Server file. Include the committed `Server/deploy.py` alongside its `Server/` payload. Do not package user data, logs, tokens or private files.
2. Quit the root terminal console. Wait for active claimed/printing jobs to settle; never force completion or replay an uncertain job.
3. Run `sudo python3 PACKAGE/Server/deploy.py --package PACKAGE`.
4. The updater verifies file hashes and unchanged dependency requirements, snapshots SQLite using `Connection.backup`, runs new and old code on the copy and compares every persistent row and schema. An unexpected difference aborts before the service stops.
5. It retains the previous immutable release and environment. A protected `paperang/backups/TIMESTAMP/` stores a readable integrity-checked SQLite snapshot, configuration, public-key/credential files, supplementary data/templates, console and systemd/Nginx definitions. No backup is committed or automatically deleted.
6. The updater stops the service, checks active claims again, takes the final consistent backup, switches only the code symlink/console, and starts the service. Local and public HTTPS version checks must pass. Current configuration, identities, credentials, sessions, quotas, task history and printer settings remain in place. There is no schema change for this release.
7. Verify `systemctl status paperang-relay`, `sudo python3 /home/elt/paperang/manage.py --check`, the webpage and original receiver connection. Use dedicated, recognizable one-copy jobs for real paper tests; confirm paper manually, never automatically resend a timeout.

All operational files stay under `/home/elt/paperang`, except existing systemd and Nginx definitions. The backend remains loopback-only; no new listener or firewall rule is needed. TLS, Cloudflare, unrelated sites and shared services are unchanged.

## Executable rollback

Use the same pinned updater and the exact backup printed by the upgrade:

```sh
sudo python3 PACKAGE/Server/deploy.py --package PACKAGE --rollback /home/elt/paperang/backups/TIMESTAMP
```

It refuses active prints, stops the relay, backs up the **current** database again, switches to the old program/console, then restarts. **It never replaces the live database with the pre-upgrade snapshot.** All post-upgrade writes survive. This is safe only because both programs have been rehearsed against the same persistent schema/rows. A future incompatible schema change must supply a different verified migration/rollback procedure; this updater refuses rehearsal differences.

Startup or public health failure automatically restores the previous program and keeps current data. Inspect the protected backup and service status before retrying. Old code must not be deleted until final acceptance; backups remain until the operator explicitly removes them.

## Final publication gates

Native macOS and Windows checks must be rerun if their release-discovery code changes. Historical installed packages must not be relabeled as the new build. Product tag `v0.3.2` and platform runtime suffixes coexist. The release must include correctly named platform assets for update discovery: `PaperangStudio-0.3.2-macclient-macos-ARCH.zip` (or `.dmg`) and `PaperangStudio-0.3.2-winlite-win-x64-setup.exe`.

Ubuntu deployment alone is not final release acceptance. The new relay must complete each available native receiver's authenticated submit/claim/print/report/paper-confirmation path. Missing native evidence, unverified artifacts, failed CI or missing final cross-platform paper checks block merge/tag/release. Experimental ChatGPT gateway/account-side integration remains unverified unless tested externally.
