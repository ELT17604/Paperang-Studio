import Foundation
import AppKit
import Combine
import CoreBluetooth
import PrintCore

struct MacConfiguration: Codable
{
	var schemaVersion = 1
	var server = ""
	var authMode = "owner"
	var ownerPath = ""
	var allowHTTP = false
	var autoConnect = false
	var reconnectPrinter = false
	var printerID = ""
	var keepReconnecting = true
	var defaultAutoPrint = false
	var style = "便签"
	var density = 100
	var feed = 150
	var includeText = true
	var includeSender = true
	var includeTime = true
	var includeID = false
	var includeIP = false
	var includeUA = false
	var includeDevice = false
	var templates = [RelayTemplate]()
	var templateID = ""
	var settings = PrintSettings()
	var page = "打印工作台"
	var engineer = false
	var engineerPage = "设备字段"
	var showAllDevices = false
	var filter = "全部"
	var apiPort = 8176
	var apiOrigins = "http://localhost:3000,http://127.0.0.1:3000"
	func validate() throws
	{
		guard schemaVersion == 1, ["owner", "M"].contains(authMode), server.utf8.count <= 4096,
			ownerPath.utf8.count <= 4096, (0...100).contains(density), (0...2000).contains(feed), templates.count <= 30,
			(1024...65535).contains(apiPort), apiOrigins.count <= 4096
		else
		{
			throw PrintFailure("保存的配置无效")
		}
		try settings.validate()
		for template in templates
		{
			try template.validate()
		}
	}
}
extension Lab
{
	func configurationSnapshot() -> MacConfiguration
	{
		var c = MacConfiguration()
		c.server = relayURL
		c.authMode = relayAuthMode
		c.ownerPath = relayOwnerPath
		c.allowHTTP = relayAllowHTTP
		c.autoConnect = relayAutoConnect
		c.reconnectPrinter = relayReconnectPrinter
		c.printerID = relayPrinterID
		c.keepReconnecting = relayKeepReconnecting
		c.defaultAutoPrint = relayDefaultAutoPrint
		c.style = relayStyle
		c.density = relayDensity
		c.feed = relayFeed
		c.includeText = relayIncludeText
		c.includeSender = relayIncludeSender
		c.includeTime = relayIncludeTime
		c.includeID = relayIncludeID
		c.includeIP = relayIncludeIP
		c.includeUA = relayIncludeUA
		c.includeDevice = relayIncludeDevice
		c.templates = relayTemplates
		c.templateID = relayTemplateID
		c.settings = settings
		c.page = page
		c.engineer = engineer
		c.engineerPage = engineerPage
		c.showAllDevices = showAllDevices
		c.filter = relayFilter
		c.apiPort = apiPort
		c.apiOrigins = apiOrigins
		return c
	}
	func restoreRelayConfiguration()
	{
		do
		{
			if let active = try RelayVault.read("connection", as: RelaySavedConnection.self)
			{
				relayURL = active.server
				relayKey = active.credential
				relayAuthMode = active.authMode ?? "M"
				relayOwnerPath = active.ownerPath ?? ""
			}
			let active = RelaySavedConnection(
				server: relayURL, credential: relayKey, authMode: relayAuthMode, ownerPath: relayOwnerPath)
			if let data = UserDefaults.standard.data(forKey: "mac.configuration.v1")
			{
				guard data.count <= 4 * 1024 * 1024 else
				{
					throw PrintFailure("配置过大")
				}
				let c = try JSONDecoder().decode(MacConfiguration.self, from: data)
				try c.validate()
				relayURL = c.server
				relayAuthMode = c.authMode
				relayOwnerPath = c.ownerPath
				relayAllowHTTP = c.allowHTTP
				relayAutoConnect = c.autoConnect
				relayReconnectPrinter = c.reconnectPrinter
				relayPrinterID = c.printerID
				relayKeepReconnecting = c.keepReconnecting
				relayDefaultAutoPrint = c.defaultAutoPrint
				relayStyle = c.style
				relayDensity = c.density
				relayFeed = c.feed
				relayIncludeText = c.includeText
				relayIncludeSender = c.includeSender
				relayIncludeTime = c.includeTime
				relayIncludeID = c.includeID
				relayIncludeIP = c.includeIP
				relayIncludeUA = c.includeUA
				relayIncludeDevice = c.includeDevice
				if !c.templates.isEmpty
				{
					relayTemplates = c.templates
				}
				relayTemplateID = c.templateID
				settings = c.settings
				page = c.page
				engineer = c.engineer
				engineerPage = c.engineerPage
				showAllDevices = c.showAllDevices
				relayFilter = c.filter
				apiPort = c.apiPort
				apiOrigins = c.apiOrigins
			}
			if let draft = try RelayVault.read("connectionDraft", as: RelaySavedConnection.self),
				draft.server == relayURL, draft.authMode == relayAuthMode
			{
				relayKey = draft.credential
			}
			// Pending results always retain the last active recovery identity, never an unfinished draft.
			if try RelayVault.read("pending", as: RelaySavedClaim.self) != nil
			{
				relayURL = active.server
				relayKey = active.credential
				relayAuthMode = active.authMode ?? "M"
				relayOwnerPath = active.ownerPath ?? ""
				relayMessage = "恢复待回报任务的原连接；不会重打"
			}
		} catch
		{
			relayMessage = "配置恢复失败：" + error.localizedDescription
		}
	}
	func observeRelayConfiguration()
	{
		configurationObserver = objectWillChange.debounce(for: .milliseconds(350), scheduler: RunLoop.main).sink
		{
			[weak self] in Task
			{ @MainActor in self?.flushRelayConfiguration() }
		}
		if relayAutoConnect
		{
			Task
		{
			self.startRelay()
		} }
	}
	func flushRelayConfiguration()
	{
		do
		{
			let snapshot = configurationSnapshot()
			try snapshot.validate()
			let encoder = JSONEncoder()
			encoder.outputFormatting = [.sortedKeys]
			let bytes = try encoder.encode(snapshot)
			if UserDefaults.standard.data(forKey: "mac.configuration.v1") != bytes
			{
				UserDefaults.standard.set(bytes, forKey: "mac.configuration.v1")
			}
			let draft = RelaySavedConnection(
				server: relayURL, credential: relayAuthMode == "M" ? relayKey : "", authMode: relayAuthMode,
				ownerPath: relayOwnerPath)
			let secret = try encoder.encode(draft)
			if configurationDraftBytes != secret
			{
				try RelayVault.save("connectionDraft", draft)
				configurationDraftBytes = secret
			}
		} catch
		{
			if configurationLastError != error.localizedDescription
			{
				configurationLastError = error.localizedDescription
				log("CONFIG", error.localizedDescription)
			}
		}
	}
	func chooseRelayOwner()
	{
		let panel = NSOpenPanel()
		panel.allowsMultipleSelection = false
		panel.canChooseDirectories = false
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		relayOwnerPath = url.path
		flushRelayConfiguration()
	}
	func exportRelayConfiguration()
	{
		let panel = NSSavePanel()
		panel.nameFieldStringValue = "Paperang-MacClient-config.json"
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		do
		{
			guard URL(fileURLWithPath: relayOwnerPath).standardizedFileURL != url.standardizedFileURL else
			{
				throw PrintFailure("配置导出不能覆盖所有者私钥")
			}
			let c = configurationSnapshot()
			try c.validate()
			let encoder = JSONEncoder()
			let settings = try JSONSerialization.jsonObject(with: encoder.encode(c))
			let value: [String: Any] = [
				"format": "paperang-macclient-config", "schemaVersion": 1, "version": AppIdentity.version,
				"exportedAt": ISO8601DateFormatter().string(from: Date()), "settings": settings,
				"managementUI": UserDefaults.standard.dictionary(forKey: "mac.admin.ui.v1") ?? [:],
				"note": "不含原始凭证、私钥内容、会话或待回报任务；换机需单独迁移私钥并更新路径"
			]
			try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys]).write(
				to: url, options: .atomic)
			try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url.path)
			notice = "配置已导出（不含凭证或私钥）"
		} catch
		{
			self.error = error.localizedDescription
		}
	}
	func maybeReconnectRelayPrinter()
	{
		guard relayRunning, relayReconnectPrinter, !ready, !busy, peripheral == nil, central?.state == .poweredOn,
			Date() >= relayPrinterRetryAt, let id = UUID(uuidString: relayPrinterID)
		else
		{
			return
		}
		relayPrinterRetryAt = Date().addingTimeInterval(30)
		if let device = central.retrievePeripherals(withIdentifiers: [id]).first
		{
			known[id] = device
			connect(id)
		}
	}
}
