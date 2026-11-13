import AppKit
import CryptoKit
import PrintCore
import UniformTypeIdentifiers

enum WorkspaceRaster
{
	private struct Envelope: Decodable
	{
		let type, encoding, bitOrder, data, sha256: String
		let schemaVersion, width, height: Int
		let blackIsOne: Bool
	}

	static func load(_ url: URL) throws -> Raster
	{
		let isRaw = url.pathExtension.lowercased() == "raw"
		let isJSON = url.pathExtension.lowercased() == "json"
		guard isRaw || isJSON else
		{
			throw PrintFailure("请选择 .raw 或 raster1 JSON 文件")
		}
		let limit = isRaw ? 72 * 1400 : 144_000
		guard let stream = InputStream(url: url) else
		{
			throw PrintFailure("无法打开位图文件")
		}
		stream.open()
		defer
		{
			stream.close()
		}
		var data = Data()
		var buffer = [UInt8](repeating: 0, count: 8192)
		while data.count <= limit
		{
			let count = stream.read(&buffer, maxLength: min(buffer.count, limit + 1 - data.count))
			guard count >= 0 else
			{
				throw PrintFailure("无法读取位图文件")
			}
			if count == 0
			{
				break
			}
			data.append(contentsOf: buffer.prefix(count))
		}
		guard !data.isEmpty, data.count <= limit else
		{
			throw PrintFailure("位图文件为空或超过大小上限")
		}
		if isRaw
		{
			guard data.count.isMultiple(of: 72) else
			{
				throw PrintFailure("RAW 文件必须每行正好 72 字节")
			}
			return try Raster.packed(data, height: data.count / 72)
		}
		let envelope: Envelope
		do
		{
			envelope = try JSONDecoder().decode(Envelope.self, from: data)
		} catch
		{
			throw PrintFailure("JSON 文件必须是完整的 raster1 对象")
		}
		guard envelope.type == "raster1", envelope.schemaVersion == 1, envelope.width == 576,
			(1...1400).contains(envelope.height), envelope.encoding == "base64",
			envelope.bitOrder == "msb-first", envelope.blackIsOne
		else
		{
			throw PrintFailure("不支持该 raster1 版本、尺寸或编码")
		}
		guard envelope.data.utf8.count <= 134_400, let bytes = Data(base64Encoded: envelope.data),
			bytes.count == 72 * envelope.height, bytes.base64EncodedString() == envelope.data
		else
		{
			throw PrintFailure("Base64 格式或位图字节长度不正确")
		}
		let checksum = SHA256.hash(data: bytes).map
		{
			String(format: "%02x", $0)
		}.joined()
		guard checksum == envelope.sha256 else
		{
			throw PrintFailure("位图 SHA-256 校验不一致")
		}
		return try Raster.packed(bytes, height: envelope.height)
	}
}

extension Lab
{
	func importRaster()
	{
		let panel = NSOpenPanel()
		panel.allowedContentTypes = [UTType(filenameExtension: "raw") ?? .data, .json]
		panel.canChooseDirectories = false
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		do
		{
			importedRaster = try WorkspaceRaster.load(url)
			importedRasterName = url.lastPathComponent
			mode = "导入位图"
			try updatePreview()
		} catch
		{
			self.error = error.localizedDescription
		}
	}
}
