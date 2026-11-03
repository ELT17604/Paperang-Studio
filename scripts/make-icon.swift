import AppKit
let root=URL(fileURLWithPath:CommandLine.arguments[1],isDirectory:true)
try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
func color(_ hex:UInt32)->NSColor {NSColor(srgbRed:Double(hex>>16&255)/255,green:Double(hex>>8&255)/255,blue:Double(hex&255)/255,alpha:1)}
let rep=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:1024,pixelsHigh:1024,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
NSGraphicsContext.saveGraphicsState();NSGraphicsContext.current=NSGraphicsContext(bitmapImageRep:rep)
func rounded(_ x:Double,_ y:Double,_ w:Double,_ h:Double,_ r:Double,_ fill:UInt32){color(fill).setFill();NSBezierPath(roundedRect:NSRect(x:x,y:y,width:w,height:h),xRadius:r,yRadius:r).fill()}
rounded(32,32,960,960,216,0x102C36)
rounded(178,278,668,400,105,0x31D5C4)
rounded(226,358,572,245,63,0x153F48)
rounded(280,538,464,261,30,0xF6F3E9)
rounded(319,687,250,22,11,0xA5BEB9)
rounded(319,638,386,22,11,0xA5BEB9)
rounded(279,248,466,224,26,0xF6F3E9)
let prompt=NSBezierPath();prompt.move(to:NSPoint(x:337,y:409));prompt.line(to:NSPoint(x:381,y:372));prompt.line(to:NSPoint(x:337,y:335));prompt.lineWidth=19;prompt.lineCapStyle = .round;prompt.lineJoinStyle = .round;color(0x153F48).setStroke();prompt.stroke()
rounded(416,330,94,17,8,0x153F48)
color(0xF6F3E9).setFill();NSBezierPath(ovalIn:NSRect(x:759,y:618,width:28,height:28)).fill()
NSGraphicsContext.restoreGraphicsState()
try rep.representation(using:.png,properties:[:])!.write(to:root.appendingPathComponent("AppIcon.png"))
