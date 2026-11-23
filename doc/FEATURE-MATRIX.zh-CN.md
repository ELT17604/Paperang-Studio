# 功能矩阵 · 0.3.x

[English](FEATURE-MATRIX.md) · [版本规则](VERSIONING.zh-CN.md) · [版本登记](../VERSIONS.json)

[v0.3.3 统一发布](RELEASE-v0.3.3.md) 包含 MacClient、WinClient、AndroidClient 与 Relay。Web 以静态源码发行，使用同一 `0.3.x` 兼容基线。各端实现的接口和文件格式见下表。

## 发行版

| 发行版 | 当前版本 | 指南 |
|---|---|---|
| MacClient | `0.3.3-macclient-opt2` | [macOS 指南](USER-GUIDE.zh-CN.md) |
| WinClient（当前） | `0.3.3-winclient-dev5` | [Windows 指南](WINDOWS.zh-CN.md) |
| AndroidClient | `0.3.3-androidclient-opt2` | [Android 指南](../Android/README.md) |
| Web | `0.3.3-web` | [网页直连与部署](WEB.zh-CN.md) |
| LinuxRelayServer | `0.3.2-linuxrelayserver-opt1` | [服务端部署](../Server/README.md) |

## 当前已实现功能

表中「—」表示该发行版未提供对应功能。

| 功能 | MacClient | WinClient | AndroidClient | LinuxRelayServer | Web |
|---|---|---|---|---| --- |
| P2 直连 | BLE | USB 优先/BLE 回退或仅 BLE | BLE | — | Web Bluetooth GATT |
| 本地工作台 | 图文、富文本图层、二维码 | 图文、可编辑图层、二维码、实时扫描 | 图文、富文本图层、二维码、触屏绘图 | — | 图文、图层、手绘 |
| 文件与预设 | RAW/raster1、PNG/RAW、schema 1 预设 | RAW/raster1、PNG/RAW/JSON、Mac/Android 预设 | RAW/raster1、PNG/RAW/JSON、schema 1 预设 | Raster1 与网页预览 | raster1 JSON、原生预设导入 |
| 打印控制 | 浓度、份数、送纸 | 浓度、份数、送纸、取消 | 浓度、份数、送纸、截断尾部空白 | — | 四边距、尾部裁白、浓度、份数、送纸、传输节奏 |
| 历史复用 | 本地与中转历史 | 可选本机素材/历史与中转管理 | 可编辑历史/素材 | 持久历史/导出 | — |
| 中转 API v2 | 接收与所有者管理 | 接收与所有者管理 | — | 队列/权限/管理 | — |
| 本机 API v2 | 环回 API | 主要操作，平台差异单独记录 | — | — | — |
| 后台/终端 | 托盘接收/纸带终端 | 托盘接收/[纸带终端（ConPTY）](TERMINAL.zh-CN.md)/登录自启 | — | systemd | — |

中转角色、任务归属与回报恢复遵循 [API v2 契约](API-v2-032.md)。[本地位图生成 Skill](../Skills/paperang-local-raster/SKILL.md) 生成供手动导入的文件。当前源码暂停 ChatGPT MCP/GPT Actions、RelaySkill 和 LocalBLESkill。[v0.3.2 发行记录](RELEASE-v0.3.2.md)收录历史 RelaySkill 发行版。

其他功能与历史状态见[文档目录](INDEX.zh-CN.md)和[暂停维护与历史分支](PAUSED-BRANCHES.zh-CN.md)。

[暂停维护声明与当前替代方案](PAUSED-BRANCHES.zh-CN.md)
