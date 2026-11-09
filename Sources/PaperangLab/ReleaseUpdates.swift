import AppKit
import Foundation
import IntegrationCore

@MainActor enum ReleaseUpdates {
 private static var busy=false
 private struct Release:Decodable {let tag_name:String;let html_url:String;let draft:Bool;let prerelease:Bool}
 static func check() {
  guard !busy else{return};busy=true
  Task { @MainActor in
   defer{busy=false}
   do {
    var request=URLRequest(url:URL(string:"https://api.github.com/repos/ELT17604/Paperang-Studio/releases?per_page=100")!)
    request.timeoutInterval=20;request.cachePolicy = .reloadIgnoringLocalCacheData
    request.setValue("PaperangStudio/"+AppIdentity.version,forHTTPHeaderField:"User-Agent")
    let (data,response)=try await URLSession.shared.data(for:request)
    guard let http=response as? HTTPURLResponse,http.statusCode==200,data.count<=2*1024*1024 else{throw URLError(.badServerResponse)}
    let releases=try JSONDecoder().decode([Release].self,from:data)
    let latest=releases.compactMap{release -> (Release,[Int])? in
     guard !release.draft,!release.prerelease,let number=ProductVersion.parse(release.tag_name) else{return nil};return (release,number)
    }.max{$0.1.lexicographicallyPrecedes($1.1)}
    guard let latest else{show("GitHub 尚未发布 MacClient 正式发行版。");return}
    guard let current=ProductVersion.parse(AppIdentity.version) else{show("当前版本格式无法比较，请查看 GitHub 发行页面。");return}
    guard current.lexicographicallyPrecedes(latest.1) else{show("当前已是最新 MacClient 正式版。");return}
    guard latest.0.html_url.hasPrefix("https://github.com/ELT17604/Paperang-Studio/releases/tag/"),let url=URL(string:latest.0.html_url) else{throw URLError(.badURL)}
    let alert=NSAlert();alert.messageText="发现新版本 "+latest.0.tag_name
    alert.informativeText="当前版本：v"+AppIdentity.version+"\n打开 GitHub 发行页面下载安装包？"
    alert.addButton(withTitle:"打开下载页面");alert.addButton(withTitle:"稍后")
    if alert.runModal() == .alertFirstButtonReturn{NSWorkspace.shared.open(url)}
   } catch {show("无法检查更新，请检查网络或稍后重试。GitHub 可能限制了请求频率。")}
  }
 }
 private static func show(_ message:String){let alert=NSAlert();alert.messageText="检查 GitHub 更新";alert.informativeText=message+"\n当前版本：v"+AppIdentity.version;alert.addButton(withTitle:"好");alert.runModal()}
}
