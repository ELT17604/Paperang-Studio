# Paperang Studio relay

The English page includes raster partition progress and lookup by the 12-character
ID printed on a note. It works with the existing Mac app, without rebuilding it.
See [progress and lookup](../doc/REMOTE-PRINTING.md#partition-progress-and-12-character-lookup).
The current administrator, last active admin/device key, and online device key are
protected from accidental revocation; configure replacements before rotating keys.

[Full deployment and API guide](../doc/REMOTE-PRINTING.md) · [中文说明](../doc/REMOTE-PRINTING.zh-CN.md)

A lightweight Ubuntu relay with a submission webpage, SQLite inbox, multiple revocable keys, anonymous character limits, and hourly per-IP quotas. Python 3.10+ standard library only. The Mac app polls outward, reports printer state, and prints through its existing Bluetooth pipeline.

Install from this checkout on Ubuntu 22.04+ with systemd:

```sh
sudo bash Server/install.sh
```

Preview a non-interactive configuration without changing the system:

```sh
bash Server/install.sh --dry-run --port 9088 --anonymous-max-length 300 --anonymous-hourly-limit 3
```

For an HTTPS reverse proxy on the same server:

```sh
sudo bash Server/install.sh --non-interactive --bind 127.0.0.1 --port 8177 \
  --anonymous-max-length 500 --anonymous-hourly-limit 5 \
  --anonymous-hourly-chars 2000 --trusted-proxy 127.0.0.1/32
```

Raw initial keys are written once to `/var/lib/paperang-relay/initial-credentials.jsonl`, readable by root. Enter the **device** key in the Mac app's Remote printing (`远程打印`) page. The **submit** key identifies an individual sender; the **admin** key manages access keys. Do not distribute admin/device keys to guests.

`--help` lists all configurable ports, addresses, quotas, paths, and installation switches. Existing settings and keys survive upgrades; use `--reconfigure` to replace settings with a timestamped backup. Installation does not modify firewall rules, DNS, or reverse-proxy configuration.

## Temporary foreground experience

```sh
bash Server/run-temporary.sh --port 9088
# Optional: --bind 0.0.0.0 --anonymous-max-length 300 --anonymous-hourly-limit 3
```

No sudo or systemd registration. Press Ctrl-C or normally close the terminal to stop. The database and generated credentials are temporary and discarded on normal shutdown. For persistent installation use `install.sh` instead; manage it with `sudo systemctl start|stop|restart|status paperang-relay` and `sudo systemctl enable|disable paperang-relay` (choose one action, not the literal pipe-separated expression).


## Available parameters

Shared by Ubuntu `install.sh`, `run-temporary.sh`, and `macOS-Temporary/run.sh`:

| Argument | Default | Meaning |
|---|---|---|
| `--port N` |8177 |IPv4 listening port1024–65535 |
| `--bind IPV4` |127.0.0.1 |Listening address; use0.0.0.0 for a LAN test |
| `--device-id ID` |p2 |1–64 letters/digits/underscore/hyphen |
| `--anonymous-max-length N` |500 |Anonymous message length,1–10000 characters |
| `--anonymous-hourly-limit N` |5 |Anonymous accepted jobs/IP/UTC hour |
| `--anonymous-hourly-chars N` |2000 |Anonymous accepted characters/IP/UTC hour |
| `--authenticated-max-length N` |10000 |Authenticated message length,1–10000 characters |
| `--authenticated-hourly-limit N` |60 |Authenticated accepted jobs/IP/UTC hour |
| `--authenticated-hourly-chars N` |60000 |Authenticated accepted characters/IP/UTC hour |
| `--disable-anonymous` |Off |Require a submission key |
| `--trusted-proxy CIDR` |None |Trust a forwarding proxy; repeatable |
| `--dry-run` |Off |Validate/show configuration without changing the system or opening a port |
| `--help`, `-h` |— |Show supported options |

Hourly job/character allowances must be positive integers. Shared per-IP usage and fixed UTC-hour reset semantics are described in the [deployment guide](../doc/REMOTE-PRINTING.md).

Ubuntu persistent installation **only**:

| Argument | Default | Meaning |
|---|---|---|
| `--prefix PATH` |/opt/paperang-relay |Dedicated program directory |
| `--config PATH` |/etc/paperang-relay/config.json |Configuration JSON in its dedicated directory |
| `--state-dir PATH` |/var/lib/paperang-relay |Dedicated database/state directory |
| `--non-interactive` |Off |Use flags/defaults without prompting |
| `--reconfigure` |Off |Replace an existing config with a backup |
| `--no-start` |Off |Install without enabling/starting systemd |

Code, config and data directories must be dedicated, non-overlapping absolute paths without spaces, parent traversal, or systemd escape characters. `queue_limit` and `retention_days` are configuration-file settings, not startup flags.

macOS launcher environment variable: `PAPERANG_PYTHON=/path/to/python3` chooses a specific Python3.10+ executable. It is optional and does not accept embedded command arguments.

## macOS-specific folder

Use [macOS-Temporary](macOS-Temporary/README.md), which provides `run.sh` and the Finder-friendly `start.command`:

```sh
bash Server/macOS-Temporary/run.sh --port 9088 \
  --anonymous-max-length 300 --anonymous-hourly-limit 3
```

For temporary mode, do not supply installation-only flags such as `--config` or `--prefix`.

## Version 0.2.2 status and key API

The webpage and Mac refresh every 2 seconds; server device presence expires after 15 seconds. Task receipts include sent versus manually confirmed paper output. The Mac supports administrator key creation, rename, revoke and deletion. See the [complete status, retry and API contract](../doc/REMOTE-PRINTING.md#022--connection-health-task-feedback-and-key-management) and [中文说明](../doc/REMOTE-PRINTING.zh-CN.md). Both client and server must be updated. Existing command-line installation parameters above remain available.
