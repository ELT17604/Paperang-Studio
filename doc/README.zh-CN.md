# Paperang Studio

[English](../README.md) · [下载](https://github.com/ELT17604/Paperang-Studio/releases) · [文档目录](INDEX.zh-CN.md)

Paperang Studio 是面向 **Paperang P2 喵喵机**的桌面与移动打印工具。可在客户端本地排版打印，也可部署中转服务，从浏览器向桌面接收端投递纸条和绘图。

## 功能

| 平台 | 发行版 | 功能 |
|---|---|---|
| macOS | [MacClient](USER-GUIDE.zh-CN.md) | 蓝牙打印、图文编辑、图层排版、二维码、纸带终端和中转接收 |
| Android | [AndroidClient](../Android/README.md) | 蓝牙打印、触屏排版、全屏手绘、毫米尺标、素材库和可编辑的打印历史 |
| Windows | [WinLite](WINDOWS.zh-CN.md) | USB 优先及蓝牙回退、托盘接收、模板和中转管理 |
| Ubuntu | [LinuxRelayServer](../Server/README.md) | 网页投递、打印队列、权限和管理 |

## 界面预览

| macOS 工作台 | Windows 模板编辑器 | Android 手绘 |
|---|---|---|
| ![MacClient 工作台](assets/macclient-v0.3.1-fix3-workspace.png) | ![WinLite 模板编辑器](../assets/screenshots/winlite-v0.3.0-template-editor.jpg) | <img src="../assets/screenshots/androidclient-v0.3.3-drawing.png" width="180" alt="AndroidClient 手绘"> |

[更多截图](../assets/screenshots/README.md)

## 快速开始

1. 从 [Releases](https://github.com/ELT17604/Paperang-Studio/releases) 下载桌面客户端，或[构建 Android APK](../Android/README.md)。
2. 在客户端连接 Paperang P2。MacClient 和 AndroidClient 使用蓝牙，WinLite 支持 USB 和蓝牙。
3. 核对预览并设置浓度和送纸后打印。需要浏览器投递时，[安装中转服务](../Server/README.md)并配置桌面接收端。

## 文档

- [功能矩阵](FEATURE-MATRIX.zh-CN.md)和[版本规则](VERSIONING.zh-CN.md)
- [中转与本机 HTTP API](API-v2-032.md)、[蓝牙协议](PROTOCOL.zh-CN.md)和 [USB 协议](USB-PROTOCOL.zh-CN.md)
- [构建与架构](ARCHITECTURE.zh-CN.md)、[参与开发](CONTRIBUTING.zh-CN.md)和[变更记录](CHANGELOG.zh-CN.md)

## 许可

[CC BY-NC-SA 4.0](../LICENSE) · [声明与署名](../NOTICE)

© 2026 ELT Group
