# v0.3.2 contract and native validation

Run from repository root on macOS. These tests never access a real printer or production Relay. `probe_relay.swift` is the separate explicit read-only production check.

```sh
python3 -m venv work/venv
work/venv/bin/python -m pip install -r Server/requirements.txt
PAPERANG_WORK_DIR="$PWD/work/native" zsh scripts/build.sh
python3 tests/test_contract_inventory.py
python3 scripts/check-versions.py
python3 tests/test_local_v2.py --app 'work/native/Release/Paperang Studio.app'
work/venv/bin/python tests/test_relay_v2.py
python3 tests/build_native_check.py
work/venv/bin/python tests/run_native_check.py
python3 -m compileall -q Clients tests
node --check Clients/paperang-client.js
```

The native check compiles actual AppKit/SwiftUI application sources with the ordinary application entry point disabled, linking the actual release libraries. It uses a temporary database and owner files, synthetic jobs, and a unique Keychain service. It restores the prior saved configuration and deletes only its own Keychain items. It needs normal macOS Keychain access; a filesystem sandbox can deny Keychain writes. Do not interpret a sandbox-only Keychain failure as a passed check. It covers RSA PKCS8/PKCS1 proof/renewal, C paging/quotas/private views, M execution, report-only recovery without reprinting, template save/restore, configuration migration, local job state mapping and update ordering. No native checks rely on copied alternative application implementations.

`run_native_check.py` includes one fixture-only route and a wrapper simulating an older v2 server's pagination response. These exist only inside the temporary test process, never in the shipped Relay. The native harness and runner were promoted from the project's historical external validation directory so they are now reproducible in Git.

For an explicit production capability probe (no login or job changes):

```sh
swiftc -parse-as-library -module-cache-path work/swift-module-cache tests/probe_relay.swift -o work/probe
work/probe "$PAPERANG_RELAY_URL"
```

Physical text/image/density tests require the native GUI and a real P2. Consult [validation evidence](../doc/VALIDATION-v0.3.2-macos.md); never automatically replay an uncertain print. HTTP acceptance/transport tests are distinct from owner paper confirmation.

Windows-native reproduction: [tests/windows/README.md](windows/README.md). Windows has no Mac loopback HTTP service; its runtime surface is Relay v2. Shared HTTP tests and inventory/version guards also run on Windows.

## Ubuntu / reproducible server and sender regressions

Python 3.10+; keep the venv outside tracked source. Install `Server/requirements.txt`, Pillow and httpx (the experimental MCP server additionally uses `Integrations/ChatGPT/requirements.txt`). Install a real CJK font, or point `PAPERANG_TEST_CJK_FONT` at a privately unpacked Noto CJK TTC file. Missing CJK fonts are a test prerequisite failure, not a successful rendering check.

```sh
PYTHONPATH=Server:Skills/paperang-relay-print/scripts:Integrations/ChatGPT python -B -m unittest discover -s tests/server -v
python -B tests/test_relay_v2.py
python tests/test_contract_inventory.py
python scripts/check-versions.py
bash -n Server/install.sh
bash Server/install.sh --dry-run --port 28177
node --check Server/web/app.js
node --check Clients/paperang-client.js
```

`test_deploy.py` simulates service control while exercising actual SQLite snapshots, row/schema comparison, code switching and rollback retention. It does not claim a real systemd deployment. `test_adapters.py` uses actual HTTP sender clients and an isolated real Relay, with no printer. Native tests and production physical round trips remain separate gates. The historical one-time factory-reset installer is removed; normal upgrades use [the preserving updater](../doc/UPGRADE-v0.3.2.md).
