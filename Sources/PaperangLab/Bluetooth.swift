import SwiftUI
import CoreBluetooth
import Combine
import ProtocolCore
import PrintCore
import IntegrationCore
import TerminalCore
struct DeviceRow: Identifiable { let id:UUID;let name:String;let rssi:Int }
struct Reading: Identifiable {let id:UInt8;let title:String;let value:String;let raw:String;let time:Date}
struct LogRow: Identifiable {let id=UUID();let time=Date();let type:String;let text:String}
@MainActor final class Lab: NSObject,ObservableObject,@preconcurrency CBCentralManagerDelegate,@preconcurrency CBPeripheralDelegate {
 @Published var showAllDevices=false
 @Published var devices=[DeviceRow]();@Published var readings=[Reading]();@Published var logs=[LogRow]();@Published var services=[String]()
 @Published var state="未连接";@Published var ready=false;@Published var busy=false;@Published var scanning=false;@Published var error=""
 @Published var text="Hello Paperang P2!\n你好，喵喵机。" {didSet{schedulePreview()}}
 @Published var settings=PrintSettings() {didSet{schedulePreview()}}
 @Published var mode="文字" {didSet{schedulePreview()}}
 @Published var page="打印工作台";@Published var engineer=false;@Published var engineerPage="设备字段"
 @Published var search="";@Published var selected:UInt8=4;@Published var rawCommand="04";@Published var rawPayload="01"
 @Published var allowPaper=false;@Published var allowEngineeringWrites=false
 @Published var imageName="尚未导入图片";@Published var preview:NSImage?;@Published var previewError="";@Published var rendering=false
 @Published var progress=0.0;@Published var jobStatus="等待打印";@Published var jobs=[JobRecord]();@Published var logFilter="";@Published var notice=""
 @Published var ttyRunning=false;@Published var ttyName="未启动";@Published var ttyScreen="";@Published var ttyInput="";@Published var ttyDirectory=FileManager.default.currentDirectoryPath
 @Published var ttyColumns=72;@Published var ttyRows=24;@Published var ttyLoadProfile=false;@Published var ttyAutoPrint=false;@Published var ttyPendingCount=0;@Published var ttyMessage="本地PTY尚未启动"
 @Published var ttyIdleSeconds=3;@Published var ttyFeed=0;@Published var ttyFontSize=12.0;@Published var ttyPrivateInput=false
 @Published var apiRunning=false;@Published var apiPort=8176;@Published var apiToken=UUID().uuidString+UUID().uuidString;@Published var apiOrigins="http://localhost:3000,http://127.0.0.1:3000"
 var terminal:PTYSession?;var terminalBuffer=PaperTerminalBuffer();var terminalTimer:Task<Void,Never>?;var ttyLastOutput=Date();var ttyFlight:(UUID,String)?
 var api:LocalAPI?
 var sourceImage:CGImage?;var raster:Raster?;var previewTask:Task<Void,Never>?;var activeSettings:PrintSettings?
 var central:CBCentralManager!;var peripheral:CBPeripheral?;var known=[UUID:CBPeripheral]();var write:CBCharacteristic?
 var decoder=Decoder();var credits=0;var packetLimit=20;var notifyCount=0;var queue=[Data]();var pumping=false;var ackCommand:UInt8?;var ackResult:Bool?;var response:Data?;var request:UInt8?;var work:Task<Void,Never>?;var generation=0
 override convenience init(){self.init(offline:false)}
 init(offline:Bool){super.init();if !offline{central=CBCentralManager(delegate:self,queue:.main)}else{state="离线API模式：不访问蓝牙"};schedulePreview();let root=Bundle.main.bundleURL.deletingLastPathComponent().deletingLastPathComponent();let candidates=[root,root.deletingLastPathComponent().appendingPathComponent("PaperangStudio")];ttyDirectory=candidates.first{FileManager.default.fileExists(atPath:$0.appendingPathComponent("Package.swift").path)}?.path ?? FileManager.default.homeDirectoryForCurrentUser.path;configureLaunchAPI()}
 var visibleDevices:[DeviceRow] {devices.filter{showAllDevices || DeviceMatcher.paperang($0.name)}}
 func log(_ type:String,_ s:String){logs.append(.init(type:type,text:s));if logs.count>4000{logs.removeFirst(logs.count-4000)}}
 func centralManagerDidUpdateState(_ central:CBCentralManager){state=[CBManagerState.poweredOn:"蓝牙已开启",.poweredOff:"蓝牙已关闭",.unauthorized:"请在系统设置允许本App使用蓝牙",.unsupported:"不支持蓝牙" ][central.state] ?? "蓝牙初始化中";log("SYSTEM",state);if central.state != .poweredOn {reset()}}
 func scan(){guard let central,central.state == .poweredOn else{error=state;return};devices=[];scanning=true;central.scanForPeripherals(withServices:nil,options:[CBCentralManagerScanOptionAllowDuplicatesKey:false]);log("SCAN","扫描附近蓝牙；仅用户选择后连接");Task{try? await Task.sleep(for:.seconds(10));central.stopScan();scanning=false}}
 func centralManager(_ central:CBCentralManager,didDiscover p:CBPeripheral,advertisementData:[String:Any],rssi RSSI:NSNumber){known[p.identifier]=p;let row=DeviceRow(id:p.identifier,name:(advertisementData[CBAdvertisementDataLocalNameKey] as? String) ?? p.name ?? "未命名",rssi:RSSI.intValue);devices.removeAll{$0.id==row.id};devices.append(row);devices.sort{($0.name.contains("Paperang") ? 0:1,$0.name)<($1.name.contains("Paperang") ? 0:1,$1.name)}}
 func connect(_ id:UUID){disconnect();guard let p=known[id] else{return};central.stopScan();scanning=false;peripheral=p;p.delegate=self;state="连接 \(p.name ?? id.uuidString)";central.connect(p);Task{let g=generation;try? await Task.sleep(for:.seconds(20));if g==generation && !ready {error="连接/握手超时";disconnect()}}}
 func centralManager(_ central:CBCentralManager,didConnect p:CBPeripheral){guard peripheral?.identifier==p.identifier else{return};state="发现服务";log("CONNECTED",p.identifier.uuidString);p.discoverServices(nil)}
 func centralManager(_ central:CBCentralManager,didFailToConnect p:CBPeripheral,error:Error?){guard peripheral?.identifier==p.identifier else{return};self.error=error?.localizedDescription ?? "连接失败";reset()}
 func centralManager(_ central:CBCentralManager,didDisconnectPeripheral p:CBPeripheral,error:Error?){guard peripheral?.identifier==p.identifier else{return};log("DISCONNECT",error?.localizedDescription ?? "连接已断开");reset();state="未连接"}
 func disconnect(){if let p=peripheral{central.cancelPeripheralConnection(p)};reset();state="未连接"}
 func reset(){generation+=1;work?.cancel();work=nil;ready=false;busy=false;write=nil;queue=[];credits=0;notifyCount=0;pumping=false;decoder.reset();ackCommand=nil;request=nil;peripheral=nil;services=[]}
 func peripheral(_ p:CBPeripheral,didDiscoverServices error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{self.error=error.localizedDescription;return};for s in p.services ?? []{p.discoverCharacteristics(nil,for:s)}}
 func peripheral(_ p:CBPeripheral,didDiscoverCharacteristicsFor service:CBService,error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{self.error=error.localizedDescription;return};services.append("服务 \(service.uuid)");for c in service.characteristics ?? []{services.append("  \(c.uuid) properties=0x\(String(c.properties.rawValue,radix:16))");if service.uuid==CBUUID(string:"FF00"){if c.uuid==CBUUID(string:"FF02"){write=c;packetLimit=min(180,p.maximumWriteValueLength(for:.withoutResponse))};if ["FF01","FF03"].contains(c.uuid.uuidString){p.setNotifyValue(true,for:c)}};if c.properties.contains(.read){p.readValue(for:c)}}}
 func peripheral(_ p:CBPeripheral,didUpdateNotificationStateFor c:CBCharacteristic,error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{self.error=error.localizedDescription;return};if c.isNotifying{notifyCount+=1};if notifyCount==2 && write != nil {run{try await self.handshake()}}}
 func peripheral(_ p:CBPeripheral,didUpdateValueFor c:CBCharacteristic,error:Error?){guard peripheral?.identifier==p.identifier else{return};if let error{log("READ ERROR","\(c.uuid): \(error)");return};guard let d=c.value else{return};log("RX \(c.uuid)",Wire.hex(d));if c.uuid==CBUUID(string:"FF03"){let b=[UInt8](d);if b.count>=2 && b[0]==1{credits+=Int(b[1])};if b.count>=3 && b[0]==2{packetLimit=min(packetLimit,Int(b[1])|Int(b[2])<<8)};pump();return};if c.uuid != CBUUID(string:"FF01"){services.append("读取 \(c.uuid): \(String(data:d,encoding:.utf8) ?? "") [\(Wire.hex(d))]");return};for f in decoder.push(d){log(f.valid ? "FRAME":"CRC ERROR",String(format:"cmd=%02X index=%d ",f.command,f.index)+Wire.hex(f.payload));guard f.valid else{continue};if f.command==ackCommand && f.payload.count==1 {ackResult=f.payload.first==0};if f.command==13 {record(12,f.payload)};if let req=request,Command.expected[req]==f.command {response=f.payload;record(req,f.payload)}}}
 func peripheralIsReady(toSendWriteWithoutResponse p:CBPeripheral){pump()}
 func pump(){guard !pumping,!queue.isEmpty,credits>0,let p=peripheral,let w=write,p.canSendWriteWithoutResponse else{return};pumping=true;credits-=1;let d=queue.removeFirst();log("TX BLE",Wire.hex(d));p.writeValue(d,for:w,type:.withoutResponse);Task{let g=generation;try? await Task.sleep(for:.milliseconds((activeSettings ?? settings).writePauseMS));guard g==generation else{return};pumping=false;pump()}}
 enum Failure:LocalizedError{case message(String);var errorDescription:String?{if case .message(let s)=self{return s};return nil}}
 func send(_ c:UInt8,_ payload:Data,seed:UInt32=0,index:UInt8=0,ack:Bool=true) async throws {
  guard write != nil,peripheral?.state == .connected else{throw Failure.message("蓝牙未连接")};try Task.checkCancellation();let g=generation
  ackCommand=c;ackResult=nil;let data=Wire.frame(c,payload,seed:seed,index:index);log("TX FRAME",Wire.hex(data));let size=max(1,min(packetLimit,(activeSettings ?? settings).bleChunk))
  for offset in stride(from:0,to:data.count,by:size){queue.append(Data(data[offset..<min(offset+size,data.count)]))};pump()
  let end=Date().addingTimeInterval(Double((activeSettings ?? settings).timeoutSeconds))
  while !queue.isEmpty || pumping || (ack && ackResult==nil){try Task.checkCancellation();if g != generation{throw Failure.message("连接已改变")};if Date()>end {queue=[];throw Failure.message(String(format:"0x%02X 超时：未收到完整确认或流控额度",c))};try await Task.sleep(for:.milliseconds(30))}
  if ack && ackResult != true {throw Failure.message(String(format:"0x%02X 返回失败 ACK",c))};ackCommand=nil
 }
 func handshake() async throws {try await send(24,Data([0x21,0x95,0x76,0x35]),seed:0x35769521);try await query(6);guard let name=response.flatMap({String(data:$0,encoding:.utf8)}),["Paperang_P2","P2"].contains(name.trimmingCharacters(in:.controlCharacters)) else{throw Failure.message("此版本只验证了Paperang P2，型号未确认")};try await query(16);try await query(12);try await query(28);ready=true;state="P2 会话已就绪";log("SESSION","CRC key=0；传统协议，非A5")}
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
