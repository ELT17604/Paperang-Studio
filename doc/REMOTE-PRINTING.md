# Remote printing and Ubuntu relay

[Documentation](README.md) · [简体中文](REMOTE-PRINTING.zh-CN.md)

The webpage submits text to an Ubuntu relay. The Mac app connects outward over HTTP(S), reports printer status, polls tasks every two seconds, chooses local layout/content, and sends the final raster over Bluetooth. The Mac needs no inbound public port. Text is supported in this first version; the existing local image API is separate.

## macOS terminal entry

The [macOS temporary folder](../Server/macOS-Temporary/README.md) provides a terminal launcher and a double-clickable start.command. See its complete option table and the [server parameter reference](../Server/README.md#available-parameters).

## Choose a run mode

**Persistent Ubuntu installation:** `sudo bash Server/install.sh`. Registers systemd and retains the SQLite database/keys across terminal closure and server restart.

```sh
sudo systemctl start paperang-relay
sudo systemctl stop paperang-relay
sudo systemctl restart paperang-relay
sudo systemctl status paperang-relay
sudo systemctl enable paperang-relay  # start on boot
sudo systemctl disable paperang-relay
```

**Temporary foreground experience:** no root, installation or systemd registration.

```sh
bash Server/run-temporary.sh --port 9088
# Add --bind 0.0.0.0 to test from another machine.
```

Temporary mode supports the same port, anonymous/authenticated length/hourly quotas, device ID, and proxy CIDRs. It prints a device and submission key in the terminal; all temporary keys/data are discarded on normal shutdown. Ctrl-C stops it; normal terminal closure sends SIGHUP and stops it too. It also handles SIGTERM. A force kill may leave an OS temporary directory, but no daemon survives. `--dry-run` shows settings without opening a port.

## Install

Use Ubuntu 22.04+ with systemd and Python 3.10+. No pip dependencies, Docker, or downloaded shell scripts are required. Copy/clone this project onto the server, then:

```sh
sudo bash Server/install.sh
```

The interactive installer asks for the IPv4 address, port, anonymous message length, anonymous jobs/characters per hour, authenticated jobs/hour, and device ID. Use flags for unattended installation:

```sh
sudo bash Server/install.sh --non-interactive --port 8177 --bind 127.0.0.1 \
  --anonymous-max-length 500 --anonymous-hourly-limit 5 \
  --anonymous-hourly-chars 2000 --authenticated-max-length 10000 \
  --authenticated-hourly-limit 60 --authenticated-hourly-chars 60000 \
  --device-id p2 --trusted-proxy 127.0.0.1/32
```

Run `bash Server/install.sh --help` for all parameters. `--dry-run` validates and prints the plan without modifying the machine; it also works on macOS. `--disable-anonymous` requires submission keys. Paths are customizable through `--prefix`, `--config`, and `--state-dir`. `--no-start` installs without enabling/starting the service. Bind addresses are IPv4; a reverse proxy may provide public IPv6.

Defaults:

| Item | Value |
|---|---|
| Listen | 127.0.0.1:8177 |
| Code | /opt/paperang-relay |
| Config | /etc/paperang-relay/config.json |
| Database/state | /var/lib/paperang-relay |
| Service | paperang-relay.service, dedicated unprivileged user |
| Anonymous | Enabled; 500 characters/message, 5 jobs/IP/hour, 2000 characters/IP/hour |
| Authenticated | 10,000 characters/message, 60 jobs/IP/hour, 60,000 characters/IP/hour |
| Inbox cap | 200 queued/claimed/printing jobs |
| Retention | 7 days for old queued/final records; cleanup runs during normal activity |

Quota windows are fixed UTC clock hours: for example 12:00:00–12:59:59 UTC, resetting at 13:00. They are not rolling sixty-minute windows. SQLite stores the counters across restarts. Both anonymous and authenticated submissions share each IP's accepted-use counter; authenticated requests are evaluated against the higher authenticated allowance. Rotating keys does not reset the IP counter. Only accepted submissions consume quota. Distinct users behind one public/NAT address share its counter.

Install reruns preserve the existing config and key database. To change flags on an existing installation, add `--reconfigure`; the old config is backed up. Alternatively edit the JSON and restart. Code/web files are updated, then the service is restarted unless `--no-start` is used.

```sh
sudo systemctl status paperang-relay
sudo journalctl -u paperang-relay
sudo systemctl restart paperang-relay
```

## HTTPS and client addresses

The default listener is loopback. Put the service behind an HTTPS reverse proxy, preserving Host and forwarding the client address. A minimal location inside an existing HTTPS nginx server is:

```nginx
location / {
    proxy_pass http://127.0.0.1:8177;
    proxy_set_header Host $http_host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}
```

Trust only your actual proxy addresses with repeated `--trusted-proxy CIDR` arguments. Without a trusted socket peer, X-Forwarded-For is ignored. With one, the chain is walked from right to left until the first untrusted hop; visitor-supplied leftmost addresses cannot override that hop. Do not trust all Internet addresses. TLS certificates, firewall rules, and DNS are configured separately. Direct HTTP testing can bind `0.0.0.0`; the Mac requires explicitly enabling its HTTP test option for non-loopback HTTP.

## Multiple authentication keys

First install generates admin, device, and submit keys locally and writes their raw values once to the root-only `initial-credentials.jsonl` file. This is not a public registration service. The database stores only SHA-256 key hashes. Each key has an ID, name, role, and target device, and can be revoked independently.

| Role | Permission |
|---|---|
| admin | List/create/revoke keys; submit to its target device |
| device | Report telemetry, see its device's inbox, claim/reject/report jobs |
| submit | Submit as its named identity to its target device |

Create additional named keys on the server:

```sh
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-add --role submit --name Alice --device p2
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-list
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-revoke KEY_ID
```

`key-add` prints the new raw key once; lists never return secrets. Keep generated files out of Git.

You may also generate a key **offline on your Mac**, then import it on the server:

```sh
python3 Server/keygen.py --name Alice --role submit --device p2 \
  --out ../PaperangStudio-Development/RelayKeys/alice.keys.json
# Transfer that private file through your existing SSH channel, then on Ubuntu:
sudo -u paperang-relay python3 /opt/paperang-relay/relay.py \
  --config /etc/paperang-relay/config.json key-import --file /private/path/alice.keys.json
```

The generator uses exclusive creation with mode0600, prints only the filename, and never overwrites a file. Grant the service user read access to the transferred file for import, then remove the transfer copy when no longer needed. Imported keys are not recognized until imported into that relay. Imports are sequential; duplicate key material is rejected by SQLite. Admins may also manage keys through the authenticated API below.

## Mac app

Open **Remote printing** (`远程打印`), enter the relay root URL and a device key, then connect. Credentials stay in memory for the app run. The relay must be explicitly connected and automatic printing defaults off. Connecting sends current status and cached device readings, including raw fields available to the app. Public status exposes only availability, connection, busy, and last-seen time.

The task list shows active/recent states and opens full details on selection. See the [task list and graphical editor guide](LAYOUT-EDITOR.md). Choose **note**, **receipt**, **retro teletype**, or **visitor keepsake**; select body/sender/time/IP/UA/browser fields, density, and final feed. IP/UA/browser details are excluded from paper by default. Preview the current layout, then approve or reject. Automatic mode can be remembered as a next-connection default and still requires the sidebar's single global paper switch. Oversized rendered content stops before claim/send; choose a smaller style or reject it. The current raster height guard remains 1400 rows.

Submitted metadata includes server-observed IP, UTC submission time, UA, language, Client Hints when sent, and browser-reported platform/timezone/screen. Browser fields and UA are not verified hardware identity. No precise location is requested. The webpage discloses that metadata goes to the printer owner and may be printed; it does not store access keys in browser storage.

## Job semantics

`queued → claimed → printing → sent`, or `rejected/unknown`. Claims are atomic and return a separate per-job claim secret. Other device keys cannot forge completion for that claim. Live printing renews its state during polling. Unreported claims older than five minutes become unknown when cleanup runs, never automatically queued again.

If claim delivery is lost, inspect the paper/task before submitting a replacement. A local failure after claim is reported unknown, not automatically retried. Result reports are idempotent when the final state matches. “Sent” means transport completion, not independent paper confirmation. The app can abandon an unreportable local association without reprinting; the server record remains subject to its unknown-state timeout. Stopping relay access stops a locally active remote print; already buffered printer data cannot be recalled.

## Relay API

This relay API is separate from the app's loopback API. Use `Authorization: Bearer psr_…` where required and JSON objects for POST. Credentials never belong in URLs. Requests are limited to64KiB; at most32 active connections and a12-second socket timeout. Cross-origin browser mutations are rejected. The web UI is served at `/`, `/app.js`, and `/style.css`.

| Method/path | Role | Input/result |
|---|---|---|
| GET /v1/public/status | Public | `{status:{online,printerConnected,busy,lastSeen},policy:{…}}`; online heartbeat expires after15seconds |
| POST /v1/jobs | Anonymous/submit/admin | `{text,browser?:{timezone,screen,platform,language}}` →202 `{id,shortID,receipt,state,resetAt}` |
| GET /v1/jobs/{id} | Receipt | Header `X-Job-Receipt`; returns state/time/detail and nullable transport progress, not content or visitor metadata |
| GET /v1/jobs/lookup/{shortID} | Public/scoped authorization | 12 hex characters, optionally grouped `ABCD-EF12-3456`; public state/time/progress, private details with a receipt or authorized key |
| POST /v1/device/status | Device | `{status:{…}}`, bounded16KiB telemetry |
| GET /v1/device/jobs | Device | Up to100 active/recent summaries with states, shortID and truncated text; active jobs prioritized |
| GET /v1/device/jobs/{id} | Device | Full text, metadata, state and shortID for one owned task |
| POST /v1/device/jobs/{id}/claim | Device | `{}` → `{id,claim,state}`;409 if already handled |
| POST /v1/device/jobs/{id}/reject | Device | `{}`; queued jobs only |
| POST /v1/device/jobs/{id}/result | Owning device key/claim | `{claim,state:"printing"|"sent"|"unknown"|"rejected",detail?:string}` |
| GET /v1/admin/keys | Admin | IDs/names/roles/devices/revocation/time, never raw values |
| POST /v1/admin/keys | Admin | `{name,role,device?}` → newly generated key, raw value once |
| POST /v1/admin/keys/{id}/revoke | Admin | `{}` |

Errors use `{error:string}`. Codes:400 invalid input,401 invalid/revoked key,403 role/origin/claim denied,404 missing/unreadable job,409 state conflict,413 body too large,415 wrong content type,429 IP quota,503 full inbox.429 also supplies `resetAt`, `retryAfter`, and Retry-After. A supplied but invalid key is rejected, not downgraded to anonymous.

## Validation scope

Local checks cover concurrent SQLite quota admission, persistence/restart, UTC-hour reset, length limits, key hashing/revocation/import, target/role isolation, atomic claims, final states, unknown outcomes, trusted-proxy parsing, and actual HTTP/web requests. The macOS app is built and signed independently. No actual Ubuntu host installation, production TLS setup, or new physical paper output is implied by these local checks.

Task IDs use a40-hex internal identity and a separately unique12-hex printable short ID. Short-ID migration preserves old UUID jobs. See [task IDs and templates](LAYOUT-EDITOR.md).

## 0.2.2 — Connection health, task feedback and key management

The Mac performs authenticated status and inbox requests every 2 seconds while connected. Each request times out after 4 seconds. An initial failed cycle marks the connection unavailable immediately; five further retry cycles are allowed, 2 seconds apart, before the app disconnects, disables automatic printing and shows an alert. A successful cycle resets the retry count. The interface displays the last successful response and labels cached tasks as a stale snapshot while unavailable. Old-session responses cannot update a new session. In-flight output is never automatically replayed.

The public status endpoint refreshes every 2 seconds in the webpage. A device heartbeat expires after 15 seconds: `online`, `printerConnected` and `busy` all become false. `lastSeen`, `serverTime` and `staleAfter` expose freshness. Web requests also time out after 4 seconds and clear the online indicator on failure. Device telemetry contains cached hardware readings; a relay heartbeat alone does not freshly query each printer field. Hardware can still disconnect between observations.

The server stores all task transitions: `queued → claimed → printing → sent → confirmed`. `sent` means the Mac finished sending raster and feed commands, not independent proof of paper output. The owner must inspect the paper and click **确认打印完成** to reach `confirmed` (explicitly labelled manual confirmation). The webpage polls the receipt every 2 seconds, including after `sent`, until confirmed, rejected or unknown. Printing progress is reported in `detail`. If a claimed/printing job has no update for 300 seconds, a subsequent device heartbeat, inbox or receipt operation expires it to `unknown`; receipt polling also triggers this expiry even with the Mac offline. No timer runs while the server itself is stopped. Queued tasks remain queued. The current webpage keeps one receipt in memory; reloading the page does not restore its tracking. Server/API records remain available with the receipt.

Expand **用户密钥管理** in the Mac remote-printing page and enter a separate administrator key. Device keys cannot administer users. Refresh lists server records (no raw secrets); create a key with name, role and target device, copy its one-time token, rename its display identity, revoke it, then optionally delete the revoked record. Renames affect future submissions; historical jobs preserve their original identity. The current administrator cannot rename/revoke/delete itself through these endpoints. Existing installations need the updated server code as well as app 0.2.2.

Additional JSON POST endpoints (Bearer roles specified below):

| Endpoint | Body | Result |
|---|---|---|
| `/v1/admin/keys/{id}/rename` | `{"name":"Alice"}` | `{"updated":true}`; trimmed 1–80 characters |
| `/v1/admin/keys/{id}/delete` | `{}` | `{"updated":true}`; revoke first, otherwise 409 |
| `/v1/device/jobs/{id}/confirm` | `{}` | **Device** key for the target device; sent/confirmed only, returns `{"state":"confirmed"}` |

Missing keys return 404; self-management returns 409; role isolation remains enforced. The existing revoke endpoint now returns 404 for missing records. Confirmation is an explicit human observation, not printer telemetry.

New print defaults reduce BLE write pause from 30 to **10 ms** and image-frame pause from 80 to **0 ms**. The transfer-drain polling interval is 5 ms rather than 30 ms. Printer credits, CoreBluetooth backpressure, command acknowledgements, paper/cover checks, density readback and the 2-second finishing wait remain enforced. Imported presets preserve their saved timing. Engineering controls can restore the prior 30/80 ms timings if necessary. This removes fixed transfer delays but does not change thermal-head mechanical speed. No new physical speed or print-quality measurement is claimed by the software checks.

## Partition progress and 12-character lookup

The webpage shows `20 / 90 · 22.2%` when the existing Mac reports
`第1/1份 · 分包20/90` in its result detail. The Mac app and Bluetooth protocol are
unchanged. This measures **raster partitions sent**, not independently measured
paper output. The Mac reports approximately every two seconds; quick jobs may
finish before an intermediate count is sampled.

Receipt, device-list, device-detail and short-ID lookup responses include:

```json
{"progress":{"completed":20,"total":90,"unit":"partitions","basis":"transport","percent":22.2}}
```

One bar contains five equal workflow segments: Queued, Accepted, Printing, Sent,
Confirmed. Only the Printing segment advances fractionally using partition counts;
20/90 fills 22.2% of that segment, rather than 22.2% of the whole workflow. Without
counts the bar stays at the start of Printing and says it is waiting for counts.
Sent does not fill Confirmed, even when all partitions have been sent. These
segment positions are milestones, not estimates of elapsed time.

The visible tab polls queued tasks every 500ms, claimed/printing tasks every 250ms,
and sent tasks every second until confirmation. Hidden tabs poll every four
seconds. Each automatic poll waits for the previous response before scheduling
the next one. Existing Mac reports still arrive about every two seconds.

Before any count is reported, `progress` is null. Counts aggregate all copies. Stale/malformed reports cannot
decrease accepted counts or change the total. A `sent` report completes an
already known transport total, while physical confirmation still requires
`confirmed`. An `unknown` result preserves the last partial count. No automatic
resend occurs and no missing total is invented.

Type the note's 12 hexadecimal characters into **Track**, with or without hyphens
and in either case. The route is `GET /v1/jobs/lookup/ABCD-EF12-3456`. An ID alone
returns state, timestamps, progress and `detailsAvailable:false`. It never exposes
the full internal ID, note text, IP/browser metadata or device-supplied freeform
detail. Removed or expired tasks return 404 under the normal retention policy.

Full details require the original `X-Job-Receipt`, an admin key, a key for the
owning device, or the exact submission key associated with that task. Wrong
receipts produce 404; unrelated valid keys get public fields only. Supplied
invalid/revoked keys produce 401. Historical tasks without submission-key
association require a receipt or admin/owning-device key; a matching display
name is never authorization. Keys and receipts belong in headers, not URLs.

The page automatically reuses up to 20 of this tab's original receipts. These
receipt capabilities remain in sessionStorage; access keys, note text and
metadata are not stored there. The submission-key field can unlock that key's
own notes. Private text is rendered as text, never HTML.

Public status advertises
`capabilities:{partitionProgress:"transport",shortIDLookup:true}`; older Macs can
ignore the additive field. Startup adds nullable `progress` and `submit_key`
columns, preserving keys, jobs and receipts. Existing matching progress details
are backfilled; historical completed jobs whose final detail replaced their
counts remain unknown. Back up SQLite consistently before upgrading; roll back
code with its corresponding database snapshot.

### Key rotation and recovery

The server rejects revocation of the admin credential used by the request, the
last active admin/device key for a target device, and a device key whose heartbeat
is online. Create a replacement, update the Mac and verify its connection before
revoking the old key. Revocation is immediate; deletion does not create a new key.

If all credentials are lost, use the installed service account's local `key-add`
command and configuration to issue fresh admin/device keys without resetting
the database. `key-revoke KEY_ID --allow-lockout` is an explicit local emergency
override and may disconnect clients; the API has no bypass. Protect credential
exports; restoring old backups restores their historical key state.

Server/browser tests do not imply a new Mac build or physical printing test.

See [relay storage and retention](RELAY-DATA.md) for live database versus browser
receipts and backups. `policy.retention_days` reports the actual configured task
retention in public status; the default is seven days after the last task update.
