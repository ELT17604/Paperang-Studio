# Paperang Studio LinuxRelayServer v0.3.0


> opt1: Regression sources are archived outside the shipping repository. See [validation and reproduction](../doc/OPTIMIZATION-opt1.md). Historical test paths below refer to Git revision `4393ae0`.
[Authentication and all API routes](../doc/AUTH-v2.md) · [中文升级指南](../doc/UPGRADE-v0.3.0.zh-CN.md) · [Agent handoff](../doc/HANDOFF-v0.3.0.zh-CN.md)

Python 3.10+, SQLite, argon2-cffi and cryptography. Relay API v2 is incompatible with v0.2.x; old relay /v1 routes return 410. The Mac local API v1 is separate. Receivers use scoped M keys; C administration is available in native clients. Public submissions require a registered browser identity; Explicit A keys cannot read print status; anonymous browser identities and B can read only their own states. Task content is available only in C history or a matching device-scoped active claim (M or proven public-key C).

## Ubuntu installation

Install python3, python3-venv and systemd prerequisites, then run `sudo bash Server/install.sh`. The installer creates `/opt/paperang-relay/venv`, installs requirements.txt and uses this interpreter in systemd. Source is installed in a dedicated directory; database/config are separate. Review the script before privileged use. Linux deployment must be verified on Linux.

```sh
bash Server/install.sh --dry-run --port 9088
sudo bash Server/install.sh --non-interactive --port 8177 --trusted-proxy 127.0.0.1/32
sudo systemctl status paperang-relay
```

Initial C/M/B credentials are written once to `/var/lib/paperang-relay/initial-credentials.jsonl` with root-only permissions. Keep M in the receiving client and C only for management. Existing configs and credentials are preserved. For upgrades, drain active claims, stop the old service, back up the database and follow the coordinated migration guide before invoking the installer. `--reconfigure` backs up and replaces the config; it does not make an incompatible old client work.

## Temporary development

```sh
python3 -m venv /private/path/paperang-venv
/private/path/paperang-venv/bin/python -m pip install -r Server/requirements.txt
PAPERANG_PYTHON=/private/path/paperang-venv/bin/python bash Server/run-temporary.sh --port 9088
```

Temporary database/keys are discarded on exit. Use HTTPS for browser cookies; loopback HTTP is for API/testing, with secure-context browser behavior depending on the browser. Use an HTTPS reverse proxy for dependable web testing. Do not disable Secure cookies to expose a public HTTP service.

## Parameters

Install and temporary launchers retain `--port`, `--bind`, `--device-id`, `--trusted-proxy`, `--disable-anonymous`, `--anonymous-max-length`, `--anonymous-hourly-limit`, `--anonymous-hourly-chars`, `--authenticated-max-length`, `--authenticated-hourly-limit`, `--authenticated-hourly-chars`. Installer-only: `--prefix`, `--config`, `--state-dir`, `--non-interactive`, `--reconfigure`, `--no-start`, `--dry-run`.

Configuration defaults: loopback:8177, device p2, anonymous 500 units/job, 5 jobs/hour, 2000 units/hour; authenticated 10000/60/60000; queue 200; retention seven days; trusted_proxies empty. Units are text characters plus raster rows. Identity/credential and aggregate quotas supplement IP quotas. `session_days` may be added to JSON (1–180). Aggregate `global_hourly_limit` and `global_hourly_chars` default to 6000 jobs and 6000000 units/hour. All forwarded addresses require explicitly trusted proxy networks. Reverse proxy must preserve Host/Origin correctly.

## Credentials and owner files

```sh
/path/venv/bin/python Server/relay.py --config CONFIG key-add --name Owner --role C
/path/venv/bin/python Server/relay.py --config CONFIG key-add --name Printer --role M
/path/venv/bin/python Server/relay.py --config CONFIG key-list
/path/venv/bin/python Server/relay.py --config CONFIG trust-public-key --name OwnerFile --file PUBLIC.pem
/path/venv/bin/python Server/relay.py --config CONFIG key-revoke CREDENTIAL_ID
```

key-add prints a newly generated secret once; redirect to a private file rather than logging it. key-import accepts existing offline keygen JSON for legacy role aliases (submit→B, admin→C, device→M). Use C UI/API to create A/passphrase credentials. Fixed owner keys are local RSA PEM files; register only their public key, then prove possession. Never upload the private JSON.

Run regression validation from the external archive described above. See the full auth contract for route schemas, status-only responses, CSRF, pagination, revocation, retention and proof limitations. Tests run with temporary databases; do not run them against a production config.

Quota/passphrase revision: `v0.3.1-linuxrelayserver-fix1`. C-only class and stable-identity limits persist in SQLite. Follow [deployment handoff](../doc/HANDOFF-QUOTAS-v0.3.1.zh-CN.md); never rerun owner cutover for a routine upgrade. Local 39-test validation does not mean the production service is deployed.
