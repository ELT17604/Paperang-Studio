# P2 USB 协议、遥测与实机验证

[English](USB-PROTOCOL.md) · [传统协议](PROTOCOL.zh-CN.md) · [文档目录](INDEX.zh-CN.md)

验证日期：2026-11-10，Asia/Shanghai。对象为用户的传统 `Paperang_P2`，固件原始字节 `09 00 01`，Windows x64。基线 `v0.3.2` / `46d55b7d6782d02ba0ff296e0af3c79a7f19b674`；分支 `dev/p2-usb-protocol-research`。**0.3.3后续：原生USB现已接入WinLite，默认USB优先与BLE回退，详见[发行与升级](RELEASE-v0.3.3-winlite.zh-CN.md)。** 下文2026-11-10诊断记录保留原始证据；BLE业务协议与Relay HTTP v2保持不变；隐藏固件命令仍不能声称已全部发现。

## 1. 传输、发现与 Windows 驱动

| 项目 | 本机证据 |
| --- | --- |
| USB 标识 | VID `4348`、PID `5584`；PnP 描述 `MPTII Printer` |
| 驱动 | Microsoft `usbprint.sys` / `usbprint.inf`，未安装替代驱动 |
| 设备接口 GUID | `{28D78FAD-5A12-11D1-AE5B-0000F803A8C2}` |
| 枚举 | SetupDiGetClassDevsW 的 PRESENT + DEVICEINTERFACE，枚举当前接口并筛选 P2 VID/PID；只接受唯一设备 |
| 打开/读写 | CreateFileW，GENERIC_READ/WRITE，OPEN_EXISTING，FILE_FLAG_OVERLAPPED；ReadFile/WriteFile |
| 参考 Bulk 端点 | Java 固定源码中的 interface0、OUT `02`、IN `81`；USBPRINT 已抽象端点，本轮未直接抓取端点描述符 |

USB 测试没有使用蓝牙、打印队列、GDI 打印驱动、PyUSB、libusb、Zadig 或管理员权限。须开机并使用数据线，充电线不等于数据线。初版从注册表找到接口路径；注册表可能保留拔掉设备的旧记录，现已改为 SetupAPI 枚举在场接口。多台匹配设备时拒绝自动选择。测试前应断开占用同一打印机的其他客户端；工具内部锁只串行化自身请求，不能协调独立蓝牙程序。

OVERLAPPED 读写等待有超时；超时调用 CancelIoEx，并通过 GetOverlappedResult 等待取消收尾，期间保持缓冲区与结构体存活。短写、超时写结果未知，不自动重发。取消最终完成仍依赖 Windows 驱动，不能强行释放尚被驱动使用的内存。打开/读取失败是传输错误，不等于缺纸或电量为零。

## 2. 帧与会话

```text
02 | command:u8 | remaining:u8 | payloadLength:u16LE | payload | crc32:u32LE | 03
```

外壳 10 字节，载荷至多 1023。CRC 只覆盖载荷，等价于 `zlib.crc32(payload, 0x35769521)`；反射多项式 `EDB88320`，标准初末异或。本轮 USB 全程使用默认 seed，没有发送 `18` 注册 key0，也没有 FF03 额度控制。不能直接套用 BLE 握手后的 seed0。后续已确认BLE key0影响USB，见本文末尾跨传输补充。

```text
型号查询 /01：02 06 00 01 00 01 D0 B9 59 E9 03
状态查询 /01：02 0C 00 01 00 01 D0 B9 59 E9 03
送纸150行  ：02 1A 00 02 00 96 00 3D 78 38 6A 03
```

接收按字节流处理，允许半帧、粘包、上一条 ACK 与本条数据混合；必须校验长度、结束符和 CRC。纸仓切换时捕获过截断头，误形成合法范围内的 514 字节长度。解码器现可跳到后续完整且 CRC 正确的帧，避免一直等待假长度。这是诊断恢复策略：若未完整载荷内恰好嵌入完整有效帧，存在歧义，不声称能无歧义恢复任意损坏。

## 3. 查询窗口、旧事件与结果分类

GET 的载荷为 `01`。常见成功 ACK 为“请求命令号 + 单字节 `00`”，ACK 与字段数据可先后独立到达。关闭再打开句柄后仍可能读到缓冲中的旧 ACK/状态。收到正确数据而窗口内未收到 ACK 也属于取得字段数据。`00` 点阵不等待 ACK，和 Java 参考及本机初次等待超时一致。

工具每次请求前读取旧输入，直到一次 60 ms 安静超时，排空窗口最多 500 ms，随后丢弃未完整的旧帧；记录事件与采样时间，再发送请求一次。查询最多等 1.5 秒数据，只继续读，不重发请求；文末只读型号seed识别是明确例外。MAC 请求 `08` 同时接受源码期望 `09` 和本机实际 `0B`；序列号 `0A` 也回 `0B`，必须串行关联。`0D` 同时可能是主动状态事件，没有事务 ID；排空和时间戳能减少旧读数，但无法从协议上证明每帧必由当前 GET 触发。

| 工具结果 | 含义 |
| --- | --- |
| `data` | 收到预期命令的有效非空数据；ACK 可缺失 |
| `empty_data` | 收到预期数据命令，但载荷长度为零 |
| `ack_only` | 收到成功 ACK，但在查询期限内没有字段数据 |
| `no_response` | 没有接受到 ACK/数据；可能是不支持、损坏或超时 |
| `negative_ack` | 本命令返回非零单字节结果；不把伴随数据解释为成功 |
| 抛出 I/O 错误 | Windows 传输失败，不转换为传感器数值 |

ACK-only 只描述本次查询，不证明固件永久没有该功能。ACK、数据和纸面完成是不同证据。MAC/序列号在终端、事件和证据中默认脱敏，保存长度及响应编号。

## 4. 全部已知 GET 的 USB 实测

编号均为十六进制。共执行 17 个已知 GET：14 个非空数据、1 个空数据、2 个只有 ACK。

| 字段 | 请求 → 数据响应 | USB 结果 | 含义与边界 |
| --- | --- | --- | --- |
| 固件版本 | 04 → 05 | `09 00 01` | 不擅自推导版本字节顺序 |
| 型号 | 06 → 07 | `Paperang_P2` | 打印机型号，不是 USB 模块描述 |
| 蓝牙 MAC | 08 → **0B** | 12 字节 ASCII，脱敏 | 源码期望09，本机例外已复核 |
| 序列号 | 0A → 0B | 14 字节 ASCII，脱敏 | 与 MAC 串行处理 |
| 纸仓/缺纸 | 0C → 0D | u16LE，见下一节 | 用户实际切换校准 |
| 电压 | 0E → 0F（期望） | 只有 ACK | 未取得电压，不是0V |
| 电量 | 10 → 11 | `64` | 估计100%，不代表正在充电 |
| 温度 | 12 → 13（期望） | 只有 ACK | 未取得温度，不是0°C |
| 工厂状态 | 15 → 16 | `FF` | 语义未知，未改工厂模式 |
| 浓度 | 1C → 1D | `64` |100；初次USB读到60 |
| 自动关机时间 | 1F → 20 | `08 07` |1800秒；设置/重启持久性未测 |
| 主板版本 | 23 → 24 | `V1.00.01 ` | 带尾部空格 |
| 硬件信息 | 25 → 26 | `00 00 00` | 子字段含义未知 |
| 最大间隙 | 28 → 29 | `70` | 数值112，单位未知 |
| 纸张类型 | 2A → 2B | `00` | 配置字段，不是纸张传感器；枚举未校准 |
| 国家 | 2D → 2E | 空载荷 | 空数据，与ACK-only不同 |
| 短名称 | 30 → 31 | `P2` | 型号短名称 |

[机器可读 USB 配置](p2-usb-profile.json)包含实测、完整已知操作码目录与证据等级；它是研究资料，现有客户端不会自动加载。

## 5. 纸仓开合、缺纸与恢复

由用户实际操作，只读采样，开盖/无纸期间没有打印。

| 用户确认物理状态 | 稳定 USB 载荷 | 采样 |
| --- | --- | --- |
| 开盖，保留纸卷 | `11 00` | 连续3次 |
| 合盖，取出纸卷 | `01 00` | 连续3次 |
| 装回纸卷并合盖 | `00 00` | 排空后新查询及后续多次采样 |

开合过程中还收到 `10 00`。恢复正常后首次曾读到旧的 `01/11/10`，这是接收缓冲保留事件，未将它们硬改为正常值。排空后再确认稳定状态。

```text
raw = u16LE(payload)                    # 要求载荷恰好2字节
coverOpen = (raw & 0x10) != 0
paperPresent = null if coverOpen else (raw & 0x01) == 0
unknownBits = raw & ~0x11
```

开盖时纸状态必须为未知，不能把 bit0 解释成“确定纸卷不在”。其他位不命名。历史 BLE 的 `08/19/09` 是不同时间的采样，不能把本轮 `00/11/01` 或历史 bit3 当作已确认的充电、低电、忙碌标志。

## 6. 点阵、浓度、多包与打印状态

576点宽、72字节/行，MSB-first、1黑0白、上到下。每帧按完整行打包，至多1008字节/14行；全部图像后送纸150行一次。字体、缩放、灰度和抖动在电脑端完成。

`19` 设置浓度为 **单字节** 0–100，`1C→1D` 回读。初始USB浓度60时用户反馈偏淡；设100回读成功。Java README 写short，但其 Commands.kt 实际用单字节；本机以实测为准。`1A` 送纸载荷为u16LE行数。

新测试为576×56点、四包各1008字节，剩余包数字段 **3→2→1→0**，包间插入状态查询。图案左右有边框，内部竖线每14行向右移动。用户明确确认：**四段完整、顺序正确，深浅改善**。因此单包与短四包倒计数路径均有纸面验证；这不证明255包、长图、最大吞吐或缓存深度。BLE递增index的既有实现保留独立证据。

测试前、包间、送纸后以及随后4秒监视中，状态均为 `00 00`。未识别出额外busy或机械完成事件。短测试可能漏掉短暂变化，不据此断言固件绝对没有这类字段。

| 状态 | 能保证的含义 |
| --- | --- |
| 主机准备/发送中 | 仅为本地程序阶段 |
| `sent` | 本批写入完成，最终送纸收到成功ACK |
| 中断、短写或超时 | `unknown`，不自动重打 |
| 设备 `printing` | `null`，尚未取得协议依据 |
| 机械打印完成 | `null`；正常纸仓或ACK均不能证明 |
| 纸面确认 | 用户独立检查实物后确认 |

工具在耗纸前独占创建新证据/意图文件，首包前持久记录 `unknown`，送纸ACK后才写 `sent`。不把本聊天中的纸面反馈自动上报成生产Relay的C确认；实物确认记录在本文和研究记录。

## 7. 标准 USB 打印类接口

此通道与 Bulk 传统 `0C` 不同，不能混用含义：

| IOCTL | 控制码 | 本机结果 |
| --- | --- | --- |
| GET_LPT_STATUS | `00220030` | WinError23：数据错误/循环冗余检查，没有有效状态字节 |
| GET_1284_ID | `00220034` |96字节，MODEL=`MiaoMiaoJi`，MANUFACTURER为空 |

IEEE1284返回前缀原始值为 `60 60`，不假定其长度编码正确。工具保留实际返回字节数和前缀，仅展示允许的通用标识字段，其余脱敏。普通USB型号字符串不代表IPP、ESC/POS、打印队列或内置字体。这里WinError23属于类控制请求失败，与已校验正确的传统帧CRC不同；不能将类端口状态作为本机纸仓/busy的替代源。未执行soft reset、vendor control、端口重启或协议切换。

## 8. 完整已知操作码目录与未验证项

机器配置覆盖传统 `00–31` 每个操作码，包括响应与事件，不能称为50个主机功能。除上表17个GET外，其余主机/事件定义如下：

| 操作码 | 功能 | USB证据 |
| --- | --- | --- |
| 00 | 原始点阵 | 单包、四包物理打印确认 |
| 01 | 压缩点阵 | 仅源码定义，压缩格式/流控未确认 |
| 02/03 | 固件数据/更新 | 未执行 |
| 14 | 设置工厂模式 | mode、退出、持久性未知，未执行 |
| 17 | 蓝牙状态事件 | 不是已知主机SET，未取得事件意义 |
| 18 | CRC注册 | BLE有既有证据，本轮USB未执行 |
| 19 | 设置浓度 | 单字节100及回读验证 |
| 1A | 送纸 | u16LE150，ACK及纸面确认 |
| 1B | 自检页 | 仅源码定义，未执行 |
| 1E | 设置自动关机 | 源码u16LE秒，未执行 |
| 21 | 送至打印头 | 源码u16LE，与1A差异未确认 |
| 22 | 打印默认参数 | 仅源码定义，未执行 |
| 27 | 设置最大间隙 | 单位、持久性未知，未执行 |
| 2C | 设置纸张类型 | 枚举、持久性未知，未执行 |
| 2F | 断开蓝牙 | USB不需要，未执行 |

模拟器线索 `39/40/42/7F/81` 单列为研究项，不能据此确认真实P2 GET语义，尤其不能把40解释成充电或打印状态。没有扫描未知操作码。LED控制、独立充电标志、动态打印头宽度、压缩、过热故障位、缓存容量、掉电恢复、最大长图及可靠机械完成仍未取得证据。

## 9. 复现工具

仓库根目录，Windows Python3，标准库，无第三方依赖：

```powershell
python Windows/scripts/usb_probe.py
python Windows/scripts/usb_probe.py --all --class-info --output work/usb/new-query.json
python Windows/scripts/usb_probe.py --query status --watch --duration 10
python Windows/scripts/usb_probe.py --print-test --rows 56 --watch --duration 4
python -m unittest discover -s tests/windows -p test_usb_probe.py -v
```

默认、`--all`及watch只读不打印。`--print-test` 会核对型号、合盖有纸、设置浓度100并回读，打印14–140行（14的倍数），最终送纸150行；会耗纸并保留浓度100。没指定输出时自动生成忽略目录内的唯一 `work/usb/attempt-<uuid>.json`。已存在输出文件一律拒绝覆盖。watch最多120秒；interval是每轮查询后的延时，不是保证采样率。`--trace` 输出已解析脱敏帧；没有任意原始命令入口。

20项独立测试覆盖实机黄金帧、分片/粘包、损坏头恢复、纸状态三值、ACK/数据顺序、负ACK、仅ACK/空数据/无响应、MAC例外与脱敏、命令边界。Python诊断与打包.NET后端分离；后续已完成原生集成，实际构建/硬件范围见[0.3.3验证](VALIDATION-v0.3.3-windows.zh-CN.md)。

## 10. 证据、文档审阅与来源

本地忽略目录：`work/usb/telemetry-all-01.json`、`print-status-01.json`、`class-status-01.json`。物理状态切换采样在本聊天工具记录中，公开脱敏结果同步到本文和机器配置。[文档核对清单](USB-DOCUMENTATION-AUDIT.md)记录基线文件、关联内容与更新决定。

- [Java P2 USB固定版本](https://github.com/hurui200320/java-paperang-p2-usb/tree/c50594c62d6fd6387d41e86b14f1fdf489d2b560)：端点、CRC、单字节浓度、倒计数、不等点阵ACK。AGPL-3.0；仅参考协议行为，未移入源文件。
- [P2库固定版本](https://github.com/mdj2812/paperang-p2-lib/tree/656fc8f37ff6f7b0744ef10f8a0ae38d4d594730)：USB/SPP及GET目录；未照搬固定cmd+1、u16浓度和跳过CRC的解析。
- [P1 Connect固定版本](https://github.com/Orangray-MDJX/paperang-p1-connect/tree/9ec2cb535ec8ac09a7dc1ce2464bc2a114d569bf)：短名称与协议分代。
- [模拟器分析](ESP32-EMULATOR.zh-CN.md)：硬编码响应不等于实机传感器。
- Microsoft：[USBPRINT](https://learn.microsoft.com/en-us/windows-hardware/drivers/print/usb-printing)、[SetupAPI枚举](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdienumdeviceinterfaces)、[取消I/O](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-cancelioex)、[类端口状态](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbprint/ni-usbprint-ioctl_usbprint_get_lpt_status)、[1284标识](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbprint/ni-usbprint-ioctl_usbprint_get_1284_id)、[头文件常量](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/shared/usbprint.h)。

## 跨传输 CRC 后续 — WinLite0.3.3

后续实体USB→BLE→USB实验确认：BLE注册key0后，本机USB接受的CRC也变为seed0。此时默认seed型号查询无数据，seed0型号查询返回CRC正确的Paperang_P2。原生UsbP2Session及Python诊断现先试默认seed，仅在无响应后用seed0重复**只读型号查询**，后续包沿用识别结果；不发送CRC注册、重置或其他写入。前文默认seed捕获仍是正确的历史测量。修复后恢复USB优先已实测成功；不会为识别seed重打点阵。
