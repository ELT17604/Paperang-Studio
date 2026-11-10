import Foundation
import AppKit
import Security
import PrintCore
import IntegrationCore

@main struct MacUpgradeCheck {
 static func ensure(_ condition:@autoclosure()->Bool,line:UInt=#line){if !condition(){FileHandle.standardError.write(Data("FAILED native check at line \(line)\n".utf8));exit(1)}}
 @MainActor static func main() async throws {
  _ = NSApplication.shared;NSApp.finishLaunching()
  for old in ["0.3.1-macclient", "v0.3.1-macclient-opt99", "0.3.1-macclient-fix99"] {ensure(ProductVersion.parse(old)!.lexicographicallyPrecedes(ProductVersion.parse("0.3.2-macclient")!))}
  ensure(ProductVersion.parse("v0.3.2-macclient")==ProductVersion.parse("0.3.2-macclient"));ensure(ProductVersion.parse("0.3.2")==nil);ensure(ProductVersion.parse("0.3.2-winlite")==nil);ensure(ProductVersion.parse("0.3.2-rc1")==nil)
  ensure(ProductVersion.release("v0.3.2",assets:["PaperangStudio-0.3.2-macclient-macos-arm64.zip"])==ProductVersion.parse("0.3.2-macclient"))
  ensure(ProductVersion.release("v0.3.2",assets:[])==nil)
  ensure(ProductVersion.release("v0.3.2",assets:["PaperangStudio-0.3.2-winlite-win-x64-setup.exe"])==nil)
  let stateLab=Lab(offline:true,restore:false)
  for (label,state) in [("发送中","printing"),("已发送，待核对纸面","sent"),("已停止","cancelled"),("未完成","unknown")] {ensure(stateLab.localJobState(label)==state)}
  let data=try Data(contentsOf:URL(fileURLWithPath:CommandLine.arguments[1]));let c=try JSONSerialization.jsonObject(with:data) as! [String:Any]
  let base=try RelayHTTP.root(c["server"] as! String);let path=c["ownerPath"] as! String;let owner=try RelayOwnerAuthentication(path:path,server:base)
  let second=try RelayOwnerFile(path:c["pkcs1Path"] as! String,server:base)
  ensure(owner.owner.fingerprint==second.fingerprint && owner.owner.fingerprint==c["fingerprint"] as? String)
  do{_ = try RelayOwnerFile(path:path,server:RelayHTTP.root("http://localhost:1"));fatalError("Wrong server accepted")}catch{}
  async let a=owner.token();async let b=owner.token();let tokens=try await (a,b);ensure(tokens.0==tokens.1)
  let access=tokens.0;let pub=try await RelayHTTP.request(base,"GET","public/status");ensure((pub["capabilities"] as? [String:Any])?["authVersion"] as? Int==2)
  _ = try await RelayHTTP.request(base,"POST","device/status",["status":["connected":false]],token:access)
  let queue=try await RelayHTTP.request(base,"GET","device/jobs",token:access);let jobs=(queue["jobs"] as! [[String:Any]]).compactMap(RelayJob.decode);ensure(jobs.count>=2 && jobs.allSatisfy{!$0.hasFullText && $0.text.isEmpty})
  let first=jobs[0];let claim=try await RelayHTTP.request(base,"POST","device/jobs/\(first.id)/claim",[:],token:access)["claim"] as! String
  do{_ = try await RelayHTTP.request(base,"GET","device/jobs/\(first.id)",token:access);fatalError("Missing claim accepted")}catch let e as RelayHTTPError{ensure(e.status==403)}
  let full=try await RelayHTTP.request(base,"GET","device/jobs/\(first.id)",token:access,claim:claim);ensure(((full["text"] as? String)?.count ?? 0)>120)
  _ = try await RelayHTTP.request(base,"POST","device/jobs/\(first.id)/result",["claim":claim,"state":"sent"],token:access)
  do{_ = try await RelayHTTP.request(base,"GET","device/jobs/\(first.id)",token:access,claim:claim);fatalError("Historical payload exposed via device route")}catch let e as RelayHTTPError{ensure(e.status==403)}
  owner.invalidate();let renewed=try await owner.token();ensure(renewed != access)
  fflush(stdout);print("PASS: PKCS8/PKCS1 and Windows-compatible fingerprint; RSA-PSS proof, coalesced renewal, C execution and active-claim privacy")

  var published="";let model=RelayAdminModel(server:base.absoluteString,publishSession:{published=$0},clearPreview:{},ownerSelected:{_ in})
  model.section="keys";model.pageSize=25;model.userID="";model.hideRevoked=true
  try await model.accept(["role":"C","token":renewed,"maxAge":600]);ensure(published==renewed && model.rows.count==25)
  let firstPage=model.rows.compactMap{$0["id"] as? String};try await model.nextPage();let nextPage=model.rows.compactMap{$0["id"] as? String};ensure(Set(firstPage).isDisjoint(with:Set(nextPage)))
  model.previousPage();ensure(model.rows.compactMap{$0["id"] as? String}==firstPage)
  model.pageSize=50;try await model.refresh();ensure(model.rows.count==50)
  model.pageSize=100;try await model.refresh();ensure(model.rows.count>=80)
  model.section="jobs";model.pageSize=25;try await model.refresh();ensure(model.rows.allSatisfy{$0["text"]==nil && $0["content"]==nil})
  model.selected="0";try await model.selectDetails();ensure(model.detailRecord?["text"] != nil)
  let rasterIndex=model.rows.firstIndex{$0["id"] as? String==c["rasterJob"] as? String}!
  model.selected=String(rasterIndex);try await model.selectDetails();ensure(model.detailImage != nil && !model.detailText.isEmpty)
  model.quotaTarget="anonymous";try await model.quotaRequest(false);model.quotaJobs="0";try await model.quotaRequest(true);ensure(model.quotaJobs=="0")
  model.quotaJobs="";try await model.quotaRequest(true);ensure(model.quotaJobs.isEmpty)
  model.section="keys";model.rows=[["id":"fixture1","user_id":c["user1"]!],["id":"fixture2","user_id":c["user2"]!]];model.selected="0";model.quotaTarget="selected";try await model.quotaRequest(false);model.selected="1"
  do{try await model.quotaRequest(true);fatalError("Stale quota target accepted")}catch{}
  model.name="short phrase fixture";model.kind="passphrase";model.role="B";model.phrase="x";model.existingIdentity="";try await model.create()
  let created=try JSONSerialization.jsonObject(with:Data(model.details.utf8)) as! [String:Any];let login=try await RelayHTTP.request(base,"POST","auth/login",["credential":created["token"]!]);ensure(login["role"] as? String=="B")
  _ = try await RelayHTTP.request(base,"POST","admin/validation-mode",["legacy":true],token:renewed)
  model.section="keys";model.pageSize=25;try await model.refresh();let legacyFirst=model.rows.compactMap{$0["id"] as? String};try await model.nextPage();ensure(Set(legacyFirst).isDisjoint(with:Set(model.rows.compactMap{$0["id"] as? String})))
  model.section="jobs";try await model.refresh();ensure(model.rows.count==20 && model.rows.allSatisfy{$0["text"]==nil && $0["content"]==nil})
  _ = try await RelayHTTP.request(base,"POST","admin/validation-mode",["legacy":false],token:renewed)
  let generated=URL(fileURLWithPath:CommandLine.arguments[1]).deletingLastPathComponent().appendingPathComponent("native-generated-owner.json")
  try await model.createOwner(at:generated)
  let permissions=try FileManager.default.attributesOfItem(atPath:generated.path)[.posixPermissions] as! NSNumber;ensure(permissions.intValue&0o077==0)
  let generatedAuth=try RelayOwnerAuthentication(path:generated.path,server:base);_ = try await generatedAuth.token()
  let originalBytes=try Data(contentsOf:URL(fileURLWithPath:path))
  do{try await model.createOwner(at:URL(fileURLWithPath:path));fatalError("Existing private file overwritten")}catch{}
  let preservedBytes=try Data(contentsOf:URL(fileURLWithPath:path));ensure(preservedBytes==originalBytes)
  model.lock();ensure(model.token.isEmpty && published.isEmpty && model.rows.isEmpty && model.details.isEmpty)
  fflush(stdout);print("PASS: C native management 25/50/100 paging, previous page, private text/raster detail, zero/inherit quotas, stale-target refusal, unrestricted phrase and lock")

  RelayVault.service=c["vaultService"] as! String
  defer{for account in ["connection","pending","connectionDraft"]{try? RelayVault.remove(account)}}
  let lab=Lab(offline:true,restore:false);lab.relayURL=base.absoluteString;lab.relayAuthMode="owner";lab.relayOwnerPath=path;lab.relayAutoConnect=false
  lab.startRelay();for _ in 0..<100{if lab.relayOnline && !lab.relayJobs.isEmpty{break};try await Task.sleep(for:.milliseconds(50))};if !lab.relayOnline{print("Relay connection diagnostic: "+lab.relayMessage);fflush(stdout)};ensure(lab.relayOnline && !lab.relayJobs.isEmpty && !lab.relayIdentityFingerprint.isEmpty)
  owner.invalidate();lab.relayAdminKey=try await owner.token();ensure(!lab.relayJobs.isEmpty);lab.relaySelection=lab.relayJobs[0].id;lab.previewRelay()
  for _ in 0..<100{if !lab.relayPreviewLoading{break};try await Task.sleep(for:.milliseconds(30))}
  ensure(lab.relayPreviewIsCurrent);try await lab.relayTick();ensure(lab.relayPreviewIsCurrent)
  let originalInvert=lab.settings.invert;lab.settings.invert.toggle();ensure(!lab.relayPreviewIsCurrent);lab.settings.invert=originalInvert
  lab.clearRelayPrivateViews();ensure(lab.relayPreview==nil && !lab.relayPreviewLoading)
  print("PASS: actual full preview survives queue refresh, print-setting changes invalidate it, and locking clears private views")

  // Heartbeat online is published before queue refresh; wait for both before
  // selecting another job after report-only recovery. Never reclaim stale rows.
  let pendingJob=lab.relayJobs[0];let pendingClaim=try await lab.relayRequest("POST","v2/device/jobs/\(pendingJob.id)/claim",[:])["claim"] as! String
  lab.relayFlight=RelayFlight(serverID:pendingJob.id,claim:pendingClaim,localID:nil,outcome:"unknown");try lab.persistRelayFlight();lab.stopRelay();ensure(lab.relayFlight?.outcome=="unknown")
  lab.startRelay();for _ in 0..<100{if lab.relayOnline && lab.relayFlight==nil && !lab.relayJobs.contains(where:{$0.id==pendingJob.id}){break};try await Task.sleep(for:.milliseconds(50))};ensure(lab.relayOnline && lab.relayFlight==nil && !lab.relayJobs.contains(where:{$0.id==pendingJob.id}))
  let active=try RelayVault.read("connection",as:RelaySavedConnection.self)!;ensure(active.credential.isEmpty && active.authMode=="owner" && active.ownerPath==path)
  let secondJob=lab.relayJobs[0];let secondClaim=try await lab.relayRequest("POST","v2/device/jobs/\(secondJob.id)/claim",[:])["claim"] as! String
  lab.relayFlight=RelayFlight(serverID:secondJob.id,claim:secondClaim,localID:nil,outcome:"sent");try lab.persistRelayFlight();lab.stopRelay();ensure(lab.relayFlight?.outcome=="sent")
  let durable=try RelayVault.read("pending",as:RelaySavedClaim.self)!;ensure(durable.state=="sent")
  lab.startRelay();for _ in 0..<100{if lab.relayOnline && lab.relayFlight==nil && !lab.relayJobs.contains(where:{$0.id==secondJob.id}){break};try await Task.sleep(for:.milliseconds(50))};ensure(lab.relayFlight==nil && lab.relayOnline && !lab.relayJobs.contains(where:{$0.id==secondJob.id}))
  lab.stopRelay()
  var legacy=RelaySavedConnection(server:base.absoluteString,credential:"fixture");let bytes=try JSONEncoder().encode(legacy);legacy=try JSONDecoder().decode(RelaySavedConnection.self,from:bytes);ensure(legacy.authMode==nil)
  lab.relayStyle="自定义";lab.relayTemplates=[.preset("便签")];lab.relayTemplateID=lab.relayTemplates[0].id.uuidString
  let plan=lab.relayRenderPlan();let decoded=RelayJob.decode(full)!;let imageBefore=try plan.raster(decoded);lab.relayTemplates[0].blocks[0].size=72;lab.relayFeed=999;let imageAfter=try plan.raster(decoded);ensure(imageBefore.width==576 && imageAfter.width==576 && imageBefore.height==imageAfter.height && plan.template.blocks[0].size==24 && plan.settings.feedAfter != 999)
  let snapshot=lab.configurationSnapshot();let encoded=try JSONEncoder().encode(snapshot);try snapshot.validate();let restored=try JSONDecoder().decode(MacConfiguration.self,from:encoded);ensure(restored.ownerPath==path)
  let text=String(decoding:encoded,as:UTF8.self);ensure(!text.contains("privateKeyPem") && !text.contains(renewed) && !text.contains("psr_"))
  let oldPreferences=UserDefaults.standard.data(forKey:"mac.configuration.v1")
  defer{if let oldPreferences{UserDefaults.standard.set(oldPreferences,forKey:"mac.configuration.v1")}else{UserDefaults.standard.removeObject(forKey:"mac.configuration.v1")}}
  lab.flushRelayConfiguration()
  let restoredLab=Lab(offline:true,restore:true);restoredLab.configurationObserver?.cancel();restoredLab.configurationObserver=nil;restoredLab.previewTask?.cancel()
  ensure(restoredLab.relayTemplates==lab.relayTemplates && restoredLab.relayTemplateID==lab.relayTemplateID)
  ensure(restoredLab.relayOwnerPath==path && restoredLab.relayFeed==999 && restoredLab.relayAuthMode=="owner" && !restoredLab.relayAutoConnect)
  let m=Lab(offline:true,restore:false);m.relayURL=base.absoluteString;m.relayAuthMode="M";m.relayKey=c["mToken"] as! String;m.startRelay()
  for _ in 0..<100{if m.relayOnline{break};try await Task.sleep(for:.milliseconds(50))};ensure(m.relayOnline && m.relayOwner==nil);m.stopRelay()
  let noBrowser=RelayJob(id:String(repeating:"a",count:40),text:"body",metadata:["userAgent":"UA fixture","language":"zh-CN"],created:Date());ensure(noBrowser.browserDescription.contains("UA fixture"))
  fflush(stdout);print("PASS: actual Mac relay owner loop, isolated Keychain connection/pending recovery, report-only unknown/sent, sent preservation, layout snapshots and safe configuration schema")
 }
}
