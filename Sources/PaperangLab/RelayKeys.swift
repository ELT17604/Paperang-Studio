import SwiftUI
extension Content {
 var relayKeyManager:some View {
  DisclosureGroup("v0.3.0 · C 权限管理面板") { RelaySecurityManager(server:lab.relayURL,previewCredential:$lab.relayAdminKey,clearPreview:{lab.clearRelayPrivateViews()}) }
 }
}

extension Lab {
 func clearRelayPrivateViews(){
  relayDetailTask?.cancel();relayPreviewTask?.cancel();relayDetailCache.removeAll();relayPreview=nil;relayPreviewRenderedSignature=""
  for i in relayJobs.indices{relayJobs[i].text="";relayJobs[i].content=nil;relayJobs[i].hasFullText=false}
 }
}
