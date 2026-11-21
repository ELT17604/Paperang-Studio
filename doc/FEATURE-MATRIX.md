# Feature matrix · 0.3.x

[简体中文](FEATURE-MATRIX.zh-CN.md) · [Versioning](VERSIONING.md) · [Version registry](../VERSIONS.json)

The four active distributions ship in [v0.3.3](RELEASE-v0.3.3.md). They share the `0.3.x` compatibility baseline, Relay API v2, Local API v2 and applicable schema 1 formats.

## Distributions

| Distribution | Current version | Guide |
|---|---|---|
| MacClient | `0.3.3-macclient-opt2` | [macOS guide](USER-GUIDE.md) |
| WinClient (in development) | `0.3.3-winclient-dev5` | [Windows workspace and validation](WINCLIENT.md) |
| AndroidClient | `0.3.3-androidclient-opt2` | [Android guide](../Android/README.md) |
| LinuxRelayServer | `0.3.2-linuxrelayserver-opt1` | [Server deployment](../Server/README.md) |

## Implemented features

A dash indicates that the distribution does not provide the feature.

| Feature | MacClient | WinClient | AndroidClient | LinuxRelayServer |
|---|---|---|---|---|
| Direct P2 transport | BLE | USB first, BLE fallback or BLE-only | BLE | — |
| Local workspace | Text/image, styled layers, QR | Text/image, editable layers, QR, live scan | Text/image, styled layers, QR, touch drawing | — |
| Files and presets | RAW/raster1, PNG/RAW, schema 1 presets | RAW/raster1, PNG/RAW/JSON, Mac/Android presets | RAW/raster1, PNG/RAW/JSON, schema 1 presets | Raster1/web preview |
| Paper controls | Density, copies, feed | Density, copies, feed, cancellation | Density, copies, feed, trailing-blank trim | — |
| History/reuse | Local and Relay | Opt-in local materials/history and Relay administration | Editable history/materials | Persistent task history/export |
| Relay API v2 | Receiver/owner management | Receiver/owner management | — | Queue/access/admin |
| Local API v2 | Loopback API | Principal operations; documented platform differences | — | — |
| Background/terminal | Tray receiver, paper terminal | Tray receiver, [paper terminal (ConPTY)](TERMINAL.md), sign-in startup | — | systemd |

Relay roles, task ownership and report recovery follow the [v2 API contract](API-v2-032.md). The [local raster generation Skill](../Skills/paperang-local-raster/SKILL.md) generates files for manual import. ChatGPT MCP/GPT Actions, RelaySkill and LocalBLESkill are paused in the current source tree. The [v0.3.2 release record](RELEASE-v0.3.2.md) covers the historical RelaySkill distribution.

For additional topics and historical status, see the [documentation index](README.md) and [paused branches](PAUSED-BRANCHES.md).
