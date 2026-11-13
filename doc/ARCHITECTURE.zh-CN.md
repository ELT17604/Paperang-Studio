# 架构、构建与发布

[AndroidClient](../Android/README.md) 使用原生 Android Views 排版，经共享的 P2 BLE 协议发送 576 点位图。任务草稿、打印位图和素材保存在应用私有目录。

[English](ARCHITECTURE.md) · [中文文档目录](INDEX.zh-CN.md)

## 源码结构

```
Sources/
  ProtocolCore/       CRC/帧重组、命令目录、媒体状态
  PrintCore/          JSON参数、ImageIO/AppKit渲染、1位打包与PNG
  PTYBridge/          C openpty/posix_spawn/resize/stop
  PaperangPTYHost/    C controlling terminal + 前台进程组 + exec zsh
  TerminalCore/       非阻塞PTY读写、waitpid、文本归一化/待打印缓冲
  IntegrationCore/    HTTP解析、鉴权、候选名称匹配
  PaperangLab/        SwiftUI、CoreBluetooth、共享任务锁、HTTP路由
```

GUI的Lab由MainActor管理；BLE队列只有一个在途请求；图像发送、只读GET、送纸共用busy，异步通知仍会更新状态。所有打印来源调用同一submitRaster，锁定参数快照并检查媒体/浓度。终端用自己的串行IO队列，回调切回MainActor；HTTP连接队列负责分帧/鉴权，业务路由切回MainActor，不并发占用设备。

## 构建

macOS14+、Swift6/Xcode16+（或相应Command Line Tools），无外部Swift Package依赖。

```sh
./scripts/build.sh
```

默认输出同级`PaperangStudio-Development/build`、`Release/Paperang Studio.app`。`PAPERANG_WORK_DIR=/your/output ./scripts/build.sh`可覆盖；缓存、配置、安全目录也指定到输出路径。

脚本将主程序、PTY 助手、OpenAPI、图标及许可证打包，使用 ad-hoc 签名。Bundle ID 为 `local.paperang.lab`。

## Logo与应用图标

原创图标为深蓝绿底、青色打印机、暖白纸带和终端提示符，表达“实体输出+可编程终端”。`assets/logo.svg`为可编辑矢量版，`AppIcon.png`1024像素，`AppIcon.icns`供macOS，`AppIcon.ico`供跨平台引用（ICO不是macOS原生图标格式）。

重建PNG：给Swift指定外部module cache后运行`swift scripts/make-icon.swift assets`；再`python3 scripts/make-icons.py`打包。图标中间尺寸默认写到外部开发目录。无需图像生成API或字体依赖。

## 发布与维护

OpenAPI 定义与应用路由同步维护。发布包包含应用、许可证和署名文件。

## Windows USB 传输架构

WinLite0.3.3新增Core/PreferredPrinter.cs连接边界选择、Core/UsbP2Session.cs串行USB会话、WinLite/UsbPrinter.cs在场枚举与可取消USBPRINT流。复用IPrinter/RelayEngine持久结果机制，保留BLE会话，Python诊断仍独立。[发行](RELEASE-v0.3.3-winlite.zh-CN.md) · [协议](USB-PROTOCOL.zh-CN.md)。
