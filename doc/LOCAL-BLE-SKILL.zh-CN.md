# LocalBLESkill 本地蓝牙打印 · v0.3.2-localbleskill

[English](LOCAL-BLE-SKILL.md) · [技能与命令](../Skills/paperang-local-ble/SKILL.md) · [P2 协议](PROTOCOL.zh-CN.md)

此 macOS 命令行工具通过 CoreBluetooth 直连使用传统协议的 Paperang P2。需要 Xcode Command Line Tools 和本地仓库中的 ProtocolCore、PrintCore。保留仓库中的技能目录，并在 Agent 的本地技能目录创建符号链接。可用 `scripts/paperang --version` 核对已安装版本。

- **传输**使用 FF00 服务、FF01 帧通知、FF02 无响应写入及 FF03 流控额度和包长。每次设备操作按扫描得到的 UUID 建立连接，注册 CRC 会话，核对 P2 型号，结束后断开。
- **读数**包括型号、纸仓、电量估计和浓度。查询只接受协议目录中已知的只读命令。仅收到 ACK 时，字段值仍为未知。
- **排版**固定 576 点宽，每行 72 字节，最高位在前，1 为黑。默认内容宽 552 点，两侧各 12 点，最高 1400 行。PNG 预览和打印点阵使用同一次渲染结果。按标称 300 DPI 估算，点阵区长度为行数 × 25.4 ÷ 300 毫米，走纸另计。
- **打印**发送前读取纸仓并回读浓度。点阵按整行分帧并遵循蓝牙流控。`sent` 表示发送完成，超时后先核对纸面再决定是否重试。

版本遵循 0.3.2 兼容基线。独立编译和离线文本渲染已验证，真实蓝牙握手、设备读数与出纸待实机验证。
