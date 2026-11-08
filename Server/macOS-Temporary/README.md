# Temporary relay on macOS — v0.3.0

Install `Server/requirements.txt` into a private Python 3.10+ venv, then set `PAPERANG_PYTHON=/path/venv/bin/python` and run `bash Server/macOS-Temporary/run.sh`. The launcher remains foreground; Ctrl-C discards temporary data and keys. `start.command` uses the same launcher. [Parameters](../README.md#parameters), [v2 auth](../../doc/AUTH-v2.md), [upgrade](../../doc/UPGRADE-v0.3.0.zh-CN.md).

The terminal supplies temporary M/B keys and a private credential file containing C. M goes in the receiver; B identifies a submitter. Browser anonymous registration uses A by default and cannot read status. Browser login persists as a Secure/HttpOnly cookie; use an HTTPS reverse proxy for dependable browser testing. Public HTTP is not supported for persistent credentials. No pip installation occurs automatically in this launcher. Native macOS testing remains required.
