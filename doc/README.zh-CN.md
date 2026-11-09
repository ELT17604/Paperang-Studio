<p align="center"><img src="../assets/AppIcon.png" width="112" alt="Paperang Studio 标志"></p>

# Paperang Studio

<p align="center"><strong>原生客户端、共享中转，让信息落在真实的纸上。</strong><br>macOS · Windows · Linux Relay · Web · RelaySkill · ChatGPT 集成</p>
<p align="center"><a href="../README.md">English</a> · <a href="README.zh-CN.md">简体中文</a> · <a href="../VERSIONS.json">v0.3.1 共同基线</a> · <a href="FEATURE-MATRIX.zh-CN.md">功能矩阵</a></p>

Paperang Studio 是面向 **Paperang P2 喵喵机** 的多平台打印项目。电脑上的原生客户端通过蓝牙连接打印机；Linux 中转服务器接受网页和 RelaySkill 投递，再由接收端完成排版与打印。各端共享内容格式、模板规范和权限合同。

需要完整工作台和纸带终端时，使用 **MacClient**；希望电脑长期安静收纸条时，使用 **WinLite Windows 托盘客户端**。服务器与投稿工具可独立部署，通过统一接口协作。

[选择平台](#平台与分发) · [Windows 实际界面](#windows-lite-实际界面) · [快速开始](#快速开始) · [文档入口](#文档入口)

## 平台与分发

| 分发 | 职责 | 入口 | v0.3.1 验证状态 |
|---|---|---|---|
| **MacClient · macOS** | SwiftUI/CoreBluetooth 完整工作台、图片排版、模板、纸带终端、本地 API 与中转接收 | [使用指南](USER-GUIDE.zh-CN.md) · [构建](ARCHITECTURE.zh-CN.md) | 原生构建、界面及本地 API v2 联调通过 |
| **WinLite · Windows** | 原生 WinForms/BLE 托盘接收、自动打印、模板恢复、自启、C 管理与历史导出 | [Windows 指南](WINDOWS.zh-CN.md) · [构建](../Windows/README.md) | Windows 构建、回归及本机中转集成已验证 |
| **LinuxRelayServer · Linux** | HTTP/SQLite 中转、投稿网页、身份与权限、队列及管理 API | [服务器部署](../Server/README.md) | 本机 HTTP/SQLite 测试通过，Linux 部署验证待完成 |
| **ChatGPT 集成 · 实验性** | Relay API v2 的 MCP 发送端适配与传统 GPT Actions 兼容接口 | [集成指南](CHATGPT.zh-CN.md) · [源码](../Integrations/ChatGPT/) | 仅源码；网关、ChatGPT 连接和实机验证待完成 |
| **RelaySkill · 投稿工具** | Agent 打印技能：文字/图片渲染、参数调整、明确投递及按权限查询状态 | [技能指南](RELAY-SKILL.zh-CN.md) · [源码](../Skills/paperang-relay-print/SKILL.md) | 本地协议与渲染测试通过 |

完整 **WinClient**、**LinuxClient** 仍在规划中。当前 Windows 接收端是 WinLite；LinuxRelayServer 是服务器，不是 Linux 蓝牙客户端。各端范围与证据见[功能实现矩阵](FEATURE-MATRIX.zh-CN.md)。

## 各端如何协作

```mermaid
flowchart LR
    Browser["网页 · 文字与绘图"] -->|HTTPS| Relay["LinuxRelayServer"]
    Skill["RelaySkill · 文字与图片"] -->|API v2| Relay
    Relay <-->|"客户端向外轮询 · 分级凭证"| Receiver["MacClient 或 WinLite"]
    Receiver -->|Bluetooth LE| P2["Paperang P2 · 实体纸条"]
```

**同一台打印机只使用一个活动蓝牙接收端。** 中转负责身份、队列与投递；电脑客户端负责最终模板、打印浓度、送纸和蓝牙输出。MacClient 也可以脱离中转进行本地打印。

## MacClient 升级

[0.3.1-macclient-fix3 升级与验证](MACCLIENT-v0.3.1.zh-CN.md)：所有者私钥接收、分页 C 管理、配置保存与当前中转及 WinLite 合同对齐。

<img src="assets/macclient-v0.3.1-fix3-workspace.png" width="776" alt="Mac 工作台离线示例">
<img src="assets/macclient-v0.3.1-fix3-management.png" width="776" alt="Mac 管理中心离线示例">

截图为离线示例，不含真实用户数据；本次未连接打印机。

## Windows Lite 实际界面

**像搭积木一样编辑纸条。** 与 Mac 共用 v1 模板 JSON，支持区块排序、文字/点阵混排、字体、留白和实时黑白预览。

<p align="center"><img src="../assets/screenshots/winlite-v0.3.0-template-editor.jpg" width="776" alt="WinLite 原生图形模板编辑器：左侧区块、中间属性、右侧文字和点阵混排预览"></p>

**设置好，留在托盘中。** 在原生窗口中连接中转与 P2、选择模板和打印参数，并从设置进入 C 权限管理面板。

<table>
<tr><th>服务器与打印机连接</th><th>模板与打印参数</th></tr>
<tr>
<td><img src="../assets/screenshots/winlite-v0.3.0-connection.jpg" width="380" alt="WinLite 连接页，含示例服务器地址、M 凭证输入和打印机扫描入口"></td>
<td><img src="../assets/screenshots/winlite-v0.3.0-print-settings.jpg" width="380" alt="WinLite 打印设置页，含模板选择、浓度和批后送纸参数"></td>
</tr>
</table>

<sub>以上为 v0.3.0 WinLite 实际窗口，使用示例配置与公开演示内容。[截图说明](../assets/screenshots/README.md)。MacClient 截图与验证见上方。</sub>

## 主要功能

- **从其他地方投递纸条：**通过网页或 RelaySkill 发送文字/绘图，由已配置的桌面接收端处理。
- **自定义纸面：**MacClient 与 WinLite 共享区块模板、预设、文字/图片组合及 576 点输出格式；不同系统的字体效果可能不同。
- **安静驻留 Windows：**启动进入托盘，DPAPI 保存凭证，恢复当前模板，支持登录自启，并以有间隔的批次持续重连。
- **在原生客户端中管理：**A 投稿密钥只投递；匿名浏览器可查自己任务的进度；B 可查本身份状态；C 管理身份、凭证、会话、公钥和完整历史；M 独立执行打印。C 可按用户筛选并导出含点阵内容的 JSONL。
- **使用完整 Mac 工作台：**图片变换与抖动、交互式 zsh 纸带终端、协议读数，以及本地 HTTP API 和 Python/JavaScript SDK。
- **区分发送和纸面结果：**sent 仅表示传输完成，confirmed 需要人工核对；中断或结果不确定的任务不自动重印。

## 快速开始

### Windows · WinLite

需要 Windows 10 2004+ / Windows 11 和 BLE 适配器；轻量包运行需要 .NET 10 Desktop Runtime。从源码构建需 .NET 10 SDK：

```powershell
.\Windows\scripts\build.ps1
```

在固定目录运行 PaperangStudio.exe，从托盘打开设置，填写 v0.3.1 中转地址和**已配对的所有者私钥文件**，扫描并选择 P2，点击“保存并连接”。配置后默认自动连接和允许自动打印，均可修改；“设置→管理”提供 C 管理面板。[完整说明](WINDOWS.zh-CN.md)。

### macOS · MacClient

需要 macOS 14+、Swift 6 / Xcode 16+：

```sh
./scripts/build.sh
open '../PaperangStudio-Development/Release/Paperang Studio.app'
```

在打印机页扫描连接，预览内容并开启全局耗纸开关后打印；中转接收使用 M，完整管理使用 C。[使用指南](USER-GUIDE.zh-CN.md) · [原生验证接手要求](HANDOFF-v0.3.0.zh-CN.md)。

### Linux · 中转服务器

在支持的 Ubuntu/systemd 环境中，先阅读安装器和[升级指南](UPGRADE-v0.3.0.zh-CN.md)，再运行：

```sh
sudo bash Server/install.sh
```

安装器使用独立 Python venv，依赖 argon2-cffi 和 cryptography。为网页配置 HTTPS，再连接桌面接收端。[部署、凭证和临时运行](../Server/README.md)。

### Agent · RelaySkill

将 Skills/paperang-relay-print/ 安装到 Agent 的 skills 目录，配置 **A/B 随机密钥**，调用 `$paperang-relay-print` 并说明文字/图片及排版参数。投稿工具不借用打印接收端的 M 凭证。[安装和参数](RELAY-SKILL.zh-CN.md)。

> **从 v0.2.x 升级：**中转 API v2 必须协调升级；旧中转 /v1 和 receipt 正文能力已关闭，**Mac 本地 API 仍为 v1**。部署前阅读[升级与回滚](UPGRADE-v0.3.0.zh-CN.md)、[鉴权合同](AUTH-v2.zh-CN.md)和[验证记录](VALIDATION-v0.3.0.md)。源码已提供不代表各平台已经部署或通过原生验证。

## 文档入口

[全部中文文档](INDEX.zh-CN.md) · [English index](README.md)

| 主题 | 资料 |
|---|---|
| 平台与版本 | [版本规则](VERSIONING.zh-CN.md) · [功能矩阵](FEATURE-MATRIX.zh-CN.md) · [登记表](../VERSIONS.json) |
| Windows | [托盘、设置与管理](WINDOWS.zh-CN.md) · [构建](../Windows/README.md) |
| macOS | [工作台](USER-GUIDE.zh-CN.md) · [模板编辑](LAYOUT-EDITOR.zh-CN.md) · [纸带终端](TERMINAL.zh-CN.md) · [托盘模式](TRAY-MODE.zh-CN.md) |
| 中转与安全 | [部署](../Server/README.md) · [身份/API v2](AUTH-v2.zh-CN.md) · [数据保留](RELAY-DATA.zh-CN.md) |
| 集成 | [ChatGPT 实验性集成](CHATGPT.zh-CN.md) · [RelaySkill](RELAY-SKILL.zh-CN.md) · [Mac 本地 API](API.zh-CN.md) / [OpenAPI](openapi.json) · [Swift 模块](SWIFT-API.zh-CN.md) |
| 升级与维护 | [更新日志](CHANGELOG.zh-CN.md) · [升级步骤](UPGRADE-v0.3.0.zh-CN.md) · [Mac/Linux Agent Prompt](HANDOFF-v0.3.0.zh-CN.md) · [验证记录](VALIDATION-v0.3.0.md) |
| 打印机研究 | [协议](PROTOCOL.zh-CN.md) · [实物记录](ACQUISITION.zh-CN.md) · [研究](RESEARCH.zh-CN.md) · [相关项目](RELATED-PROJECTS.zh-CN.md) · [来源](SOURCES.zh-CN.md) |

## 仓库结构

| 目录 | 内容 |
|---|---|
| Sources/ | 原生 Swift/C MacClient 与可复用模块 |
| Windows/ | 原生 WinLite、模板/管理 UI、构建和测试 |
| Server/ | 中转 API v2、投稿网页、SQLite 身份/队列与 Linux 部署 |
| Skills/ | 独立版本的 RelaySkill |
| Clients/ | 面向 Mac 本地 API 的 Python/JavaScript SDK |
| Integrations/ChatGPT/ | 实验性 MCP/GPT Actions 适配器，不作为正式发行版本 |
| Configurations/ | 可导入打印预设 |
| assets/ | 原始品牌资源与实际应用截图 |
| doc/ | 使用指南、接口合同、升级记录和研究 |

构建产物留在源码树外：Mac 默认 PaperangStudio-Development/，Windows 默认 PaperangStudio-Windows-Development/；可分别通过 PAPERANG_WORK_DIR、PAPERANG_WIN_WORK_DIR 调整。凭证、数据库和私人打印内容不进入 Git。

## 许可与联系

© 2026 ELT Group · [vox@calx.cc](mailto:vox@calx.cc)

采用 [CC BY-NC-SA 4.0](../LICENSE)：署名 ELT Group、非商业使用、修改标注及同方式共享。用于个人研究学习，禁止商用。非商业限制意味着本项目不属于 OSI 定义的开源许可。完整声明见 [NOTICE](../NOTICE)。

Paperang Studio is an independent, unofficial project. It is not affiliated with, endorsed by, sponsored by, or a subsidiary product of Paperang, its parent company, or any related companies or affiliates. All third-party names and trademarks belong to their respective owners. Referenced materials retain their own licenses.

Windows v0.3.1-winlite-fix3：[标准安装、手动更新和卸载](WINDOWS-RELEASE-ARTIFACTS.zh-CN.md)。
