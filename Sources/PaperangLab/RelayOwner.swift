import Foundation
import Security
import CryptoKit
import PrintCore

struct RelayHTTPError:LocalizedError {let status:Int;let code:String;var errorDescription:String?{"中转 HTTP \(status)：\(code)"}}
enum RelayHTTP {
 static func root(_ text:String,allowHTTP:Bool=false)throws->URL {
  guard var u=URLComponents(string:text.trimmingCharacters(in:.whitespacesAndNewlines)),let host=u.host,u.user==nil,u.password==nil,u.query==nil,u.fragment==nil,["","/"].contains(u.path),u.scheme=="https" || (u.scheme=="http" && (allowHTTP || ["localhost","127.0.0.1","::1"].contains(host))) else{throw PrintFailure("请输入 HTTPS 根地址；HTTP 测试需明确开启")}
  u.scheme=u.scheme?.lowercased();u.host=host.lowercased();u.path="/";if (u.scheme=="https" && u.port==443)||(u.scheme=="http" && u.port==80){u.port=nil}
  guard let url=u.url else{throw PrintFailure("服务器地址无效")};return url
 }
 static func request(_ base:URL,_ method:String,_ path:String,_ body:[String:Any]?=nil,token:String="",claim:String?=nil)async throws->[String:Any] {
  var r=URLRequest(url:base.appendingPathComponent("v2/"+path));r.httpMethod=method;r.timeoutInterval=8;r.cachePolicy = .reloadIgnoringLocalCacheData
  r.setValue("1",forHTTPHeaderField:"X-Paperang-Native");r.setValue("text,raster1",forHTTPHeaderField:"X-Paperang-Content-Types")
  if !token.isEmpty{r.setValue("Bearer "+token,forHTTPHeaderField:"Authorization")};if let claim{r.setValue(claim,forHTTPHeaderField:"X-Job-Claim")}
  if let body{r.httpBody=try JSONSerialization.data(withJSONObject:body);r.setValue("application/json",forHTTPHeaderField:"Content-Type")}
  let (data,response)=try await RelayTransport.data(for:r);try Task.checkCancellation()
  guard let http=response as? HTTPURLResponse,let value=try JSONSerialization.jsonObject(with:data) as? [String:Any] else{throw PrintFailure("中转返回无效 JSON")}
  guard (200...299).contains(http.statusCode) else{throw RelayHTTPError(status:http.statusCode,code:value["error"] as? String ?? "request_failed")};return value
 }
}

struct RelayOwnerFile {
 let server:URL;let id:String;let key:SecKey;let fingerprint:String
 init(path:String,server:URL)throws {
  let bytes=try RelayTransport.file(URL(fileURLWithPath:path),limit:32768)
  guard let value=try JSONSerialization.jsonObject(with:bytes) as? [String:Any],let bound=value["server"] as? String,try RelayHTTP.root(bound,allowHTTP:true)==server,let id=value["id"] as? String,UUID(uuidString:id) != nil,let pem=value["privateKeyPem"] as? String else{throw PrintFailure("所有者文件未配对或绑定的服务器不一致")}
  guard pem.contains("BEGIN RSA PRIVATE KEY") || pem.contains("BEGIN PRIVATE KEY"),let raw=Data(base64Encoded:pem.components(separatedBy:.newlines).filter{!$0.hasPrefix("---")}.joined()) else{throw PrintFailure("私钥 PEM 无效")}
  let der=try pem.contains("BEGIN RSA PRIVATE KEY") ? raw:Self.pkcs1(raw)
  var error:Unmanaged<CFError>?
  guard let key=SecKeyCreateWithData(der as CFData,[kSecAttrKeyType:kSecAttrKeyTypeRSA,kSecAttrKeyClass:kSecAttrKeyClassPrivate] as CFDictionary,&error),let attributes=SecKeyCopyAttributes(key) as? [String:Any],let size=attributes[kSecAttrKeySizeInBits as String] as? Int,[3072,4096].contains(size),let pub=SecKeyCopyPublicKey(key),let pubDER=SecKeyCopyExternalRepresentation(pub,&error) as Data? else{throw PrintFailure("需要有效的 RSA 3072/4096 私钥")}
  let algorithm=Data([0x30,0x0d,0x06,0x09,0x2a,0x86,0x48,0x86,0xf7,0x0d,0x01,0x01,0x01,0x05,0x00])
  let spki=Self.der(0x30,algorithm+Self.der(0x03,Data([0])+pubDER))
  self.server=server;self.id=id;self.key=key;fingerprint=id+":"+SHA256.hash(data:spki).map{String(format:"%02x",$0)}.joined()
 }
 static func der(_ tag:UInt8,_ bytes:Data)->Data {
  var prefix=Data([tag]);if bytes.count<128{prefix.append(UInt8(bytes.count))}else{var n=bytes.count;var length=[UInt8]();while n>0{length.insert(UInt8(n&255),at:0);n>>=8};prefix.append(0x80|UInt8(length.count));prefix.append(contentsOf:length)};return prefix+bytes
 }
 static func pkcs1(_ data:Data)throws->Data {
  let bytes=[UInt8](data);var offset=0
  func read(_ expected:UInt8)throws->Range<Int>{guard offset+2<=bytes.count,bytes[offset]==expected else{throw PrintFailure("PKCS8 结构无效")};offset+=1;var length=Int(bytes[offset]);offset+=1;if length>127{let count=length&127;guard count>0,count<=4,offset+count<=bytes.count else{throw PrintFailure("DER 长度无效")};length=0;for _ in 0..<count{length=(length<<8)|Int(bytes[offset]);offset+=1}};guard length<=bytes.count-offset else{throw PrintFailure("DER 越界")};let range=offset..<(offset+length);offset+=length;return range}
  let outer=try read(0x30);guard offset==bytes.count else{throw PrintFailure("PKCS8 尾部数据无效")};offset=outer.lowerBound;_ = try read(0x02);_ = try read(0x30);let octets=try read(0x04);guard offset==outer.upperBound else{throw PrintFailure("不支持的 PKCS8 扩展")};return Data(bytes[octets])
 }
}

@MainActor final class RelayOwnerAuthentication {
 let owner:RelayOwnerFile
 private var access="";private var expires=Date.distantPast;private var renewal:Task<String,Error>?
 init(path:String,server:URL)throws{owner=try RelayOwnerFile(path:path,server:server)}
 func invalidate(){access="";expires = .distantPast}
 func cancel(){renewal?.cancel();renewal=nil;invalidate()}
 func token() async throws->String {
  if !access.isEmpty,Date()<expires{return access};if let renewal{let value=try await renewal.value;try Task.checkCancellation();return value}
  let task=Task<String,Error>{
   let challenge=try await RelayHTTP.request(owner.server,"POST","auth/challenge",["id":owner.id])
   guard challenge["algorithm"] as? String=="RSA-PSS-SHA256",let message=challenge["message"] as? String,let nonce=challenge["challenge"] as? String else{throw PrintFailure("不支持的所有者挑战")}
   var error:Unmanaged<CFError>?
   guard let signature=SecKeyCreateSignature(owner.key,.rsaSignatureMessagePSSSHA256,Data(message.utf8) as CFData,&error) as Data? else{throw PrintFailure("所有者签名失败")}
   let proof=try await RelayHTTP.request(owner.server,"POST","auth/prove",["challenge":nonce,"signature":signature.base64EncodedString()])
   guard proof["role"] as? String=="C",let token=proof["token"] as? String,!token.isEmpty,let age=proof["maxAge"] as? Int,age>0 else{throw PrintFailure("未取得 C 所有者会话")}
   try Task.checkCancellation();access=token;expires=Date().addingTimeInterval(Double(max(0,min(age,600)-60)));return token
  };renewal=task;defer{renewal=nil};let value=try await task.value;try Task.checkCancellation();return value
 }
}
