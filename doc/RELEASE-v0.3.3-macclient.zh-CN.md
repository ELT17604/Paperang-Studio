# Paperang Studio v0.3.3-macclient

> **历史版本记录。** 当前下载见 [v0.3.3](RELEASE-v0.3.3.md)，使用与部署请参阅 [MacClient](USER-GUIDE.zh-CN.md)、[WinClient](WINDOWS.zh-CN.md)、[AndroidClient](../Android/README.md) 和 [LinuxRelayServer](../Server/README.md)。

当前源码修订为 **0.3.3-macclient-opt2**，bundle build **3303**。默认四边 0px、576 点满幅、与 Android 对齐的传输时序、批后送纸 160 行。此修订尚待 macOS 原生构建与运行验收，不表示已发布新的 GitHub 二进制包。[English](RELEASE-v0.3.3-macclient.md)

打印工作台新增可搜索的系统字体、自定义二维码和自由排版。文字框支持对选中的单字或片段设置样式。工作台可导入既有 .raw 或带校验的 raster1 JSON，也可让模型借助[本地位图 Skill](../Skills/paperang-local-raster/SKILL.md)生成文件，再手动预览和打印。[工作台使用说明](MACCLIENT-WORKSPACE.zh-CN.md)

从本代开始暂停支持 ChatGPT MCP／GPT Actions、RelaySkill 和 LocalBLESkill。当前源码树已移除这些组件的实现、文档和专用自动化。桌面中转接收和新增的本地位图生成 Skill 仍可使用。

## 安装和升级

退出正在运行的 Paperang Studio，包括托盘模式。在 macOS 14 或更高版本的 Apple Silicon Mac 上，把新版 `Paperang Studio.app` 拖入「应用程序」，选择「替换」。应用继续使用 `local.paperang.lab` bundle ID 和项目现有的临时签名方式。需要回退时可用原有应用替换。

运行时版本为 `0.3.3-macclient`，构建号为 3301，独立发行标签名为 `v0.3.3-macclient`。兼容基线为 0.3.x，沿用 Relay API v2、本机 API v2、schema 1 和 P2 蓝牙传输。升级 Mac 无需迁移服务器。

[暂停维护声明与当前替代方案](PAUSED-BRANCHES.zh-CN.md)
