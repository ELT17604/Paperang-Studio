# macOS terminal temporary experience

[中文说明](../../doc/MACOS-TEMPORARY.zh-CN.md) · [All relay parameters](../README.md#available-parameters)

From the project root, run in Terminal:

```sh
bash Server/macOS-Temporary/run.sh
```

Or double-click `start.command` in Finder. Keep this folder inside the complete `Server/` tree; it reuses `../run.py`, `../relay.py`, and `../web/` rather than keeping a second server implementation.

Requires macOS and Python3.10+. The launcher checks Python on PATH and standard Apple Silicon/Intel Homebrew paths. It installs no packages and requires no sudo. To choose an interpreter explicitly:

```sh
PAPERANG_PYTHON=/opt/homebrew/bin/python3 bash Server/macOS-Temporary/run.sh --port 9088
```

Open `http://127.0.0.1:8177` (or your chosen port). The terminal shows temporary device/submission keys. Enter the device key and this URL in Paperang Studio → Remote printing. Use a submission key for an identified visitor, or leave it blank when anonymous submissions are enabled.

## Optional arguments

All arguments are forwarded unchanged to the shared temporary server. Supply them after `run.sh` or `start.command`.

| Argument | Default | Meaning |
|---|---|---|
| `--port N` |8177 |Listening port1024–65535 |
| `--bind IPV4` |127.0.0.1 |Loopback only;0.0.0.0 allows LAN connections |
| `--device-id ID` |p2 |1–64 letters/digits/underscore/hyphen |
| `--anonymous-max-length N` |500 |Characters per anonymous message,1–10000 |
| `--anonymous-hourly-limit N` |5 |Anonymous accepted jobs per IP/hour, positive integer |
| `--anonymous-hourly-chars N` |2000 |Anonymous accepted characters per IP/hour, positive integer |
| `--authenticated-max-length N` |10000 |Characters per authenticated message,1–10000 |
| `--authenticated-hourly-limit N` |60 |Authenticated accepted jobs per IP/hour, positive integer |
| `--authenticated-hourly-chars N` |60000 |Authenticated accepted characters per IP/hour, positive integer |
| `--disable-anonymous` |Off |Require a submission key |
| `--trusted-proxy CIDR` |None |Trusted forwarding proxy; repeat for several CIDRs |
| `--dry-run` |Off |Validate/show settings without starting or generating keys |
| `--help`, `-h` |— |Show supported arguments |

IP quotas reset at UTC clock hours. Example:

```sh
bash Server/macOS-Temporary/run.sh \
  --port 9088 --bind 127.0.0.1 --device-id p2 \
  --anonymous-max-length 300 --anonymous-hourly-limit 3 \
  --anonymous-hourly-chars 900 --authenticated-hourly-limit 30

bash Server/macOS-Temporary/run.sh --port 9088 --disable-anonymous
bash Server/macOS-Temporary/run.sh --port 9088 --dry-run
bash Server/macOS-Temporary/run.sh --help
```

For a LAN test, append `--bind 0.0.0.0`, then use the Mac's LAN address from another device.0.0.0.0 is a listening address, not a browser destination. macOS may ask about incoming network access. The relay does not change firewall settings.

## Stop and data lifetime

Keep the terminal open. Ctrl-C or a normal terminal closure (SIGHUP) stops the foreground process and removes its temporary database/keys. Nothing is registered in launchd or systemd, and restarting creates fresh keys and an empty inbox. A force kill may leave an OS temporary directory. Ubuntu persistent installation is separate: `sudo bash Server/install.sh`; its installation-only flags are not accepted by this macOS launcher.
