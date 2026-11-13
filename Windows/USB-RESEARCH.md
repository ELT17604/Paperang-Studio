> 原生集成后续：[WinLite0.3.3发行与升级](../doc/RELEASE-v0.3.3-winlite.zh-CN.md)。下文是此前诊断阶段的历史记录。

# P2 USB 协议探索 · 2026-11-10

基线：正式仓库 `v0.3.2`，commit `46d55b7d6782d02ba0ff296e0af3c79a7f19b674`；Windows 版本 `0.3.2-winlite`。
分支：`dev/p2-usb-protocol-research`。原外层 Windows 0.2.3 文件未覆盖。

## USB 传输

实体 P2 枚举为 `USB\VID_4348&PID_5584`，描述 `MPTII Printer`，服务 `usbprint`，Microsoft `usbprint.inf`。
通过 USBPRINT 接口 GUID `{28d78fad-5a12-11d1-ae5b-0000f803a8c2}` 打开原始设备，使用 overlapped ReadFile/WriteFile。
没有安装驱动、创建打印队列或使用蓝牙。USBPRINT 抽象 Bulk 端点，无须应用直接调用 libusb。

| 查询 | 命令 / 回复 | 实机值 |
| --- | --- | --- |
| 型号 | 06 / 07 | `Paperang_P2` |
| 版本 | 04 / 05 | `09 00 01`（保留原始字节，未推断版本号） |
| 纸仓状态 | 0C / 0D | `00 00` |
| 电量 | 10 / 11 | `64` = 100% |
| 浓度 | 1C / 1D | 初始 `3C` = 60；设置后 `64` = 100 |

第一次发送一包 1008 字节点阵（576×14 点稀疏竖线），用户确认有图案但很淡。
点阵写入成功后等待 ACK 超时；未重发点阵，只补发 `1A` 送纸 150 行，收到 `00` ACK。
参考 Java 源码确认其点阵不等 ACK，工具已按此修正。
第二次用 `19` 单字节 `64` 设置浓度并成功回读，发送同一图案一次，送纸 ACK 成功；第二张深浅待用户确认。
测试结束浓度保留为 100，符合 Windows BLE 客户端默认浓度。

## 协议与差异

帧：`02 CMD REMAIN LEN16LE DATA CRC32LE 03`；CRC 为 `zlib.crc32(DATA, 0x35769521)`。
GET 载荷 `01`；响应为 command+1，可与上一条 ACK 合并或分开到达，必须保留流式解码状态并校验 CRC。
USB 不发送 BLE FF03 额度握手，也不发送会改变 CRC 的 key-zero 命令。
参考 USB 实现把第三字段解释为剩余包数；BLE 实现使用递增序号。本轮只验证 `REMAIN=0` 的单包，尚未确认多包排序、最大作业与吞吐。
参考 Java README 写浓度为 short，实际 Commands.kt 用单字节；本机已验证单字节设置/回读。
P1 常见 384 点，不能复用 P2 的 576 点渲染参数。相近型号仅作对照，不自动向未知型号发送打印。

## 使用

Windows Python 3，无第三方依赖。默认只查询，不打印，不读取中转密钥。

```powershell
python Windows/scripts/usb_probe.py
python Windows/scripts/usb_probe.py --print-test
python -m unittest discover -s tests/windows -p test_usb_probe.py -v
```

`--print-test` 验证型号、纸仓后设置浓度100并回读，打印一次短图案和送纸150行。
USB 读写有超时取消；失败不自动重放写入。终端只输出设备协议数据。
当前接口路径发现使用本机注册表，历史未插入设备的记录可能残留：打开失败需检查实际 PnP 状态。
还未集成托盘 UI、传输选择、重连与中转作业；这是一份能独立运行的实机探索工具。

## 来源

- [java-paperang-p2-usb](https://github.com/hurui200320/java-paperang-p2-usb)，审阅 `c50594c62d6fd6387d41e86b14f1fdf489d2b560`：USB ID `4348:5584`，IN `81` / OUT `02`，CRC、点阵不等ACK、单字节浓度。AGPL-3.0；只参考协议与行为，未移入其源文件。
- [paperang-p2-lib](https://github.com/mdj2812/paperang-p2-lib)，审阅 `656fc8f37ff6f7b0744ef10f8a0ae38d4d594730`：USB/PyUSB 与 SPP 两种传输，型号与协议对照。不能因旧移植文档只提 SPP 而忽略其 USB 实现。其接收函数会吞掉 USB 错误，解码不校验CRC，本工具未沿用这些行为。
- [paperang-p1-connect](https://github.com/Orangray-MDJX/paperang-p1-connect)：既有移植参考，P1 的384点/A5路径不能当作 P2 USB 实测证据。
- [Microsoft USB printer driver](https://learn.microsoft.com/en-us/windows-hardware/drivers/print/usb-printing)：usbprint.sys 提供上层驱动的通信通道。
- [Microsoft USBPRINT programming](https://learn.microsoft.com/en-us/windows-hardware/drivers/print/programming-considerations-for-usbprint)：USBPRINT 与读写接口背景。

WinLite 的原生 USB 路径见[使用指南](../doc/WINDOWS.zh-CN.md)。

## 完整协议后续

完整双语协议现见[USB中文参考](../doc/USB-PROTOCOL.zh-CN.md) / [English](../doc/USB-PROTOCOL.md)，包括17个GET、纸仓三态、59个已知/研究操作码、打印状态边界、类IOCTL及复现。新四包3→2→1→0测试已由用户确认“四段完整、顺序正确，深浅改善”。原第二张单包的单独反馈仍未取得，不以新测试追溯替代。最终独立离线测试为17项；本文上文保留最初探索过程。
