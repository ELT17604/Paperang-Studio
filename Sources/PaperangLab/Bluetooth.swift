import SwiftUI
import CoreBluetooth
import Combine
import ProtocolCore
import PrintCore
import IntegrationCore
import TerminalCore
struct DeviceRow: Identifiable { let id:UUID;let name:String;let rssi:Int;var source="BLE广播" }
struct Reading: Identifiable {let id:UInt8;let title:String;let value:String;let raw:String;let time:Date}
struct LogRow: Identifiable {let id=UUID();let time=Date();let type:String;let text:String}
@MainActor final class Lab: NSObject,ObservableObject,@preconcurrency CBCentralManagerDelegate,@preconcurrency CBPeripheralDelegate {
 @Published var showAllDevices=false
 @Published var scanSummary="尚未扫描"
 var scanTask:Task<Void,Never>?;var scanSession=UUID();var scanAfterInitialization=false;var scanBroadcastIDs=Set<UUID>()
 @Published var devices=[DeviceRow]();@Published var readings=[Reading]();@Published var logs=[LogRow]();@Published var services=[String]()
 @Published var state="未连接";@Published var ready=false;@Published var busy=false;@Published var scanning=false;@Published var error=""
 @Published var text="Hello Paperang P2!\n你好，喵喵机。" {didSet{schedulePreview()}}
 @Published var settings=PrintSettings() {didSet{schedulePreview()}}
 @Published var mode="文字" {didSet{schedulePreview()}}
 @Published var page="打印工作台";@Published var engineer=false;@Published var engineerPage="设备字段"
 @Published var search="";@Published var selected:UInt8=4;@Published var rawCommand="04";@Published var rawPayload="01"
 @Published var trayMode=false;@Published var trayPrintPaused=false
 var trayPreviousAutoPrint=false;var trayStartedAPI=false
 @Published var allowPaper=false;@Published var allowEngineeringWrites=false
 @Published var imageName="尚未导入图片";@Published var preview:NSImage?;@Published var previewError="";@Published var rendering=false
 @Published var progress=0.0;@Published var jobStatus="等待打印";@Published var jobs=[JobRecord]();@Published var logFilter="";@Published var notice=""
 @Published var ttyRunning=false;@Published var ttyName="未启动";@Published var ttyScreen="";@Published var ttyInput="";@Published var ttyDirectory=FileManager.default.currentDirectoryPath
 @Published var ttyColumns=72;@Published var ttyRows=24;@Published var ttyLoadProfile=false;@Published var ttyAutoPrint=false;@Published var ttyPendingCount=0;@Published var ttyMessage="本地PTY尚未启动"
 @Published var ttyIdleSeconds=3;@Published var ttyFeed=0;@Published var ttyFontSize=12.0;@Published var ttyPrivateInput=false
 @Published var apiRunning=false;@Published var apiPort=8176;@Published var apiToken=UUID().uuidString+UUID().uuidString;@Published var apiOrigins="http://localhost:3000,http://127.0.0.1:3000"
 @Published var relayOnline=false;@Published var relayFailures=0;@Published var relayLastSeen:Date?;@Published var relayWarning=false
 @Published var relayAdminKey=""
 @Published var relayTab="任务"
 var relaySession=UUID();var relayFlushing=false
 @Published var relayAutoConnect=false;@Published var relayReconnectPrinter=false;@Published var relayPrinterID=""
 var relayPrinterRetryAt=Date.distantPast;var configurationObserver:AnyCancellable?;var configurationDraftBytes=Data();var configurationLastError=""
 @Published var relayAuthMode="owner";@Published var relayOwnerPath=""
 var relayOwner:RelayOwnerAuthentication?;var relayIdentityFingerprint=""
 @Published var relayURL="";@Published var relayKey="";@Published var relayAllowHTTP=false;@Published var relayRunning=false;@Published var relayAutoPrint=false
 @Published var relayMessage="尚未连接中转服务器";@Published var relayJobs=[RelayJob]();@Published var relaySelection="";@Published var relayStyle=UserDefaults.standard.string(forKey:"relay.style") ?? "便签"
 @Published var relayIncludeID=false;@Published var relayIncludeText=true;@Published var relayIncludeSender=true;@Published var relayIncludeTime=true;@Published var relayIncludeIP=false;@Published var relayIncludeUA=false;@Published var relayIncludeDevice=false
 @Published var relayPreviewLoading=false;@Published var relayPreviewRenderedSignature=""
 var relayPreviewTask:Task<Void,Never>?;var relayPreviewSession=UUID()
 @Published var relayDensity=100;@Published var relayFeed=150;@Published var relayPreview:NSImage?;@Published var relayMutating=false
 @Published var relayKeepReconnecting=UserDefaults.standard.object(forKey:"relay.keepReconnecting") as? Bool ?? true
 @Published var relayDefaultAutoPrint=UserDefaults.standard.bool(forKey:"relay.defaultAutoPrint")
 @Published var relayTemplates=RelayTemplate.load();@Published var relayTemplateID=UserDefaults.standard.string(forKey:"relay.templateID") ?? ""
 @Published var relayEditorOpen=false;@Published var relayFilter="全部";@Published var relaySearch=""
 var relayDetailTask:Task<Void,Never>?
 var relayTask:Task<Void,Never>?;var relayEndpoint:URL?;var relayCredential="";var relayFlight:RelayFlight?
 var terminal:PTYSession?;var terminalBuffer=PaperTerminalBuffer();var terminalTimer:Task<Void,Never>?;var ttyLastOutput=Date();var ttyFlight:(UUID,String)?
 var api:LocalAPI?
 var sourceImage:CGImage?;var raster:Raster?;var previewTask:Task<Void,Never>?;var activeSettings:PrintSettings?
 var central:CBCentralManager!;var peripheral:CBPeripheral?;var known=[UUID:CBPeripheral]();var write:CBCharacteristic?
 var decoder=Decoder();var credits=0;var packetLimit=20;var notifyCount=0;var queue=[Data]();var pumping=false;var ackCommand:UInt8?;var ackResult:Bool?;var response:Data?;var request:UInt8?;var work:Task<Void,Never>?;var generation=0
 override convenience init(){self.init(offline:false)}
 init(offline:Bool,restore:Bool?=nil){super.init();if !offline{central=CBCentralManager(delegate:self,queue:.main)}else{state="离线API模式：不访问蓝牙"};schedulePreview();let root=Bundle.main.bundleURL.deletingLastPathComponent().deletingLastPathComponent();let candidates=[root,root.deletingLastPathComponent().appendingPathComponent("PaperangStudio")];ttyDirectory=candidates.first{FileManager.default.fileExists(atPath:$0.appendingPathComponent("Package.swift").path)}?.path ?? FileManager.default.homeDirectoryForCurrentUser.path;configureLaunchAPI();if restore ?? !offline{restoreRelayConfiguration();observeRelayConfiguration()}}

 var visibleDevices:[DeviceRow] {devices.filter{showAllDevices || DeviceMatcher.paperang($0.name)}}
 func log(_ type:String,_ s:String){logs.append(.init(type:type,text:s));if logs.count>4000{logs.removeFirst(logs.count-4000)}}
 func centralManagerDidUpdateState(_ central:CBCentralManager){
  guard self.central===central else{return}
  state=[CBManagerState.poweredOn:"蓝牙已开启",.poweredOff:"蓝牙已关闭",.unauthorized:"请在系统设置允许本App使用蓝牙",.unsupported:"不支持蓝牙"][central.state] ?? "蓝牙初始化中";log("SYSTEM",state)
  if central.state != .poweredOn{reset();if [.poweredOff,.unauthorized,.unsupported].contains(central.state){scanAfterInitialization=false;scanSummary=state}}
  else if scanAfterInitialization{scanAfterInitialization=false;scan()}
 }
 func stopScan(){scanTask?.cancel();scanTask=nil;scanSession=UUID();if central?.state == .poweredOn{central.stopScan()};scanning=false}
 func restartScanner(){
  guard !busy,!ready,peripheral==nil else{error="请先断开当前会话，再重新初始化扫描";return}
  stopScan();central?.delegate=nil;scanAfterInitialization=true;scanSummary="正在重新初始化本App的扫描连接…";error=""
  central=CBCentralManager(delegate:self,queue:.main);log("SCAN","重新创建本App的CoreBluetooth中心；未重置系统蓝牙")
 }
 func addScanDevice(_ p:CBPeripheral,name:String,rssi:Int,source:String){
  known[p.identifier]=p
  if let previous=devices.first(where:{$0.id==p.identifier}),previous.name==name,previous.source==source,abs(previous.rssi-rssi)<5{return}
  devices.removeAll{$0.id==p.identifier};devices.append(DeviceRow(id:p.identifier,name:name,rssi:rssi,source:source))
  devices.sort{(DeviceMatcher.paperang($0.name) ? 0:1,$0.name)<(DeviceMatcher.paperang($1.name) ? 0:1,$1.name)}
 }
 func scan(){
  guard let central,central.state == .poweredOn else{error=state;scanSummary=state;return}
  guard !busy,peripheral==nil || ready else{error="连接处理中，请先等待或断开";return}
  stopScan();error="";devices=[];scanBroadcastIDs=[];let session=UUID();scanSession=session
  let recent=UserDefaults.standard.dictionary(forKey:"ble.recentPrinters") as? [String:String] ?? [:]
  let ids=recent.keys.compactMap{UUID(uuidString:$0)}
  for p in central.retrievePeripherals(withIdentifiers:ids){addScanDevice(p,name:p.name ?? recent[p.identifier.uuidString] ?? "历史打印机",rssi:0,source:"历史记录（未确认在线）")}
  for p in central.retrieveConnectedPeripherals(withServices:[CBUUID(string:"FF00")]){addScanDevice(p,name:p.name ?? "已连接设备",rssi:0,source:"本机已有连接（需核对型号）")}
  scanning=true;scanSummary="扫描中：等待BLE广播；历史记录不代表在线"
  central.scanForPeripherals(withServices:nil,options:[CBCentralManagerScanOptionAllowDuplicatesKey:true])
  log("SCAN","开始12秒扫描；isScanning=\(central.isScanning)，允许重复广播，已知记录\(devices.count)项")
  scanTask=Task{do{try await Task.sleep(for:.seconds(12))}catch{return};guard scanSession==session,self.central===central else{return};central.stopScan();scanning=false;scanTask=nil;scanSummary="扫描结束：\(scanBroadcastIDs.count)个广播设备，\(visibleDevices.count)个当前筛选结果";if scanBroadcastIDs.isEmpty{scanSummary+="；可点击重新初始化扫描"};log("SCAN",scanSummary)}
 }
 func centralManager(_ central:CBCentralManager,didDiscover p:CBPeripheral,advertisementData:[String:Any],rssi RSSI:NSNumber){
  guard self.central===central,scanning else{return}
  let name=(advertisementData[CBAdvertisementDataLocalNameKey] as? String) ?? p.name ?? "未命名"
  let first=scanBroadcastIDs.insert(p.identifier).inserted
  addScanDevice(p,name:name,rssi:RSSI.intValue,source:"BLE广播")
  if first{log("DISCOVER","\(name) · \(RSSI.intValue)dBm · \(p.identifier)")}
  scanSummary="扫描中：\(scanBroadcastIDs.count)个广播设备，\(visibleDevices.count)个当前筛选结果"
  if DeviceMatcher.paperang(name){var recent=UserDefaults.standard.dictionary(forKey:"ble.recentPrinters") as? [String:String] ?? [:];recent[p.identifier.uuidString]=name;if recent.count>16{for key in recent.keys.sorted().prefix(recent.count-16){recent.removeValue(forKey:key)}};UserDefaults.standard.set(recent,forKey:"ble.recentPrinters")}
 }
 func connect(_ id:UUID){relayPrinterID=id.uuidString;disconnect();guard let p=known[id] else{return};stopScan();peripheral=p;p.delegate=self;state="连接 \(p.name ?? id.uuidString)";central.connect(p);Task{let g=generation;try? await Task.sleep(for:.seconds(20));if g==generation && !ready {error="连接/握手超时";disconnect()}}}
 func centralManager(_ central:CBCentralManager,didConnect p:CBPeripheral){guard peripheral?.identifier==p.identifier else{return};state="发现服务";log("CONNECTED",p.identifier.uuidString);p.discoverServices(nil)}
 func centralManager(_ central:CBCentralManager,didFailToConnect p:CBPeripheral,error:Error?){guard peripheral?.identifier==p.identifier else{return};self.error=error?.localizedDescription ?? "连接失败";reset()}
 func centralManager(_ central:CBCentralManager,didDisconnectPeripheral p:CBPeripheral,error:Error?){guard peripheral?.identifier==p.identifier else{return};log("DISCONNECT",error?.localizedDescription ?? "连接已断开");reset();state="未连接"}
 func disconnect(){if let p=peripheral{central.cancelPeripheralConnection(p)};reset();state="未连接"}
 func reset(){stopScan();generation+=1;work?.cancel();work=nil;ready=false;busy=false;write=nil;queue=[];credits=0;notifyCount=0;pumping=false;decoder.reset();ackCommand=nil;request=nil;peripheral=nil;services=[]}
 func peripheral(_ p:CBPeripheral,didDiscoverServices error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{self.error=error.localizedDescription;return};for s in p.services ?? []{p.discoverCharacteristics(nil,for:s)}}
 func peripheral(_ p:CBPeripheral,didDiscoverCharacteristicsFor service:CBService,error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{self.error=error.localizedDescription;return};services.append("服务 \(service.uuid)");for c in service.characteristics ?? []{services.append("  \(c.uuid) properties=0x\(String(c.properties.rawValue,radix:16))");if service.uuid==CBUUID(string:"FF00"){if c.uuid==CBUUID(string:"FF02"){write=c;packetLimit=min(180,p.maximumWriteValueLength(for:.withoutResponse))};if ["FF01","FF03"].contains(c.uuid.uuidString){p.setNotifyValue(true,for:c)}};if c.properties.contains(.read){p.readValue(for:c)}}}
 func peripheral(_ p:CBPeripheral,didUpdateNotificationStateFor c:CBCharacteristic,error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{self.error=error.localizedDescription;return};if c.isNotifying{notifyCount+=1};if notifyCount==2 && write != nil {run{try await self.handshake()}}}
 func peripheral(_ p:CBPeripheral,didUpdateValueFor c:CBCharacteristic,error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{log("READ ERROR","\(c.uuid): \(error)");return};guard let d=c.value else{return};log("RX \(c.uuid)",Wire.hex(d));if c.uuid==CBUUID(string:"FF03"){let b=[UInt8](d);if b.count>=2 && b[0]==1{credits+=Int(b[1])};if b.count>=3 && b[0]==2{packetLimit=min(packetLimit,Int(b[1])|Int(b[2])<<8)};pump();return};if c.uuid != CBUUID(string:"FF01"){services.append("读取 \(c.uuid): \(String(data:d,encoding:.utf8) ?? "") [\(Wire.hex(d))]");return};for f in decoder.push(d){log(f.valid ? "FRAME":"CRC ERROR",String(format:"cmd=%02X index=%d ",f.command,f.index)+Wire.hex(f.payload));guard f.valid else{continue};if f.command==ackCommand && f.payload.count==1 {ackResult=f.payload.first==0};if f.command==13 {record(12,f.payload)};if let req=request,Command.expected[req]==f.command {response=f.payload;record(req,f.payload)}}}
 func peripheralIsReady(toSendWriteWithoutResponse p:CBPeripheral){pump()}
 func pump(){guard !pumping,!queue.isEmpty,credits>0,let p=peripheral,let w=write,p.canSendWriteWithoutResponse else{return};pumping=true;credits-=1;let d=queue.removeFirst();log("TX BLE",Wire.hex(d));p.writeValue(d,for:w,type:.withoutResponse);Task{let g=generation;try? await Task.sleep(for:.milliseconds((activeSettings ?? settings).writePauseMS));guard g==generation else{return};pumping=false;pump()}}
 func send(_ c:UInt8,_ payload:Data,seed:UInt32=0,index:UInt8=0,ack:Bool=true) async throws {
  guard write != nil,peripheral?.state == .connected else{throw PrintFailure("蓝牙未连接")};try Task.checkCancellation();let g=generation
  ackCommand=c;ackResult=nil;let data=Wire.frame(c,payload,seed:seed,index:index);log("TX FRAME",Wire.hex(data));let size=max(1,min(packetLimit,(activeSettings ?? settings).bleChunk))
  for offset in stride(from:0,to:data.count,by:size){queue.append(Data(data[offset..<min(offset+size,data.count)]))};pump()
  let end=Date().addingTimeInterval(Double((activeSettings ?? settings).timeoutSeconds))
  while !queue.isEmpty || pumping || (ack && ackResult==nil){try Task.checkCancellation();if g != generation{throw PrintFailure("连接已改变")};if Date()>end {queue=[];throw PrintFailure(String(format:"0x%02X 超时：未收到完整确认或流控额度",c))};try await Task.sleep(for:.milliseconds(5))}
  if ack && ackResult != true {throw PrintFailure(String(format:"0x%02X 返回失败 ACK",c))};ackCommand=nil
 }
 func handshake() async throws {try await send(24,Data([0x21,0x95,0x76,0x35]),seed:0x35769521);try await query(6);guard let name=response.flatMap({String(data:$0,encoding:.utf8)}),["Paperang_P2","P2"].contains(name.trimmingCharacters(in:.controlCharacters)) else{throw PrintFailure("此版本只验证了Paperang P2，型号未确认")};try await query(16);try await query(12);try await query(28);ready=true;state="P2 会话已就绪";log("SESSION","CRC key=0；传统协议，非A5")}
 func run(_ block:@escaping @MainActor () async throws -> Void){guard !busy else{return};busy=true;error="";let g=generation;work=Task{do{try await block()}catch{if g==generation {self.error=error.localizedDescription;queue=[];log("ERROR",error.localizedDescription)}};if g==generation{busy=false;activeSettings=nil}}}
 func query(_ c:UInt8) async throws {request=c;response=nil;defer{request=nil};try await send(c,Data([1]));let end=Date().addingTimeInterval(1);while response==nil && Date()<end{try await Task.sleep(for:.milliseconds(50))};if response==nil{record(c,nil)}}
 func querySelected(){guard ready else{return};run{try await self.query(self.selected)}}
 func queryAll(){guard ready else{return};run{for c in Command.all where c.kind=="查询"{try Task.checkCancellation();try await self.query(c.code)}}}
 func record(_ c:UInt8,_ d:Data?){let title=Command.all.first{$0.code==c}?.title ?? "字段";let value:String
  if let d {let n=d.enumerated().prefix(4).reduce(UInt32(0)){$0 | UInt32($1.element)<<($1.offset*8)};switch c{case 6,8,10,35,45,48:value=String(data:d,encoding:.utf8) ?? "非UTF8";case 16,28,31,40,42:value="\(n)";case 12:value=P2MediaStatus(UInt16(truncatingIfNeeded:n)).description;default:value=Wire.hex(d)} }else{value="仅 ACK / 未返回数据"}
  readings.removeAll{$0.id==c};readings.append(.init(id:c,title:title,value:value,raw:d.map(Wire.hex) ?? "—",time:Date()));readings.sort{$0.id<$1.id}
 }
 func rawQuery(){guard ready,let c=UInt8(rawCommand,radix:16),Command.expected[c] != nil,let data=Wire.parseHex(rawPayload),data==Data([1]) else{error="原始测试仅允许目录中的只读查询，载荷固定01";return};run{try await self.query(c)}}
 func export(){let panel=NSSavePanel();panel.nameFieldStringValue="Paperang-session.json";guard panel.runModal() == .OK,let url=panel.url else{return};let object:[String:Any] = ["timestamp":ISO8601DateFormatter().string(from:Date()),"state":state,"printSettings":(try? JSONSerialization.jsonObject(with:JSONEncoder().encode(settings))) ?? [:],"jobs":jobs.map{["title":$0.title,"detail":$0.detail,"status":$0.status,"date":ISO8601DateFormatter().string(from:$0.date)]},"services":services,"readings":readings.map{["command":String(format:"%02X",$0.id),"title":$0.title,"value":$0.value,"raw":$0.raw]},"logs":logs.map{["time":ISO8601DateFormatter().string(from:$0.time),"type":$0.type,"text":$0.text]}];do{try JSONSerialization.data(withJSONObject:object,options:[.prettyPrinted,.sortedKeys]).write(to:url)}catch{self.error=error.localizedDescription}}
}
