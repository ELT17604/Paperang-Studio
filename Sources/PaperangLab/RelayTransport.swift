import PrintCore
import Foundation

final class RelayNoRedirect:NSObject,URLSessionTaskDelegate,@unchecked Sendable {
 func urlSession(_ session:URLSession,task:URLSessionTask,willPerformHTTPRedirection response:HTTPURLResponse,newRequest request:URLRequest,completionHandler:@escaping (URLRequest?)->Void){completionHandler(nil)}
}

enum RelayTransport {
 static let session:URLSession = {
  let config=URLSessionConfiguration.ephemeral
  config.httpShouldSetCookies=false;config.urlCache=nil
  config.timeoutIntervalForRequest=8;config.timeoutIntervalForResource=8
  return URLSession(configuration:config,delegate:RelayNoRedirect(),delegateQueue:nil)
 }()
 static func data(for request:URLRequest) async throws->(Data,URLResponse) {
  let (stream,response)=try await session.bytes(for:request)
  var data=Data()
  for try await byte in stream {
   guard data.count<1_048_576 else{throw PrintFailure("中转响应超过 1 MB")}
   data.append(byte)
  }
  return (data,response)
 }
 static func file(_ url:URL,limit:Int)throws->Data {
  let file=try FileHandle(forReadingFrom:url);defer{try? file.close()}
  let data=try file.read(upToCount:limit+1) ?? Data()
  guard data.count<=limit else{throw PrintFailure("文件过大")};return data
 }
}
