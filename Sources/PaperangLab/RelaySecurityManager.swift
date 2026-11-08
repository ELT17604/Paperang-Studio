import SwiftUI
import AppKit
import Security
import Darwin

/// Owner UI is deliberately separate from the unattended M printing credential.
@MainActor
struct RelaySecurityManager: View {
 let server:String
 @Binding var previewCredential:String
 var clearPreview:()->Void
 @State private var credential=""
 @State private var token=""
 @State private var name=""
 @State private var userID=""
 @State private var role="A"
 @State private var phrase=""
 @State private var kind="token"
 @State private var section="keys"
 @State private var message=""
 @State private var selected=""
 @State private var details=""
 @State private var rows=[[String:Any]]()
 @State private var before:Double=1e20
 @State private var busy=false
 private let sections=["keys","identities","sessions","jobs","audit"]
 var body:some View {
  VStack(alignment:.leading,spacing:10) {
   SecureField("C 密钥 / 完整口令（仅保留在内存）",text:$credential)
   HStack {
    Button("验证 C 权限"){perform{let value=try await request("auth/login",["credential":credential]);try await accept(value);credential=""}}
    Button("私钥文件登录"){perform{try await privateLogin()}}
    Button("生成所有者私钥"){perform{try await generateOwner()}}
    Button("锁定"){token="";previewCredential="";clearPreview();phrase="";credential="";rows=[];details=""}
   }
   Group {
    Picker("页面",selection:$section){ForEach(sections,id:\.self){Text($0).tag($0)}}
    HStack{TextField("展示名称",text:$name);TextField("用户 ID（筛选/现有身份）",text:$userID)}
    SecureField("可选自定义口令（至少12字）",text:$phrase)
    HStack{Picker("权限",selection:$role){ForEach(["A","B","C","M"],id:\.self){Text($0).tag($0)}};Picker("凭证",selection:$kind){Text("随机密钥").tag("token");Text("可读口令").tag("passphrase")}}
    HStack {
     Button("刷新"){perform{try await refresh()}};Button("下一页"){perform{try await refresh(more:true)}}
     Button("新建凭证"){perform{var body:[String:Any]=["name":name,"role":role,"kind":kind];if !phrase.isEmpty{body["password"]=phrase};if !userID.isEmpty{body["user_id"]=userID};details=try pretty(await request("admin/keys",body));message="新凭证仅展示本次；请保存后清除。"}}
     Button("修改名称"){perform{_ = try await request("admin/keys/\(selected)/rename",["name":name]);try await refresh()}}
     Button("轮换"){perform{details=try pretty(await request("admin/keys/\(selected)/rotate",[:]));message="旧凭证及会话已失效；请保存新凭证。"}}
    }
    HStack {
     Button("吊销所选",role:.destructive){perform{try await revoke()}}
     Button("登记公钥"){perform{try await trustPublicKey()}}
     Button("导出历史 JSONL"){perform{try await exportHistory()}}
     Button("纸面确认"){perform{_ = try await request("admin/jobs/\(selected)/confirm",[:]);try await refresh()}}
    }
    List(rows.indices,id:\.self) { index in
     let row=rows[index]
     Button(String(describing:row["name"] ?? row["id"] ?? row["credential_id"] ?? "")+" · "+String(describing:row["role"] ?? row["state"] ?? row["action"] ?? "")) {selected=String(describing:row["id"] ?? row["credential_id"] ?? "");details=(try? pretty(row)) ?? ""}.buttonStyle(.plain)
    }.frame(minHeight:130,maxHeight:230)
    TextEditor(text:$details).font(.system(.caption,design:.monospaced)).frame(minHeight:150,maxHeight:280)
    Button("清除详情 / 新凭证"){details=""}
   }.disabled(token.isEmpty || busy)
   Text(message).font(.caption).textSelection(.enabled)
   Text("A 仅投递；B 本身份状态；C 全部管理；M 限定设备执行。导出含私人正文和投递记录。会话过期后重新登录。主打印循环不会使用此处的 C 凭证。").font(.caption).foregroundStyle(.secondary)
  }.task(id:token){
   let active=token;guard !active.isEmpty else{return}
   do{try await Task.sleep(for:.seconds(600));guard token==active else{return};token="";previewCredential="";clearPreview();details="";rows=[];message="管理会话已过期，请重新登录"}catch{}
  }.onDisappear{token="";previewCredential="";clearPreview();phrase="";credential="";details="";rows=[]}
 }
 @MainActor private func perform(_ work:@escaping @MainActor () async throws ->Void) {
  guard !busy else{return};busy=true
  Task{@MainActor in defer{busy=false};do{try await work()}catch{message=error.localizedDescription}}
 }
 private func pretty(_ value:Any)throws->String {String(decoding:try JSONSerialization.data(withJSONObject:value,options:[.prettyPrinted,.sortedKeys]),as:UTF8.self)}
 private func request(_ path:String,_ body:[String:Any]?=nil) async throws->[String:Any] {
  guard let parts=URLComponents(string:server),let base=parts.url,parts.user==nil,parts.password==nil,parts.query==nil,parts.fragment==nil,["","/"].contains(parts.path),parts.scheme=="https" || (parts.scheme=="http" && ["localhost","127.0.0.1","::1"].contains(parts.host ?? "")) else{throw PrintFailure("管理地址必须为 HTTPS 或本机 HTTP 根地址")}
  var req=URLRequest(url:base.appendingPathComponent("v2/"+path));req.httpMethod=body==nil ? "GET":"POST";req.timeoutInterval=8;req.cachePolicy = .reloadIgnoringLocalCacheData
  req.setValue("1",forHTTPHeaderField:"X-Paperang-Native")
  if !token.isEmpty{req.setValue("Bearer "+token,forHTTPHeaderField:"Authorization")}
  if let body{req.httpBody=try JSONSerialization.data(withJSONObject:body);req.setValue("application/json",forHTTPHeaderField:"Content-Type")}
  let (data,response)=try await RelayTransport.data(for:req)
  guard data.count<=1_048_576,let http=response as? HTTPURLResponse,let result=try JSONSerialization.jsonObject(with:data) as? [String:Any] else{throw PrintFailure("服务器响应无效")}
  guard (200...299).contains(http.statusCode) else{if http.statusCode==401{await MainActor.run{token=""}};throw PrintFailure(result["error"] as? String ?? "权限或连接错误")}
  return result
 }
 @MainActor private func accept(_ value:[String:Any]) async throws {
  guard value["role"] as? String=="C",let access=value["token"] as? String else{throw PrintFailure("需要 C 管理权限")};token=access;previewCredential=access;try await refresh()
 }
 @MainActor private func refresh(more:Bool=false) async throws {
  let result:[String:Any],property:String
  if section=="jobs"{result=try await request("admin/jobs/search",["user_id":userID,"before":more ? before:1e20]);property="jobs"}
  else if section=="audit"{result=try await request("admin/audit",["before":more && before<9e18 ? Int64(before):Int64.max]);property="events"}
  else{result=try await request("admin/"+section+"/search",["before":more ? before:1e20]);property=section}
  rows=result[property] as? [[String:Any]] ?? [];before=(result["next"] as? NSNumber)?.doubleValue ?? 1e20;details="";selected="";message="已读取 \(rows.count) 条，详情仅保留当前页。"
 }
 @MainActor private func revoke() async throws {
  guard !selected.isEmpty else{throw PrintFailure("请先选择一行")}
  let alert=NSAlert();alert.messageText="吊销所选身份或凭证？";alert.informativeText="身份吊销将拒绝排队中的任务。";alert.addButton(withTitle:"吊销");alert.addButton(withTitle:"取消");guard alert.runModal() == .alertFirstButtonReturn else{return}
  if section=="sessions"{_ = try await request("admin/sessions/revoke",["credential_id":selected])}
  else if section=="keys" || section=="identities"{_ = try await request("admin/\(section)/\(selected)/revoke",[:])}
  else{throw PrintFailure("请选择凭证、身份或会话")};try await refresh()
 }
 @MainActor private func exportHistory() async throws {
  let panel=NSSavePanel();panel.nameFieldStringValue="paperang-history.jsonl";guard panel.runModal() == .OK,let url=panel.url else{return}
  FileManager.default.createFile(atPath:url.path,contents:nil,attributes:[.posixPermissions:0o600]);let file=try FileHandle(forWritingTo:url);defer{try? file.close()};try file.truncate(atOffset:0)
  var cursor=1e20,count=0
  while true{let page=try await request("admin/jobs/export",["user_id":userID,"before":cursor]);let jobs=page["jobs"] as? [[String:Any]] ?? [];if jobs.isEmpty{break};for job in jobs{var bytes=try JSONSerialization.data(withJSONObject:job);bytes.append(10);try file.write(contentsOf:bytes);count+=1};guard let next=page["next"] as? Double,next<cursor else{throw PrintFailure("无效分页")};cursor=next};message="已导出 \(count) 条完整任务。"
 }
 @MainActor private func trustPublicKey() async throws {
  let panel=NSOpenPanel();panel.allowsMultipleSelection=false;guard panel.runModal() == .OK,let url=panel.url else{return};let bytes=try RelayTransport.file(url,limit:16384);guard bytes.count<=16384 else{throw PrintFailure("公钥文件过大")};details=try pretty(await request("admin/public-keys",["name":name,"public_key":String(decoding:bytes,as:UTF8.self)]))
 }
 @MainActor private func privateLogin() async throws {
  let panel=NSOpenPanel();panel.allowsMultipleSelection=false;guard panel.runModal() == .OK,let url=panel.url else{return}
  let bytes=try RelayTransport.file(url,limit:32768);guard bytes.count<=32768,let value=try JSONSerialization.jsonObject(with:bytes) as? [String:Any],let id=value["id"] as? String,let pem=value["privateKeyPem"] as? String,let bound=value["server"] as? String,bound.trimmingCharacters(in:CharacterSet(charactersIn:"/"))==server.trimmingCharacters(in:CharacterSet(charactersIn:"/")) else{throw PrintFailure("私钥文件或绑定地址无效")}
  guard var der=Data(base64Encoded:pem.components(separatedBy:.newlines).filter{!$0.hasPrefix("---")}.joined()) else{throw PrintFailure("PEM 无效")}
  if pem.contains("BEGIN PRIVATE KEY") {der=try Self.pkcs1(der)}
  var error:Unmanaged<CFError>?
  guard let key=SecKeyCreateWithData(der as CFData,[kSecAttrKeyType:kSecAttrKeyTypeRSA,kSecAttrKeyClass:kSecAttrKeyClassPrivate] as CFDictionary,&error) else{throw PrintFailure("无法读取 RSA 私钥")}
  let challenge=try await request("auth/challenge",["id":id]);guard let text=challenge["message"] as? String,let nonce=challenge["challenge"] as? String,let signature=SecKeyCreateSignature(key,.rsaSignatureMessagePSSSHA256,Data(text.utf8) as CFData,&error) as Data? else{throw PrintFailure("签名失败")}
  try await accept(request("auth/prove",["challenge":nonce,"signature":signature.base64EncodedString()]))
 }
 private static func pkcs1(_ data:Data)throws->Data {
  let bytes=[UInt8](data);var offset=0
  func read(_ expected:UInt8)throws->Range<Int>{guard offset+2<=bytes.count,bytes[offset]==expected else{throw PrintFailure("PKCS8 无效")};offset+=1;var length=Int(bytes[offset]);offset+=1;if length>127{let count=length&127;guard count<=4,offset+count<=bytes.count else{throw PrintFailure("DER 无效")};length=0;for _ in 0..<count{length=(length<<8)|Int(bytes[offset]);offset+=1}};guard offset+length<=bytes.count else{throw PrintFailure("DER 越界")};let range=offset..<(offset+length);offset+=length;return range}
  let sequence=try read(0x30);offset=sequence.lowerBound;_ = try read(0x02);_ = try read(0x30);let octets=try read(0x04);return Data(bytes[octets])
 }
 @MainActor private func generateOwner() async throws {
  let panel=NSSavePanel();panel.nameFieldStringValue="paperang-owner.json";guard panel.runModal() == .OK,let url=panel.url else{return}
  var error:Unmanaged<CFError>?;guard let key=SecKeyCreateRandomKey([kSecAttrKeyType:kSecAttrKeyTypeRSA,kSecAttrKeySizeInBits:3072] as CFDictionary,&error),let pub=SecKeyCopyPublicKey(key),let raw=SecKeyCopyExternalRepresentation(key,&error) as Data?,let publicRaw=SecKeyCopyExternalRepresentation(pub,&error) as Data? else{throw PrintFailure("生成私钥失败")}
  let publicPEM="-----BEGIN RSA PUBLIC KEY-----\n"+publicRaw.base64EncodedString(options:.lineLength64Characters)+"\n-----END RSA PUBLIC KEY-----"
  var id="REPLACE_WITH_SERVER_CREDENTIAL_ID"
  if !token.isEmpty{let entry=try await request("admin/public-keys",["name":name.isEmpty ? "Mac owner":name,"public_key":publicPEM]);id=entry["id"] as? String ?? id}
  let body:[String:Any]=["server":server.trimmingCharacters(in:CharacterSet(charactersIn:"/")),"id":id,"privateKeyPem":"-----BEGIN RSA PRIVATE KEY-----\n"+raw.base64EncodedString(options:.lineLength64Characters)+"\n-----END RSA PRIVATE KEY-----"]
  let temporary=url.deletingLastPathComponent().appendingPathComponent(".paperang-owner-"+UUID().uuidString)
  guard FileManager.default.createFile(atPath:temporary.path,contents:nil,attributes:[.posixPermissions:0o600]) else{throw PrintFailure("无法创建私钥文件")}
  defer{try? FileManager.default.removeItem(at:temporary)}
  let handle=try FileHandle(forWritingTo:temporary);do{try handle.write(contentsOf:JSONSerialization.data(withJSONObject:body,options:.prettyPrinted));try handle.synchronize();try handle.close()}catch{try? handle.close();throw error}
  guard Darwin.rename(temporary.path,url.path)==0 else{throw PrintFailure("无法原子保存私钥")};try publicPEM.write(to:URL(fileURLWithPath:url.path+".public.pem"),atomically:true,encoding:.utf8);message="文件已保存；未登录时请先在服务器登记公钥，再填写 id。"
 }
}
