import SwiftUI

extension Content
{
	var relayPage: some View
	{
		VStack(alignment: .leading, spacing: 16)
		{
			relayConnectionSummary
			Picker("远程打印页面", selection: $lab.relayTab)
			{
				ForEach(["任务", "连接", "样式", "管理"], id: \.self)
				{
					Text($0)
				}
			}.pickerStyle(.segmented)
			ScrollView
			{
				VStack(alignment: .leading, spacing: 16)
				{
					switch lab.relayTab
					{
					case "连接": relayConnectionSettings
					case "样式": relayStyleSettings
					case "管理": EmptyView()
					default: relayTaskList
					}
					relayManagement
						.opacity(lab.relayTab == "管理" ? 1 : 0)
						.frame(height: lab.relayTab == "管理" ? nil : 0)
						.clipped().allowsHitTesting(lab.relayTab == "管理").accessibilityHidden(lab.relayTab != "管理")
				}.padding(2).frame(maxWidth: .infinity, alignment: .leading)
			}
		}
		.alert("中转连接已断开", isPresented: $lab.relayWarning)
		{
			Button("知道了", role: .cancel)
			{
			}
		} message:
		{
			Text(lab.relayMessage)
		}
		.sheet(isPresented: $lab.relayEditorOpen, onDismiss: lab.previewRelay)
		{
			RelayLayoutEditor(lab: lab)
		}
		.onChange(of: lab.relaySelection)
		{ _, _ in lab.loadRelaySelection() }
		.onChange(of: lab.relayFilter)
		{ _, _ in lab.relaySelection = lab.relayVisibleJobs.first?.id ?? "" }
		.onChange(of: lab.relaySearch)
		{ _, _ in lab.relaySelection = lab.relayVisibleJobs.first?.id ?? "" }
		.onAppear
		{
			if !lab.relayTemplates.contains(where:
			{
				$0.id.uuidString == lab.relayTemplateID
			})
			{
				lab.relayTemplateID = lab.relayTemplates.first?.id.uuidString ?? ""
			}
			lab.loadRelaySelection()
		}
	}
	private var relayConnectionSummary: some View
	{
		GroupBox
		{
			HStack(alignment: .top, spacing: 12)
			{
				Image(systemName: lab.relayOnline ? "checkmark.circle.fill" : "network")
					.foregroundStyle(lab.relayOnline ? .green : .secondary).font(.title2)
				VStack(alignment: .leading, spacing: 4)
				{
					Text(lab.relayOnline ? "中转在线" : lab.relayRunning ? "正在连接中转" : "中转未连接").font(.headline)
					Text(lab.relayMessage).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
					if let time = lab.relayLastSeen
					{
						Text("最近响应：\(time.formatted(date: .omitted, time: .standard))").font(.caption).foregroundStyle(
							.secondary)
					}
				}
				Spacer()
				if lab.relayRunning
				{
					Button("断开", action: lab.stopRelay)
				} else
				{
					Button("连接中转", action: lab.startRelay).buttonStyle(.borderedProminent)
				}
			}.frame(maxWidth: .infinity, alignment: .leading).padding(4)
		}
	}
	@ViewBuilder private var relayConnectionSettings: some View
	{
		GroupBox("服务器与身份")
		{
			VStack(alignment: .leading, spacing: 12)
			{
				TextField("服务器根地址 · https://print.example.com", text: $lab.relayURL)
				Picker("认证方式", selection: $lab.relayAuthMode)
				{
					Text("所有者私钥").tag("owner")
					Text("M 设备密钥（兼容）").tag("M")
				}
				if lab.relayAuthMode == "owner"
				{
					HStack
					{
						TextField("所有者 JSON 文件路径", text: $lab.relayOwnerPath)
						Button("选择文件…", action: lab.chooseRelayOwner)
					}
				} else
				{
					SecureField("M 设备密钥 psr_…", text: $lab.relayKey)
				}
				Toggle("允许 HTTP 连接", isOn: $lab.relayAllowHTTP)
				Text("公网使用 HTTPS。连接中的服务器与身份在重新连接后应用。").font(.caption).foregroundStyle(.secondary)
			}.textFieldStyle(.roundedBorder).padding(6).disabled(lab.relayRunning || lab.relayFlight != nil)
		}
		GroupBox("接收与重连")
		{
			VStack(alignment: .leading, spacing: 12)
			{
				Toggle("启动后自动连接中转", isOn: $lab.relayAutoConnect)
				Toggle("连接后默认自动打印", isOn: $lab.relayDefaultAutoPrint)
				Toggle("自动重连已选打印机", isOn: $lab.relayReconnectPrinter)
				Toggle("本轮重试失败后，一分钟后继续连接", isOn: $lab.relayKeepReconnecting)
				Text("中转每 2 秒刷新。打印机每 30 秒尝试重连；全局耗纸授权仍由侧栏控制。").font(.caption).foregroundStyle(.secondary)
			}.frame(maxWidth: .infinity, alignment: .leading).padding(6)
		}
		HStack
		{
			Text("配置自动保存在本机").font(.caption).foregroundStyle(.secondary)
			Spacer()
			Button("导出配置…", action: lab.exportRelayConfiguration)
		}
		if lab.relayFlight != nil, !lab.busy
		{
			Button("放弃未回报结果（不会重打）", action: lab.abandonRelayReport).disabled(lab.relayMutating)
		}
	}
	@ViewBuilder private var relayStyleSettings: some View
	{
		GroupBox("纸条样式")
		{
			VStack(alignment: .leading, spacing: 14)
			{
				Picker("样式", selection: $lab.relayStyle)
				{
					ForEach(["便签", "小票", "复古电传", "访客纪念", "自定义"], id: \.self)
					{
						Text($0)
					}
				}
				if lab.relayStyle == "自定义"
				{
					Picker("模板", selection: $lab.relayTemplateID)
					{
						ForEach(lab.relayTemplates)
						{
							Text($0.name).tag($0.id.uuidString)
						}
					}
				}
				Button("打开图形化样式编辑器…")
				{
					lab.relayEditorOpen = true
				}
				Divider()
				integer("打印浓度", value: $lab.relayDensity, range: 0...100)
				integer("批后送纸（行）", value: $lab.relayFeed, range: 0...2000)
			}.padding(6)
		}
		GroupBox("纸面附加信息")
		{
			LazyVGrid(
				columns: [GridItem(.adaptive(minimum: 150), alignment: .leading)], alignment: .leading, spacing: 12
			)
			{
				Toggle("正文", isOn: $lab.relayIncludeText)
				Toggle("投稿身份", isOn: $lab.relayIncludeSender)
				Toggle("时间", isOn: $lab.relayIncludeTime)
				Toggle("短编号", isOn: $lab.relayIncludeID)
				Toggle("IP 地址", isOn: $lab.relayIncludeIP)
				Toggle("User Agent", isOn: $lab.relayIncludeUA)
				Toggle("浏览器信息", isOn: $lab.relayIncludeDevice)
			}.padding(6).disabled(lab.relayStyle == "自定义")
		}
		Text(lab.relayStyle == "自定义" ? "自定义模板的字段、顺序和留白由编辑器中的区块控制。" : "文字和绘图共用样式。IP、UA 和短编号默认不打印。").font(.caption)
			.foregroundStyle(.secondary)
	}
	@ViewBuilder private var relayManagement: some View
	{
		Text("管理凭证、配额与完整历史").font(.headline)
		Text("使用 C 凭证或所有者私钥登录。管理会话独立于后台接收。").font(.callout).foregroundStyle(.secondary)
		RelaySecurityManager(
			server: lab.relayURL, previewCredential: $lab.relayAdminKey, clearPreview:
			{
				lab.clearRelayPrivateViews()
			},
			ownerSelected:
			{
				lab.relayOwnerPath = $0
				lab.relayAuthMode = "owner"
				lab.flushRelayConfiguration()
			}
		).id(lab.relayURL)
	}
	@ViewBuilder private var relayTaskList: some View
	{
		HStack
		{
			Text("接收队列").font(.headline)
			Spacer()
			Toggle("自动打印", isOn: $lab.relayAutoPrint).disabled(!lab.relayRunning).toggleStyle(.switch)
		}
		HStack
		{
			Picker("状态", selection: $lab.relayFilter)
			{
				ForEach(["全部", "待处理", "进行中", "已发送", "异常", "已拒绝"], id: \.self)
				{
					Text($0)
				}
			}.frame(width: 190)
			TextField("搜索编号、身份或内容", text: $lab.relaySearch).textFieldStyle(.roundedBorder)
		}
		if !lab.relayOnline, !lab.relayJobs.isEmpty
		{
			Label("离线快照，任务状态尚未刷新", systemImage: "clock.badge.exclamationmark").font(.caption).foregroundStyle(.orange)
		}
		List(
			selection: Binding<String?>(
				get:
				{
					lab.relaySelection.isEmpty ? nil : lab.relaySelection
				}, set:
				{
					lab.relaySelection = $0 ?? ""
				})
		)
		{
			ForEach(lab.relayVisibleJobs)
			{
				job in
				HStack
				{
					VStack(alignment: .leading, spacing: 4)
					{
						Text("#" + job.printableID).font(.system(.callout, design: .monospaced))
						Text(job.hasFullText ? job.sender + " · " + String(job.text.prefix(70)) : "等待认领 · 正文受权限保护")
							.font(.caption).foregroundStyle(.secondary).lineLimit(1)
					}
					Spacer()
					VStack(alignment: .trailing, spacing: 4)
					{
						Text(job.stateLabel).foregroundStyle(job.stateColor)
						Text(job.created, style: .time).font(.caption).foregroundStyle(.secondary)
					}
				}.padding(.vertical, 5).tag(job.id)
			}
		}.frame(height: 220)
		if lab.relayVisibleJobs.isEmpty
		{
			ContentUnavailableView("暂无排队任务", systemImage: "tray", description: Text("完整任务历史请进入管理页面"))
		}
		if let job = lab.selectedRelayJob
		{
			relayTaskDetail(job)
		}
		ViewThatFits(in: .horizontal)
		{
			HStack
			{
				relayTaskActions
			}
			VStack(alignment: .leading, spacing: 8)
			{
				relayTaskActions
			}
		}
		if lab.relayPreviewLoading
		{
			HStack
			{
				ProgressView().controlSize(.small)
				Text("正在生成预览…").font(.caption)
			}
		}
		if lab.relayPreviewIsCurrent, let image = lab.relayPreview
		{
			Image(nsImage: image).resizable().interpolation(.none).scaledToFit().frame(maxWidth: 460).background(.white)
		}
		Text("预览及复制正文需要 C 管理登录。打印时先认领再读取内容；不自动重打结果不明的任务。").font(.caption).foregroundStyle(.secondary)
	}
	private func relayTaskDetail(_ job: RelayJob) -> some View
	{
		GroupBox("任务详情")
		{
			VStack(alignment: .leading, spacing: 8)
			{
				HStack
				{
					Text("#" + job.printableID).font(.headline.monospaced()).textSelection(.enabled)
					Spacer()
					Text(job.stateLabel).foregroundStyle(job.stateColor)
				}
				Text(job.created, format: .dateTime).font(.caption).foregroundStyle(.secondary)
				if job.hasFullText
				{
					Text(job.text).textSelection(.enabled)
				}
				if let content = job.content
				{
					Text("黑白绘图 · \(content["width"] as? Int ?? 576) × \(content["height"] as? Int ?? 0) 点").font(
						.caption)
				}
				if !job.detail.isEmpty
				{
					Text(job.detail).font(.caption).foregroundStyle(.secondary)
				}
				DisclosureGroup("原始信息")
				{
					Text(job.id + "\n" + job.rawMetadata).font(.caption.monospaced()).textSelection(.enabled)
				}
			}.frame(maxWidth: .infinity, alignment: .leading).padding(4)
		}
	}
	@ViewBuilder private var relayTaskActions: some View
	{
		Button("生成预览", action: lab.previewRelay).disabled(lab.selectedRelayJob == nil || lab.relayPreviewLoading)
		Button("打印所选任务", action: lab.printSelectedRelay).buttonStyle(.borderedProminent).disabled(
			lab.selectedRelayJob?.state != "queued" || !lab.relayOnline || !lab.ready || lab.busy
				|| !lab.networkPrintAllowed || lab.relayMutating || lab.relayFlight != nil)
		Button("拒绝", role: .destructive, action: lab.rejectRelay).disabled(
			lab.selectedRelayJob?.state != "queued" || !lab.relayOnline || lab.relayMutating)
		Button("复制到工作台", action: lab.copyRelayToWorkspace).disabled(lab.selectedRelayJob == nil)
		if lab.selectedRelayJob?.state == "sent"
		{
			Button("确认纸面完成", action: lab.confirmRelayOutput).disabled(!lab.relayOnline || lab.relayMutating)
		}
	}
}
