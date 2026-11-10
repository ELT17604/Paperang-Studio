import SwiftUI
import PrintCore
import ProtocolCore
import IntegrationCore
import CoreFoundation
extension Lab {
 func configureLaunchAPI(){let args=ProcessInfo.processInfo.arguments;guard args.contains("--api") else{return}
  func value(_ key:String)->String?{guard let i=args.firstIndex(of:key),i+1<args.count else{return nil};return args[i+1]}
  guard let path=value("--api-token-file"),let bytes=try? Data(contentsOf:URL(fileURLWithPath:path)),bytes.count<=4096,let raw=String(data:bytes,encoding:.utf8) else{error="--api需要有效的--api-token-file";return}
  apiToken=raw.trimmingCharacters(in:.whitespacesAndNewlines);apiPort=Int(value("--api-port") ?? "8176") ?? 0
  startAPI()
 }

 func startAPI(){guard api==nil,(1024...65535).contains(apiPort),(32...256).contains(apiToken.utf8.count),apiToken.utf8.allSatisfy({(33...126).contains($0)}) else{error="端口需为1024–65535，令牌需为32–256个非空白ASCII字符";return};let origins=Set(apiOrigins.split(separator:",").map{$0.trimmingCharacters(in:.whitespaces)}.filter{!$0.isEmpty})
  guard origins.allSatisfy({s in guard let u=URLComponents(string:s) else{return false};return ["http","https"].contains(u.scheme ?? "") && u.host != nil && u.path.isEmpty && u.query==nil && u.fragment==nil && u.user==nil && u.password==nil}) else{error="Origin需为不带路径的http(s)来源，逗号分隔";return}
  let service=LocalAPI(port:UInt16(apiPort),token:apiToken,origins:origins)
  service.handler={[weak self] req in guard let self else{return .failure(503,"closed","App已退出")};return await self.handleAPI(req)}
  service.onState={[weak self,weak service] running,message in Task{@MainActor in guard let self,let service,self.api===service else{return};self.apiRunning=running;self.notice=message;if ProcessInfo.processInfo.arguments.contains("--api-offline"){FileHandle.standardError.write(Data((message+"\n").utf8))};if !running{self.api=nil}}}
  do{try service.start();api=service;log("API","启动本机HTTP服务；不记录令牌")}catch{self.error=error.localizedDescription}
 }
 func stopAPI(){api?.stop();api=nil;apiRunning=false;notice="本地API已停止"}
 func copyAPIToken(){NSPasteboard.general.clearContents();NSPasteboard.general.setString(apiToken,forType:.string);notice="访问令牌已复制"}
 func statusObject()->[String:Any]{["appVersion":AppIdentity.version,"trayMode":trayMode,"networkPrintAllowed":networkPrintAllowed,"automaticNetworkConsent":automaticNetworkConsent,"relayAutomaticPrinting":relayRunning && relayAutoPrint && networkPrintAllowed,"relayContentTypes":["text","raster1"],"apiVersion":"v2","connected":ready,"busy":busy,"printer":connectedName,"state":state,"lastError":error,"progress":progress,"jobStatus":jobStatus,"charging":"unknown","readings":readings.map{["command":Int($0.id),"name":$0.title,"value":$0.value,"rawHex":$0.raw,"sampledAt":ISO8601DateFormatter().string(from:$0.time)]},"printingEnabled":networkPrintAllowed,"workspacePrintingEnabled":allowPaper]}
 func localJobState(_ label:String)->String {if label=="发送中"{return "printing"};if label.hasPrefix("已发送"){return "sent"};if label=="已停止"{return "cancelled"};return "unknown"}
 func jobObject(_ job:JobRecord)->[String:Any]{["id":job.id.uuidString,"title":job.title,"detail":job.detail,"state":localJobState(job.status),"stateLabel":job.status,"physicalCompletion":"unknown","createdAt":ISO8601DateFormatter().string(from:job.date)]}
 func apiSettings(_ object:Any?)throws->PrintSettings {var defaults=try JSONSerialization.jsonObject(with:JSONEncoder().encode(PrintSettings())) as! [String:Any];if let object{guard let patch=object as? [String:Any],Set(patch.keys).isSubset(of:Set(defaults.keys)) else{throw PrintFailure("settings包含未知字段或格式错误")};defaults.merge(patch,uniquingKeysWith:{$1})};let s=try JSONDecoder().decode(PrintSettings.self,from:JSONSerialization.data(withJSONObject:defaults));try s.validate();return s}
 func apiRaster(_ body:[String:Any],settings:PrintSettings)throws->Raster {let text=body["text"] as? String;let encoded=body["imageBase64"] as? String;guard (text != nil) != (encoded != nil) else{throw PrintFailure("text与imageBase64必须且只能提供一个")};if let encoded{guard let data=Data(base64Encoded:encoded) else{throw PrintFailure("图片Base64无效")};return try Renderer.render(text:"",image:Renderer.loadImageData(data),mode:"图片",settings:settings)};return try Renderer.render(text:text!,image:nil,mode:"文字",settings:settings)}
 func trueConsent(_ value:Any?)->Bool{guard let n=value as? NSNumber,CFGetTypeID(n)==CFBooleanGetTypeID() else{return false};return n.boolValue}
 func handleAPI(_ req:HTTPRequest)async->APIResponse {
  let path=req.path;log("API",req.method+" "+path)
  if path=="/v1" || path.hasPrefix("/v1/"){return .failure(410,"api_v1_retired","Use the local API v2 contract")}
  if req.method=="GET" {
   switch path {
   case "/v2/status":return .json(statusObject())
   case "/v2/devices":let all=req.query["all"]=="true";return .json(["scanning":scanning,"devices":devices.filter{all || DeviceMatcher.paperang($0.name)}.map{["id":$0.id.uuidString,"name":$0.name,"rssi":$0.rssi,"paperangCandidate":DeviceMatcher.paperang($0.name)] as [String:Any]}])
   case "/v2/protocol":return .json(["model":"Paperang_P2","widthDots":576,"bytesPerRow":72,"nominalDPI":300,"defaultFeedLines":150,"commands":Command.all.map{["command":Int($0.code),"name":$0.title,"kind":$0.kind,"evidence":$0.evidence,"description":$0.detail,"expectedResponse":Command.expected[$0.code].map{Int($0)} as Any? ?? NSNull()]}])
   case "/v2/settings":return .json(["defaults":try! JSONSerialization.jsonObject(with:JSONEncoder().encode(PrintSettings())),"note":"每次请求可传部分settings；宽度固定576" ])
   case "/v2/jobs":return .json(["jobs":jobs.reversed().map(jobObject)])
   case "/v2/logs":let limit=min(500,max(1,Int(req.query["limit"] ?? "100") ?? 100));return .json(["order":"newest_first","logs":logs.reversed().prefix(limit).map{["timestamp":ISO8601DateFormatter().string(from:$0.time),"type":$0.type,"text":$0.text]}])
   case "/v2/openapi":if let url=Bundle.main.resourceURL?.appendingPathComponent("openapi.json"),let data=try? Data(contentsOf:url){return APIResponse(status:200,data:data)};return .failure(404,"missing_schema","未找到API文档")
   default:if path.hasPrefix("/v2/jobs/"),let id=UUID(uuidString:String(path.dropFirst(9))),let job=jobs.first(where:{$0.id==id}){return .json(jobObject(job))};return .failure(404,"not_found","未知路由")
   }
  }
  guard req.method=="POST" else{return .failure(404,"not_found","未知路由")}
  do {
   guard let body=try JSONSerialization.jsonObject(with:req.body) as? [String:Any] else{throw PrintFailure("JSON需为对象")}
   switch path {
   case "/v2/scan":guard central != nil else{return .failure(409,"offline","离线API模式不能扫描蓝牙")};guard !busy else{return .failure(409,"busy","设备正在执行任务")};scan();return .json(["accepted":true,"scanning":scanning],status:202)
   case "/v2/connect":guard !busy,!ready,let raw=body["id"] as? String,let id=UUID(uuidString:raw),known[id] != nil else{return .failure(409,"connection","请先扫描，当前不能连接该设备")};connect(id);return .json(["accepted":true,"id":raw],status:202)
   case "/v2/disconnect":guard !busy else{return .failure(409,"busy","请先停止活动打印任务")};disconnect();return .json(["connected":false])
   case "/v2/query":guard let command=body["command"] as? Int,(0...255).contains(command),Command.expected[UInt8(command)] != nil else{throw PrintFailure("仅允许已知只读GET命令，command为整数")};guard ready,!busy else{return .failure(409,"busy","设备未就绪或正忙")};let g=generation;busy=true;defer{if generation==g{busy=false}};try await query(UInt8(command));return .json(["command":command,"payloadHex":response.map(Wire.hex) as Any? ?? NSNull(),"hasData":response != nil,"status":statusObject()])
   case "/v2/render":let s=try apiSettings(body["settings"]);let r=try apiRaster(body,settings:s);var result:[String:Any]=["width":r.width,"height":r.height,"bytes":r.bytes.count,"previewPngBase64":r.png?.base64EncodedString() ?? ""];if trueConsent(body["includeRaw"]){result["rawBase64"]=r.bytes.base64EncodedString()};return .json(result)
   case "/v2/jobs":guard networkJobAuthorized(body["allowPaper"]) else{return .failure(403,"paper_disabled","请开启全局耗纸并指定allowPaper:true，或进入未暂停的托盘模式")};guard ready,!busy else{return .failure(409,"busy","设备未就绪或正忙")};let s=try apiSettings(body["settings"]);let r=try apiRaster(body,settings:s);let id=try submitRaster(r,settings:s,title:"API · "+String((body["title"] as? String ?? "打印任务").prefix(120)));return .json(["id":id.uuidString,"state":"accepted","note":"请轮询任务；接收不等于纸面完成"],status:202)
   case "/v2/feed":guard networkPrintAllowed,trueConsent(body["allowPaper"]) else{return .failure(403,"paper_disabled","耗纸未授权")};guard let lines=body["lines"] as? Int,(0...2000).contains(lines),ready,!busy else{return .failure(409,"busy","行数无效、设备未就绪或正忙")};let g=generation;busy=true;defer{if generation==g{busy=false}};try await verifyMedia();if lines>0{try await send(26,Wire.u16(lines))};return .json(["acknowledged":true,"lines":lines,"physicalCompletion":"unknown"])
   default:if path.hasPrefix("/v2/jobs/"),path.hasSuffix("/cancel"),let id=UUID(uuidString:String(path.dropFirst(9).dropLast(7))),jobs.last?.id==id,jobs.last?.status=="发送中"{stopJob();return .json(["id":id.uuidString,"state":"cancelled","note":"设备已缓存的数据无法撤回"])};return .failure(404,"not_found","未知路由或没有此活动任务")
   }
  }catch{return .failure(400,"request_failed",error.localizedDescription)}
 }
}
extension Content {
 var apiPage:some View{ScrollView{VStack(alignment:.leading,spacing:18){Label("本机HTTP API",systemImage:"network").font(.title2);Text("供本机程序和你允许的网页调用。默认关闭，仅绑定127.0.0.1；不提供shell执行或终端输入接口。").foregroundStyle(.secondary)
  integer("端口",value:$lab.apiPort,range:1024...65535).disabled(lab.api != nil)
  HStack{SecureField("Bearer访问令牌",text:$lab.apiToken).disabled(lab.api != nil);Button("复制令牌",action:lab.copyAPIToken);Button("重新生成"){lab.apiToken=UUID().uuidString+UUID().uuidString}.disabled(lab.api != nil)}
  TextField("允许的网页Origin，逗号分隔",text:$lab.apiOrigins).textFieldStyle(.roundedBorder).disabled(lab.api != nil)
  Text("耗纸由左侧全局开关统一控制；API打印请求还需allowPaper:true。").foregroundStyle(.secondary)
  HStack{if lab.api != nil{Button("停止API",role:.destructive,action:lab.stopAPI)}else{Button("启动本地API",action:lab.startAPI).buttonStyle(.borderedProminent)};Text(lab.apiRunning ? "http://127.0.0.1:\(String(lab.apiPort))":"API未监听")}
  GroupBox("v2接口"){Text("GET /v2/status · /v2/devices · /v2/protocol · /v2/settings\nGET /v2/jobs · /v2/jobs/{id} · /v2/logs · /v2/openapi\nPOST /v2/scan · /v2/connect · /v2/disconnect · /v2/query\nPOST /v2/render · /v2/jobs · /v2/jobs/{id}/cancel · /v2/feed").font(.system(.body,design:.monospaced)).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading).padding(8)}
  Text("所有实际请求需要Authorization: Bearer <令牌>。跨域网页还需匹配Origin白名单；不支持通配符。打印返回任务ID，不会自动重试。HTTP请求上限2MB、最多8个并发连接；打印、GUI和纸带终端共用同一设备锁。").font(.callout)
  Text("完整说明和示例客户端：工程 doc/API.md、doc/openapi.json、Clients/。不要将本机令牌硬编码进公开网页。").foregroundStyle(.secondary)
 }.padding(2)}}
}
