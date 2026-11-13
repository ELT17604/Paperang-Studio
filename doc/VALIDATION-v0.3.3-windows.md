# WinLite0.3.3 Windows validation

[简体中文](VALIDATION-v0.3.3-windows.zh-CN.md) · [Release/upgrade](RELEASE-v0.3.3-winlite.md) · [Reproducible tests](../tests/windows/README.md)

Date:2026-11-10, Asia/Shanghai. Branch `dev/p2-usb-protocol-research`, based on `v0.3.2` commit `46d55b7d6782d02ba0ff296e0af3c79a7f19b674`. Local0.3.3-winlite candidate; no GitHub publication, production deployment or installed-user upgrade is claimed.

## Scope and preservation

Only Windows runtime/packaging advances to0.3.3. Other components and common API compatibility remain0.3.2, Relay/local HTTP v2, content/template/config schema1. Existing WinLite install/data paths, AppId, owner authentication, DPAPI, pending-result affinity, rendering and automatic-print preferences remain. Additive `printerTransport` defaults to `auto` in old settings and persists `bluetooth` for manual USB opt-out. Windows system Bluetooth is never toggled. The existing BLE GATT implementation was not modified.

Native USB uses IPrinter, UsbP2Session, present SetupAPI interfaces and asynchronous FileStream over usbprint.sys. PreferredPrinter selects at connection boundaries; a print failure never causes fallback/replay. Active jobs block UI save/switch. Normal receiver printer connection retains the existing Relay lifecycle. The offline hardware harness does not poll the production Relay.

## Results

| Check | Actual result |
| --- | --- |
| Native C#/.NET Release build | Passed with .NET SDK10.0.403, Windows x64; final build without warnings/errors |
| Native regression | **73 checks passed**: inherited BLE/protocol/template/DPAPI/claim/no-replay tests plus USB CRC/framing/countdown/media/density, preference defaults, priority/manual/fallback/cancellation and uncertain-print isolation |
| Isolated real Relay integration | **12 checks passed** using actual native owner proof/session renewal, C scope, M history denial, real renderer/engine text+raster1 and fake printer; temporary database only |
| Python diagnostics | **20 tests passed**; captured vectors, corrupt-header recovery, ACK/data distinctions, media tri-state, redaction and guards |
| Shared contract/version checks | check-versions.py and test_contract_inventory.py passed; only documented WinLite0.3.3 exception allowed |
| USB actual hardware | Actual modelP2,100% battery, normal media; disconnect/reconnect passed |
| Actual USB→BLE→USB cycle | Passed after the CRC-state fix; fresh BLE scan matched the remembered device, BLE handshake/media read passed, BLE disposed and USB restored |
| Real SettingsForm | Actual form with isolated preferences and real transport; USB status visible, BLE selection/scan disabled; manual BLE released USB and reopened scan; after scanning, actual BLE status showed battery100%/closed/loaded. Final USB view again showed scan/selection disabled. User configuration/production Relay was not used for writes. |
| Native physical USB pattern | Four1008-byte countdown packets3/2/1/0,576×56 dots, density100/feed150; **user explicitly confirmed complete, correct order, normal darkness** |
| Self-contained portable publish | Passed; matching0.3.3-winlite ZIP and SHA-256 |
| Framework-dependent lite publish | Passed; matching0.3.3-winlite ZIP and SHA-256 |
| Inno Setup build | Passed with Inno Setup6.7.3; self-contained x64 installer and SHA-256 |

One repeat integration invocation initially collided with the still-open isolated UI fixture locking its loaded test DLLs. After the fixture exited, the actual integration was rebuilt/rerun and all12 checks passed. This was a validation-process file lock, not a transport failure.

## New measured CRC behavior

The first real transport-cycle check exposed a failure: a default-seed USB model query stopped returning data after the unchanged BLE handshake registered key0. Read-only captures then showed USB accepts seed0 and returns a CRC-valid model frame. USB now probes the two known CRC states using model GET only, never a reset/CRC-registration command and never a replayed physical write. The full hardware cycle passed after this fix, and a native regression covers the persisted-zero case. Earlier default-seed physical paper evidence remains valid; no additional paper confirmation was invented for the read-only cycle.

## Packages and remaining limits

Output remains outside the tracked checkout, under sibling `PaperangStudio-Windows-Development/Release/`:

- `PaperangStudio-0.3.3-winlite-win-x64-setup.exe`
- `V0.3.3-winlite-win-x64-portable.zip`
- `V0.3.3-winlite-win-x64-lite.zip`
- Per-file `.sha256` sidecars.

Packages contain program payload, project icon, README, LICENSE and NOTICE; no user configuration/owner files, device identifiers or research captures. Packaging versions and registry agree. The installer retains the existing AppId, data location and uninstall/data-retention code. This run **did not install/uninstall/overlay the user's running installation**, execute a sign-in/reboot, validate ARM64, run a clean VM, or claim new production end-to-end printing. Historical0.3.2 installer evidence is not relabeled0.3.3. Packages are unsigned as before.

Device busy, mechanical completion and charging remain unknown. USB four-packet output is confirmed, but firmware maximum height/buffer depth, long-image throughput, unplug-during-print behavior and other devices/Windows releases remain unmeasured. Interrupted writes stay unknown; no fallback replay is allowed. See [USB evidence](USB-PROTOCOL.md) and [upgrade/rollback](RELEASE-v0.3.3-winlite.md).
