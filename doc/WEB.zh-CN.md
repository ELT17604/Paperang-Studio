# 网页工作台 · v0.3.3-web

[English](WEB.md) · [功能矩阵](FEATURE-MATRIX.zh-CN.md) · [静态项目目录](../Web/)

## 第一次打印

1. 使用支持 Web Bluetooth 的浏览器打开工作台，例如 Android、macOS 或 Windows 上的 Chrome。线上使用 HTTPS，本机开发使用 localhost。开启蓝牙和打印机电源。
2. 发起设备选择，在浏览器弹出的列表中选取 P2 并授权连接。如果其他客户端正在使用打印机，先在该客户端断开连接。[浏览器蓝牙访问说明](https://developer.chrome.com/docs/capabilities/bluetooth)
3. 输入文字、打开图片、排版图层、手绘，或导入 raster1 点阵 JSON。检查黑白预览和点阵尺寸。每行宽 576 点，占 72 字节，最高位对应最左侧像素。[raster1 文件格式](../Skills/paperang-local-raster/references/raster1.md)
4. 调整边距与送纸参数，启用打印并点击打印。发送期间保持页面打开。若连接中断，先查看已打印的纸面，再决定是否重新发送。

## 五种编辑模式

| 模式 | 操作 |
|---|---|
| 文本 | 输入或粘贴文字，选择字体、字号、字重，调整对齐与行距。 |
| 图片 | 按纸宽适配、保持像素或裁切填满；可旋转图片，调整亮度、对比度、Gamma、反相和黑白转换方式。 |
| 高级排版 | 组合文字框、带框标签、图片与二维码，调整各图层的位置、尺寸与叠放次序。 |
| 手绘 | 使用手指、触控笔或鼠标绘制，调整笔宽、擦除、撤销或重做。 |
| JSON | 载入 raster1 点阵纸条，或应用原生工作台参数预设。 |

与原生客户端交换 JSON 时，可导入 raster1 文件以保留原始点阵，或导入 schema 1 参数预设以应用支持的文字、图片与打印设置。raster1 文件包含宽高、位序和 SHA-256 校验和。

## 纸张与传输设置

| 设置 | 默认值 | 作用 |
|---|---:|---|
| 上、下、左、右边距 | 0 px | 控制排版留白 |
| 裁去尾部空白 | 开启 | 去除最后一行墨点以后的内容留白，再保留设定的下边距 |
| 份数 | 1 | 每个任务的打印份数 |
| 浓度 | 100 | 设置 P2 打印浓度 |
| 打印前 / 完成后送纸 | 0 / 160 行 | 首份之前、打印结束后的走纸量 |
| 每次写入上限 | 180 字节 | 与打印机通告共同限制单次写入；若连接仅支持默认 ATT MTU，可设为 20 字节。 |
| 写入起始间隔 | 10 ms | 相邻两次 GATT 写入开始时间的最小间隔 |

Android 每次写入消耗一个打印机流控额度，并等待前一次写入结束。每帧最多包含 1008 字节点阵（14 行），帧间无附加延迟。原生单次写入最多 180 字节，同时受打印机通告上限与协商 ATT MTU 限制。Web Bluetooth 没有向 JavaScript 提供协商 MTU 的读取接口。实际耗时随分段设置、浏览器蓝牙栈和打印机流控变化。[Android 传输参数](../Android/src/com/elt/pprstudio/adc/P2Transfer.java) · [Android 会话](../Android/src/com/elt/pprstudio/adc/P2Session.java)

## 本地运行与部署

在仓库根目录执行 `python -m http.server 8080 --bind 127.0.0.1 --directory Web`，然后在浏览器打开 `http://localhost:8080/`。手机使用部署后的 HTTPS 地址。

在 Cloudflare Pages 导入本 GitHub 仓库，生产分支选 `main`，根目录保留为仓库根。框架预设选无，构建命令填 `exit 0`，输出目录填 `Web`。站点以静态文件部署，编辑和打印均由正在访问页面的浏览器执行。[Cloudflare Pages 静态 HTML 部署指南](https://developers.cloudflare.com/pages/framework-guides/deploy-anything/)

工作台自动采用浏览器语言与系统深浅色。在偏好设置中可手动选择语言和主题。选择 Frutiger Aero 后，透明玻璃面板会透出蓝天、草坡与湖面的背景，浅色和深色模式分别使用冰蓝和深海蓝玻璃。也可切换至莫兰迪主题。

## 原生源码对照

下表是网页版采用的原生实现参考。各发行版实际提供的能力见[功能矩阵](FEATURE-MATRIX.zh-CN.md)。

| 客户端 | 编辑与点阵 | 设备操作 |
|---|---|---|
| MacClient | 富文本、标签、图片与二维码按各自边框裁剪，再合成为 576 点宽的位图。RAW 与 schema 1 点阵导入时检查尺寸和字节数。[图层排版](../Sources/PaperangLab/PaperComposition.swift) · [点阵导入](../Sources/PaperangLab/WorkspaceRaster.swift) | [CoreBluetooth 会话](../Sources/PaperangLab/Bluetooth.swift) |
| WinClient | 字体、对齐、缩放、旋转、色调和抖动参数进入预览渲染路径。JSON 导入分别处理原生参数预设和点阵。[工作台控件](../Windows/src/Paperang.WinClient/WorkspaceEditing.cs) · [JSON 导入](../Windows/src/Paperang.WinClient/WorkspaceJson.cs) | [设备发现](../Windows/src/Paperang.WinClient/PrinterDiscovery.cs) · [P2 会话](../Windows/src/Paperang.Core/P2Session.cs) |
| AndroidClient | 文字与图形按纸宽栅格化；手绘可调笔宽、撤销。自动纸长按最后一行有墨点的位置截断。[渲染](../Android/src/com/elt/pprstudio/adc/PaperRenderer.java) · [手绘](../Android/src/com/elt/pprstudio/adc/DrawingEditor.java) · [点阵处理](../Android/src/com/elt/pprstudio/adc/Raster.java) | FF00 服务、型号握手、纸仓检查、浓度回读、按额度写入与送纸。[P2 会话](../Android/src/com/elt/pprstudio/adc/P2Session.java) |
