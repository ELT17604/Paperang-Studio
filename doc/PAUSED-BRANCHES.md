# Maintenance notice

As of v0.3.3, MacClient, AndroidClient, WinClient and LinuxRelayServer are the active release lines. Maintenance or support for the following components is paused. A resumption date has not been announced.

## Paused components

| Component | Status | Current option |
|---|---|---|
| WinLite | Maintenance paused. WinForms source and build records are archived. | [WinClient](WINDOWS.md) provides Windows USB/BLE printing, Relay reception and a paper terminal. |
| ChatGPT MCP / GPT Actions | Support paused. The current release contains no hosted printing entry point for these integrations. | Use a [current client](FEATURE-MATRIX.md) or a [self-hosted Relay](REMOTE-PRINTING.md) for browser submissions. |
| RelaySkill | Support paused. See [v0.3.2](RELEASE-v0.3.2.md) for the historical release. | Use [Relay API v2](API-v2-032.md) with [MacClient](USER-GUIDE.md) or [WinClient](WINDOWS.md). |
| LocalBLESkill | Support paused. | Connect over BLE using [MacClient](USER-GUIDE.md) or [AndroidClient](../Android/README.md). |

## Retired APIs

Relay HTTP API v1 and Mac Local HTTP API v1 are retired. Old routes return HTTP 410. See the [Relay v2 contract](API-v2-032.md) and [Local API v2 guide](API.md) for migration.

## Other statuses

- The [local raster generation Skill](../Skills/paperang-local-raster/SKILL.md) remains available for preparing raster1 files for manual import.
- LinuxClient is planned. LinuxRelayServer is the current deployable Linux server.
- The [temporary macOS Relay](../Server/macOS-Temporary/README.md) is for local development. Use [LinuxRelayServer](../Server/README.md) for persistent deployments.
- USB research, the ESP32 emulator and third-party implementations remain indexed as reference material. Use WinClient for current Windows USB printing.

[Current release and downloads](RELEASE-v0.3.3.md) · [Historical WinLite guide](WINLITE-LEGACY.md) · [Version registry](../VERSIONS.json)
