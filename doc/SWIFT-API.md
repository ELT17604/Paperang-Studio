# Swift module APIs

[Documentation](README.md) · [简体中文](SWIFT-API.zh-CN.md)

The package exports four library products. It targets macOS 14+ using Swift 6 tools with Swift language mode 5.

| Product / import | Responsibility |
|---|---|
| PaperangProtocol / ProtocolCore | Wire, Decoder, Frame, Command, P2MediaStatus |
| PaperangPrint / PrintCore | PrintSettings, Renderer, Raster, PrintFailure |
| PaperangTerminal / TerminalCore | PTYSession, PaperTerminalBuffer, backed by the C PTYBridge |
| PaperangIntegration / IntegrationCore | HTTPRequest, HTTPParser, DeviceMatcher |

Add a local package dependency and select its library products. SwiftPM uses the directory's package identity: for a checkout named `Paperang Studio`, use `.package(path:"../Paperang Studio")` and `.product(name:"PaperangProtocol", package:"Paperang Studio")`; adjust the identity to your checkout name. The public source defines exact signatures.

## Protocol

```swift
import Foundation
import ProtocolCore
let handshake = Wire.frame(0x18, Data([0x21,0x95,0x76,0x35]), seed:0x35769521)
let feed = Wire.frame(0x1A, Wire.u16(150)) // Encode only; does not send.
var decoder = Decoder()
for frame in decoder.push(notificationBytes) where frame.valid {
    print(frame.command, Wire.hex(frame.payload))
}
let status = P2MediaStatus(0x0019)
print(status.coverOpen, status.paperPresent as Any) // true, nil
```

`frame(command,payload,seed:0,index:0)` requires payload ≤1023 bytes; exceeding it triggers a precondition failure. `parseHex` returns `Data?`, accepts whitespace, and rejects `0x` prefixes. Decoder preserves partial frames, supports reset, and returns a CRC `valid` flag that callers must check. Keep each decoder on one serial execution context.

`Command.all` includes evidence metadata; `expected` contains trusted GET response mappings. Research entries are not in the automatic-query allowlist. `paperPresent` is optional and must remain nil while the cover is open.

## Rendering

```swift
import PrintCore
var settings = PrintSettings()
settings.fontSize = 24
settings.density = 100
settings.feedAfter = 150
try settings.validate()
let raster = try Renderer.render(text:"Hello P2", image:nil, mode:"文字", settings:settings)
let bytes = raster.bytes // 576 columns, 72 bytes/row, 1=black.
let preview = raster.png // PNG Data? derived from the same raster.
```

Text uses AppKit fonts; render on the main thread (the app uses MainActor). `Renderer.loadImage(URL)` and `loadImageData(Data)` return CGImage. Image rendering requires `mode:"图片"` and an image. Keep these literal mode values unchanged. `Raster.convert` converts an array of grayscale values through the shared monochrome pipeline. Importing these libraries does not initialize Bluetooth or send data. See [settings](API.md#all-settings).

## PTY

```swift
import TerminalCore
let session = PTYSession()
session.onOutput = { bytes in /* PTY serial queue; dispatch UI work to MainActor. */ }
session.onExit = { waitStatus in /* Raw waitpid status, not a plain exit code. */ }
try session.start(directory:"/your/folder", columns:72, rows:24,
                  loadProfile:false, helperURL:helperExecutableURL)
session.write(Data("pwd\r".utf8))
session.write(Data([3])) // Ctrl-C
session.resize(columns:80, rows:24)
// Finish with session.stop().
```

Each PTYSession starts only once. Set callbacks before startup and do not mutate them concurrently. Retain the session until exit; stop is asynchronous and the child is reaped. `PaperangPTYHost` must be packaged and signed with the host app; its default location is beside the host executable. A library dependency alone does not copy the helper.

`PaperTerminalBuffer` is a value type: `feed` normalizes bytes, `transcript` exposes display text, `pendingCount` reports pending content, `take` extracts batches, `prepend` restores failed content, and `clearPending` discards it. See [paper semantics](TERMINAL.md).

## Device integration

The app's Lab owns CoreBluetooth connection state, notifications, credits, ACK waits, and the shared task lock. Those are not exported as cross-process objects. Other software should prefer the [HTTP API](API.md) rather than competing for the same printer. HTTP parser utilities do not start a listener; name matching is only a discovery hint.
