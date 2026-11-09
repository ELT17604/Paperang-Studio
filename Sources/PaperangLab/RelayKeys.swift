import SwiftUI
extension Lab {
 func clearRelayPrivateViews(){
  relayDetailTask?.cancel();relayPreviewTask?.cancel();relayPreview=nil;relayPreviewRenderedSignature="";relayPreviewLoading=false
  for i in relayJobs.indices{relayJobs[i].text="";relayJobs[i].content=nil;relayJobs[i].hasFullText=false}
 }
}
