import CoreBluetooth
import Foundation

@MainActor
final class Session: NSObject, @preconcurrency CBCentralManagerDelegate, @preconcurrency CBPeripheralDelegate {
    private lazy var central = CBCentralManager(delegate: self, queue: .main)
    private var devices: [UUID: CBPeripheral] = [:]
    private var names: [UUID: String] = [:]
    private var peer: CBPeripheral?
    private var writer: CBCharacteristic?
    private var notifications = Set<String>()
    private var decoder = Decoder()
    private var credits = 0
    private var packetLimit = 180
    private var error: Error?
    private var ack: Bool?
    private var awaiting: UInt8?
    private var expected: UInt8?
    private var response: Data?
    private var media: Data?
    let settings: PrintSettings

    init(settings: PrintSettings) { self.settings = settings }

    private func wait(_ seconds: Int, _ label: String, until condition: () -> Bool) async throws {
        let clock = ContinuousClock()
        let deadline = clock.now.advanced(by: .seconds(seconds))
        while true {
            if let error { throw error }
            if condition() { return }
            if clock.now >= deadline { throw PrintFailure("超时：\(label)") }
            try await Task.sleep(for: .milliseconds(10))
        }
    }

    func scan() async throws -> [[String: String]] {
        _ = central
        try await wait(8, "蓝牙初始化") { central.state == .poweredOn }
        central.scanForPeripherals(withServices: nil)
        defer { central.stopScan() }
        try await Task.sleep(for: .seconds(10))
        if let error { throw error }
        return names.sorted { $0.key.uuidString < $1.key.uuidString }.map {
            ["id": $0.key.uuidString, "name": $0.value]
        }
    }

    func connect(_ id: UUID) async throws {
        _ = central
        try await wait(8, "蓝牙初始化") { central.state == .poweredOn }
        if devices[id] == nil {
            central.scanForPeripherals(withServices: nil)
            defer { central.stopScan() }
            try await wait(12, "查找所选 P2") { devices[id] != nil }
        }
        guard let peer = devices[id] else { throw PrintFailure("找不到所选设备") }
        self.peer = peer
        peer.delegate = self
        central.connect(peer)
        try await wait(20, "发现 FF00 并订阅 FF01/FF03") { writer != nil && notifications.count == 2 }
        try await send(24, Data([0x21, 0x95, 0x76, 0x35]), seed: 0x35769521)
        let model = try await query(6).flatMap { String(data: $0, encoding: .utf8) }
        guard let model, ["P2", "Paperang_P2"].contains(model.trimmingCharacters(in: .controlCharacters)) else {
            throw PrintFailure("未确认兼容的 P2 型号")
        }
    }

    func disconnect() {
        if let peer { central.cancelPeripheralConnection(peer) }
    }

    func centralManagerDidUpdateState(_ central: CBCentralManager) {
        if [.poweredOff, .unauthorized, .unsupported].contains(central.state) {
            error = PrintFailure("蓝牙不可用。检查蓝牙开关及终端的蓝牙权限")
        }
    }

    func centralManager(_ central: CBCentralManager, didDiscover peer: CBPeripheral,
                        advertisementData: [String: Any], rssi: NSNumber) {
        let name = advertisementData[CBAdvertisementDataLocalNameKey] as? String ?? peer.name ?? ""
        guard name.lowercased().contains("paperang") || name.uppercased() == "P2" else { return }
        devices[peer.identifier] = peer
        names[peer.identifier] = name
    }

    func centralManager(_ central: CBCentralManager, didConnect peer: CBPeripheral) {
        peer.discoverServices([CBUUID(string: "FF00")])
    }

    func centralManager(_ central: CBCentralManager, didFailToConnect peer: CBPeripheral, error: Error?) {
        self.error = error ?? PrintFailure("连接失败")
    }

    func centralManager(_ central: CBCentralManager, didDisconnectPeripheral peer: CBPeripheral, error: Error?) {
        self.error = error ?? PrintFailure("蓝牙连接已断开")
    }

    func peripheral(_ peer: CBPeripheral, didDiscoverServices error: Error?) {
        if let error { self.error = error; return }
        for service in peer.services ?? [] where service.uuid == CBUUID(string: "FF00") {
            peer.discoverCharacteristics(["FF01", "FF02", "FF03"].map(CBUUID.init(string:)), for: service)
        }
    }

    func peripheral(_ peer: CBPeripheral, didDiscoverCharacteristicsFor service: CBService, error: Error?) {
        if let error { self.error = error; return }
        for characteristic in service.characteristics ?? [] {
            switch characteristic.uuid.uuidString {
            case "FF02":
                guard characteristic.properties.contains(.writeWithoutResponse) else {
                    self.error = PrintFailure("FF02 不支持无响应写入")
                    return
                }
                writer = characteristic
                packetLimit = min(packetLimit, peer.maximumWriteValueLength(for: .withoutResponse))
            case "FF01", "FF03": peer.setNotifyValue(true, for: characteristic)
            default: break
            }
        }
    }

    func peripheral(_ peer: CBPeripheral, didUpdateNotificationStateFor characteristic: CBCharacteristic, error: Error?) {
        if let error { self.error = error; return }
        if characteristic.isNotifying { notifications.insert(characteristic.uuid.uuidString) }
    }

    func peripheral(_ peer: CBPeripheral, didUpdateValueFor characteristic: CBCharacteristic, error: Error?) {
        if let error { self.error = error; return }
        guard let data = characteristic.value else { return }
        if characteristic.uuid.uuidString == "FF03" {
            let bytes = Array(data)
            if bytes.count >= 2 && bytes[0] == 1 { credits += Int(bytes[1]) }
            if bytes.count >= 3 && bytes[0] == 2 {
                packetLimit = min(packetLimit, Int(bytes[1]) | Int(bytes[2]) << 8)
            }
        } else if characteristic.uuid.uuidString == "FF01" {
            for frame in decoder.push(data) where frame.valid {
                if frame.command == awaiting && frame.payload.count == 1 { ack = frame.payload.first == 0 }
                if frame.command == expected { response = frame.payload }
                if frame.command == 13 && frame.payload.count == 2 { media = frame.payload }
            }
        }
    }

    private func send(_ command: UInt8, _ data: Data, seed: UInt32 = 0,
                      index: UInt8 = 0, requireACK: Bool = true) async throws {
        guard let peer, let writer else { throw PrintFailure("未连接") }
        awaiting = requireACK ? command : nil
        ack = nil
        defer { awaiting = nil }
        let frame = Wire.frame(command, data, seed: seed, index: index)
        var offset = 0
        while offset < frame.count {
            try await wait(settings.timeoutSeconds, "BLE 写入额度") {
                peer.state == .connected && credits > 0 && peer.canSendWriteWithoutResponse
            }
            let size = min(packetLimit, settings.bleChunk)
            guard size > 0 else { throw PrintFailure("无效的 BLE 分包尺寸") }
            let end = min(offset + size, frame.count)
            credits -= 1
            peer.writeValue(frame.subdata(in: offset..<end), for: writer, type: .withoutResponse)
            offset = end
            try await Task.sleep(for: .milliseconds(settings.writePauseMS))
        }
        if requireACK {
            try await wait(settings.timeoutSeconds, "命令 \(command) ACK") { ack != nil }
            guard ack == true else { throw PrintFailure("命令 \(command) 被设备拒绝") }
        }
    }

    func query(_ code: UInt8) async throws -> Data? {
        guard let reply = Command.expected[code] else { throw PrintFailure("仅允许已知只读查询") }
        response = nil
        expected = reply
        defer { expected = nil }
        try await send(code, Data([1]))
        let deadline = ContinuousClock.now.advanced(by: .seconds(1))
        while response == nil && ContinuousClock.now < deadline {
            if let error { throw error }
            try await Task.sleep(for: .milliseconds(20))
        }
        return response
    }

    private func checkMedia(_ sample: Data?) throws {
        guard let sample, sample.count == 2 else { throw PrintFailure("没有有效的纸仓读数") }
        let status = P2MediaStatus(UInt16(sample[0]) | UInt16(sample[1]) << 8)
        guard !status.coverOpen, status.paperPresent == true else { throw PrintFailure("请装纸并合盖") }
    }

    func printRaster(_ raster: Raster) async throws {
        let size = settings.packetBytes
        guard (raster.bytes.count + size - 1) / size <= 256 else {
            throw PrintFailure("点阵分包超过 256 帧")
        }
        try checkMedia(try await query(12))
        let density = Data([UInt8(settings.density)])
        try await send(25, density)
        guard try await query(28) == density else { throw PrintFailure("浓度回读不一致") }
        if settings.feedBefore > 0 { try await send(26, Wire.u16(settings.feedBefore)) }
        for copy in 0..<settings.copies {
            try checkMedia(try await query(12))
            for (index, offset) in stride(from: 0, to: raster.bytes.count, by: size).enumerated() {
                try checkMedia(media)
                try await send(0, raster.bytes.subdata(in: offset..<min(offset + size, raster.bytes.count)),
                               index: UInt8(index), requireACK: false)
                try await Task.sleep(for: .milliseconds(settings.framePauseMS))
            }
            try await Task.sleep(for: .seconds(settings.finishWaitSeconds))
            if settings.feedAfter > 0 && (settings.feedBetweenCopies || copy == settings.copies - 1) {
                try await send(26, Wire.u16(settings.feedAfter))
            }
        }
        _ = try await query(12)
    }
}

@main
struct Direct {
    static let usage = """
    paperang scan
    paperang status UUID
    paperang query UUID DECIMAL_CODE
    paperang settings
    paperang render text|image INPUT PREVIEW.png [SETTINGS.json]
    paperang print UUID RASTER.bin --allow-paper [SETTINGS.json]
    """

    static func emit(_ value: Any) throws {
        let data = try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys])
        print(String(decoding: data, as: UTF8.self))
    }

    static func settings(_ path: String?) throws -> PrintSettings {
        let defaults = PrintSettings()
        guard let path else { return defaults }
        var merged = try JSONSerialization.jsonObject(with: JSONEncoder().encode(defaults)) as! [String: Any]
        guard let overrides = try JSONSerialization.jsonObject(with: Data(contentsOf: URL(fileURLWithPath: path))) as? [String: Any],
              overrides.keys.allSatisfy({ merged[$0] != nil }) else { throw PrintFailure("无效的排版设置") }
        merged.merge(overrides) { _, override in override }
        let result = try JSONDecoder().decode(PrintSettings.self, from: JSONSerialization.data(withJSONObject: merged))
        try result.validate()
        return result
    }

    @MainActor static func run(_ args: [String]) async throws {
        switch args.first {
        case "settings" where args.count == 1:
            try emit(JSONSerialization.jsonObject(with: JSONEncoder().encode(PrintSettings())))
        case "scan" where args.count == 1:
            try await emit(Session(settings: PrintSettings()).scan())
        case "render" where (4...5).contains(args.count):
            let s = try settings(args.count == 5 ? args[4] : nil)
            let input = URL(fileURLWithPath: args[2])
            let preview = URL(fileURLWithPath: args[3])
            let raw = preview.appendingPathExtension("bin")
            let raster: Raster
            switch args[1] {
            case "text":
                let text = try String(contentsOf: input, encoding: .utf8)
                guard !text.isEmpty, text.count <= 10000 else { throw PrintFailure("文字必须为 1–10000 字符") }
                raster = try Renderer.render(text: text, image: nil, mode: "文字", settings: s)
            case "image":
                raster = try Renderer.render(text: "", image: Renderer.loadImage(input), mode: "图片", settings: s)
            default: throw PrintFailure(usage)
            }
            guard let png = raster.png else { throw PrintFailure("无法编码 PNG") }
            try png.write(to: preview, options: .withoutOverwriting)
            try raster.bytes.write(to: raw, options: .withoutOverwriting)
            try emit(["width": raster.width, "rows": raster.height, "preview": preview.path, "raster": raw.path])
        case "status" where args.count == 2, "query" where args.count == 3, "print" where (4...5).contains(args.count):
            guard let id = UUID(uuidString: args[1]) else { throw PrintFailure("需要扫描返回的 UUID") }
            var raster: Raster?
            var codes: [UInt8] = [6, 12, 16, 28]
            var s = PrintSettings()
            if args[0] == "query" {
                guard let code = UInt8(args[2]), Command.expected[code] != nil else {
                    throw PrintFailure("需要已知的十进制只读命令")
                }
                codes = [code]
            }
            if args[0] == "print" {
                guard args[3] == "--allow-paper" else { throw PrintFailure(usage) }
                s = try settings(args.count == 5 ? args[4] : nil)
                let data = try Data(contentsOf: URL(fileURLWithPath: args[2]))
                raster = try Raster.packed(data, height: data.count / 72)
            }
            let session = Session(settings: s)
            defer { session.disconnect() }
            try await session.connect(id)
            if let raster {
                try await session.printRaster(raster)
                try emit(["state": "sent", "physicalCompletion": "unknown"])
            } else {
                var results: [[String: Any]] = []
                for code in codes {
                    let data = try await session.query(code)
                    results.append(["command": code, "hex": data.map(Wire.hex) as Any? ?? NSNull()])
                }
                try emit(["id": id.uuidString, "readings": results])
            }
        case "-h", "--help": print(usage)
        default: throw PrintFailure(usage)
        }
    }

    static func main() async {
        do { try await run(Array(CommandLine.arguments.dropFirst())) }
        catch {
            FileHandle.standardError.write(Data((error.localizedDescription + "\n").utf8))
            exit(1)
        }
    }
}
