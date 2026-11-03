# Swift 模块 API

Package.swift提供四个library产品。macOS14+、Swift6，内部Swift语言模式5。

| 产品 / import | 职责 |
|---|---|
| PaperangProtocol / ProtocolCore | Wire、Decoder、Frame、Command、P2MediaStatus |
| PaperangPrint / PrintCore | PrintSettings、Renderer、Raster、PrintFailure |
| PaperangTerminal / TerminalCore | PTYSession、PaperTerminalBuffer；依赖C桥PTYBridge |
| PaperangIntegration / IntegrationCore | HTTPRequest、HTTPParser、DeviceMatcher；主要供本机服务复用 |

其他Swift Package可用`.package(path:"../PaperangStudio")`，目标依赖`.product(name:"PaperangProtocol",package:"PaperangStudio")`等。公开API源代码是精确签名依据。

## 协议

```swift
import Foundation
import ProtocolCore
let handshake = Wire.frame(0x18, Data([0x21,0x95,0x76,0x35]), seed:0x35769521)
let feed = Wire.frame(0x1A, Wire.u16(150)) // 仅封包，不会发送
var decoder = Decoder()
for frame in decoder.push(notificationBytes) where frame.valid {
    print(frame.command, Wire.hex(frame.payload))
}
let status = P2MediaStatus(0x0019)
print(status.coverOpen, status.paperPresent as Any) // true, nil
```

`frame(command,payload,seed:0,index:0)`要求payload≤1023（超限precondition失败）。`parseHex`接受空白分隔但不接受0x前缀，返回Data?。Decoder可reset，保留半帧、返回CRC valid标志；调用方必须检查valid。Decoder不是线程安全共享对象，请由同一串行上下文管理。

Command.all包含证据目录；expected仅可信GET映射；研究目录条目不加入自动查询白名单。P2MediaStatus的纸张状态为Bool?，开盖时必须保留nil。

## 图像和排版

```swift
import PrintCore
var settings = PrintSettings()
settings.fontSize = 24
settings.density = 100
settings.feedAfter = 150
try settings.validate()
let raster = try Renderer.render(text:"你好 P2", image:nil, mode:"文字", settings:settings)
let bytes = raster.bytes // 72字节/行，固定576列，1黑0白
let preview = raster.png // 同一数据生成的PNG Data?
```

文字渲染使用AppKit字体，建议在主线程执行（App中由MainActor管理）；图片解码`Renderer.loadImage(URL)`、`loadImageData(Data)`返回CGImage；图片模式传image并设mode="图片"。所有参数与范围见[HTTP文档](API.md)。Raster.convert可直接接收灰度数组进行统一1位转换。复用库不会自动初始化蓝牙或发送数据。

## PTY

```swift
import TerminalCore
let session = PTYSession()
session.onOutput = { bytes in /* 回调位于PTY串行队列，UI须切回MainActor */ }
session.onExit = { waitStatus in /* 原始waitpid状态，非直接exit code */ }
try session.start(directory:"/your/folder", columns:72, rows:24,
                  loadProfile:false, helperURL:helperExecutableURL)
session.write(Data("pwd\r".utf8))
session.write(Data([3])) // Ctrl-C
session.resize(columns:80, rows:24)
// 结束：session.stop()
```

PTYSession每个对象只能start一次；回调应在start之前设置，不并发修改。持有session直到退出，stop异步投递；退出时会回收子进程。helper必须是项目PaperangPTYHost可执行文件，默认位于主程序同目录；宿主App须打包并签名helper。只依赖library不会自动复制helper。

PaperTerminalBuffer是值类型：feed(Data)归一化，transcript显示，pendingCount查看缓冲，take(maxCharacters:includePartial:)提取，prepend用于失败回补，clearPending丢弃。限制与纸面语义见[终端指南](TERMINAL.md)。

## 设备传输边界

CoreBluetooth的连接、通知、额度、ACK等待及共享任务锁目前由App的Lab负责，不作为跨进程对象公开。外部App/网页应优先调用[HTTP API](API.md)，避免两个中心同时占用同一台P2。HTTP解析工具本身不启动listener；设备候选匹配只看名称，不是身份保证。
