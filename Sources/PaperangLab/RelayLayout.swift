import SwiftUI
import AppKit
import CryptoKit
import PrintCore
import UniformTypeIdentifiers

extension RelayJob {
 /// A single decoder is shared by template parts, previews and the built-in renderer.
 func verifiedRaster()throws->Raster? {
  guard let content else{return nil}
   guard content["type"] as? String=="raster1",content["schemaVersion"] as? Int==1,content["width"] as? Int==576,let height=content["height"] as? Int,content["bitOrder"] as? String=="msb-first",content["blackIsOne"] as? Bool==true,content["encoding"] as? String=="base64",let encoded=content["data"] as? String,encoded.utf8.count<=134400,let data=Data(base64Encoded:encoded),let checksum=content["sha256"] as? String,SHA256.hash(data:data).map({String(format:"%02x",$0)}).joined()==checksum else{throw PrintFailure("远程内容类型、尺寸或校验和不正确，请更新客户端或重新提交")}
  return try Raster.packed(data,height:height)
 }
 var layoutSignature:String {
  id+shortID+text+rawMetadata+String(created.timeIntervalSince1970)+String(hasFullText)+(content?["sha256"] as? String ?? "")+String(content?["data"] != nil)
 }
}

struct RelayBlock:Codable,Identifiable,Equatable {
 var id=UUID();var kind="正文";var text="";var enabled=true;var font="PingFang SC";var size=24.0;var bold=false;var alignment="居左";var margin=12;var gap=8
 static let kinds=["标题","正文","投稿身份","时间","任务编号","IP","UA","浏览器信息","分隔线","自定义文字"]
 static func make(_ kind:String,_ text:String="",size:Double=24)->Self {var b=Self();b.kind=kind;b.text=text;b.size=size;return b}
 func value(_ job:RelayJob)->String {
  let browser=job.metadata["browser"] as? [String:Any] ?? [:]
  let fields=["text":job.text,"sender":job.sender,"id":job.printableID,"time":ISO8601DateFormatter().string(from:job.created),"ip":job.metadata["ip"] as? String ?? "unknown","ua":job.metadata["userAgent"] as? String ?? "unknown","device":browser.keys.sorted().map{"\($0)=\(browser[$0]!)"}.joined(separator:" · ")]
  switch kind {
  case "正文":return fields["text"]!
  case "投稿身份":return "From: "+fields["sender"]!
  case "时间":return fields["time"]!
  case "任务编号":return "#"+fields["id"]!
  case "IP":return "IP: "+fields["ip"]!
  case "UA":return "UA: "+fields["ua"]!
  case "浏览器信息":return fields["device"]!
  case "分隔线":return "────────────────────────"
  default:
   // Replace placeholders in the template once; never expand tokens inside visitor text.
   let re=try! NSRegularExpression(pattern:"\\{\\{(text|sender|id|time|ip|ua|device)\\}\\}")
   let ns=text as NSString;var out="";var position=0
   for m in re.matches(in:text,range:NSRange(location:0,length:ns.length)){out+=ns.substring(with:NSRange(location:position,length:m.range.location-position));out+=fields[ns.substring(with:m.range(at:1))] ?? "";position=m.range.location+m.range.length}
   out+=ns.substring(from:position);return out
  }
 }
}
struct RelayTemplate:Codable,Identifiable,Equatable {
 var layoutSignature:String {let encoder=JSONEncoder();encoder.outputFormatting=[.sortedKeys];return (try? encoder.encode(self).base64EncodedString()) ?? "invalid-template"}
 var id=UUID();var version=1;var name="我的样式";var blocks=[RelayBlock]()
 static let defaultsKey="relay.templates.v1"
 static func preset(_ name:String)->Self {
  var t=Self();t.name=name
  var body=RelayBlock.make("正文");var from=RelayBlock.make("投稿身份",size:16);var time=RelayBlock.make("时间",size:14)
  switch name {
  case "小票":body.font="Menlo";body.size=19;from.font="Menlo";t.blocks=[.make("标题","PAPERANG INBOX",size:25),.make("分隔线",size:16),body,.make("分隔线",size:16),from,time]
  case "复古电传":body.font="Menlo";body.size=15;from.font="Menlo";time.font="Menlo";t.blocks=[.make("标题","[ INCOMING TRANSMISSION ]",size:17),from,time,body,.make("标题","[ END ]",size:15)]
  case "访客纪念":body.size=27;body.alignment="居中";from.alignment="居中";time.alignment="居中";var title=RelayBlock.make("标题","A NOTE TO KEEP",size:35);title.alignment="居中";title.bold=true;t.blocks=[title,body,from,time]
  default:t.blocks=[body,from,time]
  }
  var identifier=RelayBlock.make("任务编号",size:14);identifier.enabled=false;t.blocks.append(identifier)
  return t
 }
 static func load()->[Self] {if let data=UserDefaults.standard.data(forKey:defaultsKey),let value=try? JSONDecoder().decode([Self].self,from:data),!value.isEmpty,value.count<=30,value.allSatisfy({(try? $0.validate()) != nil}){return value};return ["便签","小票","复古电传","访客纪念"].map(preset)}
 func validate()throws {
  guard version==1,!name.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty,name.count<=80,!blocks.isEmpty,blocks.count<=24,Set(blocks.map(\.id)).count==blocks.count else{throw PrintFailure("模板需有名称、1–24个区块，且编号不能重复")}
  guard blocks.contains(where:{$0.enabled}) else{throw PrintFailure("至少启用一个区块")}
  for b in blocks {guard RelayBlock.kinds.contains(b.kind),b.text.count<=2000,b.font.count<=120,(8...96).contains(b.size),(0...120).contains(b.margin),(0...200).contains(b.gap),["居左","居中","居右"].contains(b.alignment) else{throw PrintFailure("区块参数超出范围")}}
 }
 func parts(_ job:RelayJob,settings:PrintSettings)throws->[RelayRenderedBlock] {
  try validate();let image=try job.verifiedRaster();var output=[RelayRenderedBlock]()
  for block in blocks where block.enabled {
   let text=block.value(job);let hasText = !text.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty
   if !hasText && !(block.kind=="正文" && image != nil){continue}
   var s=settings;s.fontName=block.font;s.fontSize=block.size;s.bold=block.bold;s.alignment=block.alignment;s.marginX=block.margin;s.contentWidth=576-block.margin*2;s.marginY=0;s.lineSpacing=3
   let r:Raster
   if block.kind=="正文",let image {
    let fitted=try image.fitting(margin:block.margin)
    if hasText{r=try Raster.stack([(fitted,0),(try Renderer.render(text:text,image:nil,mode:"文字",settings:s),4)])}else{r=fitted}
   }else{
    r=try Renderer.render(text:text,image:nil,mode:"文字",settings:s)
   }
   output.append(.init(id:block.id,raster:r,gap:block.gap))
  }
  return output
 }
 func raster(_ job:RelayJob,settings:PrintSettings)throws->Raster {try Raster.stack(parts(job,settings:settings).map{($0.raster,$0.gap)})}
}
struct RelayRenderedBlock:Identifiable {let id:UUID;let raster:Raster;let gap:Int}

extension Lab {
 var currentRelayTemplate:RelayTemplate {relayTemplates.first{$0.id.uuidString==relayTemplateID} ?? relayTemplates.first ?? .preset("便签")}
 func relayRaster(_ job:RelayJob,settings:PrintSettings)throws->Raster {
  if relayStyle=="自定义"{return try currentRelayTemplate.raster(job,settings:settings)}
  if let image=try job.verifiedRaster() {
   let footer=relayDocument(job)
   if footer.isEmpty{return image}
   let text=try Renderer.render(text:footer,image:nil,mode:"文字",settings:settings)
   return try Raster.stack([(image,0),(text,8)])
  }
  return try Renderer.render(text:relayDocument(job),image:nil,mode:"文字",settings:settings)
 }
 var relaySampleJob:RelayJob {if let job=selectedRelayJob,job.hasFullText{return job};return RelayJob(id:String(repeating:"a",count:40),shortID:"A1B2C3D4E5F6",text:"Hello Paperang!\n这是一条用于设计版式的示例纸条。",metadata:["identity":"Sample visitor","ip":"192.0.2.1","userAgent":"Example browser","browser":["platform":"Sample device"]],created:Date(timeIntervalSince1970:1793740800))}
}

@MainActor final class RelayEditorDraft:ObservableObject {
 @Published var templates:[RelayTemplate]
 @Published var templateID:UUID
 @Published var blockID:UUID?
 @Published var rendered=[RelayRenderedBlock]()
 @Published var message=""
 init(lab:Lab){let values=lab.relayTemplates.isEmpty ? [RelayTemplate.preset("便签")]:lab.relayTemplates;templates=values;let selected=values.first{$0.id.uuidString==lab.relayTemplateID} ?? values[0];templateID=selected.id;blockID=selected.blocks.first?.id}
}

struct RelayLayoutEditor:View {
 @ObservedObject var lab:Lab
 @Environment(\.dismiss) private var dismiss
 @StateObject private var draft:RelayEditorDraft
 @MainActor init(lab:Lab){self.lab=lab;_draft=StateObject(wrappedValue:RelayEditorDraft(lab:lab))}
 private var ti:Int {draft.templates.firstIndex{$0.id==draft.templateID} ?? 0}
 private var previewKey:String {draft.templates[ti].layoutSignature + lab.relaySampleJob.layoutSignature}
 var body:some View {
  VStack(alignment:.leading,spacing:12){
   HStack{Label("打印样式编辑器",systemImage:"rectangle.3.group").font(.title2);Spacer();Button("取消"){dismiss()};Button("保存并应用",action:save).buttonStyle(.borderedProminent)}
   HStack{Picker("模板",selection:$draft.templateID){ForEach(draft.templates){Text($0.name).tag($0.id)}};TextField("模板名称",text:$draft.templates[ti].name).textFieldStyle(.roundedBorder)
    Button("复制模板"){guard draft.templates.count<30 else{return};var copy=draft.templates[ti];copy.id=UUID();copy.name+=" 副本";draft.templates.append(copy);draft.templateID=copy.id}
    Menu("载入预设"){ForEach(["便签","小票","复古电传","访客纪念"],id:\.self){name in Button(name){guard draft.templates.count<30 else{return};let v=RelayTemplate.preset(name);draft.templates.append(v);draft.templateID=v.id}}}
    Button("导入JSON",action:importTemplate);Button("导出JSON",action:exportTemplate)
    Button("删除模板",role:.destructive){guard draft.templates.count>1 else{return};draft.templates.remove(at:ti);draft.templateID=draft.templates[0].id}.disabled(draft.templates.count<=1)
   }
   HSplitView {
    VStack(alignment:.leading){Text("区块（拖动排序）").font(.headline)
     List(selection:$draft.blockID){ForEach(draft.templates[ti].blocks){b in HStack{Image(systemName:b.enabled ? "checkmark.square":"square");Text(b.kind);Spacer();Text(Int(b.size).description).foregroundStyle(.secondary)}.tag(b.id)}.onMove{indices,destination in draft.templates[ti].blocks.move(fromOffsets:indices,toOffset:destination)}}
     HStack{Menu("添加区块"){ForEach(RelayBlock.kinds,id:\.self){kind in Button(kind){guard draft.templates[ti].blocks.count<24 else{return};let b=RelayBlock.make(kind,kind=="标题" ? "My note":"");draft.templates[ti].blocks.append(b);draft.blockID=b.id}}};Button("删除区块",role:.destructive){if let i=draft.templates[ti].blocks.firstIndex(where:{$0.id==draft.blockID}){draft.templates[ti].blocks.remove(at:i);draft.blockID=draft.templates[ti].blocks.first?.id}}}
    }.frame(minWidth:190,idealWidth:220,maxWidth:260)
    ScrollView{VStack(alignment:.leading,spacing:12){Text("区块属性").font(.headline)
     if let bi=draft.templates[ti].blocks.firstIndex(where:{$0.id==draft.blockID}) {
      Toggle("启用",isOn:$draft.templates[ti].blocks[bi].enabled)
      Picker("内容",selection:$draft.templates[ti].blocks[bi].kind){ForEach(RelayBlock.kinds,id:\.self){Text($0)}}
      if ["标题","自定义文字"].contains(draft.templates[ti].blocks[bi].kind){TextEditor(text:$draft.templates[ti].blocks[bi].text).frame(height:100).border(.quaternary);Text("变量：{{text}} {{sender}} {{id}} {{time}} {{ip}} {{ua}} {{device}}；id为可打印的短编号。").font(.caption).foregroundStyle(.secondary)}
      if draft.templates[ti].blocks[bi].kind=="正文"{Text("正文区块同时接收文字和图片。图片按本区块边距等比适配，说明文字紧随图片；字体与对齐用于文字。关闭正文会同时隐藏图片和说明。").font(.caption).foregroundStyle(.secondary)}
      Picker("字体",selection:$draft.templates[ti].blocks[bi].font){ForEach(["PingFang SC","Menlo","Helvetica","Times New Roman"],id:\.self){Text($0)}}
      Text("字号 \(Int(draft.templates[ti].blocks[bi].size))");Slider(value:$draft.templates[ti].blocks[bi].size,in:8...96,step:1)
      Toggle("粗体",isOn:$draft.templates[ti].blocks[bi].bold)
      Picker("对齐",selection:$draft.templates[ti].blocks[bi].alignment){ForEach(["居左","居中","居右"],id:\.self){Text($0)}}
      Stepper("左右边距：\(draft.templates[ti].blocks[bi].margin)点",value:$draft.templates[ti].blocks[bi].margin,in:0...120)
      Stepper("上方留白：\(draft.templates[ti].blocks[bi].gap)行",value:$draft.templates[ti].blocks[bi].gap,in:0...200)
      HStack{Button("上移"){moveBlock(-1)};Button("下移"){moveBlock(1)}}
     }else{Text("在左侧或纸面选择区块")}
     Divider();Text(draft.message).font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
     Text("编辑的是草稿，保存后才用于真实任务。纸面点击选择区块；拖动可调边距和上方留白。长正文会推动后续区块，不会重叠。").font(.caption).foregroundStyle(.secondary)
    }.padding()}.frame(minWidth:240,idealWidth:270,maxWidth:340)
    ScrollView([.vertical,.horizontal]){VStack(spacing:0){ForEach(draft.rendered){part in
     Image(nsImage:part.raster.image).resizable().interpolation(.none).frame(width:460,height:Double(part.raster.height)*460/576)
      .overlay(Rectangle().stroke(draft.blockID==part.id ? Color.accentColor:.clear,lineWidth:2))
      .padding(.top,Double(part.gap)*460/576).contentShape(Rectangle()).onTapGesture{draft.blockID=part.id}
      .gesture(DragGesture(minimumDistance:5).onEnded{value in if let i=draft.templates[ti].blocks.firstIndex(where:{$0.id==part.id}){draft.blockID=part.id;draft.templates[ti].blocks[i].margin=max(0,min(120,draft.templates[ti].blocks[i].margin+Int(value.translation.width*576/460)));draft.templates[ti].blocks[i].gap=max(0,min(200,draft.templates[ti].blocks[i].gap+Int(value.translation.height*576/460)))}})
    }}.frame(width:460).background(.white).padding(16)}.background(.quaternary.opacity(0.4)).frame(minWidth:490)
   }
  }.padding(20).frame(minWidth:1150,minHeight:700)
  .task(id:previewKey){do{try await Task.sleep(for:.milliseconds(200));let parts=try draft.templates[ti].parts(lab.relaySampleJob,settings:lab.relayPrintSettings());let total=try Raster.stack(parts.map{($0.raster,$0.gap)});draft.rendered=parts;draft.message="576 × \(total.height)点 · 所见区块与打印使用同一位图合成"}catch{if !Task.isCancelled{draft.rendered=[];draft.message=error.localizedDescription}}}
  .onChange(of:draft.templateID){_,_ in draft.blockID=draft.templates[ti].blocks.first?.id}
 }
 private func moveBlock(_ delta:Int){guard let i=draft.templates[ti].blocks.firstIndex(where:{$0.id==draft.blockID}),draft.templates[ti].blocks.indices.contains(i+delta) else{return};draft.templates[ti].blocks.swapAt(i,i+delta)}
 private func save(){do{try draft.templates[ti].validate();guard draft.templates.count<=30,Set(draft.templates.map(\.id)).count==draft.templates.count else{throw PrintFailure("模板编号重复")};for t in draft.templates{try t.validate()};let data=try JSONEncoder().encode(draft.templates);UserDefaults.standard.set(data,forKey:RelayTemplate.defaultsKey);lab.relayTemplates=draft.templates;lab.relayTemplateID=draft.templateID.uuidString;lab.relayStyle="自定义";dismiss()}catch{draft.message=error.localizedDescription}}
 private func importTemplate(){let p=NSOpenPanel();p.allowedContentTypes=[.json];guard p.runModal() == .OK,let url=p.url else{return};do{let size=try url.resourceValues(forKeys:[.fileSizeKey]).fileSize ?? 0;guard size<=65536,draft.templates.count<30 else{throw PrintFailure("模板文件过大或数量已满")};var t=try JSONDecoder().decode(RelayTemplate.self,from:Data(contentsOf:url));try t.validate();t.id=UUID();draft.templates.append(t);draft.templateID=t.id}catch{draft.message=error.localizedDescription}}
 private func exportTemplate(){let p=NSSavePanel();p.allowedContentTypes=[.json];p.nameFieldStringValue="Paperang-template.json";guard p.runModal() == .OK,let url=p.url else{return};do{try draft.templates[ti].validate();let encoder=JSONEncoder();encoder.outputFormatting=[.prettyPrinted,.sortedKeys];try encoder.encode(draft.templates[ti]).write(to:url,options:.atomic)}catch{draft.message=error.localizedDescription}}
}
