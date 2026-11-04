import SwiftUI
import AppKit

@MainActor final class StudioLifecycle:NSObject,NSApplicationDelegate {
 static weak var lab:Lab?
 func applicationShouldTerminateAfterLastWindowClosed(_ sender:NSApplication)->Bool {false}
 func applicationShouldHandleReopen(_ sender:NSApplication,hasVisibleWindows:Bool)->Bool {
  // Only the explicit menu action exits background mode. Reopen events must not stop its listener.
  return Self.lab?.trayMode != true
 }
 func applicationWillTerminate(_ notification:Notification){guard let lab=Self.lab else{return};lab.stopTTY();lab.stopAPI();lab.stopRelay();lab.disconnect()}
}

extension Lab {
 var networkPrintAllowed:Bool {trayMode ? !trayPrintPaused:allowPaper}
 var automaticNetworkConsent:Bool {trayMode && !trayPrintPaused}
 func networkJobAuthorized(_ value:Any?)->Bool {
  guard networkPrintAllowed else{return false}
  if let value{return trueConsent(value)}
  return automaticNetworkConsent
 }
 /// Shared session transition; the window action below only changes presentation.
 func beginTraySession(){
  guard !trayMode else{return}
  trayPreviousAutoPrint=relayAutoPrint;trayStartedAPI=api==nil
  trayMode=true;trayPrintPaused=false
  stopTTY();terminalTimer?.cancel();terminalTimer=nil;previewTask?.cancel();previewTask=nil;rendering=false
  relayDetailTask?.cancel();relayDetailTask=nil;relayPreviewTask?.cancel();relayPreviewTask=nil;relayPreviewLoading=false
  if api==nil{startAPI()}
  relayAutoPrint=relayRunning
  notice="托盘模式：网络请求可自动打印；工作台耗纸设置未改变"
  log("TRAY","Entered; network-only print consent active")
 }
 func enterTray(){
  beginTraySession()
  NSApp.setActivationPolicy(.accessory)
  for window in NSApp.windows where window.level == .normal && window.isVisible{window.orderOut(nil)}
 }
 func exitTray(){
  guard trayMode else{return}
  trayMode=false;trayPrintPaused=false
  relayAutoPrint=trayPreviousAutoPrint && relayRunning
  if trayStartedAPI{stopAPI()};trayStartedAPI=false
  NSApp.setActivationPolicy(.regular)
  schedulePreview()
  log("TRAY","Exited; restored prior network print policy")
 }
 func setTrayPrintingPaused(_ paused:Bool){
  guard trayMode else{return};trayPrintPaused=paused
  relayAutoPrint = !paused && relayRunning
  log("TRAY",paused ? "Paused new network prints; buffered data cannot be recalled":"Resumed automatic network prints")
 }
}

struct TrayMenu:View {
 @ObservedObject var lab:Lab
 @Environment(\.openWindow) private var openWindow
 var body:some View {
  Text("Paperang Studio v"+AppIdentity.version)
  Text(lab.trayPrintPaused ? "网络打印已暂停":"托盘模式 · 自动允许网络打印")
  Divider()
  Text(lab.ready ? "蓝牙："+lab.connectedName:"蓝牙：未连接")
  Text(lab.busy ? "打印："+lab.jobStatus:"打印：空闲")
  Text(lab.apiRunning ? "本地 API：127.0.0.1:\(lab.apiPort)":"本地 API：未监听")
  Text(lab.relayOnline ? "中转：在线":"中转："+(lab.relayRunning ? "连接/重试中":"未连接"))
  if lab.relayRunning{Text(lab.relayAutoPrint ? "中转自动处理：开启":"中转自动处理：暂停")}
  if !lab.relayMessage.isEmpty{Text(lab.relayMessage)}
  if !lab.notice.isEmpty{Text(lab.notice)}
  if !lab.error.isEmpty{Text("错误："+lab.error)}
  if lab.relayWarning{Text("警告：中转重连失败，已断开")}
  Divider()
  Button(lab.trayPrintPaused ? "恢复网络自动打印":"暂停网络自动打印"){lab.setTrayPrintingPaused(!lab.trayPrintPaused)}
  Button("复制本地 API 令牌",action:lab.copyAPIToken).disabled(!lab.apiRunning)
  Button("恢复主界面（退出托盘）"){
   lab.exitTray();openWindow(id:"main");NSApp.activate(ignoringOtherApps:true)
  }.keyboardShortcut("o",modifiers:[.command])
  Divider()
  Text(AppIdentity.copyright)
  Button("退出 Paperang Studio"){NSApp.terminate(nil)}.keyboardShortcut("q",modifiers:[.command])
 }
}
