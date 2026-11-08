<p align="center"><img src="assets/AppIcon.png" width="160" alt="Paperang Studio logo"></p>

# Paperang Studio

[English](README.md) · [简体中文](doc/README.zh-CN.md)

A native macOS app for Paperang P2: text and image printing, a physical paper terminal, protocol debugging, and a local HTTP API. Built with SwiftUI and CoreBluetooth, with no third-party runtime dependencies.

**Version 0.2.3-fix1** · macOS 14+ · Swift 6 toolchain / Xcode 16+

## Features

Cross-platform releases follow the [version naming policy](doc/VERSIONING.md), [feature implementation matrix](doc/FEATURE-MATRIX.md), and [release registry](VERSIONS.json). Current Windows release: **v0.2.3-winlite-fix1**; Mac/server legacy runtime labels remain unchanged.

**Windows:** [v0.2.3-winlite-fix1](Windows/README.md) is now available as a native tray-only client for the same relay and legacy BLE P2, including compatible block-template editing, encrypted saved credentials and optional sign-in startup. See the [Windows guide](doc/WINDOWS.md) / [简体中文](doc/WINDOWS.zh-CN.md). The full macOS application remains at 0.2.3-fix1.

- **Print workspace:** a shared final monochrome preview for text and images, with rotation, fitting, cropping, dithering, density, copies, paper feed, and reusable presets.
- **Printer discovery:** Paperang-family candidates shown by default, with an option to show all nearby Bluetooth devices.
- **Engineering tools:** device readings, a protocol catalog with evidence levels, raw frames, transport settings, and newest-first logs.
- **Paper terminal:** a real macOS controlling PTY running interactive zsh, with control keys and manual or automatic batches of printed output.
- **Remote printing:** an Ubuntu relay with a submission webpage, multiple access keys, anonymous length limits, hourly per-IP quotas, task history, and a graphical print-layout editor. [Deployment guide](doc/REMOTE-PRINTING.md).
- **Local API:** device management, read-only queries, offline rendering, print jobs, paper feed, logs, and protocol metadata. Includes OpenAPI and Python/JavaScript clients.
- **One global paper switch:** enable paper use once in the sidebar for the workspace, terminal, API, and feed controls.

## Quick start

```sh
./scripts/build.sh
open '../PaperangStudio-Development/Release/Paperang Studio.app'
```

Open the **Printer** page (`打印机`), scan, choose your P2, and connect. Allow Bluetooth access when macOS asks. Preview your content, enable the sidebar's global paper switch (`全局允许耗纸`), and print.

The defaults are density **100**, a **576-dot** canvas (**72 bytes per row**), and **150 lines** of feed at the end of a batch. The app currently uses Chinese interface labels; the documentation provides English explanations.

## Documentation

[English documentation index](doc/README.md) · [中文文档目录](doc/INDEX.zh-CN.md)

| Guide | Contents |
|---|---|
| [Layout editor](doc/LAYOUT-EDITOR.md) | Task states, optional short IDs, graphical blocks and reusable templates |
| [Remote relay](doc/REMOTE-PRINTING.md) | Ubuntu installation, keys, quotas, and the Mac inbox |
| [User guide](doc/USER-GUIDE.md) | Workspace, images, presets, settings, and engineering tools |
| [Paper terminal](doc/TERMINAL.md) | PTY sessions, control keys, output buffering, and paper batches |
| [HTTP API](doc/API.md) / [OpenAPI](doc/openapi.json) | Endpoints, parameters, responses, and client examples |
| [Swift APIs](doc/SWIFT-API.md) | Reusable protocol, rendering, terminal, and integration modules |
| [Protocol reference](doc/PROTOCOL.md) | BLE transport, handshake, frames, flow control, commands, and fields |
| [Acquisition record](doc/ACQUISITION.md) | Discovery steps, physical tests, and reproducibility |
| [Protocol research](doc/RESEARCH.md) | Cross-model comparison, media states, factory commands, and width tests |
| [ESP32 emulator analysis](doc/ESP32-EMULATOR.md) | Extended command candidates and device-side implementation clues |
| [Charging and LED research](doc/CHARGING-AND-LED.md) | Recorded observations and field interpretation |
| [Related projects](doc/RELATED-PROJECTS.md) | HACS, the P2 Python library, and P1 Connect |
| [Architecture and build](doc/ARCHITECTURE.md) | Source layout, build outputs, packaging, and validation records |
| [Sources and acknowledgments](doc/SOURCES.md) | Upstream references and pinned revisions |
| [Changelog](CHANGELOG.md) | Release history |

## Project layout

- `Sources/`: complete Swift and C implementation.
- `Windows/`: native Windows tray client, compatible template editor, build scripts and tests.
- `Configurations/`: importable print presets.
- `Server/`: lightweight relay, submission website, offline key generator, and Ubuntu installer.
- `Clients/`: HTTP API client examples.
- `assets/`: original logo, PNG, macOS ICNS, and ICO assets.
- `scripts/`: build and icon generation tools.
- `doc/`: English documentation and Chinese translations.

Build caches and the packaged app go to the sibling `PaperangStudio-Development/` directory by default. Set `PAPERANG_WORK_DIR` to choose a different output location. Historical experiments, downloaded repositories, raw sessions, and validation artifacts are kept outside this source project; they are not required to build it.

## License and contact

© 2026 ELT Group · [vox@calx.cc](mailto:vox@calx.cc)

Licensed under [CC BY-NC-SA 4.0](LICENSE): credit ELT Group, use it non-commercially, identify modifications, and follow the ShareAlike terms. Intended for personal research and learning; commercial use is prohibited. See [NOTICE](NOTICE) for attribution and the full project statement.

Paperang Studio is an independent, unofficial project. It is not affiliated with, endorsed by, sponsored by, or a subsidiary product of Paperang, its parent company, or any related companies or affiliates. All third-party names and trademarks belong to their respective owners. Referenced third-party materials retain their own licenses.

Menu bar background printing: [tray mode](doc/TRAY-MODE.md) · [中文](doc/TRAY-MODE.zh-CN.md).
