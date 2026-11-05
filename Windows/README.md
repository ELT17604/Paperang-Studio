# Paperang Studio for Windows

[English guide](../doc/WINDOWS.md) · [简体中文](../doc/WINDOWS.zh-CN.md)

**V0.2.3-win_lite** · Windows 10 2004 / Windows 11 · .NET 10 Desktop Runtime.

A native WinForms tray client for the existing Paperang relay and legacy BLE P2. Starts in the tray, with settings and the same v1 block-template model as the macOS client. Includes encrypted saved credentials, optional sign-in startup, remembered-printer reconnection, and automatic relay printing.

```powershell
dotnet build Windows/src/Paperang.WinLite -c Release
dotnet run --project Windows/tests/Paperang.Tests -c Release
./Windows/scripts/build.ps1
./Windows/scripts/build.ps1 -SelfContained
```

Run these commands from the repository root. Publish output defaults to the sibling `PaperangStudio-Windows-Development/Release/` directory; set `PAPERANG_WIN_WORK_DIR` to override it. The `lite` package shares the installed .NET runtime; `-SelfContained` produces the larger portable package. Both use the original repository icon and license notices.

Double-click the tray icon, enter a relay root URL and **device** key, scan and select your P2, then save and connect. The original relay/webpage require no changes. Closing settings keeps the client running. Use the tray menu to pause new jobs, reconnect, or quit. Automatic connection/printing are Windows-specific user-requested defaults; the macOS defaults are unchanged.

Source: `src/Paperang.Core/` and `src/Paperang.WinLite/`. Validation: `tests/`. See the [guide](../doc/WINDOWS.md) for template compatibility, privacy, recovery, limitations, and real-device verification.

© 2026 ELT Group · [CC BY-NC-SA 4.0](../LICENSE) · [NOTICE](../NOTICE). This Windows adaptation is an independent, unofficial client, not an official Paperang product.
