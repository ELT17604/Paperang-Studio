# Windows v0.3.2 native acceptance

Run from the repository root on Windows 10 2004+ / Windows 11 with .NET 10 SDK, Python 3.10+ and Inno Setup 6. Output, owner files, temporary databases and installer logs stay outside tracked source. These tests link the actual WinLite and Core projects; they do not ship an alternative client implementation.

```powershell
python -m venv work/venv
./work/venv/Scripts/python.exe -m pip install -r Server/requirements.txt
dotnet run --project tests/windows/Paperang.Tests -c Release
./work/venv/Scripts/python.exe tests/test_relay_v2.py
./work/venv/Scripts/python.exe tests/windows/run_integration.py --dotnet dotnet
python tests/test_contract_inventory.py
python scripts/check-versions.py
./Windows/scripts/build.ps1
./Windows/scripts/build-installer.ps1
```

Default native regression and Relay integration use fake printers. They check actual DPAPI in a temporary directory, P2 framing/CRC/media guards, raster/template composition, durable claim results, pause/cancellation/recovery without reprinting, platform-specific update ordering, PKCS8/PKCS1 RSA proof/session renewal, C history/device scope and M denial. Shared HTTP checks exercise A/B/C/M, UUID deduplication, payload isolation, timeout to unknown, late report-only recovery, rejection and manual C confirmation. Synthetic paper confirmation is only in a disposable database.

The harness was promoted from the historical external Windows test directory. Its old v1 mock endpoint, missing claimed-body response and obsolete authentication default were corrected to match actual v2 execution; failures were fixed in the test fixture, not by weakening the product contract.

For **explicitly authorized** local machine installation testing (installs/upgrades/uninstalls/reinstalls the user's app, preserving data):

```powershell
python tests/windows/test_installer.py --setup PATH_TO_SETUP_EXE --log-dir work/installer
```

It refuses pending results, hashes existing user data without logging content, checks running-client shutdown, version registration, overlay, silent uninstall/data retention, payload reinstall, restart and original Run preference. It waits for Inno's spawned uninstaller, not merely its short-lived launcher. This is a clean-payload reinstall on an existing user, **not** a clean Windows VM or actual sign-in/reboot test.

Read-only production capability probe, using only the saved endpoint (never logging the endpoint/key):

```powershell
dotnet run --project tests/windows/Paperang.Tests -c Release -- --probe-production
```

Hardware diagnostics without printing:

```powershell
dotnet run --project tests/windows/Paperang.Tests -c Release -- --ble
```

Actual paper requires explicit authorization and a P2. Stop other BLE clients first. These commands consume two local sheets and one isolated Relay sheet respectively. Never run them as unattended regression checks:

```powershell
dotnet run --project tests/windows/Paperang.Tests -c Release -- --physical work/physical --confirm-print
./work/venv/Scripts/python.exe tests/windows/run_integration.py --dotnet dotnet --physical-once work/physical/PS032-W-R1.json
```

Each physical attempt creates an exclusive intent record before sending; existing records refuse another attempt. Inspect paper and record owner confirmation separately. Failure/timeout is not permission to delete a record or replay. The isolated Relay uses a synthetic RSA owner and temporary DB, and the actual Windows printer/renderer/engine; it never uses the production server or its identities. Local physical labels are PS032-W-T1 and PS032-W-IT1. The real Relay job ID is recorded in the private attempt file and the public validation report (ID alone grants no authority).
