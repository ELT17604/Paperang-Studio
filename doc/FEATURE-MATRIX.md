# Feature matrix · 0.3.x

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Versioning](VERSIONING.md) · [Version registry](../VERSIONS.json)

The `0.3.x` compatibility baseline uses Relay HTTP API v2, Mac Local HTTP API v2 and schema 1 for supported data formats. The source tree includes client revisions newer than the [published v0.3.2 product release](https://github.com/ELT17604/Paperang-Studio/releases/tag/v0.3.2).

## Distributions

| Distribution | Current version | Guide |
|---|---|---|
| MacClient | `0.3.3-macclient-opt1` | [macOS guide](USER-GUIDE.md) |
| WinLite | `0.3.3-winlite-opt1` | [Windows guide](WINDOWS.md) |
| AndroidClient | `0.3.3-androidclient-opt1` | [Android guide](../Android/README.md) |
| LinuxRelayServer | `0.3.2-linuxrelayserver-opt1` | [Server deployment](../Server/README.md) |

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
