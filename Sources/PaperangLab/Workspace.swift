import SwiftUI
import UniformTypeIdentifiers
import ProtocolCore
import PrintCore
struct JobRecord: Identifiable
{
	let id = UUID()
	let date = Date()
	let title: String
	let detail: String
	var status: String
}
extension Lab
{
	var canPrint: Bool
	{
		ready && !busy && !rendering && raster != nil && previewError.isEmpty && allowPaper
	}
	var previewSummary: String
	{
		guard let r = raster else
		{
			return "无可用输出"
		}
		return
			"576 × \(r.height) px · \(r.bytes.count) bytes · 黑点 \(String(format:"%.1f",Double(r.blackPixels)/Double(576*r.height)*100))%"
	}
	var connectedName: String
	{
		peripheral?.name ?? "未连接打印机"
	}
	var mediaSummary: String
	{
		readings.first
	{
		$0.id == 12
	}?.value ?? "尚未读取纸仓状态" }
	var batterySummary: String
	{
		guard let r = readings.first(where:
		{
			$0.id == 16
		}) else
		{
			return "电量未读取"
		}
		return "电量 \(r.value)%（采样值）"
	}
	func schedulePreview()
	{
		guard !trayMode else
		{
			return
		}
		previewTask?.cancel()
		rendering = true
		previewTask = Task
		{
			do
			{
				try await Task.sleep(for: .milliseconds(220))
				try Task.checkCancellation()
				try updatePreview()
			} catch is CancellationError
			{
			} catch
			{
				previewError = error.localizedDescription
				raster = nil
				preview = nil
			}
			if !Task.isCancelled
			{
				rendering = false
			}
		}
	}
	func updatePreview() throws
	{
		let output: Raster
		switch mode
		{
		case "导入位图":
			guard let importedRaster else
			{
				throw PrintFailure("请先导入位图文件")
			}
			output = importedRaster
		case "高级排版":
			output = try paperComposition.raster(settings: settings)
		default:
			output = try Renderer.render(text: text, image: sourceImage, mode: mode, settings: settings)
		}
		raster = output
		preview = output.image
		previewError = ""
	}
	func importImage()
	{
		let panel = NSOpenPanel()
		panel.allowedContentTypes = [.png, .jpeg, .tiff, .heic, .gif, .bmp]
		panel.canChooseDirectories = false
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		do
		{
			sourceImage = try Renderer.loadImage(url)
			imageName = url.lastPathComponent
			mode = "图片"
			schedulePreview()
		} catch
		{
			self.error = error.localizedDescription
		}
	}
	func savePreview(raw: Bool = false)
	{
		do
		{
			try updatePreview()
			guard let r = raster else
			{
				return
			}
			let p = NSSavePanel()
			p.nameFieldStringValue = raw ? "Paperang-576.raw" : "Paperang-preview.png"
			p.allowedContentTypes = raw ? [.data] : [.png]
			guard p.runModal() == .OK, let url = p.url else
			{
				return
			}
			guard let data = raw ? r.bytes : r.png else
			{
				throw PrintFailure("PNG编码失败")
			}
			try data.write(to: url, options: .atomic)
			notice = "已导出与打印一致的\(raw ? "位图数据":"PNG预览")"
		} catch
		{
			self.error = error.localizedDescription
		}
	}
	func applyPreset(_ name: String)
	{
		settings = PrintSettings.preset(name)
		notice = "已应用\(name)预设，尚未修改打印机"
	}
	func exportPreset()
	{
		do
		{
			try settings.validate()
			let p = NSSavePanel()
			p.nameFieldStringValue = "Paperang-preset.json"
			guard p.runModal() == .OK, let url = p.url else
			{
				return
			}
			let e = JSONEncoder()
			e.outputFormatting = [.prettyPrinted, .sortedKeys]
			try e.encode(settings).write(to: url, options: .atomic)
			notice = "预设已保存"
		} catch
		{
			self.error = error.localizedDescription
		}
	}
	func importPreset()
	{
		let p = NSOpenPanel()
		p.allowedContentTypes = [.json]
		guard p.runModal() == .OK, let url = p.url else
		{
			return
		}
		do
		{
			let size = (try url.resourceValues(forKeys: [.fileSizeKey])).fileSize ?? 0
			guard size < 128_000 else
			{
				throw PrintFailure("预设过大")
			}
			let data = try Data(contentsOf: url)
			guard data.count < 128_000 else
			{
				throw PrintFailure("预设过大")
			}
			let s = try JSONDecoder().decode(PrintSettings.self, from: data)
			try s.validate()
			settings = s
			notice = "预设已导入；尚未向打印机发送指令"
		} catch
		{
			self.error = error.localizedDescription
		}
	}
	func refreshDevice()
	{
		guard ready, !busy else
		{
			return
		}
		run
		{ for c: UInt8 in [6, 16, 12, 28]
		{
			try await self.query(c)
		} }
	}
	func verifyMedia() async throws
	{
		try await query(12)
		guard let d = response, d.count == 2 else
		{
			throw PrintFailure("未取得新的纸仓状态，请刷新连接")
		}
		let m = P2MediaStatus(UInt16(d[0]) | UInt16(d[1]) << 8)
		guard !m.coverOpen, m.paperPresent == true else
		{
			throw PrintFailure("请装好纸卷并合上盖子")
		}
	}
	func verifyDensity(_ n: Int) async throws
	{
		try await send(25, Data([UInt8(n)]))
		try await query(28)
		guard let d = response, d.count == 1, d[0] == UInt8(n) else
		{
			throw PrintFailure("浓度回读未确认，停止发送")
		}
	}
	func applyDensity()
	{
		guard ready, !busy else
		{
			return
		}
		let n = settings.density
		run
		{
			try await self.verifyDensity(n)
			self.notice = "打印机浓度已设为\(n)并确认"
		}
	}
	func feedPaper()
	{
		guard ready, !busy, allowPaper else
		{
			return
		}
		let n = settings.feedAfter
		run
		{
			try await self.verifyMedia()
			if n > 0
			{
				try await self.send(26, Wire.u16(n))
			}
			self.notice = "送纸\(n)行指令已确认"
		}
	}
	func stopJob()
	{
		jobStatus = "已停止后续发送；已进入设备的数据无法撤回"
		if let i = jobs.indices.last, jobs[i].status == "发送中"
		{
			jobs[i].status = "已停止"
		}
		disconnect()
	}
	private var workspaceJobTitle: String
	{
		switch mode
		{
		case "导入位图": return importedRasterName
		case "高级排版": return "高级排版"
		case "图片": return imageName
		default: return String(text.prefix(30))
		}
	}
	func submitJob()
	{
		guard ready, !busy, allowPaper else
		{
			return
		}
		do
		{
			try updatePreview()
			guard let r = raster else
			{
				return
			}
			_ = try submitRaster(r, settings: settings, title: workspaceJobTitle)
		} catch
		{
			self.error = error.localizedDescription
		}
	}
	@discardableResult func submitRaster(_ r: Raster, settings s: PrintSettings, title: String) throws -> UUID
	{
		guard ready, !busy else
		{
			throw PrintFailure("打印机未就绪或正在执行另一项任务")
		}
		try s.validate()
		let packets = (r.bytes.count + s.packetBytes - 1) / s.packetBytes
		guard packets <= 256 else
		{
			throw PrintFailure("分包数量超过8位序号容量；请增加工程分包大小或缩短内容")
		}
		let entry = JobRecord(
			title: title, detail: "\(r.width)×\(r.height) · \(s.copies)份 · 浓度\(s.density) · 批后\(s.feedAfter)行",
			status: "发送中")
		jobs.append(entry)
		if jobs.count > 30
		{
			jobs.removeFirst()
		}
		progress = 0
		jobStatus = "正在准备"
		let id = entry.id
		run
		{
			self.activeSettings = s
			do
			{
				try await self.verifyMedia()
				try await self.verifyDensity(s.density)
				if s.feedBefore > 0
				{
					try await self.send(26, Wire.u16(s.feedBefore))
				}
				for copy in 0..<s.copies
				{
					try Task.checkCancellation()
					try await self.verifyMedia()
					for (index, offset) in stride(from: 0, to: r.bytes.count, by: s.packetBytes).enumerated()
					{
						if let raw = self.readings.first(where:
						{
							$0.id == 12
						})?.raw, let d = Wire.parseHex(raw),
							d.count == 2
						{
							let m = P2MediaStatus(UInt16(d[0]) | UInt16(d[1]) << 8)
							guard !m.coverOpen, m.paperPresent == true else
							{
								throw PrintFailure("检测到开盖/缺纸，已中止后续发送")
							}
						}
						try await self.send(
							0, Data(r.bytes[offset..<min(offset + s.packetBytes, r.bytes.count)]), index: UInt8(index),
							ack: false)
						self.progress = Double(copy * packets + index + 1) / Double(s.copies * packets)
						self.jobStatus = "第\(copy+1)/\(s.copies)份 · 分包\(index+1)/\(packets)"
						try await Task.sleep(for: .milliseconds(s.framePauseMS))
					}
					try await Task.sleep(for: .seconds(s.finishWaitSeconds))
					if s.feedAfter > 0 && (s.feedBetweenCopies || copy == s.copies - 1)
					{
						try await self.send(26, Wire.u16(s.feedAfter))
					}
				}
				try await self.query(12)
				self.jobStatus = "发送完成，请核对纸面"
				self.log("PRINT", entry.detail)
				if let i = self.jobs.firstIndex(where:
				{
					$0.id == id
				})
				{
					self.jobs[i].status = "已发送，待核对纸面"
				}
				if self.relayFlight?.localID == id
				{
					self.relayFlight?.outcome = "sent"
					try self.persistRelayFlight()
				}
				if self.relayRunning
				{
					do
					{
						try await self.relayFlush()
					} catch
					{
						self.relayMessage = "发送已结束，结果待回报：" + error.localizedDescription
					}
				}
			} catch
			{
				if let i = self.jobs.firstIndex(where:
				{
					$0.id == id
				}), self.jobs[i].status == "发送中"
				{
					self.jobs[i].status = "未完成"
					self.jobStatus = "未完成：" + error.localizedDescription
				}
				throw error
			}
		}
		return id
	}
	func engineeringSend()
	{
		guard ready, !busy, let c = UInt8(rawCommand, radix: 16), let d = Wire.parseHex(rawPayload) else
		{
			error = "命令或载荷十六进制无效"
			return
		}
		if Command.expected[c] != nil
		{
			rawQuery()
			return
		}
		guard allowEngineeringWrites else
		{
			error = "请先解锁工程写入；默认仅允许只读查询"
			return
		}
		let valid: Bool
		switch c
		{
		case 25: valid = d.count == 1 && (d.first ?? 255) <= 100
		case 26: valid = d.count == 2 && (Int(d[0]) | Int(d[1]) << 8) <= 2000
		case 30, 39: valid = d.count == 2
		case 20, 44: valid = d.count == 1
		case 27, 34: valid = d == Data([0])
		default: valid = false
		}
		guard valid else
		{
			error = "此指令或载荷长度不在工程白名单；固件、压缩与未知指令未开放"
			return
		}
		guard ![26, 27, 34].contains(Int(c)) || allowPaper else
		{
			error = "该命令耗纸，请先开启侧栏全局允许耗纸"
			return
		}
		let alert = NSAlert()
		alert.messageText = "发送工程指令 \(String(format:"%02X",c))？"
		alert.informativeText = "载荷：\(Wire.hex(d))。可能修改设备保存的参数；工厂模式值及退出行为尚未验证。该操作不会自动恢复设置。"
		alert.addButton(withTitle: "发送一次")
		alert.addButton(withTitle: "取消")
		guard alert.runModal() == NSApplication.ModalResponse.alertFirstButtonReturn else
		{
			return
		}
		run
		{
			try await self.send(c, d)
			self.notice = "工程指令已确认；请查阅日志及设备实际状态"
		}
	}
}
