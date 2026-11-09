import SwiftUI
extension Content {
 var relayPage:some View {ScrollView{VStack(alignment:.leading,spacing:16){
  Label("远程打印中转",systemImage:"globe").font(.title2)
  TextField("中转服务器根地址，例如https://print.example.com",text:$lab.relayURL).textFieldStyle(.roundedBorder).disabled(lab.relayRunning || lab.relayFlight != nil)
  Picker("认证方式",selection:$lab.relayAuthMode){Text("配对的所有者私钥（推荐）").tag("owner");Text("M 设备密钥（兼容）").tag("M")}.disabled(lab.relayRunning || lab.relayFlight != nil)
  if lab.relayAuthMode=="owner"{HStack{TextField("所有者 JSON 文件路径",text:$lab.relayOwnerPath).textFieldStyle(.roundedBorder);Button("选择所有者文件",action:lab.chooseRelayOwner)}.disabled(lab.relayRunning || lab.relayFlight != nil)}else{SecureField("M 设备密钥 psr_…",text:$lab.relayKey).textFieldStyle(.roundedBorder).disabled(lab.relayRunning || lab.relayFlight != nil)}
  HStack{Text("配置自动保存；连接中的服务器与身份仅在重新连接时切换。").font(.caption);Spacer();Button("导出配置（不含凭证）",action:lab.exportRelayConfiguration)}
  Toggle("允许HTTP测试连接（公网请使用HTTPS）",isOn:$lab.relayAllowHTTP).disabled(lab.relayRunning)
  HStack{if lab.relayRunning{Button("断开中转",action:lab.stopRelay)}else{Button("连接中转",action:lab.startRelay).buttonStyle(.borderedProminent)};Toggle("自动打印",isOn:$lab.relayAutoPrint).disabled(!lab.relayRunning);Toggle("下次连接默认自动打印",isOn:$lab.relayDefaultAutoPrint)}
  HStack{Circle().fill(lab.relayOnline ? Color.green:Color.orange).frame(width:8,height:8);Text(lab.relayOnline ? "服务器已响应":"服务器未确认在线");if let time=lab.relayLastSeen{Text("最近响应");Text(time,style:.relative)}}.font(.caption)
  Text(lab.relayMessage).font(.caption).textSelection(.enabled)
  if !lab.relayOnline,!lab.relayJobs.isEmpty{Text("下方为上次成功刷新时的任务快照，当前状态未经服务器确认。").font(.caption).foregroundStyle(.orange)}
  if lab.relayFlight != nil,!lab.busy{Button("放弃未回报结果（不会重打）",action:lab.abandonRelayReport).disabled(lab.relayMutating)}
  HStack{Toggle("启动后自动连接中转",isOn:$lab.relayAutoConnect);Toggle("自动重连已选打印机（30秒）",isOn:$lab.relayReconnectPrinter)}
  Toggle("持续重连：本轮失败后等待一分钟再试",isOn:$lab.relayKeepReconnecting)
  Text("连接后每2秒上报。连接身份和待回报记录保存在 Keychain；所有者会话自动续期；正文在认领后读取。历史与预览需要 C 管理登录。").font(.caption).foregroundStyle(.secondary)
  relayKeyManager
  Divider()
  HStack{Picker("本地样式",selection:$lab.relayStyle){ForEach(["便签","小票","复古电传","访客纪念","自定义"],id:\.self){Text($0)}}
   if lab.relayStyle=="自定义"{Picker("已保存模板",selection:$lab.relayTemplateID){ForEach(lab.relayTemplates){Text($0.name).tag($0.id.uuidString)}}}
   Button("图形化样式编辑器"){lab.relayEditorOpen=true}
   integer("浓度",value:$lab.relayDensity,range:0...100);integer("批后送纸",value:$lab.relayFeed,range:0...2000)
  }
  HStack{Toggle("正文",isOn:$lab.relayIncludeText);Toggle("身份",isOn:$lab.relayIncludeSender);Toggle("时间",isOn:$lab.relayIncludeTime);Toggle("短编号",isOn:$lab.relayIncludeID);Toggle("IP",isOn:$lab.relayIncludeIP);Toggle("UA",isOn:$lab.relayIncludeUA);Toggle("设备信息",isOn:$lab.relayIncludeDevice)}.disabled(lab.relayStyle=="自定义")
  if lab.relayStyle=="自定义"{Text("自定义模板的字段、顺序、字体和留白由编辑器中的区块控制。任务编号默认不打印。").font(.caption).foregroundStyle(.secondary)}
  HStack{Text("任务列表").font(.headline);Spacer();Picker("状态",selection:$lab.relayFilter){ForEach(["全部","待处理","进行中","已发送","异常","已拒绝"],id:\.self){Text($0)}}.frame(width:210);TextField("搜索编号、身份或内容",text:$lab.relaySearch).textFieldStyle(.roundedBorder).frame(maxWidth:280)}
  List(selection:Binding<String?>(get:{lab.relaySelection.isEmpty ? nil:lab.relaySelection},set:{lab.relaySelection=$0 ?? ""})){
   ForEach(lab.relayVisibleJobs){job in HStack(alignment:.top){VStack(alignment:.leading,spacing:4){Text("#"+job.printableID).font(.system(.caption,design:.monospaced));Text(job.sender+" · "+String(job.text.prefix(70))).lineLimit(1)};Spacer();VStack(alignment:.trailing,spacing:4){Text(job.stateLabel).foregroundStyle(job.stateColor);Text(job.created,style:.time).font(.caption).foregroundStyle(.secondary)}}.padding(.vertical,5).tag(job.id)}
  }.frame(minHeight:180,idealHeight:220,maxHeight:260)
  if lab.relayVisibleJobs.isEmpty{Text("当前没有待处理任务；完整历史请使用 C 管理面板。").foregroundStyle(.secondary)}
  if let job=lab.selectedRelayJob{
   GroupBox("任务详情"){VStack(alignment:.leading,spacing:8){HStack{Text("#"+job.printableID).font(.headline.monospaced()).textSelection(.enabled);Text(job.stateLabel).foregroundStyle(job.stateColor);Spacer();Text(job.created,format:.dateTime).font(.caption)}
    DisclosureGroup("完整内部ID"){Text(job.id).font(.system(.caption,design:.monospaced)).textSelection(.enabled)}
    if let content=job.content {Text("黑白绘图 · \(content["width"] as? Int ?? 576) × \(content["height"] as? Int ?? 0) 点；自定义模板由正文区块放置图片及说明，其他区块保持模板顺序和样式；总高不得超过1400行。").font(.caption)}
    Text(job.text).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)
    if !job.hasFullText{Text("正在读取完整正文…").font(.caption).foregroundStyle(.secondary)}
    if !job.detail.isEmpty{Text(job.detail).font(.caption).foregroundStyle(.secondary)}
    DisclosureGroup("来源原始信息（浏览器字段未经独立验证）"){Text(job.rawMetadata).font(.system(.caption,design:.monospaced)).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)}
   }.frame(maxWidth:.infinity,alignment:.leading)}
  }
  HStack{Button("生成预览",action:lab.previewRelay).disabled(lab.selectedRelayJob==nil || lab.relayPreviewLoading);Button("确认打印",action:lab.printSelectedRelay).buttonStyle(.borderedProminent).disabled(lab.selectedRelayJob?.state != "queued" || !lab.relayOnline || !lab.ready || lab.busy || !lab.networkPrintAllowed || lab.relayMutating || lab.relayFlight != nil);Button("拒绝",role:.destructive,action:lab.rejectRelay).disabled(lab.selectedRelayJob?.state != "queued" || lab.relayMutating);Button("复制内容到工作台",action:lab.copyRelayToWorkspace).disabled(lab.selectedRelayJob==nil)}
  if lab.selectedRelayJob?.state=="sent"{Button("我已核对纸面，确认打印完成",action:lab.confirmRelayOutput).disabled(!lab.relayOnline || lab.relayMutating)}
  if lab.relayPreviewLoading{HStack{ProgressView().controlSize(.small);Text("正在获取内容并生成预览…").font(.caption)}}
  if lab.relayPreviewIsCurrent,let image=lab.relayPreview{Image(nsImage:image).resizable().interpolation(.none).scaledToFit().frame(maxWidth:460).background(.white)}
  Text("列表不会自动重打已发送或结果不明的任务。需要人工重印时先核对纸面，再复制到工作台。IP/UA默认不打印，短编号可选。").font(.caption).foregroundStyle(.secondary)
 }.padding(2)}
 .alert("中转连接已断开",isPresented:$lab.relayWarning){Button("知道了",role:.cancel){}}message:{Text(lab.relayMessage)}
 .sheet(isPresented:$lab.relayEditorOpen,onDismiss:lab.previewRelay){RelayLayoutEditor(lab:lab)}
 .onChange(of:lab.relaySelection){_,_ in lab.loadRelaySelection()}
 .onChange(of:lab.relayFilter){_,_ in lab.relaySelection=lab.relayVisibleJobs.first?.id ?? ""}
 .onChange(of:lab.relaySearch){_,_ in lab.relaySelection=lab.relayVisibleJobs.first?.id ?? ""}
 .onAppear{if !lab.relayTemplates.contains(where:{$0.id.uuidString==lab.relayTemplateID}){lab.relayTemplateID=lab.relayTemplates.first?.id.uuidString ?? ""};lab.loadRelaySelection()}
 }
}
