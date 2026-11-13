# 功能矩阵 · 0.3.x

[English](FEATURE-MATRIX.md) · [版本规则](VERSIONING.zh-CN.md) · [版本登记](../VERSIONS.json)

`0.3.x` 兼容基线采用 Relay HTTP API v2、Mac 本机 HTTP API v2，以及各端所支持的数据格式 schema 1。当前源码包含比 [GitHub 已发布的 v0.3.2](https://github.com/ELT17604/Paperang-Studio/releases/tag/v0.3.2) 更新的客户端修订。

## 发行版

| 发行版 | 当前版本 | 指南 |
|---|---|---|
| MacClient | `0.3.3-macclient` | [macOS 指南](USER-GUIDE.zh-CN.md) |
| WinLite | `0.3.3-winlite` | [Windows 指南](WINDOWS.zh-CN.md) |
| AndroidClient | `0.3.3-androidclient` | [Android 指南](../Android/README.md) |
| LinuxRelayServer | `0.3.2-linuxrelayserver` | [服务端部署](../Server/README.md) |

## 当前已实现功能

表中「—」表示该发行版未提供对应功能。

| 功能 | MacClient | WinLite | AndroidClient | LinuxRelayServer |
|---|---|---|---|---|
| P2 直连 | BLE | USB 优先，BLE 回退 | BLE | — |
| 本地图文工作台 | 文字、图片、富文本图层及二维码 | 区块模板渲染 | 文字、图片、富文本图层、二维码及全屏手绘 | — |
| 位图文件与预览 | RAW/raster1 导入，PNG/RAW 导出 | 接收 Relay raster1，模板预览 | RAW/raster1 导入，PNG/RAW/raster1 导出 | raster1 输入与网页预览 |
| 打印与送纸 | 浓度、份数、送纸 | 浓度、送纸、模板输出 | 浓度、份数、送纸及自动截断尾部空白 | — |
| 历史与复用 | 本地任务与 Relay 历史 | Relay 历史及导出 | 本地点阵历史、恢复可编辑任务及素材库 | 持久化任务历史及管理导出 |
| Relay HTTP API v2 | 接收端与所有者管理 | 接收端与所有者管理 | — | 网页投递、队列、权限与管理 |
| 本机 HTTP API v2 | 环回地址 API | — | — | — |
| 后台工作流 | 托盘接收与纸带终端 | 托盘接收与 Windows 登录自启 | — | systemd 服务 |

中转角色、任务归属与回报恢复遵循 [API v2 契约](API-v2-032.md)。[本地位图生成 Skill](../Skills/paperang-local-raster/SKILL.md) 生成供手动导入的文件。当前源码暂停 ChatGPT MCP/GPT Actions、RelaySkill 和 LocalBLESkill。[v0.3.2 发行记录](RELEASE-v0.3.2.md)收录历史 RelaySkill 发行版。
