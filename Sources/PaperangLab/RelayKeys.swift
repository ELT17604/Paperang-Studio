import SwiftUI
extension Content {
 var relayKeyManager:some View {
  DisclosureGroup("API v2 · C 权限管理中心") { RelaySecurityManager(server:lab.relayURL,previewCredential:$lab.relayAdminKey,clearPreview:{lab.clearRelayPrivateViews()},ownerSelected:{lab.relayOwnerPath=$0;lab.relayAuthMode="owner";lab.flushRelayConfiguration()}) }
 }
}

extension Lab {
 func clearRelayPrivateViews(){
  relayDetailTask?.cancel();relayPreviewTask?.cancel();relayDetailCache.removeAll();relayPreview=nil;relayPreviewRenderedSignature=""
  for i in relayJobs.indices{relayJobs[i].text="";relayJobs[i].content=nil;relayJobs[i].hasFullText=false}
 }
}
