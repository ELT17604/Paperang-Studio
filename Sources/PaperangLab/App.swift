import SwiftUI
import ProtocolCore
import PrintCore
@main enum PaperangEntry {
 @MainActor static var offlineLab:Lab?
 @MainActor static func main(){
  if ProcessInfo.processInfo.arguments.contains("--api-offline") {
   FileHandle.standardError.write(Data("Paperang Studio offline API starting\n".utf8))
   offlineLab=Lab(offline:true)
   if offlineLab?.api==nil {FileHandle.standardError.write(Data(((offlineLab?.error ?? "API not enabled")+"\n").utf8));return}
   dispatchMain()
  } else {PaperangApp.main()}
 }
}
@MainActor struct PaperangApp:App {
 @NSApplicationDelegateAdaptor(StudioLifecycle.self) private var lifecycle
 @StateObject var lab:Lab
 init(){let model=Lab();_lab=StateObject(wrappedValue:model);StudioLifecycle.lab=model}
 var body:some Scene{Window("Paperang Studio",id:"main"){Content(lab:lab).frame(minWidth:1120,minHeight:760)}.defaultSize(width:1420,height:940).commands{
 CommandGroup(replacing:.appInfo){Button("About Paperang Studio",action:lab.showAbout);Button("检查 GitHub 更新…",action:ReleaseUpdates.check)}
 CommandGroup(after:.newItem){Divider();Button("进入托盘模式",action:lab.enterTray);Button("关于 Paperang Studio",action:lab.showAbout)}
 }
 MenuBarExtra(isInserted:Binding(get:{lab.trayMode},set:{if !$0{lab.exitTray()}})) {
  TrayMenu(lab:lab)
 } label: {Image(systemName:lab.trayPrintPaused || lab.relayWarning ? "exclamationmark.triangle.fill":"printer.fill").help("Paperang Studio v"+AppIdentity.version)}.menuBarExtraStyle(.menu)
 }
}
struct Content:View {
 @ObservedObject var lab:Lab
 var body:some View {
  NavigationSplitView {
   VStack(alignment:.leading,spacing:16){Label("Paperang Studio",systemImage:"printer.fill").font(.title2.bold());Text("P2 · 打印与调试").foregroundStyle(.secondary)
    List(selection:$lab.page){Label("打印工作台",systemImage:"doc.richtext").tag("打印工作台");Label("打印机",systemImage:"antenna.radiowaves.left.and.right").tag("打印机");Label("打印记录",systemImage:"clock").tag("打印记录");Label("纸带终端",systemImage:"terminal").tag("纸带终端");Label("本地 API",systemImage:"network").tag("本地 API");Label("远程打印",systemImage:"globe").tag("远程打印");if lab.engineer{Label("工程界面",systemImage:"wrench.and.screwdriver").tag("工程界面")}}
    Toggle("显示工程界面",isOn:$lab.engineer).onChange(of:lab.engineer){_,v in if !v && lab.page=="工程界面"{lab.page="打印工作台"}}
    Divider()
    Toggle("全局允许耗纸",isOn:$lab.allowPaper).toggleStyle(.switch).onChange(of:lab.allowPaper){_,v in if !v{lab.ttyAutoPrint=false;lab.relayAutoPrint=false;if lab.jobs.last?.status=="发送中"{lab.stopJob()}}}
    Text(lab.allowPaper ? "工作台 · 终端 · 远程 · API · 送纸已允许":"开启后，本次运行全局生效").font(.caption).foregroundStyle(.secondary)
    Divider();Label(lab.ready ? lab.connectedName:"未连接",systemImage:lab.ready ? "checkmark.circle.fill":"circle.dotted").foregroundStyle(lab.ready ? .green:.secondary)
    Text(lab.state).font(.caption).textSelection(.enabled)
    Text(lab.batterySummary).font(.caption)
    if lab.ready{Button("刷新状态",action:lab.refreshDevice).disabled(lab.busy);Button("断开连接",action:lab.disconnect)}else{Button("连接打印机"){lab.page="打印机"}}
       Divider()
    Button("进入托盘模式",action:lab.enterTray).help("维持蓝牙和网络监听，自动允许网络请求打印")
    Text("v"+AppIdentity.version).font(.caption2).foregroundStyle(.secondary)
    Text(AppIdentity.copyright).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
   }.padding().navigationSplitViewColumnWidth(220)
  } detail:{VStack(alignment:.leading,spacing:14){header
   if !lab.error.isEmpty {Label(lab.error,systemImage:"exclamationmark.triangle.fill").foregroundStyle(.red).textSelection(.enabled)}
   if !lab.notice.isEmpty {HStack{Text(lab.notice).font(.callout).foregroundStyle(.secondary);Spacer();Button{lab.notice=""}label:{Image(systemName:"xmark")}.buttonStyle(.plain)}}
   switch lab.page{case "打印机":devices;case "打印记录":history;case "工程界面":engineering;case "纸带终端":terminalPage;case "本地 API":apiPage;case "远程打印":relayPage;default:workspace}
  }.padding(22)}
 }
 var header:some View{HStack(alignment:.firstTextBaseline){VStack(alignment:.leading,spacing:4){Text(lab.page).font(.largeTitle.bold());Text(lab.page=="打印工作台" ? "先预览，再打印。所有内容固定在576点画布内。":"本地蓝牙 · 原生macOS").font(.callout).foregroundStyle(.secondary)};Spacer();if lab.busy{ProgressView().controlSize(.small)};Text("P2 / 300 DPI").font(.system(.caption,design:.monospaced)).padding(8).background(.quaternary,in:RoundedRectangle(cornerRadius:8))}}
 var workspace:some View{VStack(spacing:12){
  HStack{Menu("快捷预设"){ForEach(["默认","照片","标签","线稿"],id:\.self){name in Button(name){lab.applyPreset(name)}}};Button("导入预设",action:lab.importPreset);Button("保存预设",action:lab.exportPreset);Spacer();Text(lab.mediaSummary).font(.caption).foregroundStyle(.secondary)}.disabled(lab.busy)
  HSplitView {
   ScrollView{VStack(alignment:.leading,spacing:18){input;layout;imageProcessing;output}.padding(2)}.frame(minWidth:330,idealWidth:390,maxWidth:470).disabled(lab.busy)
   VStack(alignment:.leading,spacing:12){HStack{Label("最终黑白预览",systemImage:"eye").font(.headline);Spacer();if lab.rendering{ProgressView().controlSize(.small)}}
    Text(lab.previewSummary).font(.caption.monospaced()).foregroundStyle(.secondary)
    if !lab.previewError.isEmpty{Text(lab.previewError).foregroundStyle(.orange).textSelection(.enabled)}
    ScrollView([.vertical,.horizontal]){if let image=lab.preview{Image(nsImage:image).resizable().interpolation(.none).frame(width:460,height:460*image.size.height/576).background(.white).padding(16).shadow(color:.black.opacity(0.15),radius:5)}else{ContentUnavailableView("尚无预览",systemImage:"doc.viewfinder",description:Text("输入文字或导入图片，调整参数后自动更新"))}}.frame(maxWidth:.infinity,maxHeight:.infinity).background(.quaternary.opacity(0.5),in:RoundedRectangle(cornerRadius:12))
    HStack{Button("导出PNG"){lab.savePreview()};Button("导出位图数据"){lab.savePreview(raw:true)};Spacer();Text("1黑0白 · 72字节/行").font(.caption)}.disabled(lab.raster==nil || lab.busy)
   }.padding(.leading,18).frame(minWidth:430)
  }
  Divider();footer
 }}
 var input:some View{GroupBox{VStack(alignment:.leading,spacing:12){Picker("内容",selection:$lab.mode){Text("文字").tag("文字");Text("图片").tag("图片")}.pickerStyle(.segmented)
  if lab.mode=="文字"{TextEditor(text:$lab.text).font(.system(size:16)).frame(height:130).overlay(RoundedRectangle(cornerRadius:5).stroke(.quaternary));Text("支持中文、换行和自动折行；最多10000字符，最终高度不超过1400像素。").font(.caption).foregroundStyle(.secondary)}else{Button("选择图片…",action:lab.importImage);Text(lab.imageName).lineLimit(2).textSelection(.enabled);Text("PNG / JPEG / HEIC / TIFF / BMP / GIF首帧，透明区域先合成白底，自动处理照片方向。").font(.caption).foregroundStyle(.secondary)}
 }}label:{Label("打印内容",systemImage:"square.and.pencil")}}
 var layout:some View{GroupBox{VStack(alignment:.leading,spacing:12){
  if lab.mode=="文字" {Picker("字体",selection:$lab.settings.fontName){ForEach(["PingFang SC","Menlo","Helvetica","Times New Roman"],id:\.self){Text($0)}};slider("字号",value:$lab.settings.fontSize,range:8...96,step:1);Toggle("粗体",isOn:$lab.settings.bold);slider("行间距",value:$lab.settings.lineSpacing,range:0...40,step:1)}
  else{Picker("缩放方式",selection:$lab.settings.imageFit){ForEach(["适配宽度","保持像素","裁切填满"],id:\.self){Text($0)}};Picker("旋转",selection:$lab.settings.rotation){ForEach([0,90,180,270],id:\.self){Text("\($0)°").tag($0)}};if lab.settings.imageFit=="裁切填满"{integer("裁切高度 px",value:$lab.settings.cropHeight,range:24...1400)}}
  Picker("对齐",selection:$lab.settings.alignment){ForEach(["居左","居中","居右"],id:\.self){Text($0)}}.pickerStyle(.segmented)
  integer("左右最小边距 px",value:$lab.settings.marginX,range:0...120)
  integer("上下边距 px",value:$lab.settings.marginY,range:0...200)
  integer("内容宽度 px",value:$lab.settings.contentWidth,range:48...576)
  Text("左右边距×2 + 内容宽度 ≤ 576。超出时暂停打印并显示原因，不会静默改变宽度。").font(.caption).foregroundStyle(.secondary)
 }}label:{Label("排版与尺寸",systemImage:"ruler")}}
 var imageProcessing:some View{GroupBox{VStack(alignment:.leading,spacing:12){Picker("黑白方式",selection:$lab.settings.conversion){ForEach(["阈值","Floyd–Steinberg","有序抖动"],id:\.self){Text($0)}}
  slider("黑白阈值",value:$lab.settings.threshold,range:0...255,step:1)
  slider("亮度",value:$lab.settings.brightness,range:-100...100,step:1)
  slider("对比度",value:$lab.settings.contrast,range:0.2...3,step:0.1)
  slider("Gamma",value:$lab.settings.gamma,range:0.2...3,step:0.1)
  Toggle("黑白反相",isOn:$lab.settings.invert)
  Text("文字、线稿适合阈值；照片可尝试抖动。预览和实际发送共用同一份最终位图。").font(.caption).foregroundStyle(.secondary)
 }}label:{Label("黑白处理",systemImage:"circle.lefthalf.filled")}}
 var output:some View{GroupBox{VStack(alignment:.leading,spacing:12){integer("打印浓度",value:$lab.settings.density,range:0...100);integer("份数",value:$lab.settings.copies,range:1...20);integer("打印前送纸 行",value:$lab.settings.feedBefore,range:0...2000);integer("打印后送纸 行",value:$lab.settings.feedAfter,range:0...2000);Toggle("每份后都送纸",isOn:$lab.settings.feedBetweenCopies);Text("关闭时仅整批末尾送纸。当前机器确认150行便于撕取；过期纸测试默认浓度100。").font(.caption).foregroundStyle(.secondary);Button("仅应用浓度并回读",action:lab.applyDensity).disabled(!lab.ready)} }label:{Label("打印与送纸",systemImage:"printer")}}
 var footer:some View{VStack(alignment:.leading,spacing:10){if lab.busy{ProgressView(value:lab.progress);Text(lab.jobStatus).font(.caption)}else if !lab.jobs.isEmpty{Label(lab.jobStatus,systemImage:"checkmark.circle").font(.caption).foregroundStyle(.secondary)}
  HStack{Spacer();Button("仅送纸 \(lab.settings.feedAfter)行",action:lab.feedPaper).disabled(!lab.ready || lab.busy || !lab.allowPaper);if lab.busy{Button("停止并断开",role:.destructive,action:lab.stopJob)};Button("打印 \(lab.settings.copies)份",action:lab.submitJob).buttonStyle(.borderedProminent).disabled(!lab.canPrint)}
  Text(lab.busy ? "停止会阻止后续发送，无法撤回已进入打印机的内容。":"先检查盖子/纸张与浓度。发送完成后请核对纸面；300 DPI为固定规格，毫米尺寸尚未尺量校准。").font(.caption).foregroundStyle(.secondary)
 }}
 var devices:some View{VStack(alignment:.leading,spacing:14){HStack{Button(lab.scanning ? "扫描中…":"扫描附近设备",action:lab.scan).disabled(lab.scanning || lab.busy);Button("重新初始化扫描",action:lab.restartScanner).disabled(lab.ready || lab.busy || lab.peripheral != nil);Button("刷新电量与纸仓",action:lab.refreshDevice).disabled(!lab.ready || lab.busy);Toggle("展示全部蓝牙设备",isOn:$lab.showAllDevices)}
  Text(lab.scanSummary).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
  List(lab.visibleDevices){d in HStack{Image(systemName:d.name.contains("Paperang") ? "printer":"wave.3.right").font(.title2);VStack(alignment:.leading){Text(d.name).font(.headline);Text(d.rssi==0 || d.rssi==127 ? d.source:d.source+" · 信号 \(d.rssi) dBm").foregroundStyle(.secondary);Text(d.id.uuidString).font(.caption.monospaced()).textSelection(.enabled)};Spacer();Button("连接"){lab.connect(d.id)}.disabled(lab.busy)}}
  GroupBox("当前设备"){VStack(alignment:.leading,spacing:8){Text(lab.connectedName).font(.headline);Text(lab.mediaSummary);Text(lab.batterySummary);Text("充电：暂无独立实时字段 · LED控制：当前协议未确认").foregroundStyle(.secondary);Text("更多原始字段与GATT信息可在左侧开启工程界面查看。").font(.caption)}.frame(maxWidth:.infinity,alignment:.leading).padding(8)}
 }}
 var history:some View{VStack(alignment:.leading){Text("仅保存当前会话最近30项，不会自动重打失败任务。").foregroundStyle(.secondary);if lab.jobs.isEmpty{ContentUnavailableView("还没有打印记录",systemImage:"clock",description:Text("在工作台完成预览后发送一批打印"))}else{List(lab.jobs.reversed()){j in VStack(alignment:.leading,spacing:6){HStack{Text(j.title).bold();Spacer();Text(j.date,style:.time)};Text(j.detail);Text(j.status).foregroundStyle(.secondary)}}}}}
 var engineering:some View{VStack(alignment:.leading,spacing:12){Picker("工程页面",selection:$lab.engineerPage){ForEach(["设备字段","协议目录","原始指令","传输参数","会话日志"],id:\.self){Text($0)}}.pickerStyle(.segmented)
  switch lab.engineerPage{case "设备字段":fields;case "协议目录":catalog;case "原始指令":raw;case "传输参数":transport;default:logView}
 }}
 var fields:some View{VStack{HStack{Button("读取全部已知GET",action:lab.queryAll).disabled(!lab.ready || lab.busy);Button("导出会话JSON",action:lab.export);Spacer()}
  Table(lab.readings){TableColumn("字段"){Text($0.title)};TableColumn("值"){Text($0.value).textSelection(.enabled)};TableColumn("原始载荷"){Text($0.raw).font(.caption.monospaced()).textSelection(.enabled)};TableColumn("采样时间"){Text($0.time,style:.time)}}
  DisclosureGroup("GATT服务与原始读取"){ScrollView{Text(lab.services.joined(separator:"\n")).font(.caption.monospaced()).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)}.frame(height:170)}
  Text("ACK不是测量值。开盖位0010、合盖时缺纸位0001已在本机对照确认；其他位不猜测。ISSC/BT5050为蓝牙模块信息。").font(.caption).foregroundStyle(.secondary)
 }}
 var catalog:some View{VStack{TextField("搜索命令、说明、验证等级",text:$lab.search).textFieldStyle(.roundedBorder);List(Command.all.filter{lab.search.isEmpty || ($0.label+$0.kind+$0.evidence+$0.detail).localizedCaseInsensitiveContains(lab.search)}){c in VStack(alignment:.leading,spacing:6){HStack{Text(c.label).font(.headline.monospaced());Spacer();Text(c.kind);Text(c.evidence).foregroundStyle(.secondary)};Text(c.detail).textSelection(.enabled);if c.kind=="查询"{Button("查询"){lab.selected=c.code;lab.querySelected()}.disabled(!lab.ready || lab.busy)}}.padding(.vertical,5)};Text("02 cmd index lengthLE payload CRC32LE 03 · CRC会话seed=0 · FF03是额度流控").font(.caption.monospaced())}}
 var raw:some View{VStack(alignment:.leading,spacing:14){Text("协议指令工作台").font(.title2);Picker("只读目录",selection:$lab.selected){ForEach(Command.all.filter{$0.kind=="查询"}){Text($0.label).tag($0.code)}};Button("发送GET查询",action:lab.querySelected).disabled(!lab.ready || lab.busy)
  HStack{TextField("命令，例如04",text:$lab.rawCommand).frame(width:160);TextField("载荷，例如01",text:$lab.rawPayload)}.textFieldStyle(.roundedBorder)
  Toggle("解锁工程参数写入（每次发送前仍确认）",isOn:$lab.allowEngineeringWrites)
  Text("GET固定载荷01。已开放19浓度、1A送纸、1E关机时间、27间隙、2C纸张类型、14工厂模式、1B/22测试页的格式校验。工厂模式/间隙语义未验证，可能改变持久参数。固件更新、压缩位图及未知指令不开放。").font(.caption).foregroundStyle(.secondary)
  Button("构造帧并发送一次",action:lab.engineeringSend).disabled(!lab.ready || lab.busy);logView
 }.disabled(lab.busy)}
 var transport:some View{ScrollView{VStack(alignment:.leading,spacing:16){Text("这些参数影响实际分包与等待，不改变576点打印头宽度。默认值来自本机已成功路径。").foregroundStyle(.secondary)
  integer("图像载荷 bytes（72的倍数）",value:$lab.settings.packetBytes,range:72...1008,step:72)
  integer("BLE分段上限 bytes",value:$lab.settings.bleChunk,range:20...180)
  integer("BLE写入间隔 ms",value:$lab.settings.writePauseMS,range:5...200)
  integer("图像帧间隔 ms",value:$lab.settings.framePauseMS,range:0...1000)
  integer("单命令超时 秒",value:$lab.settings.timeoutSeconds,range:3...30)
  integer("每份数据后等待 秒",value:$lab.settings.finishWaitSeconds,range:1...15)
  Text("设备报告最大写入 \(lab.packetLimit) bytes；实际取设备与用户上限中的较小值。协议握手和CRC固定使用已验证参数，不接受无依据的宽度/固件修改。")
  Button("导出完整参数预设",action:lab.exportPreset)
 }}.disabled(lab.busy)}
 var logView:some View{VStack{HStack{TextField("过滤日志",text:$lab.logFilter).textFieldStyle(.roundedBorder);Button("导出JSON",action:lab.export);Button("清空显示"){lab.logs=[]}};ScrollView{LazyVStack(alignment:.leading,spacing:5){ForEach(lab.logs.reversed().filter{lab.logFilter.isEmpty || ($0.type+$0.text).localizedCaseInsensitiveContains(lab.logFilter)}){r in HStack(alignment:.top){Text(r.time,style:.time).frame(width:75,alignment:.leading);Text(r.type).foregroundStyle(r.type.contains("ERROR") ? .red:.blue).frame(width:110,alignment:.leading);Text(r.text).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)}.font(.system(size:11,design:.monospaced))}}}.background(.quaternary.opacity(0.3));Text("最新日志在上 · 最近4000条 · 完整帧、BLE分段、原始通知、CRC与错误").font(.caption).foregroundStyle(.secondary)}}
 func slider(_ title:String,value:Binding<Double>,range:ClosedRange<Double>,step:Double)->some View{VStack(alignment:.leading,spacing:4){HStack{Text(title);Spacer();Text(value.wrappedValue.formatted(.number.precision(.fractionLength(step<1 ? 1:0)))).monospacedDigit().foregroundStyle(.secondary)};Slider(value:value,in:range,step:step)}}
 func integer(_ title:String,value:Binding<Int>,range:ClosedRange<Int>,step:Int=1)->some View{HStack{Text(title);Spacer();TextField("",value:value,format:.number).multilineTextAlignment(.trailing).frame(width:65).textFieldStyle(.roundedBorder);Stepper("",value:value,in:range,step:step).labelsHidden()}}
}
