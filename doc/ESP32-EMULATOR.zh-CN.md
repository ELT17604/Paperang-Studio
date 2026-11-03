# ESP32 Paperang Emulator 源码补充

[English](ESP32-EMULATOR.md) · [中文文档目录](INDEX.zh-CN.md)

核对日期2026-11-04，来源[lxydiy/ESP32-Paperang-Emulator](https://github.com/lxydiy/ESP32-Paperang-Emulator)，固定commit `c38fbebb72bd9ef46a9d5b12b5afa9934ad88356`。检查了`Program/Printer/Paperang.ino`、CRC封装、Printer.ino与Python备份，未编译运行第三方程序。它是模拟设备端，不是真实P2固件；README也说明版本不完善。

## 新的扩展目录

以下编号均为**十六进制**，来源为[Paperang.ino](https://github.com/lxydiy/ESP32-Paperang-Emulator/blob/c38fbebb72bd9ef46a9d5b12b5afa9934ad88356/Program/Printer/Paperang.ino)。已放入App工程命令目录，类别“研究”，不加入只读自动发送白名单。

| 请求→响应 | 模拟器实际行为 | 对真实P2的结论 |
|---|---|---|
| 30→31 | 固定返回P1两字节 | 本机此前已实测返回P2，增加独立来源印证 |
| 39 | 只定义CMD_39，没有switch处理 | 不能推定功能或只读性质 |
| 40→41 | 试图返回1字节00 | 指针定义可疑；语义未知，不能称充电状态 |
| 42→43 | 固定ASCII BK3432 | 模块/芯片标识候选，未在本P2读取 |
| 7F→80 | 固定12字节，ASCII v3.38.19后零填充 | 扩展版本候选，未验证所属组件 |
| 81→82 | 固定16字节，ASCII HME230_P2后零填充 | 扩展硬件标识候选，未验证本机支持 |

`CMD_42`是十六进制42=十进制66，**不是十进制42=0x2A纸类型**。这些名称看似内部厂商查询，但源码没有真实设备抓包或字段单位证据。模拟器固定电量90、SN、型号P1、国家CN；不是实时传感器读取。

## 通信线索/搜索关键字

- `BluetoothSerial` / `esp_spp_api.h` / `SPP` / `RFCOMM` / `SDP`：经典蓝牙串口，和CoreBluetooth GATT不是同一传输。共享02/03业务帧不意味着共用FF03流控。
- `esp_bt_gap_set_cod` / `esp_bt_cod_t`：模拟器设置蓝牙Class of Device，major=6、minor=0b100000、service=0b00000100000。这可能影响旧客户端发现/分类，不是P2协议写命令，不能拿来修改真实打印机。
- `SET_CRC_KEY` / `0x35769521` / `paperang_send_ack`：印证传统握手/同命令00确认结构，但其seed内部表示不应直接照抄。
- `CMD_40 CMD_41`、`CMD_42 CMD_43 BK3432`、`CMD_7F CMD_80 v3.38.19`、`CMD_81 CMD_82 HME230_P2`：下一轮官方App抓包可重点观察的扩展字段。
- `STB` / `SPI` / `goFront` / `MOTOR_TIME`：DIY打印头和电机控制，属于ESP32硬件侧，不是可从P2 BLE发送的LED/工厂控制。

## 不照搬的实现细节

1. 接收解析跳过CRC字段，没有验证收到的数据CRC；本App继续严格验证。
2. `SET_CRC_KEY`位移表达式缺少清晰括号，且CRC库内部种子表示不同；不能替代本机已经成功的key0握手向量。
3. `GET_SN`分支缺break，会额外落入自动关机返回；多一帧不是新SN字段。
4. `CMD_40_DATA`声明为空地址指针，却按1字节指针传入发送函数；不能据此断言00是真实有效测量值。
5. 模拟器按48字节/行的384点打印头运行；FEED_LINE触发startPrint并固定额外走200步，不是本机P2 u16LE行数的语义证据。
6. SET_HEAT_DENSITY读取1字节，另一P2库发送2字节存在冲突；本机原生BLE已验证1字节浓度100，保持实测路径。
7. 未实现GET_STATUS/电压/温度/工厂状态的真实传感器逻辑；default也会ACK。模拟器ACK不能用于证明真实P2接受某命令。

## 后续获取步骤

在官方App操作时记录完整SPP/BLE原始帧、请求时间和用户动作，查找上述扩展；先证明它是GET及响应结构，再单独加入实验查询。若能做SPP抓包，可对照BLE模块的GATT信息和42/7F/81是否重复；仍保留空数据、仅ACK、CRC失败、无应答四类差异。没有这些证据前不盲扫未知命令，不从LED闪烁推测40的含义。
