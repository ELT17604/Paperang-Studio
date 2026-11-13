# WinLite v0.3.3 — USB-first printer connection

[简体中文](RELEASE-v0.3.3-winlite.zh-CN.md) · [Windows guide](WINDOWS.md) · [Version policy](VERSIONING.md)

WinLite 0.3.3 uses runtime `0.3.3-winlite` and assembly/file version `0.3.3.0`. It implements the `0.3.x` compatibility baseline with Relay HTTP API v2 and schema 1. A WinLite-only upgrade can retain LinuxRelayServer 0.3.2 and existing owner credentials.

## Connection behavior

- Default **Auto (USB first, Bluetooth fallback)**: at connection/reconnection, try the one attached legacy P2 through Windows usbprint.sys. If USB discovery/handshake fails, try the remembered Bluetooth address. Bluetooth discovery is still a manual choice; no nearby replacement printer is silently selected.
- Successful USB connection disposes this client's BLE session and disables its Bluetooth scan/selection controls. Windows system Bluetooth and other devices are unaffected.
- **Disconnect USB, use Bluetooth** saves `printerTransport: "bluetooth"`, releases USB, and reconnects through the remembered BLE device. Scan controls become available even if Bluetooth connection fails. USB remains skipped until **Restore USB priority and connect** / `printerTransport: "auto"` is chosen.
- Transport switching is rejected while a print is active. No mid-job transport switch or automatic Bluetooth replay follows a USB print failure. A newly attached USB printer does not preempt a healthy ongoing BLE connection: use restore/reconnect to reselect at an idle boundary.
- Existing automatic-print consent, relay polling,30-second printer retry, templates, renderer, density/feed preferences, owner authentication, encrypted storage and durable result handling are preserved. The normal receiver still connects its printer through the existing relay lifecycle; the separate hardware harness can diagnose a printer offline.

Native transport uses present-interface SetupAPI enumeration, asynchronous USBPRINT I/O with cancellation, USB CRC seed35769521 or BLE-persisted0, detected by read-only model queries, whole-row1008-byte frames and remaining-countdown numbering. BLE FF03/key-zero/increasing-index remain unchanged. Model, fresh media and density readback gate printing; battery/media appear in normal status. See [USB reference](USB-PROTOCOL.md) for all17 diagnostic GETs, results, unknown fields and physical calibration.

## Upgrade, rollback and update discovery

1. Finish active/pending work; exit the old tray normally. Install `PaperangStudio-0.3.3-winlite-win-x64-setup.exe`, or extract the complete portable/lite ZIP to its fixed location. AppId, install/data paths, startup registration and owner-file paths stay the same.
2. Existing schema1 settings load without migration/reset; missing `printerTransport` defaults to `auto`. Existing BLE address, credentials, templates, consent and density/feed are retained. USB discovery requires no Bluetooth address or new driver.
3. USB becomes the default on the next connection. To keep BLE, select **Only Bluetooth**, save/connect, then scan/select if needed. Do not reset/re-pair owner credentials to fix printer transport.
4. For rollback, finish/report pending jobs, exit and reinstall the prior0.3.2 WinLite binary while retaining the same data. Its JSON loader ignores the additive connection preference and uses BLE; USB is unavailable there. Preserve an encrypted-data backup separately, never delete unknown pending work to force rollback.

Publish under the unique Windows-only `v0.3.3-winlite` tag with matching installer/ZIPs and SHA-256 sidecars. Historical `v0.3.2` is immutable; do not attach0.3.3 packages to it or claim a shared0.3.3 product release. Manual update discovery already understands WinLite suffix tags and compares0.3.3 above0.3.2; it opens an actual published GitHub release only. It does not fabricate a downloadable release from source state.
