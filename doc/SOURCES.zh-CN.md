# 来源与致谢

[English](SOURCES.md) · [中文文档目录](INDEX.zh-CN.md)

公开项目用于理解互操作协议与对照模型；下载快照存于发布目录外，未把第三方源码并入本项目。许可归各来源。固定commit见[source-revisions.json](source-revisions.json)，新取证方法见[获取步骤](ACQUISITION.zh-CN.md)。

| 来源 | 本项目使用的依据 |
|---|---|
| [tinyprinter/python-paperang](https://github.com/tinyprinter/python-paperang) | 传统02/03、CRC注册、旧App反编译笔记 |
| [createskyblue/paperang-miaomiaoji-tool-gen2](https://github.com/createskyblue/paperang-miaomiaoji-tool-gen2) | P2传统串口、1008字节图像和送纸线索 |
| [wyrtensi/paperang-cli](https://github.com/wyrtensi/paperang-cli) | 初始CLI路径及A5失败对照 |
| [hurui200320/java-paperang-p2-usb](https://github.com/hurui200320/java-paperang-p2-usb) | 576点/72字节、USB图像格式 |
| [mdj2812/paperang-p2-lib](https://github.com/mdj2812/paperang-p2-lib) / [PyPI](https://pypi.org/project/paperang-p2-lib/) | 模型、命令目录、主机渲染、字段解析 |
| [mdj2812/paperang-hacs](https://github.com/mdj2812/paperang-hacs) | 服务化、设备锁、遥测与发现fallback |
| [Orangray-MDJX/paperang-p1-connect](https://github.com/Orangray-MDJX/paperang-p1-connect) | 30→31、传统/A5分层、队列/API/IPP参考 |
| [burndown/paperang-c2](https://github.com/burndown/paperang-c2) | 新协议/动态参数的跨型号比较，不套用本P2 |
| [Yrr0r/paperang-web](https://github.com/Yrr0r/paperang-web) | P1另一BLE通道及主机排版参考 |
| [lxydiy/ESP32-Paperang-Emulator](https://github.com/lxydiy/ESP32-Paperang-Emulator) | 设备端模拟器、扩展40/42/7F/81与SPP/CoD线索 |
| [Paperang厂商FAQ](https://www.paperang.com/en/pc/custom-service) | P2标称300DPI、热敏单色与一般灯光说明 |

当前发布源码采用独立Swift/C实现及原始图标设计；不分发厂商App/固件、不抓取私人网络、不修改系统蓝牙配置。读取更多资料不等于那些型号已经在本机验证。对照方法和引用边界见[研究记录](RESEARCH.zh-CN.md)、[三项目对照](RELATED-PROJECTS.zh-CN.md)、[ESP32补充](ESP32-EMULATOR.zh-CN.md)。
