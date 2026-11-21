# Paused maintenance and historical branches

As of v0.3.3, the active release lines are MacClient, AndroidClient, WinClient and LinuxRelayServer. Retained code, historical downloads or an installed Skill do not establish current support.

## Explicitly paused components

| Component | Status | Historical material and current path |
|---|---|---|
| WinLite | Feature development paused | The WinForms frontend and reproducible build guide are retained. Windows development targets WinClient. Historical `0.3.3-winlite-opt1` is excluded from the unified v0.3.3 assets. |
| ChatGPT MCP / GPT Actions | Support paused | Retained integration code is not a working hosted printing service. Use native clients or a self-hosted Relay. |
| RelaySkill | Support paused | The v0.3.2 release record is historical. Current remote printing uses Relay API v2 and native receivers. |
| LocalBLESkill | Support paused | The historical direct BLE experiment is not a supported current entry point. Use MacClient or AndroidClient for BLE printing. |

The version registry records the integration/Skill pause since the v0.3.3-macclient stage. Paused components have no current commitment to fixes, upgrades or verification. Permanent removal and a resumption date have not been announced.

## Retired interfaces

HTTP API v1 is retired and old routes return HTTP 410. Consumers must migrate to v2. The Local and Relay APIs are independent interfaces with distinct addresses and credentials.

## Distinguishing other states

- The **local raster generation Skill (paperang-local-raster)** remains a helper for generating RAW/raster1 files for manual import and preview. It does not print directly.
- **LinuxClient** is planned, rather than a previously maintained release that was paused.
- The **temporary macOS Relay** is a development entry point, not a Linux production deployment.
- **USB protocol research, the ESP32 emulator and third-party implementations** remain research/reference material. No blanket pause is established for them. Windows USB support has entered the WinClient mainline.

## References

[Historical WinLite guide](WINLITE-LEGACY.md) · [Version policy](VERSIONING.md) · [Feature matrix](FEATURE-MATRIX.md) · [Unified v0.3.3 release notes](RELEASE-v0.3.3.md) · [Version registry](../VERSIONS.json)

A Git branch's existence, merge status or lack of recent commits alone is not evidence of an intentional maintenance pause.
