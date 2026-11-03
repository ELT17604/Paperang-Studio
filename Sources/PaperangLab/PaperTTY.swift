import SwiftUI
import TerminalCore
import PrintCore
extension Lab {
 func startTTY(){guard !ttyRunning else{return};guard ttyFlight==nil,terminalBuffer.pendingCount==0 else{ttyMessage="请先打印或丢弃上一会话的缓冲，等待在途打印完成后再启动";return};guard FileManager.default.fileExists(atPath:ttyDirectory) else{error="终端目录不存在";return}
  let session=PTYSession();terminalBuffer=PaperTerminalBuffer();ttyFlight=nil;ttyPendingCount=0;ttyScreen="";ttyAutoPrint=false
  session.onOutput={[weak self,weak session] data in Task{@MainActor in guard let self,let session,self.terminal===session else{return};guard !self.ttyPrivateInput else{return};self.terminalBuffer.feed(data);self.ttyScreen=String(self.terminalBuffer.transcript.suffix(64000));self.ttyPendingCount=self.terminalBuffer.pendingCount;self.ttyLastOutput=Date();if self.ttyPendingCount>12000{self.ttyAutoPrint=false;self.ttyMessage="输出较多，自动打印已暂停；可丢弃缓冲或逐批打印"}}}
  session.onExit={[weak self,weak session] status in Task{@MainActor in guard let self,let session,self.terminal===session else{return};self.ttyRunning=false;self.ttyMessage="shell已退出（wait状态\(status)），缓冲仍可手动打印";self.log("TTY","shell退出；未记录命令内容")}}
  do{try session.start(directory:ttyDirectory,columns:ttyColumns,rows:ttyRows,loadProfile:ttyLoadProfile);terminal=session;ttyName=session.name;ttyRunning=true;ttyMessage="真实PTY已启动，TERM=dumb；默认不自动耗纸";log("TTY","\(session.name) pid=\(session.pid)");terminalTimer?.cancel();terminalTimer=Task{while !Task.isCancelled{try? await Task.sleep(for:.seconds(1));if Task.isCancelled{break};self.pumpTTY()}}}catch{self.error=error.localizedDescription}
 }
 func sendTTY(){guard ttyRunning else{return};guard ttyInput.utf8.count<=8192 else{ttyMessage="单次输入最多8192字节";return};terminal?.write(Data((ttyInput+"\r").utf8));ttyInput=""}
 func ttyKey(_ bytes:[UInt8]){guard ttyRunning else{return};terminal?.write(Data(bytes))}
 func stopTTY(){ttyAutoPrint=false;terminal?.stop();ttyRunning=false;ttyMessage="已停止shell；已发送给打印机的数据无法撤回"}
 func chooseTTYDirectory(){let p=NSOpenPanel();p.canChooseDirectories=true;p.canChooseFiles=false;guard p.runModal() == .OK,let url=p.url else{return};ttyDirectory=url.path}
 func discardTTY(){terminalBuffer.clearPending();ttyPendingCount=terminalBuffer.pendingCount;ttyMessage="已丢弃尚未提交的纸带缓冲"}
 func pumpTTY(manual:Bool=false){
  if let (id,text)=ttyFlight,let job=jobs.first(where:{$0.id==id}) {
   if job.status=="发送中"{return}
   ttyFlight=nil
   if !job.status.hasPrefix("已发送"){terminalBuffer.prepend(text);ttyPendingCount=terminalBuffer.pendingCount;ttyAutoPrint=false;ttyMessage="纸带发送未完成，文本已放回缓冲；请核对纸面后手动处理";return}
  }
  guard manual || ttyAutoPrint else{return};guard !ttyPrivateInput else{ttyMessage="隐私输入期间不记录或打印输出";return}
  guard ready,allowPaper,!busy else{if manual{ttyMessage="请连接P2、允许耗纸，并等待当前打印完成"};return}
  guard manual || Date().timeIntervalSince(ttyLastOutput)>=Double(ttyIdleSeconds) else{return}
  let chunk=terminalBuffer.take(maxCharacters:1200,includePartial:true);guard !chunk.isEmpty else{return}
  do{var s=PrintSettings();s.fontName="Menlo";s.fontSize=ttyFontSize;s.alignment="居左";s.marginX=8;s.marginY=4;s.contentWidth=560;s.lineSpacing=0;s.density=100;s.feedAfter=ttyFeed
   guard !chunk.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty else{ttyPendingCount=terminalBuffer.pendingCount;return};let r=try Renderer.render(text:chunk,image:nil,mode:"文字",settings:s);let id=try submitRaster(r,settings:s,title:"纸带终端 \(ttyName)");ttyFlight=(id,chunk);ttyPendingCount=terminalBuffer.pendingCount;ttyMessage="已提交纸带输出，等待设备发送完成"
  }catch{terminalBuffer.prepend(chunk);ttyPendingCount=terminalBuffer.pendingCount;ttyAutoPrint=false;ttyMessage=error.localizedDescription}
 }
 func exportTTY(){let p=NSSavePanel();p.nameFieldStringValue="Paper-TTY-transcript.txt";guard p.runModal() == .OK,let url=p.url else{return};do{try ttyScreen.write(to:url,atomically:true,encoding:.utf8)}catch{self.error=error.localizedDescription}}
}
extension Content {
 var terminalPage:some View{VStack(alignment:.leading,spacing:12){
  HStack{Label(lab.ttyName,systemImage:"terminal").font(.headline.monospaced());Spacer();Text(lab.ttyRunning ? "zsh · PID \(lab.terminal?.pid ?? 0) · TERM=dumb":"未运行").foregroundStyle(.secondary);Button("导出文本",action:lab.exportTTY)}
  if !lab.ttyRunning{GroupBox("启动真实本地PTY"){VStack{HStack{TextField("工作目录",text:$lab.ttyDirectory);Button("选择目录",action:lab.chooseTTYDirectory)};HStack{integer("列",value:$lab.ttyColumns,range:20...200);integer("行",value:$lab.ttyRows,range:5...100);Toggle("加载用户.zshrc",isOn:$lab.ttyLoadProfile);Button("启动终端",action:lab.startTTY).buttonStyle(.borderedProminent)}}.padding(6)}}
  ScrollViewReader{proxy in ScrollView([.vertical,.horizontal]){VStack(alignment:.leading){Text(lab.ttyScreen.isEmpty ? "等待终端输出…":lab.ttyScreen).font(.system(size:13,design:.monospaced)).foregroundStyle(.green).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading);Color.clear.frame(height:1).id("tty-bottom")}.padding()}.background(Color.black.opacity(0.9),in:RoundedRectangle(cornerRadius:10)).onChange(of:lab.ttyScreen){_,_ in proxy.scrollTo("tty-bottom",anchor:.bottom)}}
  HStack{if lab.ttyPrivateInput{SecureField("隐私输入",text:$lab.ttyInput).onSubmit(lab.sendTTY)}else{TextField("输入命令或交互式回答，按回车发送",text:$lab.ttyInput).onSubmit(lab.sendTTY)};Button("发送回车",action:lab.sendTTY)}.textFieldStyle(.roundedBorder).disabled(!lab.ttyRunning)
  HStack{Button("Ctrl-C"){lab.ttyKey([3])};Button("Ctrl-D"){lab.ttyKey([4])};Button("Ctrl-Z"){lab.ttyKey([26])};Button("Tab"){lab.ttyKey([9])};Button("↑"){lab.ttyKey([27,91,65])};Button("↓"){lab.ttyKey([27,91,66])};Button("停止shell",role:.destructive,action:lab.stopTTY);Toggle("隐私输入：暂停记录",isOn:$lab.ttyPrivateInput).onChange(of:lab.ttyPrivateInput){_,v in if v{lab.ttyAutoPrint=false;lab.discardTTY()}}}.disabled(!lab.ttyRunning)
  GroupBox("实体纸带"){VStack(alignment:.leading,spacing:8){HStack{Toggle("自动打印终端输出",isOn:$lab.ttyAutoPrint);Spacer();Text("待提交 \(lab.ttyPendingCount) 字符")};HStack{integer("空闲批处理 秒",value:$lab.ttyIdleSeconds,range:1...15);integer("每批送纸 行",value:$lab.ttyFeed,range:0...300);slider("终端字号",value:$lab.ttyFontSize,range:8...18,step:1)};HStack{Button("立即打印缓冲"){lab.pumpTTY(manual:true)}.disabled(lab.busy);Button("丢弃待打印缓冲",action:lab.discardTTY);if lab.busy{Button("停止打印并断开",role:.destructive,action:lab.stopJob)}};Text(lab.ttyMessage).font(.caption).textSelection(.enabled)}.padding(6)}
  Text("这里运行真实shell，命令按当前用户权限执行。纸带模式不模拟可擦除的全屏终端；ANSI颜色会清除。默认不加载用户配置、不自动打印。每批浓度固定100；连续纸带可将额外送纸设为0，末尾在工作台手动送150行撕取。隐私模式开启期间丢弃输出，不会补打印。").font(.caption).foregroundStyle(.secondary)
 }}
}
