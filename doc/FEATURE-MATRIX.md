# Feature matrix · 0.3.x

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Versioning](VERSIONING.md) · [Version registry](../VERSIONS.json)

The `0.3.x` compatibility baseline uses Relay HTTP API v2, Mac Local HTTP API v2 and schema 1 for supported data formats. The source tree includes client revisions newer than the [published v0.3.2 product release](https://github.com/ELT17604/Paperang-Studio/releases/tag/v0.3.2).

## Distribution status

| Distribution | Current source version | Validation evidence |
|---|---|---|
| MacClient | `0.3.3-macclient`, build 3301 | arm64 build/signing, 47 local HTTP checks, RAW/raster1 byte checks and QR decoding. Physical printing evidence is from v0.3.2. [Release notes](RELEASE-v0.3.3-macclient.md) · [v0.3.2 validation](VALIDATION-v0.3.2-macos.md) |
| WinLite | `0.3.3-winlite`, assembly 0.3.3.0 | Local x64 build, 73 regressions, 12 isolated Relay checks and P2 USB/BLE transport checks. Installed upgrade evidence is from v0.3.2. [Current validation](VALIDATION-v0.3.3-windows.md) · [v0.3.2 validation](VALIDATION-v0.3.2-windows.md) |
| AndroidClient | `0.3.3-androidclient`, versionCode 9 | Signed APK, Android 13 emulator and local protocol/rendering checks. Physical P2 printing awaits hardware validation. [Release notes](RELEASE-v0.3.3-androidclient.md) |
| LinuxRelayServer | `0.3.2-linuxrelayserver` | Ubuntu deployment, preserving upgrade/rollback, HTTPS/API v2 and Mac receiver acceptance. [Ubuntu validation](VALIDATION-v0.3.2-ubuntu.md) |
| WinClient / LinuxClient | Planned | — |

## Implemented features

A dash indicates that the distribution does not provide the feature.

| Feature | MacClient | WinLite | AndroidClient | LinuxRelayServer |
|---|---|---|---|---|
| Direct P2 transport | BLE | USB first, BLE fallback | BLE | — |
| Local text/image print workspace | Text, image, styled layers and QR | Block-template rendering | Text, image, styled layers, QR and full-screen drawing | — |
| Raster files and preview | RAW/raster1 import, PNG/RAW export | Raster1 Relay input and template preview | RAW/raster1 import, PNG/RAW/raster1 export | Raster1 input and web preview |
| Paper controls | Density, copies and feed | Density, feed and template output | Density, copies, feed and automatic trailing-blank trim | — |
| History and reuse | Local jobs and Relay history | Relay history and export | Local raster history, editable task restore and materials | Persistent task history and administrative export |
| Relay HTTP API v2 | Receiver and owner management | Receiver and owner management | — | Web submission, queue, access controls and administration |
| Local HTTP API v2 | Loopback API | — | — | — |
| Background workflows | Tray receiver and paper terminal | Tray receiver and Windows sign-in startup | — | systemd service |

Relay roles, task ownership and report recovery follow the [v2 API contract](API-v2-032.md). The [local raster generation Skill](../Skills/paperang-local-raster/SKILL.md) generates files for manual import. ChatGPT MCP/GPT Actions, RelaySkill and LocalBLESkill are paused in the current source tree. The [v0.3.2 release record](RELEASE-v0.3.2.md) covers the historical RelaySkill distribution.
