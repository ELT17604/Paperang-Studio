# Paperang Studio

[简体中文](doc/README.zh-CN.md) · [Releases](https://github.com/ELT17604/Paperang-Studio/releases) · [Documentation](doc/README.md)

Paperang Studio is a desktop and mobile printing toolkit for the **Paperang P2**. Compose and print locally, or use a self-hosted relay to send notes and drawings from a browser to a desktop receiver.

## Features

| Platform | Distribution | Features |
|---|---|---|
| macOS | [MacClient](doc/USER-GUIDE.md) | BLE printing, text and image editing, layered layouts, QR codes, paper terminal and Relay reception |
| Android | [AndroidClient](Android/README.md) | BLE printing, touch layout, full-screen drawing, rulers, reusable assets and editable print history |
| Windows | [WinLite](Windows/README.md) | USB-first printing with BLE fallback, tray reception, templates and Relay administration |
| Ubuntu | [LinuxRelayServer](Server/README.md) | Web submissions, print queue, access controls and administration |

## Screenshots

| macOS workspace | Windows template editor | Android drawing |
|---|---|---|
| ![MacClient workspace](doc/assets/macclient-v0.3.1-fix3-workspace.png) | ![WinLite template editor](assets/screenshots/winlite-v0.3.0-template-editor.jpg) | <img src="assets/screenshots/androidclient-v0.3.3-drawing.png" width="180" alt="AndroidClient drawing"> |

[More screenshots](assets/screenshots/README.md)

## Getting started

1. Download a desktop package from [Releases](https://github.com/ELT17604/Paperang-Studio/releases), or [build the Android APK](Android/README.md).
2. Connect your Paperang P2 in the client. MacClient and AndroidClient use Bluetooth. WinLite supports USB and Bluetooth.
3. Preview the output, set density and feed, then print. For browser submissions, [install the relay](Server/README.md) and configure a desktop receiver.

## Documentation

- [Feature matrix](doc/FEATURE-MATRIX.md) and [versioning](doc/VERSIONING.md)
- [Relay and local HTTP APIs](doc/API-v2-032.md), [BLE protocol](doc/PROTOCOL.md) and [USB protocol](doc/USB-PROTOCOL.md)
- [Build and architecture](doc/ARCHITECTURE.md), [contributing](CONTRIBUTING.md) and [changelog](CHANGELOG.md)

## License

[CC BY-NC-SA 4.0](LICENSE) · [Notices and attribution](NOTICE)

© 2026 ELT Group
