import AppKit
import ImageIO
public struct Raster
{
	public let width: Int
	public let height: Int
	public let bytes: Data
	public let blackPixels: Int
	public var image: NSImage
	{
		let rep = NSBitmapImageRep(
			bitmapDataPlanes: nil, pixelsWide: width, pixelsHigh: height, bitsPerSample: 8, samplesPerPixel: 1,
			hasAlpha: false, isPlanar: false, colorSpaceName: .deviceWhite, bytesPerRow: width, bitsPerPixel: 8)!
		let p = rep.bitmapData!
		for y in 0..<height
		{
			for x in 0..<width
			{
				p[y * width + x] = (bytes[y * (width / 8) + x / 8] & (1 << (7 - x % 8))) == 0 ? 255 : 0
			}
		}
		let im = NSImage(size: NSSize(width: width, height: height))
		im.addRepresentation(rep)
		return im
	}
	public var png: Data?
	{
		guard let rep = image.representations.first as? NSBitmapImageRep else
		{
			return nil
		}
		return rep.representation(using: .png, properties: [:])
	}
	public static func packed(_ bytes: Data, width: Int = 576, height: Int) throws -> Self
	{
		guard width == 576, (1...1400).contains(height), bytes.count == 72 * height else
		{
			throw PrintFailure("远程位图尺寸无效")
		}
		return Self(
			width: width, height: height, bytes: bytes, blackPixels: bytes.reduce(0)
			{
				$0 + $1.nonzeroBitCount
			})
	}
	/// Fit a packed image into the block's printable width, with symmetric white margins.
	/// Use nearest-neighbor sampling to keep the image strictly one-bit without re-dithering.
	public func fitting(margin: Int) throws -> Self
	{
		guard width == 576, (1...1400).contains(height), bytes.count == 72 * height, (0...120).contains(margin) else
		{
			throw PrintFailure("图片区块边距或尺寸无效")
		}
		if margin == 0
		{
			return self
		}
		let contentWidth = 576 - margin * 2
		let targetHeight = max(1, (height * contentWidth + 575) / 576)
		var output = Data(repeating: 0, count: 72 * targetHeight)
		for y in 0..<targetHeight
		{
			let sy = min(height - 1, ((2 * y + 1) * height) / (2 * targetHeight))
			for x in 0..<contentWidth
			{
				let sx = min(575, ((2 * x + 1) * 576) / (2 * contentWidth))
				if bytes[sy * 72 + sx / 8] & (1 << (7 - sx % 8)) != 0
				{
					let dx = x + margin
					output[y * 72 + dx / 8] |= 1 << (7 - dx % 8)
				}
			}
		}
		return try Self.packed(output, height: targetHeight)
	}
	public static func stack(_ parts: [(raster: Raster, spaceBefore: Int)]) throws -> Self
	{
		guard !parts.isEmpty, parts.count <= 24 else
		{
			throw PrintFailure("请启用至少一个有内容的区块（最多24个）")
		}
		var height = 0
		var data = Data()
		var black = 0
		for part in parts
		{
			guard part.raster.width == 576, part.raster.height > 0, part.raster.bytes.count == 72 * part.raster.height,
				(0...200).contains(part.spaceBefore)
			else
			{
				throw PrintFailure("区块尺寸或留白无效")
			}
			height += part.spaceBefore + part.raster.height
			guard height <= 1400 else
			{
				throw PrintFailure("组合版式超过1400行，请缩小字号、留白或关闭部分区块")
			}
			data.append(Data(repeating: 0, count: part.spaceBefore * 72))
			data.append(part.raster.bytes)
			black += part.raster.blackPixels
		}
		return Self(width: 576, height: height, bytes: data, blackPixels: black)
	}
	public static func convert(gray: [Double], width: Int, height: Int, settings s: PrintSettings) throws -> Self
	{
		try s.validate()
		guard width == 576, height > 0, height <= 1400, gray.count == width * height else
		{
			throw PrintFailure("输出画布必须为576点宽、1–1400行")
		}
		var values = gray.map
		{
			v -> Double in
			let a = max(0, min(255, (v - 127.5) * s.contrast + 127.5 + s.brightness * 2.55))
			let b = pow(a / 255, 1 / s.gamma) * 255
			return s.invert ? 255 - b : b
		}
		var result = Data(repeating: 0, count: width / 8 * height)
		var blacks = 0
		let bayer = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
		for y in 0..<height
		{
			for x in 0..<width
			{
				let i = y * width + x
				let v = values[i]
				let threshold =
					s.conversion == "有序抖动"
					? (Double(bayer[y % 4][x % 4]) + 0.5) * 16 + (s.threshold - 128) : s.threshold
				let black = v < threshold
				if black
				{
					result[y * (width / 8) + x / 8] |= 1 << (7 - x % 8)
					blacks += 1
				}
				if s.conversion == "Floyd–Steinberg"
				{
					let e = v - (black ? 0 : 255)
					if x + 1 < width
					{
						values[i + 1] += e * 7 / 16
					}
					if y + 1 < height
					{
						if x > 0
						{
							values[i + width - 1] += e * 3 / 16
						}
						values[i + width] += e * 5 / 16
						if x + 1 < width
						{
							values[i + width + 1] += e / 16
						}
					}
				}
			}
		}
		return Self(width: width, height: height, bytes: result, blackPixels: blacks)
	}
}
public enum Renderer
{
	public static func loadImage(_ url: URL) throws -> CGImage
	{
		let size = (try url.resourceValues(forKeys: [.fileSizeKey])).fileSize ?? 0
		guard size <= 64 * 1024 * 1024 else
		{
			throw PrintFailure("图片文件超过64MB")
		}
		guard let src = CGImageSourceCreateWithURL(url as CFURL, nil) else
		{
			throw PrintFailure("无法读取图片")
		}
		return try decode(src)
	}
	public static func loadImageData(_ data: Data) throws -> CGImage
	{
		guard data.count <= 2 * 1024 * 1024, let src = CGImageSourceCreateWithData(data as CFData, nil) else
		{
			throw PrintFailure("图片数据无效或超过2MB")
		}
		return try decode(src)
	}
	private static func decode(_ src: CGImageSource) throws -> CGImage
	{
		guard let info = CGImageSourceCopyPropertiesAtIndex(src, 0, nil) as? [CFString: Any] else
		{
			throw PrintFailure("无法读取图片元数据")
		}
		let w = (info[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue ?? 0
		let h = (info[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue ?? 0
		guard w > 0, h > 0, w <= 40000, h <= 40000, w * h <= 40_000_000 else
		{
			throw PrintFailure("图片需小于4000万像素")
		}
		let opts: [CFString: Any] = [
			kCGImageSourceCreateThumbnailFromImageAlways: true, kCGImageSourceThumbnailMaxPixelSize: 4096,
			kCGImageSourceCreateThumbnailWithTransform: true
		]
		guard let image = CGImageSourceCreateThumbnailAtIndex(src, 0, opts as CFDictionary) else
		{
			throw PrintFailure("图片解码失败")
		}
		return image
	}
	private static func rotate(_ image: CGImage, _ degrees: Int) throws -> CGImage
	{
		if degrees == 0
		{
			return image
		}
		let swap = degrees == 90 || degrees == 270
		let w = swap ? image.height : image.width
		let h = swap ? image.width : image.height
		guard
			let c = CGContext(
				data: nil, width: w, height: h, bitsPerComponent: 8, bytesPerRow: w * 4,
				space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
		else
		{
			throw PrintFailure("图片旋转失败")
		}
		c.translateBy(x: CGFloat(w) / 2, y: CGFloat(h) / 2)
		c.rotate(by: -CGFloat(degrees) * CGFloat.pi / 180)
		c.draw(
			image,
			in: CGRect(
				x: -CGFloat(image.width) / 2, y: -CGFloat(image.height) / 2, width: CGFloat(image.width),
				height: CGFloat(image.height)))
		guard let out = c.makeImage() else
		{
			throw PrintFailure("图片旋转失败")
		}
		return out
	}
	public static func render(text: String, image: CGImage?, mode: String, settings s: PrintSettings) throws -> Raster
	{
		try s.validate()
		let maxWidth = Double(s.contentWidth)
		let fontBase = NSFont(name: s.fontName, size: s.fontSize) ?? NSFont.systemFont(ofSize: s.fontSize)
		let font = s.bold ? NSFontManager.shared.convert(fontBase, toHaveTrait: .boldFontMask) : fontBase
		let paragraph = NSMutableParagraphStyle()
		paragraph.lineBreakMode = .byWordWrapping
		paragraph.lineSpacing = s.lineSpacing
		paragraph.alignment = s.alignment == "居左" ? .left : s.alignment == "居右" ? .right : .center
		let attrs: [NSAttributedString.Key: Any] = [
			.font: font, .paragraphStyle: paragraph, .foregroundColor: NSColor.black
		]
		var source: CGImage?
		var h = 0
		var drawRect = NSRect.zero
		var clipping = NSRect.zero
		if mode == "图片"
		{
			guard let image else
			{
				throw PrintFailure("请先导入图片")
			}
			source = try rotate(image, s.rotation)
			let iw = Double(source!.width), ih = Double(source!.height)
			let targetH: Double
			var drawW: Double, drawH: Double
			if s.imageFit == "裁切填满"
			{
				targetH = Double(s.cropHeight)
				let scale = max(maxWidth / iw, targetH / ih)
				drawW = iw * scale
				drawH = ih * scale
			} else
			{
				let scale = s.imageFit == "保持像素" ? min(1, maxWidth / iw) : maxWidth / iw
				drawW = iw * scale
				drawH = ih * scale
				targetH = ceil(drawH)
			}
			h = Int(ceil(targetH)) + s.marginY * 2
			let x =
				s.alignment == "居左"
				? Double(s.marginX) : s.alignment == "居右" ? 576 - Double(s.marginX) - maxWidth : (576 - maxWidth) / 2
			clipping = NSRect(x: x, y: Double(s.marginY), width: maxWidth, height: targetH)
			let dx = s.alignment == "居左" ? 0 : s.alignment == "居右" ? maxWidth - drawW : (maxWidth - drawW) / 2
			drawRect = NSRect(x: x + dx, y: Double(s.marginY) + (targetH - drawH) / 2, width: drawW, height: drawH)
		} else
		{
			guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, text.count <= 10000 else
			{
				throw PrintFailure("请输入1–10000字符")
			}
			let b = (text as NSString).boundingRect(
				with: NSSize(width: maxWidth, height: 100000), options: [.usesLineFragmentOrigin, .usesFontLeading],
				attributes: attrs)
			h = Int(ceil(b.height)) + s.marginY * 2 + 4
		}
		guard h > 0, h <= 1400 else
		{
			throw PrintFailure("本次预览超过1400行；请减小字号/图片尺寸、使用裁切，或拆成多批")
		}
		guard
			let rep = NSBitmapImageRep(
				bitmapDataPlanes: nil, pixelsWide: 576, pixelsHigh: h, bitsPerSample: 8, samplesPerPixel: 4,
				hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 576 * 4, bitsPerPixel: 32),
			let context = NSGraphicsContext(bitmapImageRep: rep), let pixels = rep.bitmapData
		else
		{
			throw PrintFailure("画布创建失败")
		}
		NSGraphicsContext.saveGraphicsState()
		NSGraphicsContext.current = context
		NSColor.white.setFill()
		NSRect(x: 0, y: 0, width: 576, height: h).fill()
		if let source
		{
			NSGraphicsContext.saveGraphicsState()
			NSBezierPath(rect: clipping).addClip()
			NSImage(cgImage: source, size: NSSize(width: source.width, height: source.height)).draw(
				in: drawRect, from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true,
				hints: [.interpolation: NSImageInterpolation.high.rawValue])
			NSGraphicsContext.restoreGraphicsState()
		} else
		{
			let x =
				s.alignment == "居左"
				? Double(s.marginX) : s.alignment == "居右" ? 576 - Double(s.marginX) - maxWidth : (576 - maxWidth) / 2
			(text as NSString).draw(
				with: NSRect(x: x, y: Double(s.marginY), width: maxWidth, height: Double(h - s.marginY * 2)),
				options: [.usesLineFragmentOrigin, .usesFontLeading], attributes: attrs)
		}
		context.flushGraphics()
		NSGraphicsContext.restoreGraphicsState()
		var gray = [Double](repeating: 255, count: 576 * h)
		for i in 0..<gray.count
		{
			gray[i] =
				0.2126 * Double(pixels[i * 4]) + 0.7152 * Double(pixels[i * 4 + 1]) + 0.0722 * Double(pixels[i * 4 + 2])
		}
		return try Raster.convert(gray: gray, width: 576, height: h, settings: s)
	}
}
