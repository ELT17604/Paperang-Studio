# Windows 移植记录

> v0.3.0：本文原有截图/观测和旧版行为保留为历史；当前中转鉴权、历史权限、依赖、版本及验证状态以 [AUTH v2](AUTH-v2.zh-CN.md)、[升级记录](UPGRADE-v0.3.0.zh-CN.md) 与[矩阵](FEATURE-MATRIX.md)为准。本机 API 已在 0.3.2 中迁移 v2，BLE/模板记录独立有效。

[English](WINDOWS-PORTING.md) · [Windows 使用说明](WINDOWS.zh-CN.md)

Windows 移植位于 `Windows/`，固定审阅 macOS revision：`b1788b07aacb8c0cff7f86a884e58fb4ad8afb32`，原版本 0.2.3-fix1。保留原 macOS、服务端与客户端结构。

## 原工程功能与本版对应

| 原模块 | 逻辑 | Windows 对应 |
| --- | --- | --- |
| ProtocolCore/Protocol.swift | 传统 02/03 帧、zlib CRC、状态位、命令证据 | Core/Protocol.cs、P2Session.cs；只移植收条所需命令 |
| PaperangLab/Bluetooth.swift | FF00 服务、双通知、额度、握手、型号/电量/纸仓 | WinLite/BlePrinter.cs、Core/P2Session.cs |
| PrintCore/Renderer.swift、Settings.swift | 576 点、72 字节/行、1 黑 0 白、1400 行上限 | NoteRenderer.cs、Raster；中文字体适配 Windows |
| Workspace.swift | 纸仓检查、浓度回读、1008 字节分包、递增序号、150 行送纸 | Core/P2Session.cs |
| Relay.swift、RelayHealth.swift | 鉴权、2 秒轮询、详情、认领、printing/sent/unknown、5 次重连 | RelayClient.cs、RelayEngine.cs |
| RelayLayout.swift | v1 模板、十种区块、四预设、单遍变量、图文合成、图形编辑 | Templates.cs、TemplateEditor.cs、NoteRenderer.cs |
| TrayMode.swift | 隐藏主窗口、托盘授权、暂停、后台打印 | ApplicationContext + NotifyIcon，无主窗口 |
| App.swift、About.swift | SwiftUI 工作台/设置/工程页与署名 | 只保留设置、模板编辑、关于与托盘 |
| RelayView.swift、RelayKeys.swift | 完整任务管理、人工确认、管理员密钥管理 | 本版未移植；设备密钥验证由原服务端执行 |
| LocalAPI.swift、APIRoutes.swift、IntegrationCore/HTTP.swift、Clients/ | 本机鉴权 API 和 SDK | 本版未开启本地 API |
| TerminalCore、PTYBridge、PaperangPTYHost、PaperTTY.swift | 真 PTY、交互输入、终端批次打印 | 本版未运行 shell |
| Server/relay.py、run.py、keygen.py、web/ | HTTP+SQLite、投稿、绘图、配额、权限、状态与回执 | 保持原协议，使用原服务端端到端测试 |

额外实现：Windows 当前用户 DPAPI、原子落盘、认领结果恢复、HKCU Run 自启选项、单实例和记住的 BLE 地址。Windows 身份不能复用 Mac 的 CoreBluetooth UUID，因此按扫描选择的 48 位蓝牙地址重连。

## 外部项目与官方资料

阅读了以下来源，并结合原仓库实测记录做取舍：

- [paperang-p2-lib](https://github.com/mdj2812/paperang-p2-lib)：确认 576 点和 1008 字节图像负载，但其经典 SPP 不能替换原版 BLE。默认 CRC seed、浓度载荷宽度和分包序号存在差异，本版采用原 Swift 已验证路径。
- [paperang-p1-connect](https://github.com/Orangray-MDJX/paperang-p1-connect)：参考持久任务、未知结果不自动重放、单设备通信和连接超时设计；未套用 P1 的 384 点/A5 协议。
- [Windows GATT WriteValueWithResultAsync](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattcharacteristic.writevaluewithresultasync)：Windows 原生 GATT 写入与结果接口。发送时分别检查 P2 FF03 额度和 Windows GATT 状态，以 `MaxPduSize - 3` 限制分片大小。
- [Windows RFCOMM](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/send-or-receive-files-with-rfcomm)：作为传输路径对照，本版没有混用 RFCOMM 与 FF03 BLE 额度协议。

外部项目只作为研究参考，未打包其运行时或源码。原版图标与许可证保留。

## 实际联调发现

原服务器拒绝 `Transfer-Encoding: chunked`，需要唯一 Content-Length。Windows 请求改为先序列化 UTF-8 字节后发送，以兼容原 Handler。使用原 Python Server 验证长文字全文、raster1 图片获取、任务认领、状态与最终 sent 的数据库状态。发送进度按原 `第1/1份 · 分包N/M` 形式回报，兼容现有网页解析。

字体映射造成的字形和折行差异属于平台差异；模板结构、字段含义、边距/留白、图片采样和黑白字节格式保持一致。UI 与模拟通信验证不能代替实际 Windows 蓝牙适配器和 P2 纸面验证。

## Windows 实机蓝牙修复（2026-11-06）

复现了扫描可见但无法连接：`GetGattServicesForUuidAsync(FF00, Uncached)` 返回 Success、0 个服务，而同一会话的 `GetGattServicesAsync(Uncached)` 能返回 5 个服务，包含 FF00。改为完整枚举后按 UUID 选择 FF00，并释放其余服务，避免误判为设备不支持。

实机验证了 FF01/FF03 订阅、初始额度 `01 02`、分片长度 `02 B6 00`、key-zero 握手、P2 型号、电量 100%、纸仓、浓度写入与回读、中文点阵发送及 150 行送纸。用户确认测试纸正常出纸且内容完整；随后连续两次断开重连均完成握手。蓝牙连接错误现在保留具体失败信息以供设置和托盘查看。

开发者可在关闭托盘客户端后运行 `dotnet run --project Windows/tests/Paperang.Tests -c Release -- --ble` 检查实际设备及两次重连；追加 `--print` 会消耗测试纸。工具使用当前用户已保存的打印机地址，不输出服务器密钥。正常客户端不记录通知或点阵原始日志。
