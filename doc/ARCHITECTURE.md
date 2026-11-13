# Architecture, build, and packaging

AndroidClient lives in [Android/](../Android/README.md). Native Android Views render 576-dot print content and send it over the shared legacy P2 BLE protocol. Editable drafts, print rasters and saved illustrations stay in app-private storage.

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

The build script packages the app, PTY helper, OpenAPI specification, icon, license and notices. It applies an ad-hoc signature. The bundle identifier is `local.paperang.lab`.

## Logo

The original dark teal/cyan printer, warm paper, and terminal prompt combine physical printing and programmability. `assets/logo.svg` is editable; `AppIcon.png` is 1024 pixels; ICNS is the native macOS icon; ICO is an additional cross-platform asset.

Regenerate PNG with `swift scripts/make-icon.swift assets`, specifying an external module cache as needed, then run `python3 scripts/make-icons.py`. Intermediate icon sizes go to the external development directory. No image-generation API or external font is needed.

## Maintenance

Maintain API.md, OpenAPI, clients, and route behavior together. New protocol commands must identify provenance and evidence. Source archives contain only this project, without private identifiers, tokens, caches, or downloaded repositories. The project uses CC BY-NC-SA 4.0, requiring attribution, non-commercial use, and ShareAlike. See [NOTICE](../NOTICE) and [contribution guide](../CONTRIBUTING.md).

## Windows USB transport

WinLite0.3.3 adds Core/PreferredPrinter.cs for connection-boundary transport choice, Core/UsbP2Session.cs for serialized USB protocol, and WinLite/UsbPrinter.cs for present SetupAPI discovery/cancellable USBPRINT streams. The existing IPrinter/RelayEngine journal contract and BLE session remain. Python diagnostics remain independent. [Release](RELEASE-v0.3.3-winlite.md) · [Protocol](USB-PROTOCOL.md).
