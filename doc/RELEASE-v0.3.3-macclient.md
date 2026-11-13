# Paperang Studio v0.3.3-macclient

MacClient 0.3.3 adds a searchable system font picker, configurable QR codes and freeform paper composition. Text boxes support formatting individual characters or selections. The workbench also imports packed .raw and validated raster1 JSON files. The [local raster Skill](../Skills/paperang-local-raster/SKILL.md) helps models generate files for manual preview and printing.

Starting with this release, support for ChatGPT MCP/GPT Actions, RelaySkill and LocalBLESkill is paused. Their implementations, guides and dedicated automation have been removed from the current tree. The desktop Relay receiver and the local raster generation Skill remain available.

## Install or upgrade

Quit the running Paperang Studio, including tray mode. On an Apple Silicon Mac running macOS 14 or later, drag the new `Paperang Studio.app` into Applications and choose Replace. The application retains bundle ID `local.paperang.lab`. The package uses the project's existing ad-hoc signing method. Keep the previous app if you need to roll back.

The runtime version is `0.3.3-macclient`, the bundle version is 3301 and the distribution tag name is `v0.3.3-macclient`. This is a Mac-only patch on the 0.3.x compatibility baseline. Relay API v2, Mac Local API v2, schema 1 and P2 Bluetooth transport remain compatible. No server migration is required.

## Validation scope

The arm64 release build and app signature, 47 offline local API requests, and byte-level raster import/export checks passed. Final PNGs from the three QR module styles were decoded successfully. The workspace upgrade has no new physical-print or in-place GUI migration acceptance record.
