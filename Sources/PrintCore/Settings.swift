import Foundation
public struct PrintSettings: Codable, Equatable {
 public var version=1
 public var fontName="PingFang SC"
 public var fontSize=30.0
 public var bold=false
 public var lineSpacing=4.0
 public var alignment="居中"
 public var marginX=12
 public var marginY=12
 public var contentWidth=552
 public var imageFit="适配宽度"
 public var cropHeight=400
 public var rotation=0
 public var conversion="阈值"
 public var threshold=128.0
 public var brightness=0.0
 public var contrast=1.0
 public var gamma=1.0
 public var invert=false
 public var density=100
 public var copies=1
 public var feedBefore=0
 public var feedAfter=150
 public var feedBetweenCopies=false
 public var packetBytes=1008
 public var bleChunk=180
 public var writePauseMS=30
 public var framePauseMS=80
 public var timeoutSeconds=6
 public var finishWaitSeconds=2
 public init() {}
 public func validate() throws {
  func require(_ b:Bool,_ message:String) throws {if !b{throw PrintFailure(message)}}
  try require(version==1,"不支持该预设版本")
  try require(fontSize.isFinite && (8...96).contains(fontSize),"字号需为8–96")
  try require(lineSpacing.isFinite && (0...40).contains(lineSpacing),"行距需为0–40")
  try require((0...120).contains(marginX) && (0...200).contains(marginY),"页边距超出范围")
  try require((48...576).contains(contentWidth) && contentWidth+marginX*2<=576,"内容宽度与左右边距总和不得超过576点")
  try require((24...1400).contains(cropHeight),"裁切高度需为24–1400像素")
  try require([0,90,180,270].contains(rotation),"图片旋转需为0/90/180/270度")
  try require(["居左","居中","居右"].contains(alignment),"对齐方式无效")
  try require(["适配宽度","保持像素","裁切填满"].contains(imageFit),"图片适配方式无效")
  try require(["阈值","Floyd–Steinberg","有序抖动"].contains(conversion),"黑白转换方式无效")
  try require(threshold.isFinite && (0...255).contains(threshold),"阈值需为0–255")
  try require(brightness.isFinite && (-100...100).contains(brightness),"亮度需为-100–100")
  try require(contrast.isFinite && (0.2...3).contains(contrast),"对比度需为0.2–3")
  try require(gamma.isFinite && (0.2...3).contains(gamma),"Gamma需为0.2–3")
  try require((0...100).contains(density),"浓度需为0–100")
  try require((1...20).contains(copies),"份数需为1–20")
  try require((0...2000).contains(feedBefore) && (0...2000).contains(feedAfter),"送纸需为0–2000行")
  try require((72...1008).contains(packetBytes) && packetBytes%72==0,"图像分包需为72的整数倍，最大1008")
  try require((20...180).contains(bleChunk),"BLE分段需为20–180字节")
  try require((5...200).contains(writePauseMS) && (0...1000).contains(framePauseMS),"发送间隔超出范围")
  try require((3...30).contains(timeoutSeconds) && (1...15).contains(finishWaitSeconds),"超时/完成等待超出范围")
 }
 public static func preset(_ name:String) -> Self {
  var s=Self()
  switch name {case "照片":s.conversion="Floyd–Steinberg";s.contrast=1.1
  case "标签":s.fontSize=42;s.bold=true;s.marginY=8;s.alignment="居中"
  case "线稿":s.conversion="阈值";s.threshold=160;s.marginX=0;s.contentWidth=576
  default:break};return s
 }
}
public struct PrintFailure:LocalizedError {public let message:String;public init(_ message:String){self.message=message};public var errorDescription:String?{message}}
