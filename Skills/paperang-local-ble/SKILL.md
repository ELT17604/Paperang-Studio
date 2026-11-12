---
name: paperang-local-ble
description: Scan, connect, query and print on a legacy Paperang P2 directly over macOS Bluetooth, and lay out, render or preview local text and images.
---

# Paperang P2 直连蓝牙 · 0.3.2-localbleskill

从本技能目录运行 `./scripts/paperang`。保留技能在 Paperang Studio checkout 的 `Skills` 下，脚本用 Xcode Command Line Tools 编译独立 Swift CLI，直接复用仓库的 ProtocolCore 和 PrintCore。设备通信使用 CoreBluetooth 和 P2 的 FF00 服务。无需启动客户端、HTTP API 或中转服务器。

## 扫描、连接与读取

1. 开启 macOS 蓝牙，必要时在隐私设置中允许启动工具的终端访问蓝牙。让打印机处于可发现状态。
2. 运行 `./scripts/paperang scan`，从结果选择 P2 的 UUID。多个候选不明确时让用户选。
3. 运行 `./scripts/paperang status UUID`。每次设备操作在同一进程内扫描、连接、注册 CRC 会话、读取型号并执行请求，退出时断开。支持已验证的传统 02…03 P2。
4. 状态返回型号（6）、纸仓（12）、电池（16）和浓度（28）的新鲜原始十六进制读数。可用 `query UUID 12` 单独查询。纸仓按 u16LE 解释，0x10 表示开盖，盖合时 0x01 表示缺纸。电池为单字节估计百分比，不能据此判断是否正在充电。null 代表未取得字段数据。

## 排版、渲染与修改

- `./scripts/paperang settings` 输出默认配置。另存 JSON 覆盖需要的字段，例如 `{"fontSize":26,"alignment":"居左","marginX":16,"contentWidth":544}`。
- `./scripts/paperang render text /abs/note.txt /abs/preview.png /abs/layout.json` 读取 UTF-8 文本。图片用 `render image`，输入替换为本地图片。JSON 配置可省略。渲染不访问蓝牙，输出预览 PNG 和同名 `.png.bin` 点阵。修改内容或参数后，使用新文件名重新渲染并查看预览。
- 画布固定 **576 点宽**，默认内容宽 552 点。满足 `contentWidth + 2 × marginX ≤ 576`。中文按实际字体估算每行容量，语义处主动断行，长链接和混排应看预览。图片缩放后检查小字和细线。
- 文本长度可用「换行后行数 × 实际行高 + 上下边距」初估。命令返回实际点阵行数，上限 1400 行。按标称 300 DPI 估算点阵区长度为 `rows × 25.4 / 300` 毫米，另计走纸。物理长度以实测为准。

## 发送打印

只有在用户明确要求打印该内容和份数时执行：

```sh
./scripts/paperang print UUID /abs/preview.png.bin --allow-paper /abs/layout.json
```

传入已预览的点阵，默认一份，打印前读取新鲜纸仓状态并回读浓度。命令返回 `sent` 只表示发送流程完成，需核对纸面。连接中断、超时或报错后先核对纸面和设备状态，避免重复发送。排版改动要重新渲染。打印时的设置文件用于份数、浓度、走纸和传输参数，已生成的点阵保持原样。

协议与参数：[P2 BLE 协议](https://github.com/ELT17604/Paperang-Studio/blob/b52bd27157ce0aecb669925149265111cb36ad67/doc/PROTOCOL.md) · [渲染设置](https://github.com/ELT17604/Paperang-Studio/blob/b52bd27157ce0aecb669925149265111cb36ad67/doc/API.md)。
