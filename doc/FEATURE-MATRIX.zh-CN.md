# 版本功能实现矩阵（Version Feature Implementation Matrix）

[English](FEATURE-MATRIX.md) · [版本规则](VERSIONING.zh-CN.md) · [版本登记表](../VERSIONS.json)

核对日期 **2026-11-08**，共同基线 **0.2.3**。这是跨端对齐和发布检查表，不表示每个发行版已拥有全部功能。同基线共同接口必须兼容；完整客户端目标、托盘版主动裁剪和平台差异分别列出，每次发布都需更新。

## 发行版登记

| 发行版 | 规范版本 | 实际运行时/版本文件 | 定位 |
| --- | --- | --- | --- |
| MacClient / macclient | v0.2.3-macclient-fix1 | VERSION：0.2.3-fix1，历史别名 | 完整原生 macOS 客户端 |
| LinuxRelayServer / linuxrelayserver | v0.2.3-linuxrelayserver | Server/relay.py：0.2.3，历史别名 | 中转、数据库、投稿网页，不直接连接打印机 |
| WinLite / winlite | v0.2.3-winlite-fix1 | Windows/VERSION | Windows 托盘收件与模板编辑 |
| RelaySkill / relayskill | v0.2.3-relayskill | Skills/paperang-relay-print/VERSION | 独立中转投稿/渲染/回执技能，不连接蓝牙 |
| WinClient / winclient | 未发布 | 规划中，无实现 | 未来完整 Windows 客户端 |
| LinuxClient / linuxclient | 未发布 | 规划中，无实现 | 未来本地 Linux 客户端 |

**I 已实现**（验证范围见文末）；**P 计划中/未实现**；**O 定位主动不提供**；**— 不适用**。I 不表示所有适配器/固件均完成实机验证。没有证据不会把“未实现”称为“技术上无法实现”。完整客户端 P 是对齐目标，不是承诺发布日期。

## 共同基线与互通要求

以下适用项是接收中转任务的客户端/服务器的必需契约，未通过不能声称同基线互通。

| 功能/契约 | MacClient | LinuxRelayServer | WinLite | WinClient | LinuxClient | RelaySkill |
| --- | --- | --- | --- | --- | --- | --- |
| 中转 HTTP v1、Bearer 角色隔离 | I | I 提供端 | I | P | P | I 投稿端 |
| text/raster1 schema 1 能力协商 | I | I | I | P | P | I |
| 打印前取得全文、校验尺寸/SHA-256 | I | I 校验/存储 | I | P | P | I 提交前校验 |
| queued/claimed/printing/sent/unknown/rejected 状态 | I | I 权威状态 | I | P | P | I 投稿/回执 |
| 数据发送与人工纸面 confirmed 分离 | I 含确认界面 | I 确认接口 | I 仅 sent，无确认界面 | P | P | I 不冒充纸面确认 |
| 不确定/失败发送不自动重打 | I | I 超时转 unknown | I | P | P | I 不自动重发 |
| 分包进度仅代表传输进度 | I | I 解析/存储/网页 | I | P | P | O 仅回执状态 |
| 模板 JSON v1、十种区块、单遍变量 | I | — 不渲染模板 | I | P | P | O 接收端控制 |
| 图片/说明合成及 1400 行上限 | I | I 输入限制 | I | P | P | I 点阵输入 |
| 传统 P2 576 点/72 字节每行/MSB-first/1 黑 | I | I 点阵输入 | I | P | P | I 渲染/打包 |
| BLE FF03 流控、key-zero CRC、纸仓/浓度检查 | I CoreBluetooth | — | I Windows GATT | P | P | — |

## 客户端功能与对齐目标

| 功能 | MacClient | LinuxRelayServer | WinLite | WinClient | LinuxClient | RelaySkill | 差异原因/要求 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 文字图片工作台、旋转/裁切/抖动 | I | — | O | P | P | O | 完整客户端对齐项；WinLite 明确不提供主工作台 |
| 区块模板编辑、预设、JSON 导入导出 | I | — | I | P | P | O | 同一 schema，系统字体和折行可不同 |
| 完整收件箱/历史/拒绝/人工确认界面 | I | — | O | P | P | O | WinLite 自动收取排队任务，服务器接口仍存在 |
| 托盘接收、暂停/自动耗纸授权 | I | — | I | P | P | — | Mac 手动进入托盘；WinLite 启动即托盘 |
| 重启后保留连接凭证 | O 仅内存 | I 密钥哈希 | I DPAPI | P | P | I 外部密钥路径/环境变量 | 安全存储机制不等价；Mac 增加明确安全存储前不能声称一致 |
| 模板内容/当前样式选择持久化 | I | — | I fix1 自动保存 | P | P | O 参数按次提供 | Mac 编辑器明确保存，WinLite 另自动保存打印选项 |
| 本地未回报结果持久恢复 | O 当前会话 | I SQLite 状态 | I 加密记录 | P | P | I 独占提交/回执记录 | Windows 恢复只回报 unknown，不重打旧认领 |
| 60 秒冷却后循环重连 | O 五次后停止 | — | I 默认开启可关闭 | P | P | O 不重发 POST | 重连策略差异，不改变中转协议 |
| 登录自启选项 | O | I systemd 部署 | I HKCU Run | P | P | O 按请求运行 | 服务器系统服务和桌面登录自启生命周期不同 |
| 本机鉴权 HTTP API/SDK 集成 | I | — 中转 API 不同 | O | P | P | — | 现有 Clients/Python/JS 对应 Mac 本机 API，不是 WinLite |
| 交互式实体纸带终端 | I zsh PTY | — | O | P | P | — | Windows shell/PTY 需要平台实现；WinLite 不运行 shell |
| 字段/协议目录/受控工程工具 | I | — | O 仅打印前置读取 | P | P | — | 未知/固件命令写入不作为功能一致目标 |
| 原生管理员密钥管理 | I | I 管理接口/CLI | O | P | P | O 投稿密钥足够 | WinLite 收件只需设备密钥 |

## 服务端独有职责

LinuxRelayServer 已实现投稿网页/绘图、匿名/鉴权配额、多角色密钥/吊销、收据/短编号访问控制、SQLite 持久化/保留期以及 systemd/临时部署。客户端消费这些服务，不需要各自复制一套。服务端模板渲染、USB 打印、IPP/MCP、实时充电和 LED 控制**不是当前已实现的共同基线功能**；研究引用中出现不代表已支持。

## 证据与验证边界

| 范围 | 源码/记录 | 验证情况 |
| --- | --- | --- |
| Mac 协议/打印 | [Bluetooth.swift](../Sources/PaperangLab/Bluetooth.swift)、[Workspace.swift](../Sources/PaperangLab/Workspace.swift)、[协议记录](PROTOCOL.zh-CN.md) | 原文档历史 P2 实测；此次 Windows 更新未重新运行 macOS |
| Mac 模板/托盘 | [RelayLayout.swift](../Sources/PaperangLab/RelayLayout.swift)、[TrayMode.swift](../Sources/PaperangLab/TrayMode.swift) | 源码对照，结合 Windows 测试比较结构与语义 |
| 中转契约 | [relay.py](../Server/relay.py)、[富媒体](RICH-MEDIA.zh-CN.md) | Windows 模拟传输连接原真实 HTTP/SQLite 服务；未改服务器部署 |
| Windows 协议/模板/队列 | [Core](../Windows/src/Paperang.Core)、[WinLite](../Windows/src/Paperang.WinLite)、[tests](../Windows/tests) | 回归、DPAPI 重新读取/原子写入、重连/取消/并发、1200 次离线压力检查 |
| Windows 实际 P2 | [移植记录](WINDOWS-PORTING.zh-CN.md) | 2026-11-06 用户确认中文纸面完整、150 行送纸和两次重连；不推广到所有适配器 |
| Windows 界面/自启/资源 | [Windows 说明](WINDOWS.zh-CN.md) | 实际界面检查、带引号的后台启动/单实例检查及短时资源采样；未实际重启 Windows，不保证无限期稳定 |

新完整客户端必须把 P 补成有证据的 I，或先记录并接受带原因的 O 例外，才可声称达到功能一致。真正的平台技术限制需有复现证据、用户影响和替代方案；“尚未开发”不能当作“无法实现”。

## RelaySkill 独立发行版

[使用说明](RELAY-SKILL.zh-CN.md) · [源码](../Skills/paperang-relay-print/SKILL.md)。这是投稿者，不是接收客户端：无蓝牙/托盘/本机 API；字体/边距/抖动为发送前渲染参数，浓度/送纸/接收模板依然由客户端控制。12 项本地检查和原 HTTP/SQLite 联调验证回执与不重复提交，未声称复制完整客户端功能。
