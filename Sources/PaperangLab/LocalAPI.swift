import Foundation
import Network
import IntegrationCore
struct APIResponse:Sendable {
 let status:Int;let data:Data;var type="application/json; charset=utf-8"
 static func json(_ object:Any,status:Int=200)->Self{Self(status:status,data:(try? JSONSerialization.data(withJSONObject:object,options:[.sortedKeys])) ?? Data("{}".utf8))}
 static func failure(_ status:Int,_ code:String,_ message:String)->Self{.json(["error":["code":code,"message":message]],status:status)}
}
final class LocalAPI:@unchecked Sendable {
 let port:UInt16;let token:String;let origins:Set<String>
 var handler:(@MainActor (HTTPRequest)async->APIResponse)?
 var onState:((Bool,String)->Void)?
 private let queue=DispatchQueue(label:"local.paperang.http")
 private var listener:NWListener?
 private var connections=[UUID:NWConnection]()
 private var recent=[Date]()
 init(port:UInt16,token:String,origins:Set<String>){self.port=port;self.token=token;self.origins=origins}
 func start()throws {
  let params=NWParameters.tcp;params.requiredLocalEndpoint = .hostPort(host:"127.0.0.1",port:NWEndpoint.Port(rawValue:port)!);params.allowLocalEndpointReuse=true
  let server=try NWListener(using:params);listener=server
  server.stateUpdateHandler={[weak self] state in switch state{case .ready:self?.onState?(true,"http://127.0.0.1:\(self?.port ?? 0)");case .failed(let e):self?.onState?(false,e.localizedDescription);self?.stop();case .cancelled:self?.onState?(false,"API已停止");default:break}}
  server.newConnectionHandler={[weak self] connection in self?.accept(connection)};server.start(queue:queue)
 }
 func stop(){queue.async{[self] in self.listener?.cancel();self.listener=nil;for c in self.connections.values{c.cancel()};self.connections=[:]}}
 private func accept(_ connection:NWConnection){guard connections.count<8 else{connection.cancel();return};let id=UUID();connections[id]=connection;connection.start(queue:queue);receive(connection,id:id,buffer:Data());queue.asyncAfter(deadline:.now()+12){[weak self] in if let c=self?.connections.removeValue(forKey:id){c.cancel()}}}
 private func receive(_ connection:NWConnection,id:UUID,buffer:Data){connection.receive(minimumIncompleteLength:1,maximumLength:65536){[weak self] data,_,complete,error in guard let self,self.connections[id] != nil else{return};var next=buffer;if let data{next.append(data)}
   do {if let request=try HTTPParser.parse(next){
     guard HTTPParser.authenticate(request,token:self.token,port:self.port,origins:self.origins) else{self.reply(.failure(403,"forbidden","令牌、Host或Origin不被允许"),request:request,connection:connection,id:id);return}
     self.recent=self.recent.filter{Date().timeIntervalSince($0)<1};guard self.recent.count<20 else{self.reply(.failure(429,"rate_limited","请求过快"),request:request,connection:connection,id:id);return};self.recent.append(Date())
     if request.path=="/v1" || request.path.hasPrefix("/v1/"){self.reply(.failure(410,"api_v1_retired","Use the local API v2 contract"),request:request,connection:connection,id:id);return}
     if request.method=="OPTIONS"{self.reply(APIResponse(status:204,data:Data()),request:request,connection:connection,id:id);return}
     if request.method=="POST",request.headers["content-type"]?.split(separator:";").first?.trimmingCharacters(in:.whitespaces).lowercased() != "application/json" {self.reply(.failure(415,"content_type","POST需要application/json"),request:request,connection:connection,id:id);return}
     Task{let response=await self.handler?(request) ?? .failure(503,"unavailable","App尚未准备好");self.queue.async{guard self.connections[id] != nil else{return};self.reply(response,request:request,connection:connection,id:id)}}
    }else if complete || error != nil {connection.cancel();self.connections.removeValue(forKey:id)}else{self.receive(connection,id:id,buffer:next)}
   }catch{self.reply(.failure(400,"invalid_http","请求格式无效、超长或不支持chunked"),request:nil,connection:connection,id:id)}
  }}
 private func reply(_ response:APIResponse,request:HTTPRequest?,connection:NWConnection,id:UUID){let reason=[200:"OK",202:"Accepted",204:"No Content",400:"Bad Request",403:"Forbidden",404:"Not Found",409:"Conflict",410:"Gone",413:"Payload Too Large",415:"Unsupported Media Type",429:"Too Many Requests",503:"Service Unavailable"][response.status] ?? "Error"
  var header="HTTP/1.1 \(response.status) \(reason)\r\nContent-Type: \(response.type)\r\nContent-Length: \(response.data.count)\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\n"
  if let origin=request?.headers["origin"],origins.contains(origin){header+="Access-Control-Allow-Origin: \(origin)\r\nVary: Origin\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\nAccess-Control-Allow-Private-Network: true\r\n"}
  let wire=Data((header+"\r\n").utf8)+response.data
  connection.send(content:wire,completion:.contentProcessed{[weak self] _ in self?.queue.async{self?.connections.removeValue(forKey:id);connection.cancel()}})
 }
}
