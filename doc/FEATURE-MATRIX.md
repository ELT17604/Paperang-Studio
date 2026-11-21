# Feature matrix · 0.3.x

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Versioning](VERSIONING.md) · [Version registry](../VERSIONS.json)

The `0.3.x` compatibility baseline uses Relay HTTP API v2, Mac Local HTTP API v2 and schema 1 for supported data formats. The source tree includes client revisions newer than the [published v0.3.2 product release](https://github.com/ELT17604/Paperang-Studio/releases/tag/v0.3.2).

## Distributions

| Distribution | Current version | Guide |
|---|---|---|
| MacClient | `0.3.3-macclient-opt2` | [macOS guide](USER-GUIDE.md) |
| WinClient (in development) | `0.3.3-winclient-dev5` | [Windows workspace and validation](WINCLIENT.md) |
| WinLite (paused) | `0.3.3-winlite-opt1` | [Legacy Windows guide](WINLITE-LEGACY.md) |
| AndroidClient | `0.3.3-androidclient-opt2` | [Android guide](../Android/README.md) |
| LinuxRelayServer | `0.3.2-linuxrelayserver-opt1` | [Server deployment](../Server/README.md) |

## Implemented features

A dash indicates that the distribution does not provide the feature.

| Feature | MacClient | WinClient | WinLite (paused) | AndroidClient | LinuxRelayServer |
|---|---|---|---|---|---|
| Direct P2 transport | BLE | USB first, BLE fallback or BLE-only | USB/BLE | BLE | — |
| Local workspace | Text/image, styled layers, QR | Text/image, editable layers, QR, live scan | Block templates | Text/image, styled layers, QR, touch drawing | — |
| Files and presets | RAW/raster1, PNG/RAW, schema 1 presets | RAW/raster1, PNG/RAW/JSON, Mac/Android presets | Relay raster1 | RAW/raster1, PNG/RAW/JSON, schema 1 presets | Raster1/web preview |
| Paper controls | Density, copies, feed | Density, copies, feed, cancellation | Density/feed | Density, copies, feed, trailing-blank trim | — |
| History/reuse | Local and Relay | Opt-in local materials/history and Relay administration | Relay history/export | Editable history/materials | Persistent task history/export |
| Relay API v2 | Receiver/owner management | Receiver/owner management | Receiver/owner management | — | Queue/access/admin |
| Local API v2 | Loopback API | Principal operations; documented platform differences | — | — | — |
| Background/terminal | Tray receiver, paper terminal | Tray receiver, ConPTY, sign-in startup | Tray receiver/startup | — | systemd |

Relay roles, task ownership and report recovery follow the [v2 API contract](API-v2-032.md). The [local raster generation Skill](../Skills/paperang-local-raster/SKILL.md) generates files for manual import. ChatGPT MCP/GPT Actions, RelaySkill and LocalBLESkill are paused in the current source tree. The [v0.3.2 release record](RELEASE-v0.3.2.md) covers the historical RelaySkill distribution.
