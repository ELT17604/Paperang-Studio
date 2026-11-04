import SwiftUI
import Foundation
import PrintCore
import IntegrationCore
import CryptoKit

struct RelayJob:Identifiable {
 let id:String;var shortID:String="";var text:String;let metadata:[String:Any];let created:Date
 var state="queued";var updated:Date?;var detail="";var hasFullText=true;var content:[String:Any]?=nil
 var sender:String {metadata["identity"] as? String ?? "Anonymous"}
 var printableID:String {let value=shortID.isEmpty ? String(id.replacingOccurrences(of:"-",with:"").prefix(12)).uppercased():shortID;guard value.count==12 else{return value};return stride(from:0,to:12,by:4).map{start in String(value.dropFirst(start).prefix(4))}.joined(separator:"-")}
 var stateLabel:String {["queued":"待处理","claimed":"已认领","printing":"发送中","sent":"已发送，待核对纸面","confirmed":"纸面完成（人工确认）","unknown":"结果不确定","rejected":"已拒绝"][state] ?? state}
 var stateColor:Color {switch state {case "sent","confirmed":return .green;case "unknown":return .orange;case "rejected":return .secondary;case "printing","claimed":return .blue;default:return .primary}}
 var rawMetadata:String {guard let bytes=try? JSONSerialization.data(withJSONObject:metadata,options:[.prettyPrinted,.sortedKeys]),let s=String(data:bytes,encoding:.utf8) else{return ""};return s}
 static func decode(_ row:[String:Any])->Self? {
  guard let id=row["id"] as? String,id.range(of:"^(?:[a-f0-9]{40}|[a-f0-9-]{36})$",options:.regularExpression) != nil,let text=row["text"] as? String,text.count<=10000,let meta=row["metadata"] as? [String:Any] else{return nil}
  return Self(id:id,shortID:row["shortID"] as? String ?? "",text:text,metadata:meta,created:Date(timeIntervalSince1970:(row["created"] as? Double) ?? 0),state:row["state"] as? String ?? "queued",updated:(row["updated"] as? Double).map{Date(timeIntervalSince1970:$0)},detail:row["detail"] as? String ?? "",hasFullText:!(row["truncated"] as? Bool ?? false),content:row["content"] as? [String:Any])
 }
}
struct RelayManagedKey:Identifiable {let id:String;var name:String;let role:String;let device:String;let revoked:Bool}
struct RelayFlight {let serverID:String;let claim:String;var localID:UUID?;var outcome:String?}

extension Lab {
 func relayRequest(_ method:String,_ path:String,_ body:[String:Any]?=nil, credential:String?=nil) async throws -> [String:Any] {
  guard let base=relayEndpoint else{throw PrintFailure("请先连接中转服务器")}
  let session=relaySession
  var req=URLRequest(url:base.appendingPathComponent(path));req.httpMethod=method;req.timeoutInterval=4
  req.setValue("text,raster1",forHTTPHeaderField:"X-Paperang-Content-Types")
  req.cachePolicy = .reloadIgnoringLocalCacheData;req.setValue("Bearer "+(credential ?? relayCredential),forHTTPHeaderField:"Authorization")
  if let body{req.setValue("application/json",forHTTPHeaderField:"Content-Type");req.httpBody=try JSONSerialization.data(withJSONObject:body)}
  let (data,response)=try await URLSession.shared.data(for:req)
  guard !Task.isCancelled,session==relaySession else{throw CancellationError()}
  guard data.count<=1_048_576,let http=response as? HTTPURLResponse,let object=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any] else{throw PrintFailure("服务器返回无效数据")}
  guard (200...299).contains(http.statusCode) else{throw PrintFailure("中转HTTP \(http.statusCode)：\(object["error"] as? String ?? "请求失败")")}
  return object
 }
 func startRelay(){
  guard !relayRunning,relayTask==nil else{return}
  guard let u=URLComponents(string:relayURL.trimmingCharacters(in:.whitespacesAndNewlines)),let url=u.url,let host=u.host,u.user==nil,u.password==nil,u.query==nil,u.fragment==nil,["","/"].contains(u.path),u.scheme=="https" || (u.scheme=="http" && (relayAllowHTTP || ["localhost","127.0.0.1","::1"].contains(host))) else{relayMessage="请输入服务器根地址，推荐HTTPS；HTTP测试需明确开启";return}
  relayKey=relayKey.trimmingCharacters(in:.whitespacesAndNewlines)
  guard relayKey.hasPrefix("psr_"),relayKey.utf8.count<=256 else{relayMessage="请输入设备密钥（psr_开头），不是投稿或管理密钥";return}
  if relayFlight != nil && (relayEndpoint != url || relayCredential != relayKey){relayMessage="上一任务尚待回报，请保持同一服务器和设备密钥";return}
  relaySession=UUID();relayOnline=false;relayFailures=0;relayLastSeen=nil;relayManagedKeys=[];relayCreatedToken="";relayEndpoint=url;relayCredential=relayKey;relayRunning=true;relayMessage="正在连接…";relayAutoPrint=trayMode ? !trayPrintPaused:relayDefaultAutoPrint
  let session=relaySession
  relayTask=Task{
   var health=RelayHealth()
   while !Task.isCancelled,self.relaySession==session,self.relayRunning {
    do {try await self.relayTick();guard !Task.isCancelled,self.relaySession==session else{break};health.succeeded();self.relayOnline=health.online;self.relayFailures=health.retries;self.relayLastSeen=health.lastSuccess}
    catch {guard !Task.isCancelled,self.relaySession==session else{break};self.relayOnline=false
     let exhausted=health.failed();self.relayFailures=health.retries
     self.relayMessage="连接不可用 · 重连 \(self.relayFailures)/5 · \(error.localizedDescription)"
     if exhausted {self.stopRelay();self.relayMessage="连续5次重连失败，已断开："+error.localizedDescription;self.relayWarning=true;break}
    }
    do{try await Task.sleep(for:.seconds(2))}catch{break}
   }
   if self.relaySession==session{self.relayTask=nil}
  }
 }
 func stopRelay(){relayAutoPrint=false;relayOnline=false;relayRunning=false;relayTask?.cancel();relayTask=nil;relaySession=UUID()
  if var flight=relayFlight{if let id=flight.localID,jobs.first(where:{$0.id==id})?.status=="发送中"{stopJob()};if let id=flight.localID,jobs.first(where:{$0.id==id})?.status.hasPrefix("已发送")==true{flight.outcome="sent"}else{flight.outcome="unknown"};relayFlight=flight}
  relayMessage="已断开中转；发送结果不确定的任务不会自动重打"
 }
 func relayFlush() async throws {
  guard !relayFlushing,var flight=relayFlight else{return}
  relayFlushing=true;defer{relayFlushing=false}
  if flight.outcome==nil,let localID=flight.localID{
   if let job=jobs.first(where:{$0.id==localID}){if job.status=="发送中"{_ = try await relayRequest("POST","v1/device/jobs/\(flight.serverID)/result",["claim":flight.claim,"state":"printing","detail":jobStatus]);return};flight.outcome=job.status.hasPrefix("已发送") ? "sent":"unknown"}else{flight.outcome="unknown"}
   relayFlight=flight
  }
  guard let outcome=flight.outcome else{return}
  _ = try await relayRequest("POST","v1/device/jobs/\(flight.serverID)/result",["claim":flight.claim,"state":outcome,"detail":outcome=="sent" ? "Data sent; physical output not independently confirmed":"Inspect paper before any manual retry"])
  relayFlight=nil
 }
 func relayTick() async throws {
  guard relayRunning else{return}
  if !relayMutating{try await relayFlush()}
  _ = try await relayRequest("POST","v1/device/status",["status":statusObject()])
  relayOnline=true;relayLastSeen=Date()
  guard relayRunning,!relayMutating else{return}
  let result=try await relayRequest("GET","v1/device/jobs")
  guard relayRunning else{return}
  relayJobs=(result["jobs"] as? [[String:Any]] ?? []).compactMap{row in guard var job=RelayJob.decode(row) else{return nil};if let cached=relayDetailCache[job.id]{job.text=cached.text;job.content=cached.content;job.hasFullText=true};return job}
  let ids=Set(relayJobs.map(\.id));relayDetailCache=relayDetailCache.filter{ids.contains($0.key)}
  if !relayJobs.contains(where:{$0.id==relaySelection}){relaySelection=relayJobs.first?.id ?? ""}
  relayMessage="中转已连接 · 待处理 \(relayJobs.filter{$0.state=="queued"}.count) / 最近 \(relayJobs.count) 项 · 每2秒上报/刷新"
  if relayAutoPrint,networkPrintAllowed,ready,!busy,relayFlight==nil,let job=relayJobs.filter({$0.state=="queued"}).min(by:{$0.created<$1.created}){await printRelay(job)}
 }
 var relayPreviewSignature:String {(selectedRelayJob?.layoutSignature ?? relaySelection)+relayStyle+String(relayDensity)+String(relayFeed)+[relayIncludeText,relayIncludeSender,relayIncludeTime,relayIncludeID,relayIncludeIP,relayIncludeUA,relayIncludeDevice].map{String($0)}.joined()+currentRelayTemplate.layoutSignature}
 func abandonRelayReport(){guard !relayMutating else{return};if let id=relayFlight?.localID,jobs.first(where:{$0.id==id})?.status=="发送中"{return};relayFlight=nil;relayMessage="已清除本地结果关联，不会重新打印旧任务"}
 var selectedRelayJob:RelayJob? {relayJobs.first{$0.id==relaySelection}}
 func relayDocument(_ job:RelayJob)->String {
  let date=ISO8601DateFormatter().string(from:job.created)
  var fields=[String]();if relayIncludeSender{fields.append("From: "+job.sender)};if relayIncludeTime{fields.append(date)}
  if relayIncludeIP{fields.append("IP: "+(job.metadata["ip"] as? String ?? "unknown"))}
  if relayIncludeUA{fields.append("UA: "+(job.metadata["userAgent"] as? String ?? "unknown"))}
  if relayIncludeDevice,let browser=job.metadata["browser"] as? [String:Any]{fields.append("Browser supplied: "+browser.keys.sorted().map{"\($0)=\(browser[$0]!)"}.joined(separator:" · "))}
  if relayIncludeID{fields.append("#"+job.printableID)}
  let message=relayIncludeText ? job.text:""
  let details=fields.joined(separator:"\n")
  switch relayStyle {
  case "小票":return "PAPERANG INBOX\n----------------------------\n"+message+"\n----------------------------\n"+details
  case "复古电传":return "[ INCOMING TRANSMISSION ]\n"+details+"\n\n"+message+"\n[ END ]"
  case "访客纪念":return "A NOTE TO KEEP\n\n"+message+"\n\n"+details+"\n— Paperang Studio —"
  default:return [message,details].filter{!$0.isEmpty}.joined(separator:"\n\n")
  }
 }
 func relayPrintSettings()->PrintSettings {
  var s=settings;s.copies=1;s.density=relayDensity;s.feedAfter=relayFeed;s.feedBefore=0;s.feedBetweenCopies=false
  switch relayStyle{case "复古电传":s.fontName="Menlo";s.fontSize=15;s.alignment="居左";s.lineSpacing=2
  case "小票":s.fontName="Menlo";s.fontSize=19;s.alignment="居左";s.lineSpacing=3
  case "访客纪念":s.fontName="PingFang SC";s.fontSize=27;s.alignment="居中";s.lineSpacing=7
  default:s.fontName="PingFang SC";s.fontSize=24;s.alignment="居左";s.lineSpacing=4}
  s.marginX=12;s.contentWidth=552;s.marginY=12
  return s
 }
 var relayPreviewIsCurrent:Bool {relayPreview != nil && relayPreviewRenderedSignature==relayPreviewSignature}
 func previewRelay(){
  guard let job=selectedRelayJob else{relayMessage="请先选择一个任务";return}
  relayPreviewTask?.cancel();let session=UUID();relayPreviewSession=session;relayPreviewLoading=true
  relayMessage=job.hasFullText ? "正在生成预览…":"正在读取完整任务并生成预览…"
  relayPreviewTask=Task{
   defer{if self.relayPreviewSession==session{self.relayPreviewLoading=false;self.relayPreviewTask=nil}}
   do{
    let full=try await self.relayFullJob(job)
    guard !Task.isCancelled,self.relaySelection==job.id,self.relayPreviewSession==session else{return}
    let signature=self.relayPreviewSignature
    let r=try self.relayRaster(full,settings:self.relayPrintSettings())
    guard let png=r.png,let image=NSImage(data:png) else{throw PrintFailure("预览图片编码失败")}
    self.relayPreviewRenderedSignature=signature;self.relayPreview=image
    self.relayMessage="预览 \(r.width)×\(r.height)，实际发送使用相同排版流程"
   }catch{if !Task.isCancelled,self.relayPreviewSession==session,self.relaySelection==job.id{self.relayPreview=nil;self.relayPreviewRenderedSignature="";self.relayMessage="预览失败："+error.localizedDescription}}
  }
 }

 func printSelectedRelay(){guard let job=selectedRelayJob else{return};Task{await self.printRelay(job)}}
 func printRelay(_ job:RelayJob) async {
  guard relayRunning,relayOnline,!relayMutating,relayFlight==nil,ready,!busy,networkPrintAllowed,job.state=="queued" else{relayMessage="请连接P2、开启侧栏全局耗纸并等待当前任务完成";return}
  relayMutating=true;defer{relayMutating=false}
  do{
   let job=try await relayFullJob(job);let s=relayPrintSettings();let r=try relayRaster(job,settings:s)
   let result=try await relayRequest("POST","v1/device/jobs/\(job.id)/claim",[:]);guard let claim=result["claim"] as? String else{throw PrintFailure("缺少任务认领凭据")}
   relayFlight=RelayFlight(serverID:job.id,claim:claim,localID:nil,outcome:nil)
   _ = try await relayRequest("POST","v1/device/jobs/\(job.id)/result",["claim":claim,"state":"printing"])
   guard relayRunning,networkPrintAllowed,ready,!busy else{throw PrintFailure("本地打印条件已改变，任务不会重发")}
   let id=try submitRaster(r,settings:s,title:"远程 · "+job.sender)
   relayFlight?.localID=id;if let i=relayJobs.firstIndex(where:{$0.id==job.id}){relayJobs[i].state="printing"};relayMessage="远程任务已提交；完成后回报中转"
  }catch{
   if relayFlight != nil{relayFlight?.outcome="unknown";try? await relayFlush()}
   relayAutoPrint=false;relayMessage=error.localizedDescription
  }
 }
 func rejectRelay(){guard let job=selectedRelayJob,job.state=="queued",relayRunning,!relayMutating else{return};relayMutating=true
  Task{defer{self.relayMutating=false};do{_ = try await self.relayRequest("POST","v1/device/jobs/\(job.id)/reject",[:]);if let i=self.relayJobs.firstIndex(where:{$0.id==job.id}){self.relayJobs[i].state="rejected"};self.relayMessage="已拒绝任务"}catch{self.relayMessage=error.localizedDescription}}
 }
}
extension Lab {
 var relayVisibleJobs:[RelayJob] {relayJobs.filter{job in (relayFilter=="全部" || (relayFilter=="待处理" && job.state=="queued") || (relayFilter=="进行中" && ["claimed","printing"].contains(job.state)) || (relayFilter=="已发送" && ["sent","confirmed"].contains(job.state)) || (relayFilter=="异常" && job.state=="unknown") || (relayFilter=="已拒绝" && job.state=="rejected")) && (relaySearch.isEmpty || (job.id+job.shortID+job.sender+job.text).localizedCaseInsensitiveContains(relaySearch))}.sorted{$0.created>$1.created}}
 func relayFullJob(_ job:RelayJob) async throws ->RelayJob {
  if job.hasFullText{return job}
  let result=try await relayRequest("GET","v1/device/jobs/\(job.id)")
  guard let full=RelayJob.decode(result),full.id==job.id,full.hasFullText else{throw PrintFailure("无法获取完整任务正文，已阻止打印摘要")}
  relayDetailCache[job.id]=full
  if let i=relayJobs.firstIndex(where:{$0.id==job.id}){relayJobs[i].text=full.text;relayJobs[i].hasFullText=true;relayJobs[i].content=full.content}
  return full
 }
 func loadRelaySelection(){relayDetailTask?.cancel();guard let job=selectedRelayJob,!job.hasFullText else{return};relayDetailTask=Task{do{_ = try await self.relayFullJob(job)}catch{if !Task.isCancelled{self.relayMessage=error.localizedDescription}}}}
 func copyRelayToWorkspace(){guard let job=selectedRelayJob else{return};Task{do{let full=try await self.relayFullJob(job);if full.content != nil{let image=try self.relayRaster(full,settings:self.relayPrintSettings());guard let png=image.png else{throw PrintFailure("位图转换失败")};self.sourceImage=try Renderer.loadImageData(png);self.imageName="远程绘图 · "+full.printableID;self.mode="图片"}else{self.text=full.text;self.mode="文字"};self.page="打印工作台"}catch{self.relayMessage=error.localizedDescription}}}
}

extension Lab {
 func confirmRelayOutput(){guard let job=selectedRelayJob,job.state=="sent",relayOnline,!relayMutating else{return};relayMutating=true
  Task{defer{self.relayMutating=false};do{_ = try await self.relayRequest("POST","v1/device/jobs/\(job.id)/confirm",[:]);if let i=self.relayJobs.firstIndex(where:{$0.id==job.id}){self.relayJobs[i].state="confirmed"};self.relayMessage="已记录人工纸面确认"}catch{self.relayMessage=error.localizedDescription}}
 }
}
