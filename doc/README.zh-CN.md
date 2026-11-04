<p align="center"><img src="../assets/AppIcon.png" width="160" alt="Paperang Studio logo"></p>

# Paperang Studio

[English](../README.md) · [中文文档目录](INDEX.zh-CN.md)

原生 macOS 喵喵机 P2 打印工作台、协议调试器、实体纸带终端与本机 HTTP API。SwiftUI + CoreBluetooth，无第三方运行库。当前版本 **0.2.3-fix1**，需要 macOS 14+、Swift 6 工具链（Xcode 16+）。项目为非官方工具。

- 文字与图片共用最终黑白位图预览；旋转、适配/裁切、阈值和抖动、浓度、份数、送纸及传输参数可调，支持预设。
- 设备列表默认仅显示名称匹配 Paperang / Miaomiaoji / 喵喵机的候选；“展示全部蓝牙设备”恢复完整扫描列表。名称筛选不是设备身份认证，连接后仍校验协议及型号。
- 工程界面提供字段查询、命令及证据目录、原始数据和**最新在前**的日志；导出日志保持时间正序，便于重放分析。
- **纸带终端**运行真实 controlling PTY 和交互式 zsh；Ctrl-C/Tab/方向键交给终端驱动。输出可自动或手动分批打印。
- **本地 API**供本机程序或授权网页使用，含离线渲染、设备管理、只读查询、打印任务、送纸、日志和协议目录，附 Python/JavaScript 客户端与 OpenAPI。

- **远程打印**：Ubuntu轻量中转网页、多鉴权密钥、匿名字数与每小时IP额度、本地收件箱及四种版式。[安装说明](REMOTE-PRINTING.zh-CN.md)。

## 快速开始

```sh
./scripts/build.sh
open '../PaperangStudio-Development/Release/Paperang Studio.app'
```

构建不自动连接或耗纸。打开 App → 打印机 → 扫描 → 选择 P2 → 连接。首次需允许蓝牙。工作台中预览内容、开启侧栏全局允许耗纸后再打印。

默认浓度100、固定576点/72字节一行、批末送150行。576列两端及150行送纸已由用户实物确认。300 DPI是硬件标称值，不能靠软件改成其他物理DPI。适配范围以本台传统协议P2为准。

## 文档导航

| 文档 | 内容 |
|---|---|
| [文档目录](INDEX.zh-CN.md) | 全部资料索引 |
| [用户指南](USER-GUIDE.zh-CN.md) | 工作台、参数、预设、图片和工程入口 |
| [纸带终端](TERMINAL.zh-CN.md) | 真正PTY、控制键、缓冲与打印机制 |
| [HTTP API](API.zh-CN.md) / [OpenAPI](openapi.json) | 完整接口、参数、错误与调用示例 |
| [Swift 模块 API](SWIFT-API.zh-CN.md) | 协议、图像、终端模块复用 |
| [协议规范](PROTOCOL.zh-CN.md) | BLE、握手、帧、流控、命令及设备字段 |
| [获取与验证步骤](ACQUISITION.zh-CN.md) | 从失败路径到实物确认的过程及复现方法 |
| [研究对照](RESEARCH.zh-CN.md) | 跨型号、工厂模式、盖纸对照、宽度实验 |
| [ESP32 模拟器补充](ESP32-EMULATOR.zh-CN.md) | 新命令、SPP关键字、模拟器局限 |
| [充电与LED](CHARGING-AND-LED.zh-CN.md) | 已读字段及尚未确认内容 |
| [架构与构建](ARCHITECTURE.zh-CN.md) | 源码结构、输出位置、发布与验证边界 |
| [来源与致谢](SOURCES.zh-CN.md) | 仓库及固定版本 |
| [变更记录](CHANGELOG.zh-CN.md) | 版本变化 |

## 目录

`Sources/`为完整Swift/C源码；`Configurations/`为可导入预设；`Clients/`为HTTP客户端；`assets/`为Logo、PNG、macOS ICNS与兼容ICO；`scripts/`为构建和图标源；`doc/`集中全部文档。

构建缓存、最终App与开发资料默认位于同级 **PaperangStudio-Development/**。本机历史目录、下载仓库、实验代码、原始会话、测试及验证产物均存放在那里，不进入发布源码目录。无需这些历史资料也能独立构建本项目。可用 `PAPERANG_WORK_DIR` 指定其他输出目录。


© 2026 ELT Group · [vox@calx.cc](mailto:vox@calx.cc)

采用 [CC BY-NC-SA 4.0](../LICENSE)：署名ELT Group、非商业使用、修改标注及同方式共享。用于个人研究学习，禁止商用。非商业限制意味着本项目不是OSI定义的开源许可项目；可公开源码与共享研究成果。完整版权及独立项目英文声明见 [NOTICE](../NOTICE)。

Paperang Studio is an independent, unofficial project and is not affiliated with, endorsed by, sponsored by, or a subsidiary product of Paperang, its parent company, or any related companies or affiliates. All third-party names and trademarks belong to their respective owners. Intended for personal research and learning; commercial use is prohibited.

引用与历史快照的许可归各来源，本目录未附带第三方源码。

[三个相关项目的可借鉴内容与限制](RELATED-PROJECTS.zh-CN.md)

v0.2.3 新增[菜单栏托盘模式](TRAY-MODE.zh-CN.md)，可保留蓝牙和网络监听并自动处理网络打印。
