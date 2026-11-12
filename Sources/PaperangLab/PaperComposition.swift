import AppKit
import CoreImage.CIFilterBuiltins
import PrintCore

struct PaperLayer: Identifiable {
    enum Kind: String { case text = "文字框", label = "标签", photo = "图片", qr = "二维码" }

    let id = UUID()
    let kind: Kind
    var frame: CGRect
    var text: NSAttributedString
    var image: CGImage?
    var qr = PaperQR()

    init(_ kind: Kind, image: CGImage? = nil, y: CGFloat = 24) {
        self.kind = kind
        self.image = image
        let width: CGFloat = kind == .qr ? 220 : 280
        let height: CGFloat = kind == .qr ? width : kind == .photo ? 180 : 80
        frame = CGRect(x: 24, y: y, width: width, height: height)
        text = NSAttributedString(string: kind == .label ? "标签" : "输入文字", attributes: [
            .font: NSFont.systemFont(ofSize: 30), .foregroundColor: NSColor.black
        ])
    }
}

struct PaperQR {
    enum Format: String, CaseIterable { case text = "纯文字", url = "网页链接", email = "电子邮件" }
    enum Shape: String, CaseIterable { case square = "方形", rounded = "圆角", dot = "圆点" }
    var format = Format.text
    var content = "Paperang Studio"
    var correction = "M"
    var shape = Shape.square
    var border = false

    func draw(in box: CGRect) throws {
        let value = content.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else { throw PrintFailure("请输入二维码内容") }
        let payload: String
        switch format {
        case .text: payload = content
        case .url:
            guard let url = URLComponents(string: value), ["http", "https"].contains(url.scheme?.lowercased() ?? ""), url.host?.isEmpty == false else {
                throw PrintFailure("二维码链接需以 http:// 或 https:// 开头")
            }
            payload = value
        case .email:
            guard value.contains("@"), !value.contains(where: { $0.isWhitespace }) else { throw PrintFailure("请输入邮件地址") }
            var url = URLComponents()
            url.scheme = "mailto"
            url.path = value
            guard let encoded = url.string else { throw PrintFailure("邮件地址无法编码") }
            payload = encoded
        }
        let filter = CIFilter.qrCodeGenerator()
        filter.message = Data(payload.utf8)
        filter.correctionLevel = correction
        guard let output = filter.outputImage, output.extent.width == output.extent.height,
              output.extent.width <= 185 else { throw PrintFailure("二维码内容过长") }

        let count = Int(output.extent.width)
        let module = floor((min(box.width, box.height) - (border ? 4 : 0)) / CGFloat(count + 8))
        guard module >= 3 else { throw PrintFailure("二维码太密，请放大二维码或缩短内容") }
        guard let source = Self.context.createCGImage(output, from: output.extent),
              let grid = CGContext(data: nil, width: count, height: count, bitsPerComponent: 8,
                                   bytesPerRow: 0, space: CGColorSpaceCreateDeviceGray(),
                                   bitmapInfo: CGImageAlphaInfo.none.rawValue),
              let data = grid.data else { throw PrintFailure("无法生成二维码模块") }
        grid.interpolationQuality = .none
        grid.draw(source, in: CGRect(x: 0, y: 0, width: count, height: count))
        let pixels = data.assumingMemoryBound(to: UInt8.self)
        NSColor.white.setFill()
        box.fill()
        let side = CGFloat(count) * module
        let origin = CGPoint(x: floor(box.midX - side / 2), y: floor(box.midY - side / 2))
        NSColor.black.setFill()
        for row in 0..<count {
            for column in 0..<count where pixels[(count - 1 - row) * grid.bytesPerRow + column] < 128 {
                let cell = CGRect(x: origin.x + CGFloat(column) * module,
                                  y: origin.y + CGFloat(row) * module, width: module, height: module)
                let finder = (column < 9 && (row < 9 || row >= count - 9)) ||
                             (column >= count - 9 && row >= count - 9)
                // Core Image adds one light module around the symbol.
                let timing = row == count - 8 || column == 7
                switch finder || timing ? .square : shape {
                case .square: cell.fill()
                case .rounded: NSBezierPath(roundedRect: cell, xRadius: module * 0.22, yRadius: module * 0.22).fill()
                case .dot: NSBezierPath(ovalIn: cell).fill()
                }
            }
        }
        if border {
            NSColor.black.setStroke()
            NSBezierPath(rect: box.insetBy(dx: 1, dy: 1)).stroke()
        }
    }

    private static let context = CIContext(options: [.useSoftwareRenderer: true])
}

struct PaperComposition {
    static let width: CGFloat = 576
    var height = 720
    var layers: [PaperLayer] = []

    func bitmap(strict: Bool) throws -> NSBitmapImageRep {
        guard (120...1400).contains(height), !strict || !layers.isEmpty else {
            throw PrintFailure("请插入至少一个对象，纸张高度须为120–1400点")
        }
        guard let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(Self.width),
                pixelsHigh: height, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true,
                isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: Int(Self.width) * 4,
                bitsPerPixel: 32), let context = NSGraphicsContext(bitmapImageRep: bitmap) else {
            throw PrintFailure("无法创建排版画布")
        }
        NSGraphicsContext.saveGraphicsState()
        defer { NSGraphicsContext.restoreGraphicsState() }
        NSGraphicsContext.current = context
        NSColor.white.setFill()
        CGRect(x: 0, y: 0, width: Self.width, height: CGFloat(height)).fill()
        for layer in layers {
            let frame = layer.frame
            let box = CGRect(x: frame.minX, y: CGFloat(height) - frame.maxY,
                             width: frame.width, height: frame.height)
            NSGraphicsContext.saveGraphicsState()
            defer { NSGraphicsContext.restoreGraphicsState() }
            NSBezierPath(rect: box).addClip()
            switch layer.kind {
            case .text, .label:
                if layer.kind == .label {
                    NSColor.white.setFill()
                    box.fill()
                    NSColor.black.setStroke()
                    NSBezierPath(roundedRect: box.insetBy(dx: 1, dy: 1), xRadius: 8, yRadius: 8).stroke()
                }
                let padding: CGFloat = layer.kind == .label ? 8 : 2
                layer.text.draw(with: box.insetBy(dx: padding, dy: padding), options: [.usesLineFragmentOrigin, .usesFontLeading])
            case .photo:
                if let image = layer.image {
                    let size = CGSize(width: image.width, height: image.height)
                    let scale = min(box.width / size.width, box.height / size.height)
                    let fitted = CGRect(x: box.midX - size.width * scale / 2, y: box.midY - size.height * scale / 2,
                                        width: size.width * scale, height: size.height * scale)
                    NSImage(cgImage: image, size: size).draw(in: fitted)
                }
            case .qr:
                do { try layer.qr.draw(in: box) }
                catch {
                    if strict { throw error }
                    (error.localizedDescription as NSString).draw(in: box.insetBy(dx: 8, dy: 8),
                        withAttributes: [.font: NSFont.systemFont(ofSize: 16), .foregroundColor: NSColor.systemRed])
                }
            }
        }
        return bitmap
    }

    func raster(settings: PrintSettings) throws -> Raster {
        let bitmap = try bitmap(strict: true)
        guard let pixels = bitmap.bitmapData else { throw PrintFailure("无法读取排版画布") }
        let gray = (0..<(bitmap.pixelsWide * height)).map { index -> Double in
            let offset = index * 4
            return 0.2126 * Double(pixels[offset]) + 0.7152 * Double(pixels[offset + 1]) + 0.0722 * Double(pixels[offset + 2])
        }
        return try Raster.convert(gray: gray, width: bitmap.pixelsWide, height: height, settings: settings)
    }
}
