# Paperang Studio for Windows

[User guide](../doc/WINDOWS.md) · [简体中文](../doc/WINDOWS.zh-CN.md) · [Implementation and validation](../doc/WINCLIENT.md)

WinClient is the current Windows desktop application: an editing-first workspace, live Bluetooth discovery, USB/BLE printing, templates, Relay reception/management, local API and paper terminal.

Requires Windows 10 2004+ or Windows 11. Build with .NET 10 SDK; packaging also requires Inno Setup 6. Published applications contain their required runtimes.

```powershell
# Run from the repository root
./Windows/scripts/build-client.ps1
./Windows/scripts/build-client-installer.ps1
```

The scripts first run isolated desktop checks, then publish a production application without test fixtures and smoke-test its WinUI resources. Output defaults to the sibling `PaperangStudio-Windows-Development` directory. Set `PAPERANG_WIN_WORK_DIR` to change it; do not publish over a running portable client.

| Source | Responsibility |
|---|---|
| `src/Paperang.WinClient` | WinUI desktop pages and client lifecycle |
| `src/Paperang.Windows` | Windows BLE/USB, rendering, protected storage and ConPTY |
| `src/Paperang.Core` | Protocol, settings, layouts, queue ownership and local HTTP |
| `../tests/windows/WinClientChecks` | Desktop fixtures, excluded from production builds |
| `../tests/windows/Paperang.Tests` | Shared protocol/storage/Relay regression suite |

WinLite feature development is paused. Its source/builds remain for compatibility and historical reproduction: [legacy build instructions](LEGACY.md), [legacy guide](../doc/WINLITE-LEGACY.md). WinClient does not reference the WinLite executable project.
