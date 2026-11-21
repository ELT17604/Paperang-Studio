<p align="center">
  <img src="assets/AppIcon.png" alt="Paperang Studio icon" width="112" height="112">
</p>

# Paperang Studio

[简体中文](doc/README.zh-CN.md) · [Releases](https://github.com/ELT17604/Paperang-Studio/releases) · [Online Wiki](https://paperang-studio-wiki.dependableladybird1.chatgpt.site/en/) · [Repository docs](doc/README.md)

Paperang Studio is a desktop and mobile printing toolkit for the **Paperang P2**. Compose and print locally, or use a self-hosted relay to send notes and drawings from a browser to a desktop receiver.

Latest unified release: [v0.3.3](doc/RELEASE-v0.3.3.md) — MacClient, AndroidClient, WinClient and Relay. Component runtime versions and validation limits are listed in the release notes.

## Features

| Platform | Distribution | Features |
|---|---|---|
| macOS | [MacClient](doc/USER-GUIDE.md) | BLE printing, text and image editing, layered layouts, QR codes, paper terminal and Relay reception |
| Android | [AndroidClient](Android/README.md) | BLE printing, touch layout, full-screen drawing, rulers, reusable assets and editable print history |
| Windows | [WinClient](doc/WINDOWS.md); [WinLite](doc/WINLITE-LEGACY.md) (paused) | Local print workspace, USB-first printing with BLE fallback, tray reception, templates and Relay administration |
| Ubuntu | [LinuxRelayServer](Server/README.md) | Web submissions, print queue, access controls and administration |

## Screenshots

| macOS workspace | Windows workspace | Android drawing |
|---|---|---|
| ![MacClient workspace](doc/assets/macclient-v0.3.1-fix3-workspace.png) | ![WinClient workspace](assets/screenshots/winclient-v0.3.3-workspace.png) | <img src="assets/screenshots/androidclient-v0.3.3-drawing.png" width="180" alt="AndroidClient drawing"> |

[More screenshots](assets/screenshots/README.md)

## Getting started

| Platform | Install | Connect |
|---|---|---|
| macOS 14+ (Apple Silicon) | Download MacClient from [Releases](https://github.com/ELT17604/Paperang-Studio/releases), then move the app to Applications | Scan for the P2 on the Printer page |
| Android 13+ | [Build and install the APK](Android/README.md) | Grant Nearby Devices access on the Printer tab, then scan for the P2 |
| Windows 10 2004+ / 11 | [WinClient installer and portable build](doc/WINCLIENT.md) | Open the desktop workspace, configure the printer, then connect over USB or Bluetooth |
| Ubuntu | [Install LinuxRelayServer](Server/README.md) | Configure the Relay URL and receiver credentials in MacClient or WinClient |

After connecting, preview the output, set density and feed, then print.

## Remote printing

The browser submits notes and drawings to a self-hosted Relay. MacClient or a Windows client claims each job, applies the local template and paper settings, and sends it to the P2.

[Deployment and receiver setup](doc/REMOTE-PRINTING.md) · [Authentication and permissions](doc/AUTH-v2.md)

## Build from source

| Component | Toolchain | Build |
|---|---|---|
| MacClient | Swift 6, macOS SDK | `./scripts/build.sh` |
| AndroidClient | JDK 21, Android SDK 36 | `./Android/build.sh` |
| WinClient | .NET 10 SDK | `./Windows/scripts/build-client.ps1` |
| WinLite (legacy) | .NET 10 SDK | `./Windows/scripts/build.ps1` |
| LinuxRelayServer | Python 3.10+ | [Install and run](Server/README.md) |

[Source layout](doc/ARCHITECTURE.md) · [Local tests](tests/README.md)

## Documentation

- [Feature matrix](doc/FEATURE-MATRIX.md) and [versioning](doc/VERSIONING.md)
- [Relay and local HTTP APIs](doc/API-v2-032.md), [BLE protocol](doc/PROTOCOL.md) and [USB protocol](doc/USB-PROTOCOL.md)
- [Build and architecture](doc/ARCHITECTURE.md), [contributing](CONTRIBUTING.md) and [changelog](CHANGELOG.md)

## License

[CC BY-NC-SA 4.0](LICENSE) · [Notices and attribution](NOTICE)

© 2026 ELT Group
