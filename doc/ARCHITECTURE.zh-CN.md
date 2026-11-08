# 架构、构建与发布

> v0.3.0：本文原有截图/观测和旧版行为保留为历史；当前中转鉴权、历史权限、依赖、版本及验证状态以 [AUTH v2](AUTH-v2.zh-CN.md)、[升级记录](UPGRADE-v0.3.0.zh-CN.md) 与[矩阵](FEATURE-MATRIX.md)为准。本机 API v1 和 BLE/模板记录仍独立有效。

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

默认输出同级`PaperangStudio-Development/build`、`Release/Paperang Studio.app`。`PAPERANG_WORK_DIR=/your/output ./scripts/build.sh`可覆盖；缓存、配置、安全目录也指定到输出路径。当前本机Command Line Tools的默认SwiftBuild遇到重复AIModel规格注册，脚本使用仍可工作的native构建系统；新工具链若移除该选项，应改默认系统并重新验证。

脚本编译所有目标，复制主程序、PTY helper、OpenAPI、AppIcon.icns，生成Info.plist，分别ad-hoc签名helper/App并strict验证。当前本机构建arm64，不声称已生成Universal或已测试Intel。提供源码可在Intel构建，但需独立验证。

App未公证；分发到其他电脑需由项目所有者配置Developer ID签名和notarization。CFBundleIdentifier保留local.paperang.lab以沿用本机身份；显示名Paperang Studio 0.2.3-fix1。GUI蓝牙权限由系统管理，不自动重置。

## Logo与应用图标

原创图标为深蓝绿底、青色打印机、暖白纸带和终端提示符，表达“实体输出+可编程终端”。`assets/logo.svg`为可编辑矢量版，`AppIcon.png`1024像素，`AppIcon.icns`供macOS，`AppIcon.ico`供跨平台引用（ICO不是macOS原生图标格式）。

重建PNG：给Swift指定外部module cache后运行`swift scripts/make-icon.swift assets`；再`python3 scripts/make-icons.py`打包。图标中间尺寸默认写到外部开发目录。无需图像生成API或字体依赖。

## 本地开发档案与验证

按用户要求，历史实验、测试源码、下载仓库和验证输出全部位于项目外`PaperangStudio-Development/`。`Validation/Package.swift`仅依赖当前源码库；`Validation/Sources/ProtocolChecks`覆盖帧/CRC、媒体状态、图像/预设、候选筛选、HTTP解析授权、真实PTY与前台Ctrl-C。`Validation/check-api.py`通过独立离线API进程验证真实HTTP/渲染/Python和JS客户端，不扫描蓝牙、不耗纸。它们是本机开发档案，不是发布源码的构建依赖。

历史原生工作台已获用户纸面确认（完整、方向正确、150行合适）。本轮新增纸带终端/HTTP打印复用同一位图传输路径；离线/API测试不能当作新增硬件纸面确认。没有验证独立charging、LED、工厂模式、固件长图极限或跨型号。

最新本机验证结果与日志在外部`VALIDATION.md`及`Validation/Reports/`；项目不嵌入不断变化的构建日志。最终App的签名验证只证明包一致性，不等于Apple公证或全部硬件行为通过。

## 发布与维护

项目README连接doc全部文档，OpenAPI与App路由同步维护；新增命令必须说明来源/证据及风险。发布压缩包只收录PaperangStudio目录，不夹带开发档案或令牌。本项目采用CC BY-NC-SA4.0，要求署名、禁止商用并同方式共享；本轮没有替用户向GitHub创建仓库、上传或公开任何内容。
