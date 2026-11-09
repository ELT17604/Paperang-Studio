> v0.3.1: [Owner-file authentication](../doc/OWNER-CUTOVER-v0.3.1.zh-CN.md). WinLite defaults to a paired owner private key.


> opt1: Regression sources are archived outside the shipping repository. See [validation and reproduction](../doc/OPTIMIZATION-opt1.md). Historical test paths below refer to Git revision `4393ae0`.
# Paperang Studio for Windows

> v0.3.0 uses [relay API v2](../doc/AUTH-v2.md). Settings → 管理 opens the native C panel for keys, identities, sessions, history and JSONL export. Keep the background M token separate.

[English guide](../doc/WINDOWS.md) · [简体中文](../doc/WINDOWS.zh-CN.md)

**V0.3.1-winlite-fix3** · Windows 10 2004 / Windows 11 · .NET 10 Desktop Runtime.

Uses the shared [version policy](../doc/VERSIONING.md) and [feature implementation matrix](../doc/FEATURE-MATRIX.md). Fix1 adds automatic print-preference persistence, live connection status and default continuous 60-second retry cycles, and repairs enabled sign-in startup paths after upgrades.

A native WinForms tray client for the existing Paperang relay and legacy BLE P2. Starts in the tray, with settings and the same v1 block-template model as the macOS client. Includes encrypted saved credentials, optional sign-in startup, remembered-printer reconnection, and automatic relay printing.

```powershell
dotnet build Windows/src/Paperang.WinLite -c Release
./Windows/scripts/build.ps1
./Windows/scripts/build.ps1 -SelfContained
```

Run these commands from the repository root. Publish output defaults to the sibling `PaperangStudio-Windows-Development/Release/` directory; set `PAPERANG_WIN_WORK_DIR` to override it. The `lite` package shares the installed .NET runtime; `-SelfContained` produces the larger portable package. Both use the original repository icon and license notices.

Double-click the tray icon, enter a relay root URL and select the paired **owner private file**, scan and select your P2, then save and connect. Requires the compatible v0.3.1 relay; the private key stays on this computer. Closing settings keeps the client running. Use the tray menu to pause new jobs, reconnect, or quit. Automatic connection/printing are Windows-specific user-requested defaults; the macOS defaults are unchanged.

Source: `src/Paperang.Core/` and `src/Paperang.WinLite/`. Validation: `tests/`. See the [guide](../doc/WINDOWS.md) for template compatibility, privacy, recovery, limitations, and real-device verification.

© 2026 ELT Group · [CC BY-NC-SA 4.0](../LICENSE) · [NOTICE](../NOTICE). This Windows adaptation is an independent, unofficial client, not an official Paperang product.

## 标准安装、更新与卸载

推荐 `PaperangStudio-0.3.1-winlite-fix3-win-x64-setup.exe`，自带 .NET 10，不需另外安装运行环境。按当前用户安装至 `%LOCALAPPDATA%/Programs/Paperang Studio WinLite`，在开始菜单和“已安装的应用”中可见。升级保留原有配置、模板和私钥路径。

托盘 → **检查 GitHub 更新…** 手动读取本仓库最近 100 个发行记录，只比较正式 WinLite 标签。发现新版后打开 GitHub 下载页面，不自动下载安装。没有发行记录或网络失败会明确提示。

卸载会正常退出客户端并清除登录启动项。可选择永久删除本程序数据目录（包括其中的私钥），默认保留；静默卸载保留数据。外部私钥及服务器授权不会被删除。构建：安装 Inno Setup 6，执行 `Windows/scripts/build-installer.ps1 -Dotnet PATH`。

管理面板 **用量与限额…** 支持匿名/注册默认及所选身份覆盖。0 禁止投稿，继承恢复默认；轮换不清零。需要服务器 fix1，旧服务器会明确提示不支持。

参见 [发布物说明](../doc/WINDOWS-RELEASE-ARTIFACTS.zh-CN.md) 与 [服务器/macOS 交接](../doc/HANDOFF-QUOTAS-v0.3.1.zh-CN.md)。
