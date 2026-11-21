# 暂停维护声明

截至 v0.3.3，MacClient、AndroidClient、WinClient 和 LinuxRelayServer 为当前发布主线。以下组件已暂停维护或支持，恢复时间尚未公布。

## 已暂停维护

| 组件 | 状态 | 当前可用路径 |
|---|---|---|
| WinLite | 已暂停维护。旧 WinForms 前端、构建方法和本地版本仅供归档。 | [WinClient](WINDOWS.zh-CN.md) 提供 Windows USB／蓝牙打印、中转接收和纸带终端。 |
| ChatGPT MCP / GPT Actions | 支持已暂停。当前发行不提供对应的在线打印入口。 | 用[主线客户端](FEATURE-MATRIX.zh-CN.md)打印，或用[自托管 Relay](REMOTE-PRINTING.zh-CN.md)接收网页投稿。 |
| RelaySkill | 支持已暂停。旧版记录见 [v0.3.2](RELEASE-v0.3.2.md)。 | 使用 [Relay API v2](API-v2-032.md) 配合 [MacClient](USER-GUIDE.zh-CN.md) 或 [WinClient](WINDOWS.zh-CN.md) 接收。 |
| LocalBLESkill | 支持已暂停。 | 使用 [MacClient](USER-GUIDE.zh-CN.md) 或 [AndroidClient](../Android/README.md) 通过蓝牙连接。 |

## 已退役接口

Relay HTTP API v1 与 Mac 本机 HTTP API v1 已退役，旧路由返回 HTTP 410。迁移请参阅 [Relay API v2](API-v2-032.md) 和[本机 API v2](API.zh-CN.md)。

## 其他状态

- [本地位图生成 Skill](../Skills/paperang-local-raster/SKILL.md) 仍可生成供手动导入的 raster1。
- LinuxClient 尚在规划中。LinuxRelayServer 是当前可部署的 Linux 服务端。
- [macOS 临时 Relay](MACOS-TEMPORARY.zh-CN.md) 用于本地开发。持久部署使用 [LinuxRelayServer](../Server/README.md)。
- USB 研究、ESP32 模拟器和第三方实现保留在研究索引。当前 Windows USB 打印请使用 WinClient。

[当前发行与下载](RELEASE-v0.3.3.md) · [WinLite 历史指南](WINLITE-LEGACY.zh-CN.md) · [版本登记](../VERSIONS.json)
