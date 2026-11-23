# Local tests

Run commands from the repository root.

## Version and API contracts

```sh
python3 scripts/check-versions.py
python3 scripts/check-docs.py
python3 scripts/check-source.py
python3 tests/test_contract_inventory.py
node --check Clients/paperang-client.js
node --check Server/web/app.js
```

## Browser workspace

Run `node --test tests/web/*.mjs` with Node 22.13 or newer. The Bluetooth tests use an in-memory P2 peer to exercise the protocol without consuming paper. For an end-to-end hardware check, connect a P2 in a supported browser, print a short sheet and inspect its orientation, margins and final feed. [Web guide](../doc/WEB.md)

## Relay

```sh
python3 -m venv work/venv
work/venv/bin/python -m pip install -r Server/requirements.txt
PYTHONPATH=Server work/venv/bin/python -B -m unittest discover -s tests/server
work/venv/bin/python -B tests/test_relay_v2.py
```

The suite uses temporary SQLite databases and local HTTP listeners.

## macOS

```sh
PAPERANG_WORK_DIR="$PWD/work/native" zsh scripts/build.sh
python3 tests/test_local_v2.py --app 'work/native/Release/Paperang Studio.app'
python3 tests/build_native_check.py
work/venv/bin/python tests/run_native_check.py
```

The native suite requires macOS Keychain access.

## Android

```sh
./Android/build.sh
tests/android/check.sh
tests/android/runtime.sh <emulator-serial>
```

## Windows

[Windows local tests](windows/README.md)

WinClient desktop fixtures are under `windows/WinClientChecks`, compiled only by the verification build. `Windows/scripts/build-client.ps1` runs that separate application, then publishes a fixture-free production application and checks its offline startup. The suite never authorizes physical printing or uses saved Relay credentials.

The deployment/console server tests require a POSIX Python runtime (`fcntl` and curses); use Linux/WSL rather than weakening those tests on Windows. Native macOS and full Android build/runtime checks require their own SDKs.
