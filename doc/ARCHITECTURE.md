# Architecture, build, and packaging

> v0.3.0: historical observations/screenshots below remain recorded evidence. Current relay authentication, history access, dependencies and validation status are defined by [API v2](AUTH-v2.md), [upgrade](UPGRADE-v0.3.0.zh-CN.md) and [matrix](FEATURE-MATRIX.md). Local API v2 and BLE/template records are separate contracts.

[Documentation](README.md) · [简体中文](ARCHITECTURE.zh-CN.md)

## Source layout

| Directory under Sources/ | Responsibility |
|---|---|
| ProtocolCore | CRC, stream framing, command evidence, media status |
| PrintCore | Codable settings, ImageIO/AppKit layout, monochrome packing, PNG |
| PTYBridge | C openpty, posix_spawn, resize, and stop |
| PaperangPTYHost | Controlling terminal, foreground process group, exec zsh |
| TerminalCore | Nonblocking PTY I/O, waitpid, normalized/pending text |
| IntegrationCore | HTTP parsing/authentication and candidate-name matching |
| PaperangLab | SwiftUI, CoreBluetooth, shared print lock, HTTP routes |

Lab is managed by MainActor. BLE has one in-flight request; image sends, GETs, and feed share the busy lock. Notifications can still update readings. All print sources call `submitRaster` with a settings/raster snapshot, media checks, and density verification. PTY I/O and HTTP parsing have separate serial queues; UI and business operations return to MainActor.

## Build

Requires macOS 14+ and Swift 6/Xcode 16+ or matching Command Line Tools. There are no external Swift Package dependencies.

```sh
./scripts/build.sh
```

Defaults: sibling `PaperangStudio-Development/build/` and `Release/Paperang Studio.app`. Override with `PAPERANG_WORK_DIR=/your/output ./scripts/build.sh`. Module caches and SwiftPM configuration/security paths also stay in that output location.

The recorded local toolchain failed with duplicate AIModel specifications in the default SwiftBuild system. The script uses the working `native` build-system option; that option is deprecated and should be reevaluated if a future toolchain removes it. The script builds targets, copies the executable, PTY helper, OpenAPI, ICNS, LICENSE, and NOTICE, generates Info.plist, ad-hoc signs helper/app, and performs strict signature verification. It does not run the external validation programs.

The recorded release was arm64, not Universal. Intel builds require independent verification. The app is not notarized; distribution with Developer ID and notarization requires the owner's signing setup. Bundle identifier remains `local.paperang.lab`, display name Paperang Studio, version 0.2.3-fix1. macOS manages Bluetooth permissions; the app does not reset them.

## Logo

The original dark teal/cyan printer, warm paper, and terminal prompt combine physical printing and programmability. `assets/logo.svg` is editable; `AppIcon.png` is 1024 pixels; ICNS is the native macOS icon; ICO is an additional cross-platform asset.

Regenerate PNG with `swift scripts/make-icon.swift assets`, specifying an external module cache as needed, then run `python3 scripts/make-icons.py`. Intermediate icon sizes go to the external development directory. No image-generation API or external font is needed.

## Validation and external archive

At the user's request, historical experiments, third-party checkouts, tests, and generated artifacts live outside the source project in `PaperangStudio-Development/`. They are not required for an independent build.

The external `Validation/` package checks framing/CRC, media vectors, rendering/presets, discovery filtering, HTTP authentication/parsing, real controlling PTY behavior, and foreground Ctrl-C. Its HTTP script launches an isolated offline process and exercises rendering and Python/JavaScript clients without Bluetooth or paper use.

Recorded checks passed release build/signatures, 18 HTTP/API/client cases, PTY/interrupt behavior, and byte-for-byte comparison of a historical 17-frame physical raster job with offline rendering. Earlier user feedback confirmed image direction/completeness and 150-line final feed. The final session did not add a physical TTY/API print test because the printer was off. The GUI controls/menus were inspected; a screenshot attempt returned a blank image, so no usable screenshot is claimed.

The latest local report is external `VALIDATION.md`, with `Validation/Reports/` and historical `Legacy/PaperangP2/`. Signing checks establish package integrity, not notarization or all hardware behavior. Charging flags, LEDs, factory-mode semantics, and firmware height limits remain research topics.

## Maintenance

Maintain API.md, OpenAPI, clients, and route behavior together. New protocol commands must identify provenance and evidence. Source archives contain only this project, without private identifiers, tokens, caches, or downloaded repositories. The project uses CC BY-NC-SA 4.0, requiring attribution, non-commercial use, and ShareAlike. See [NOTICE](../NOTICE) and [contribution guide](../CONTRIBUTING.md).
