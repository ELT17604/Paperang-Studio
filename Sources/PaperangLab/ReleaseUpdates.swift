import AppKit
import Foundation

@MainActor enum ReleaseUpdates {
 private static var busy=false
 private struct Release:Decodable {let tag_name:String;let html_url:String;let draft:Bool;let prerelease:Bool}
 private static func version(_ tag:String)->[Int]? {
  guard tag.range(of:"^v?[0-9]+\\.[0-9]+\\.[0-9]+-macclient(?:-fix[0-9]+)?$",options:.regularExpression) != nil else{return nil}
  let parts=tag.hasPrefix("v") ? String(tag.dropFirst()).split(separator:"-"):tag.split(separator:"-")
  var numbers=parts[0].split(separator:".").compactMap{Int($0)}
  guard numbers.count==3 else{return nil}
  if parts.count>2 {guard let fix=Int(parts[2].dropFirst(3)) else{return nil};numbers.append(fix)}else{numbers.append(0)}
  return numbers
 }
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
    let latest=releases.filter{!$0.draft && !$0.prerelease && version($0.tag_name) != nil}.max{version($0.tag_name)!.lexicographicallyPrecedes(version($1.tag_name)!)}
    guard let latest else{show("GitHub 尚未发布 MacClient 正式发行版。");return}
    guard let current=version(AppIdentity.version) else{show("当前版本格式无法比较，请查看 GitHub 发行页面。");return}
    guard current.lexicographicallyPrecedes(version(latest.tag_name)!) else{show("当前已是最新 MacClient 正式版。");return}
    guard latest.html_url.hasPrefix("https://github.com/ELT17604/Paperang-Studio/releases/tag/"),let url=URL(string:latest.html_url) else{throw URLError(.badURL)}
    let alert=NSAlert();alert.messageText="发现新版本 "+latest.tag_name
    alert.informativeText="当前版本：v"+AppIdentity.version+"\n打开 GitHub 发行页面下载安装包？"
    alert.addButton(withTitle:"打开下载页面");alert.addButton(withTitle:"稍后")
    if alert.runModal() == .alertFirstButtonReturn{NSWorkspace.shared.open(url)}
   } catch {show("无法检查更新，请检查网络或稍后重试。GitHub 可能限制了请求频率。")}
  }
 }
 private static func show(_ message:String){let alert=NSAlert();alert.messageText="检查 GitHub 更新";alert.informativeText=message+"\n当前版本：v"+AppIdentity.version;alert.addButton(withTitle:"好");alert.runModal()}
}
