# 三个相关项目的再次核对

2026-11-04读取用户提供的仓库/PyPI页面，并对照本地源码。它们有可参考的工程能力，但没有新增可信的本台P2 LED或充电bool证据。

## paperang-hacs

来源：[README](https://github.com/mdj2812/paperang-hacs)、[设备锁](https://github.com/mdj2812/paperang-hacs/blob/95211c336ef8646eb9a0a50c2574a184ab3bfa66/custom_components/paperang/core/blocking.py)、[状态采集](https://github.com/mdj2812/paperang-hacs/blob/95211c336ef8646eb9a0a50c2574a184ab3bfa66/custom_components/paperang/core/state.py)。固定commit `95211c336ef8646eb9a0a50c2574a184ab3bfa66`，README版本v1.5.2。

- 7个服务：文字、图片、QR、取件码、自检、状态、送纸；6种遥测实体含电量、状态、电压、温度、浓度、连接。
- 设备级锁将轮询与打印串行化，静态信息和动态传感器分开；本App采用共享busy锁与带采样时间readings，避免状态查询插入图像发送。
- 名称失败时用SDP FEE7 UUID补找已配对/被改名设备；这是经典蓝牙Linux bluetoothctl路径。FEE7并非Paperang独占，macOS BLE不能直接照搬为身份认证。当前默认名称筛选并保留“展示全部”处理改名设备。
- 其传感器存在不证明本P2固件会返回电压/温度；本机只有ACK时继续显示未取得。
- 可在后期通过本App API适配HA自动化；本轮没有安装HA集成或新增自动耗纸规则。

## paperang-p2-lib

来源：[PyPI 1.2.1](https://pypi.org/project/paperang-p2-lib/)、[源码](https://github.com/mdj2812/paperang-p2-lib)。页面描述USB+**经典SPP**，不能直接当CoreBluetooth BLE库使用。

值得借鉴Transport/Protocol/Printer分层、JSON图像profiles、中文字体回退、90度标签、二维码与取件码主机渲染。576点、72字节一行、1008字节分包与本机结果一致。

必须保留差异：默认CRC seed35769521，本App完成会话注册后seed0；它的packetRemain倒计数不能替代本机BLE已验证递增index。`_send_get`用cmd+1关联，无法覆盖本机08→0B例外；`set_heat_density`用u16LE，本机BLE验证为u8。48 commands含响应/事件编号，不表示48个实测功能。

电压函数仅在响应至少2字节时返回u16LE毫伏，否则None；温度需有数据。工厂模式只是发送封装，没有合法mode/退出实测。库版本1.2.1与历史CLI依赖导入失败路径已记录，未重新安装或运行它。

## paperang-p1-connect

来源：[README](https://github.com/Orangray-MDJX/paperang-p1-connect)、[传统枚举](https://github.com/Orangray-MDJX/paperang-p1-connect/blob/9ec2cb535ec8ac09a7dc1ce2464bc2a114d569bf/paperang_p1/protocol.py)、[A5文档](https://github.com/Orangray-MDJX/paperang-p1-connect/blob/main/docs/protocol-a5.md)。本地源码固定commit `9ec2cb535ec8ac09a7dc1ce2464bc2a114d569bf`，本轮重新读取当前README。

最有价值的是持久任务队列、缺纸held、发送中断unknown、不自动重打、取消边界，以及HTTP API/IPP/MCP分层。也有BLE触碰唤醒再RFCOMM连接的设备侧经验。传统30→31已帮助本机取得P2短名。

它的主要实机为P1 A5 01.03.18、384列/48字节，与本P2传统576列不同；A5开始/结束会话、纸宽查询、缺纸命令、两米长图与唤醒时序均不能直接移植。本App当前不声称支持IPP、MCP、跨重启队列或深睡唤醒；可在后续基于已稳定的HTTP任务API增补。

## 当前采纳与后续顺序

本次采纳：分层Swift模块、协议证据目录、带采样时间的状态、共享设备锁、任务ID/取消/无自动重试、JSON参数、HTTP接口。建议后续先做二维码/标签主机渲染与持久任务状态，再做PDF/分带长图，最后评估IPP/MCP。所有扩展仍统一为576列最终位图，不为高级功能先写工厂模式或固件。
