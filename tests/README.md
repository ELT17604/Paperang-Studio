# Local tests

Run commands from the repository root.

## Version and API contracts

```sh
python3 scripts/check-versions.py
python3 tests/test_contract_inventory.py
node --check Clients/paperang-client.js
node --check Server/web/app.js
```

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
