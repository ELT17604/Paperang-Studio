import SwiftUI
import AppKit
import Security
import Darwin
import PrintCore

@MainActor final class RelayAdminModel: ObservableObject
{
	let base: URL?
	let publishSession: (String) -> Void
	let clearPreview: () -> Void
	let ownerSelected: (String) -> Void
	@Published var credential = ""
	@Published var token = ""
	@Published var name = ""
	@Published var userID = ""
	@Published var role = "A"
	@Published var phrase = ""
	@Published var kind = "token"
	@Published var existingIdentity = ""
	@Published var creating = false
	@Published var section = "keys"
	@Published var message = ""
	@Published var selected = ""
	@Published var details = ""
	@Published var rows = [[String: Any]]()
	@Published var busy = false
	@Published var pageSize = 25
	@Published var hideRevoked = true
	@Published var pageNumber = 1
	@Published var hasNext = false
	@Published var hasPrevious = false
	@Published var detailText = ""
	@Published var detailImage: NSImage?
	@Published var detailRecord: [String: Any]?
	@Published var quotaTarget = "anonymous"
	@Published var quotaLoaded = false
	@Published var quotaJobs = ""
	@Published var quotaUnits = ""
	@Published var quotaLength = ""
	var loadedScope = ""
	var ownerPath = ""
	var operation: Task<Void, Never>?
	var operationID = UUID()
	var expiry: Task<Void, Never>?
	struct Page
	{
		let rows: [[String: Any]]
		let remaining: [[String: Any]]
		let cursor: Double?
	}
	var pages = [Page]()
	var pageIndex = -1
	var droppedPages = 0
	var current: Page?
	{
		pages.indices.contains(pageIndex) ? pages[pageIndex] : nil
	}
	var selectedRow: [String: Any]?
	{
		guard let index = Int(selected), rows.indices.contains(index) else
		{
			return nil
		}
		return rows[index]
	}
	var selectedID: String
	{
		selectedRow?["id"] as? String ?? selectedRow?["credential_id"] as? String ?? ""
	}
	var identityChoices: [(String, String)]
	{
		var values = [String: String]()
		for row in rows
		{
			if let id = row[section == "identities" ? "id" : "user_id"] as? String
			{
				values[id] = row["name"] as? String ?? id
			}
		}
		if !existingIdentity.isEmpty, values[existingIdentity] == nil
		{
			values[existingIdentity] = existingIdentity
		}
		return values.map
		{
			($0.key, $0.value)
		}.sorted
		{
			$0.1 < $1.1
		}
	}
	init(
		server: String, publishSession: @escaping (String) -> Void, clearPreview: @escaping () -> Void,
		ownerSelected: @escaping (String) -> Void
	)
	{
		base = try? RelayHTTP.root(server)
		self.publishSession = publishSession
		self.clearPreview = clearPreview
		self.ownerSelected = ownerSelected
		let prefs = UserDefaults.standard.dictionary(forKey: "mac.admin.ui.v1") ?? [:]
		section = prefs["section"] as? String ?? "keys"
		if !["keys", "identities", "sessions", "jobs", "audit"].contains(section)
		{
			section = "keys"
		}
		let size = prefs["pageSize"] as? Int ?? 25
		pageSize = [25, 50, 100].contains(size) ? size : 25
		hideRevoked = prefs["hideRevoked"] as? Bool ?? true
		userID = prefs["userID"] as? String ?? ""
	}
	func persistUI()
	{
		UserDefaults.standard.set(
			["section": section, "pageSize": pageSize, "hideRevoked": hideRevoked, "userID": userID],
			forKey: "mac.admin.ui.v1")
	}
	func perform(_ work: @escaping @MainActor () async throws -> Void)
	{
		guard !busy else
		{
			return
		}
		busy = true
		let id = UUID()
		operationID = id
		operation = Task
		{
			defer
			{
				if self.operationID == id
				{
					self.busy = false
					self.operation = nil
				}
			}
			do
			{
				try await work()
			} catch
			{
				if !Task.isCancelled
			{
				self.message = error.localizedDescription
			} }
		}
	}
	func request(_ path: String, _ body: [String: Any]? = nil) async throws -> [String: Any]
	{
		guard let base else
		{
			throw PrintFailure("管理地址需为 HTTPS 或本机 HTTP 根地址")
		}
		let active = token
		do
		{
			let result = try await RelayHTTP.request(base, body == nil ? "GET" : "POST", path, body, token: active)
			guard active == token else
			{
				throw CancellationError()
			}
			return result
		} catch let error as RelayHTTPError where error.status == 401
		{
			lock()
			message = "管理会话已失效，请重新登录。"
			throw error
		}
	}
	func lock()
	{
		operation?.cancel()
		operation = nil
		operationID = UUID()
		expiry?.cancel()
		expiry = nil
		token = ""
		publishSession("")
		clearPreview()
		credential = ""
		phrase = ""
		rows = []
		pages = []
		pageIndex = -1
		selected = ""
		details = ""
		detailText = ""
		detailImage = nil
		detailRecord = nil
		quotaLoaded = false
		loadedScope = ""
		busy = false
	}
	func accept(_ value: [String: Any]) async throws
	{
		guard value["role"] as? String == "C", let access = value["token"] as? String, !access.isEmpty else
		{
			throw PrintFailure("需要 C 管理会话")
		}
		token = access
		publishSession(access)
		credential = ""
		expiry?.cancel()
		let age = max(1, min(value["maxAge"] as? Int ?? 600, 600))
		expiry = Task
		{
			do
			{
				try await Task.sleep(for: .seconds(age))
				guard self.token == access else
				{
					return
				}
				self.lock()
				self.message = "管理会话已过期，请重新登录"
			} catch
			{
			}
		}
		try await refresh()
	}
	func login() async throws
	{
		try await accept(request("auth/login", ["credential": credential]))
	}
	func privateLogin() async throws
	{
		let panel = NSOpenPanel()
		panel.allowsMultipleSelection = false
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		try await privateLogin(path: url.path)
	}
	func privateLogin(path: String) async throws
	{
		guard let base else
		{
			throw PrintFailure("无效服务器地址")
		}
		let auth = try RelayOwnerAuthentication(path: path, server: base)
		let access = try await auth.token()
		try await accept(["role": "C", "token": access, "maxAge": 540])
		ownerPath = path
		ownerSelected(path)
		message = "私钥验证成功；管理与后台执行会话彼此独立"
	}
	func resetPages()
	{
		pages = []
		pageIndex = -1
		droppedPages = 0
		rows = []
		selected = ""
		details = ""
		detailText = ""
		detailImage = nil
		detailRecord = nil
		hasNext = false
		hasPrevious = false
		pageNumber = 1
		quotaLoaded = false
		loadedScope = ""
		persistUI()
	}
	func refresh() async throws
	{
		resetPages()
		try await nextPage()
	}
	func compact(_ row: [String: Any]) -> [String: Any]
	{
		let allowed: Set<String> = [
			"id", "shortID", "user_id", "name", "role", "device", "kind", "revoked", "created", "updated", "state",
			"credential_id", "browser_id", "expires", "action", "at", "target", "actor"
		]
		return row.filter
		{
			allowed.contains($0.key)
		}
	}
	func nextPage() async throws
	{
		if pages.indices.contains(pageIndex + 1)
		{
			pageIndex += 1
			showPage()
			return
		}
		var carry = current?.remaining ?? []
		var cursor = current?.cursor
		if current != nil && carry.isEmpty && cursor == nil
		{
			return
		}
		var output = [[String: Any]]()
		let targetSection = section
		while output.count < pageSize
		{
			if !carry.isEmpty
			{
				let count = min(pageSize - output.count, carry.count)
				output += carry.prefix(count)
				carry.removeFirst(count)
				if output.count == pageSize
				{
					break
				}
			}
			var body: [String: Any] = [
				"before": cursor ?? (section == "audit" ? Double(Int64.max) : 1e20), "limit": pageSize,
				"hide_revoked": hideRevoked
			]
			if section == "audit"
			{
				body["before"] = cursor.map
			{
				Int64($0)
			} ?? Int64.max }
			if section == "jobs"
			{
				body["user_id"] = userID
				body["summary"] = true
			}
			let property = section == "audit" ? "events" : section
			let value = try await request(section == "audit" ? "admin/audit" : "admin/" + section + "/search", body)
			guard targetSection == section else
			{
				throw CancellationError()
			}
			let raw = value[property] as? [[String: Any]] ?? []
			let next = (value["next"] as? NSNumber)?.doubleValue
			if let next, let cursor, !(next < cursor)
			{
				throw PrintFailure("无效分页游标")
			}
			cursor = raw.isEmpty ? nil : next
			carry = raw.filter
			{
				!hideRevoked || ((($0["revoked"] as? NSNumber)?.boolValue ?? false) == false)
			}.map(
				compact)
			if carry.isEmpty && cursor == nil
			{
				break
			}
		}
		guard !output.isEmpty || pages.isEmpty else
		{
			hasNext = false
			return
		}
		pages.append(Page(rows: output, remaining: carry, cursor: cursor))
		if pages.count > 12
		{
			pages.removeFirst()
			droppedPages += 1
		}
		pageIndex = pages.count - 1
		showPage()
	}
	func previousPage()
	{
		guard pageIndex > 0 else
		{
			return
		}
		pageIndex -= 1
		showPage()
	}
	func showPage()
	{
		guard let page = current else
		{
			return
		}
		rows = page.rows
		selected = ""
		details = ""
		detailText = ""
		detailImage = nil
		detailRecord = nil
		pageNumber = droppedPages + pageIndex + 1
		hasPrevious = pageIndex > 0
		hasNext = !page.remaining.isEmpty || page.cursor != nil
		quotaLoaded = false
		loadedScope = ""
	}
	func selectDetails() async throws
	{
		guard let row = selectedRow else
		{
			return
		}
		let selection = selected
		let category = section
		let value: [String: Any]
		if section == "jobs", let id = row["id"] as? String
		{
			let response = try await request("admin/jobs/search", ["job_id": id, "limit": 1])
			guard let record = (response["jobs"] as? [[String: Any]])?.first else
			{
				throw PrintFailure("任务不存在或已过期")
			}
			value = record
		} else
		{
			value = row
		}
		guard selected == selection, section == category else
		{
			return
		}
		details = try pretty(value)
		detailRecord = value
		detailText = value["text"] as? String ?? ""
		detailImage = nil
		if let job = RelayJob.decode(value), let raster = try job.verifiedRaster()
		{
			detailImage = raster.image
		}
	}
	func pretty(_ value: Any) throws -> String
	{
		String(
			decoding: try JSONSerialization.data(withJSONObject: value, options: [.prettyPrinted, .sortedKeys]),
			as: UTF8.self)
	}
	func create() async throws
	{
		var body: [String: Any] = ["name": name, "role": role, "kind": role == "M" ? "token" : kind]
		if !phrase.isEmpty, role != "M"
		{
			body["password"] = phrase
		}
		if !existingIdentity.isEmpty
		{
			body["user_id"] = existingIdentity
		}
		let value = try await request("admin/keys", body)
		creating = false
		phrase = ""
		try await refresh()
		details = try pretty(value)
		message = "新凭证仅显示本次，请复制保存后清除"
	}
	func rename() async throws
	{
		guard !selectedID.isEmpty else
		{
			throw PrintFailure("请先选择凭证")
		}
		_ = try await request("admin/keys/\(selectedID)/rename", ["name": name])
		try await refresh()
	}
	func rotate() async throws
	{
		guard !selectedID.isEmpty else
		{
			throw PrintFailure("请先选择凭证")
		}
		let value = try await request("admin/keys/\(selectedID)/rotate", phrase.isEmpty ? [:] : ["password": phrase])
		phrase = ""
		try await refresh()
		details = try pretty(value)
		message = "轮换已完成，旧会话失效；请保存新凭证"
	}
	func revoke() async throws
	{
		guard !selectedID.isEmpty else
		{
			throw PrintFailure("请先选择一条记录")
		}
		let id = selectedID
		let category = section
		let alert = NSAlert()
		alert.messageText = "吊销所选记录？"
		alert.informativeText = "身份吊销会拒绝排队任务，已进入打印机的数据不能撤回。"
		alert.addButton(withTitle: "吊销")
		alert.addButton(withTitle: "取消")
		guard alert.runModal() == .alertFirstButtonReturn else
		{
			return
		}
		if category == "sessions"
		{
			_ = try await request("admin/sessions/revoke", ["credential_id": id])
		} else if ["keys", "identities"].contains(category)
		{
			_ = try await request("admin/\(category)/\(id)/revoke", [:])
		} else
		{
			throw PrintFailure("此页面不支持吊销")
		}
		try await refresh()
	}
	func confirm() async throws
	{
		guard section == "jobs", !selectedID.isEmpty else
		{
			throw PrintFailure("请选择已发送任务")
		}
		_ = try await request("admin/jobs/\(selectedID)/confirm", [:])
		try await refresh()
	}
	func quotaScope() throws -> String
	{
		if quotaTarget != "selected"
		{
			return quotaTarget
		}
		guard ["keys", "identities"].contains(section), let row = selectedRow,
			let uid = row[section == "identities" ? "id" : "user_id"] as? String, UUID(uuidString: uid) != nil
		else
		{
			throw PrintFailure("请在凭证或身份页选择目标身份")
		}
		return "user:" + uid
	}
	func invalidateQuota()
	{
		quotaLoaded = false
		loadedScope = ""
		quotaJobs = ""
		quotaUnits = ""
		quotaLength = ""
	}
	func quotaRequest(_ write: Bool) async throws
	{
		let scope = try quotaScope()
		if write
		{
			guard quotaLoaded, loadedScope == scope else
		{
			throw PrintFailure("目标已变化，请先重新读取限额")
		} }
		var body: [String: Any] = ["scope": scope]
		if write
		{
			var limits = [String: Any]()
			for (field, text, max) in [
				("hourly_limit", quotaJobs, 1_000_000_000), ("hourly_chars", quotaUnits, 1_000_000_000),
				("max_length", quotaLength, 10_000)
			]
			{
				let t = text.trimmingCharacters(in: .whitespacesAndNewlines)
				if t.isEmpty
				{
					limits[field] = NSNull()
				} else
				{
					guard let number = Int(t), number >= 0, number <= max else
					{
						throw PrintFailure("限额需为范围内整数；空白继承，0 禁止")
					}
					limits[field] = number
				}
			}
			body["limits"] = limits
		}
		let result = try await request("admin/quotas/" + (write ? "set" : "get"), body)
		guard scope == (try? quotaScope()) else
		{
			invalidateQuota()
			return
		}
		let overrides = result["overrides"] as? [String: Any] ?? [:]
		quotaJobs = (overrides["hourly_limit"] as? NSNumber)?.stringValue ?? ""
		quotaUnits = (overrides["hourly_chars"] as? NSNumber)?.stringValue ?? ""
		quotaLength = (overrides["max_length"] as? NSNumber)?.stringValue ?? ""
		loadedScope = scope
		quotaLoaded = true
		details = try pretty(result)
		message = write ? "限额已保存，累计用量不清零" : "已读取限额、用量和重置时间"
	}
	func exportHistory() async throws
	{
		let panel = NSSavePanel()
		panel.nameFieldStringValue = "paperang-history.jsonl"
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		guard ownerPath.isEmpty || URL(fileURLWithPath: ownerPath).standardizedFileURL != url.standardizedFileURL else
		{
			throw PrintFailure("不能覆盖所有者私钥")
		}
		let temp = url.deletingLastPathComponent().appendingPathComponent(".paperang-export-" + UUID().uuidString)
		guard FileManager.default.createFile(atPath: temp.path, contents: nil, attributes: [.posixPermissions: 0o600])
		else
		{
			throw PrintFailure("无法创建导出文件")
		}
		defer
		{
			try? FileManager.default.removeItem(at: temp)
		}
		let handle = try FileHandle(forWritingTo: temp)
		defer
		{
			try? handle.close()
		}
		var cursor = 1e20
		var count = 0
		while true
		{
			let page = try await request("admin/jobs/export", ["user_id": userID, "before": cursor])
			let jobs = page["jobs"] as? [[String: Any]] ?? []
			if jobs.isEmpty
			{
				break
			}
			for job in jobs
			{
				var data = try JSONSerialization.data(withJSONObject: job)
				data.append(10)
				try handle.write(contentsOf: data)
				count += 1
			}
			guard let next = (page["next"] as? NSNumber)?.doubleValue, next < cursor else
			{
				throw PrintFailure("无效导出游标")
			}
			cursor = next
		}
		try handle.synchronize()
		try handle.close()
		guard Darwin.rename(temp.path, url.path) == 0 else
		{
			throw PrintFailure("无法保存导出文件")
		}
		message = "已导出 \(count) 条完整记录"
	}
	func trustPublicKey() async throws
	{
		let panel = NSOpenPanel()
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		let bytes = try RelayTransport.file(url, limit: 16384)
		details = try pretty(
			await request("admin/public-keys", ["name": name, "public_key": String(decoding: bytes, as: UTF8.self)]))
	}
	func generateOwner() async throws
	{
		guard !token.isEmpty else
		{
			throw PrintFailure("请先登录 C 权限")
		}
		let panel = NSSavePanel()
		panel.nameFieldStringValue = "paperang-owner.json"
		guard panel.runModal() == .OK, let url = panel.url else
		{
			return
		}
		try await createOwner(at: url)
	}
	func createOwner(at url: URL) async throws
	{
		guard !token.isEmpty else
		{
			throw PrintFailure("请先登录 C 权限")
		}
		let fd = Darwin.open(url.path, O_WRONLY | O_CREAT | O_EXCL, S_IRUSR | S_IWUSR)
		guard fd >= 0 else
		{
			throw PrintFailure("文件已存在或不能创建；不会覆盖已有私钥")
		}
		let file = FileHandle(fileDescriptor: fd, closeOnDealloc: true)
		var saved = false
		defer
		{
			try? file.close()
			if !saved
			{
				try? FileManager.default.removeItem(at: url)
			}
		}
		var error: Unmanaged<CFError>?
		guard
			let key = SecKeyCreateRandomKey(
				[kSecAttrKeyType: kSecAttrKeyTypeRSA, kSecAttrKeySizeInBits: 3072] as CFDictionary, &error),
			let pub = SecKeyCopyPublicKey(key), let raw = SecKeyCopyExternalRepresentation(key, &error) as Data?,
			let publicRaw = SecKeyCopyExternalRepresentation(pub, &error) as Data?
		else
		{
			throw PrintFailure("RSA 生成失败")
		}
		let publicPEM =
			"-----BEGIN RSA PUBLIC KEY-----\n" + publicRaw.base64EncodedString(options: .lineLength64Characters)
			+ "\n-----END RSA PUBLIC KEY-----"
		let entry = try await request(
			"admin/public-keys", ["name": name.isEmpty ? "Mac owner" : name, "public_key": publicPEM])
		guard let id = entry["id"] as? String, UUID(uuidString: id) != nil, let base else
		{
			throw PrintFailure("公钥登记未返回有效编号")
		}
		let body: [String: Any] = [
			"server": base.absoluteString, "id": id,
			"privateKeyPem": "-----BEGIN RSA PRIVATE KEY-----\n"
				+ raw.base64EncodedString(options: .lineLength64Characters) + "\n-----END RSA PRIVATE KEY-----"
		]
		try file.write(contentsOf: JSONSerialization.data(withJSONObject: body, options: .prettyPrinted))
		try file.synchronize()
		try file.close()
		saved = true
		message = "配对文件已保存；用私钥文件登录验证后可用于接收。"
	}
}

@MainActor struct RelaySecurityManager: View
{
	@StateObject private var model: RelayAdminModel
	init(
		server: String, previewCredential: Binding<String>, clearPreview: @escaping () -> Void,
		ownerSelected: @escaping (String) -> Void =
		{ _ in }
	)
	{
		_model = StateObject(
			wrappedValue: RelayAdminModel(
				server: server, publishSession:
				{
					previewCredential.wrappedValue = $0
				}, clearPreview: clearPreview,
				ownerSelected: ownerSelected))
	}
	var body: some View
	{
		VStack(alignment: .leading, spacing: 12)
		{
			HStack
			{
				SecureField("C 密钥或完整口令", text: $model.credential)
				Button("登录")
				{
					model.perform
				{
					try await model.login()
				} }
				Button("所有者私钥登录")
				{
					model.perform
				{
					try await model.privateLogin()
				} }
				Button("锁定", action: model.lock)
			}
			HSplitView
			{
				VStack(alignment: .leading)
				{
					Picker("管理类别", selection: $model.section)
					{
						Text("凭证").tag("keys")
						Text("身份").tag("identities")
						Text("会话").tag("sessions")
						Text("历史任务").tag("jobs")
						Text("审计").tag("audit")
					}.pickerStyle(.radioGroup)
					TextField("稳定用户 ID 筛选", text: $model.userID).textFieldStyle(.roundedBorder)
					Toggle("隐藏已吊销", isOn: $model.hideRevoked)
					Picker("每页", selection: $model.pageSize)
					{
						ForEach([25, 50, 100], id: \.self)
						{
							Text(String($0)).tag($0)
						}
					}
					Button("刷新")
					{
						model.perform
					{
						try await model.refresh()
					} }
					Button("新建凭证")
					{
						model.creating = true
					}
					Button("生成配对私钥")
					{
						model.perform
					{
						try await model.generateOwner()
					} }
					Button("登记公钥")
					{
						model.perform
					{
						try await model.trustPublicKey()
					} }
					Button("导出历史 JSONL")
					{
						model.perform
					{
						try await model.exportHistory()
					} }
				}.frame(minWidth: 180, maxWidth: 240, maxHeight: .infinity, alignment: .topLeading)
				VStack(alignment: .leading)
				{
					HStack
					{
						Button("上一页", action: model.previousPage).disabled(!model.hasPrevious)
						Text("第 \(model.pageNumber) 页")
						Button("下一页")
						{
							model.perform
						{
							try await model.nextPage()
						} }.disabled(!model.hasNext)
					}
					List(selection: $model.selected)
					{
						ForEach(model.rows.indices, id: \.self)
						{
							i in
							let row = model.rows[i]
							VStack(alignment: .leading)
							{
								Text(
									String(
										describing: row["name"] ?? row["shortID"] ?? row["id"] ?? row["credential_id"]
											?? "记录"))
								Text(String(describing: row["role"] ?? row["state"] ?? row["action"] ?? "")).font(
									.caption
								).foregroundStyle(.secondary)
							}.tag(String(i))
						}
					}.frame(minHeight: 240, maxHeight: 360)
					TextField("新展示名称", text: $model.name)
					HStack
					{
						Button("改名")
						{
							model.perform
						{
							try await model.rename()
						} }.disabled(
							model.section != "keys" || model.selected.isEmpty)
						Button("轮换")
						{
							model.perform
						{
							try await model.rotate()
						} }.disabled(
							model.section != "keys" || model.selected.isEmpty)
						Button("吊销", role: .destructive)
						{
							model.perform
						{
							try await model.revoke()
						} }.disabled(
							!["keys", "identities", "sessions"].contains(model.section) || model.selected.isEmpty)
					}
					Button("确认纸面完成")
					{
						model.perform
					{
						try await model.confirm()
					} }.disabled(
						model.section != "jobs" || model.selectedRow?["state"] as? String != "sent")
					Button("用于新凭证的现有身份")
					{
						model.existingIdentity =
							model.selectedRow?[model.section == "identities" ? "id" : "user_id"] as? String ?? ""
						model.creating = true
					}.disabled(!["keys", "identities"].contains(model.section) || model.selected.isEmpty)
				}.frame(minWidth: 230, maxWidth: 400, maxHeight: .infinity, alignment: .topLeading)
				ScrollView
				{
					VStack(alignment: .leading, spacing: 8)
					{
						Text("记录详情").font(.headline)
						if !model.detailText.isEmpty
						{
							Text(model.detailText).textSelection(.enabled)
						}
						if let image = model.detailImage
						{
							Image(nsImage: image).resizable().interpolation(.none).scaledToFit().frame(maxWidth: 460)
								.background(.white)
						}
						DisclosureGroup("原始 JSON / 新凭证（仅本次显示）")
						{
							Text(model.details).font(.system(.caption, design: .monospaced)).textSelection(.enabled)
						}
						Button("清除详情")
						{
							model.details = ""
							model.detailText = ""
							model.detailImage = nil
							model.detailRecord = nil
						}
						quotaEditor
					}.padding(6)
				}.frame(minWidth: 280)
			}.frame(height: 520, alignment: .top).disabled(model.token.isEmpty || model.busy)
			if model.busy
			{
				ProgressView().controlSize(.small)
			}
			Text(model.message).font(.caption).textSelection(.enabled)
			Text("C 管理与后台接收独立；十分钟锁定。列表只缓存最多12页摘要，正文/图片仅选中时读取。口令不限复杂度，空白随机生成。").font(.caption).foregroundStyle(
				.secondary)
		}.onChange(of: model.section)
		{ _, _ in changedFilter() }.onChange(of: model.pageSize)
		{ _, _ in changedFilter()
		}.onChange(of: model.hideRevoked)
		{ _, _ in changedFilter() }.onChange(of: model.userID)
		{
			_, _ in
			model.resetPages()
		}
		.onChange(of: model.selected)
		{
			_, _ in
			model.invalidateQuota()
			model.perform
			{
				try await model.selectDetails()
			}
		}
		.onDisappear
		{
			model.persistUI()
			model.lock()
		}
		.sheet(isPresented: $model.creating)
		{
			VStack(alignment: .leading, spacing: 12)
			{
				Text("新建凭证").font(.title2)
				TextField("展示名称", text: $model.name)
				Picker("权限", selection: $model.role)
				{
					ForEach(["A", "B", "C", "M"], id: \.self)
				{
					Text($0)
				} }
				Picker("种类", selection: $model.kind)
				{
					Text("随机密钥").tag("token")
					Text("口令").tag("passphrase")
				}.disabled(model.role == "M")
				SecureField("自定义口令（空白随机生成）", text: $model.phrase)
				Picker("身份", selection: $model.existingIdentity)
				{
					Text("由服务器新建身份").tag("")
					ForEach(model.identityChoices, id: \.0)
					{ id, name in Text(name + " · " + id).tag(id) }
				}
				Text("身份和凭证编号由服务器生成；选择现有身份可保留用量及历史关联。").font(.caption)
				HStack
				{
					Button("取消")
					{
						model.phrase = ""
						model.creating = false
					}
					Button("创建")
					{
						model.perform
					{
						try await model.create()
					} }.disabled(
						model.busy || model.name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
				}
			}.padding(20).frame(width: 510)
		}
	}
	private func changedFilter()
	{
		model.resetPages()
		if !model.token.isEmpty
		{
			model.perform
		{
			try await model.refresh()
		} }
	}
	private var quotaEditor: some View
	{
		DisclosureGroup("用量与限额")
		{
			VStack(alignment: .leading)
			{
				Picker("对象", selection: $model.quotaTarget)
				{
					Text("匿名默认").tag("anonymous")
					Text("注册默认").tag("authenticated")
					Text("所选身份").tag("selected")
				}.onChange(of: model.quotaTarget)
				{ _, _ in model.invalidateQuota() }
				TextField("每小时任务数（空白继承）", text: $model.quotaJobs)
				TextField("每小时内容量（空白继承）", text: $model.quotaUnits)
				TextField("单次内容量（空白继承）", text: $model.quotaLength)
				HStack
				{
					Button("读取")
					{
						model.perform
					{
						try await model.quotaRequest(false)
					} }
					Button("保存")
					{
						model.perform
					{
						try await model.quotaRequest(true)
					} }.disabled(!model.quotaLoaded)
				}
				Text("0 禁止，空白继承；内容量为文字字符+图片行数。切换对象后必须重新读取，轮换不清零。").font(.caption)
			}
		}
	}
}
