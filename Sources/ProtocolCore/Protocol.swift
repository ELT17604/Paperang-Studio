import Foundation
public enum Wire
{
	public static func crc(_ data: Data, seed: UInt32 = 0) -> UInt32
	{
		var c = seed ^ 0xffffffff
		for b in data
		{
			c ^= UInt32(b)
			for _ in 0..<8
			{
				c = (c >> 1) ^ ((c & 1 == 1) ? 0xedb88320 : 0)
			}
		}
		return c ^ 0xffffffff
	}
	public static func frame(_ command: UInt8, _ payload: Data, seed: UInt32 = 0, index: UInt8 = 0) -> Data
	{
		precondition(payload.count <= 1023)
		var d = Data([2, command, index, UInt8(payload.count & 255), UInt8(payload.count >> 8)])
		d.append(payload)
		let c = crc(payload, seed: seed)
		for i in 0..<4
		{
			d.append(UInt8((c >> (i * 8)) & 255))
		}
		d.append(3)
		return d
	}
	public static func hex(_ d: Data) -> String
	{
		d.map
	{
		String(format: "%02X", $0)
	}.joined(separator: " ") }
	public static func parseHex(_ s: String) -> Data?
	{
		let chars = s.filter
		{
			!$0.isWhitespace
		}
		guard chars.count % 2 == 0 else
		{
			return nil
		}
		var d = Data()
		var i = chars.startIndex
		while i < chars.endIndex
		{
			let e = chars.index(i, offsetBy: 2)
			guard let b = UInt8(chars[i..<e], radix: 16) else
			{
				return nil
			}
			d.append(b)
			i = e
		}
		return d
	}
	public static func u16(_ n: Int) -> Data
	{
		Data([UInt8(n & 255), UInt8((n >> 8) & 255)])
	}
}
public struct Frame
{
	public let command: UInt8
	public let index: UInt8
	public let payload: Data
	public let valid: Bool
}
public struct Decoder
{
	private var buffer = Data()
	public init()
	{
	}
	public mutating func reset()
	{
		buffer.removeAll()
	}
	public mutating func push(_ data: Data) -> [Frame]
	{
		buffer.append(data)
		var result = [Frame]()
		while buffer.count >= 10
		{
			let b = [UInt8](buffer)
			if b[0] != 2
			{
				buffer.removeFirst()
				continue
			}
			let n = Int(b[3]) | Int(b[4]) << 8
			if n > 1023
			{
				buffer.removeFirst()
				continue
			}
			if b.count < n + 10
			{
				break
			}
			if b[n + 9] != 3
			{
				buffer.removeFirst()
				continue
			}
			let p = Data(b[5..<5 + n])
			var c: UInt32 = 0
			for i in 0..<4
			{
				c |= UInt32(b[n + 5 + i]) << (i * 8)
			}
			result.append(Frame(command: b[1], index: b[2], payload: p, valid: Wire.crc(p) == c))
			buffer.removeFirst(n + 10)
		}
		return result
	}
}
public struct Command: Identifiable, Hashable
{
	public var id: UInt8
	{
		code
	}
	public let code: UInt8
	public let title: String
	public let kind: String
	public let evidence: String
	public let detail: String
	public var label: String
	{
		String(format: "0x%02X", code) + "  " + title
	}
	public init(_ code: UInt8, _ title: String, _ kind: String, _ evidence: String, _ detail: String)
	{
		self.code = code
		self.title = title
		self.kind = kind
		self.evidence = evidence
		self.detail = detail
	}
	public static let all: [Command] = [
		.init(0, "打印位图", "耗纸", "实物验证", "576点 / 72字节每行；1黑0白；1008字节图像分帧，序号递增。"),
		.init(1, "压缩位图", "耗纸", "源码记载", "压缩格式未确认，暂不发送。"),
		.init(2, "固件数据", "高风险", "源码记载", "禁用写入。"), .init(3, "USB固件更新", "高风险", "源码记载", "禁用写入。"),
		.init(4, "固件版本", "查询", "已读数据", "响应05；本机09 00 01，字节顺序含义待确认。"), .init(6, "型号", "查询", "已读数据", "响应07；Paperang_P2。"),
		.init(8, "蓝牙MAC", "查询", "已读数据", "源码期望09；本机实际返回0B，12位ASCII地址，原始值在本机会话中查看。"),
		.init(10, "序列号", "查询", "已读数据", "响应0B；与MAC响应冲突，按在途请求关联。"),
		.init(12, "状态位", "查询", "已读数据", "本机对照：0010开盖；合盖时0001缺纸；0008基线位含义待确认。"),
		.init(14, "电压", "查询", "仅ACK", "未取得测量值，不能将ACK的00解读为0伏。"),
		.init(16, "电量", "查询", "已读数据", "响应11；单字节整数，当时为13。"), .init(18, "温度", "查询", "仅ACK", "未取得测量数据。"),
		.init(20, "工厂模式设置", "高风险", "源码记载", "工程写入需单独解锁并确认；mode取值/退出语义未验证。"),
		.init(21, "工厂状态查询", "查询", "已读数据", "本机响应16，载荷FF；具体含义未知，未切换工厂模式。"),
		.init(24, "CRC会话握手", "连接", "实测验证", "初始seed35769521；payload21957635注册key0，后续CRC seed0。"),
		.init(25, "打印浓度", "设置", "源码记载", "0–100；先前读数75，原生会话最新100；GUI显式应用后才写入。"),
		.init(26, "送纸", "耗纸", "实物验证", "u16LE行数；用户实物确认：批次结束150行最合适。"),
		.init(27, "设备自检页", "耗纸", "源码记载", "可能大量耗纸，当前不发送。"), .init(28, "当前浓度", "查询", "已读数据", "响应1D；历史4B=75，最新64=100。"),
		.init(30, "自动关机设置", "设置", "源码记载", "u16LE秒；工程原始指令可发送，可能持久保存。"),
		.init(31, "自动关机时间", "查询", "已读数据", "响应20；0807=1800秒。"),
		.init(33, "送至打印头", "耗纸", "源码记载", "语义未实测，当前使用已验证1A送纸。"), .init(34, "打印默认参数", "耗纸", "源码记载", "不自动执行。"),
		.init(35, "主板版本", "查询", "已读数据", "响应24；V1.00.01。"), .init(37, "硬件信息", "查询", "已读数据", "响应26；原始000000，含义未知。"),
		.init(39, "最大间隙设置", "设置", "源码记载", "单位待确认；工程原始指令可发送，未验证。"), .init(40, "最大间隙", "查询", "已读数据", "响应29；70=112，单位未知。"),
		.init(42, "纸张类型", "查询", "已读数据", "响应2B；00。"), .init(44, "纸张类型设置", "设置", "源码记载", "工程原始指令可发送；不同项目对纸类型值解释有差异。"),
		.init(45, "国家字段", "查询", "空数据", "响应2E；空载荷。"), .init(47, "断开蓝牙指令", "连接", "源码记载", "使用CoreBluetooth断开即可。"),
		.init(0x39, "扩展39（仅定义）", "研究", "模拟器源码", "ESP32项目仅定义57，未实现处理分支；禁止据此推断功能。"),
		.init(0x40, "扩展40→41", "研究", "模拟器源码", "模拟器响应单字节00，但源指针可疑；本机P2未验证，不自动发送。"),
		.init(0x42, "模块标识候选42→43", "研究", "模拟器源码", "模拟器固定返回BK3432，不是本机读数；与2A纸类型区分。"),
		.init(0x7F, "扩展版本候选7F→80", "研究", "模拟器源码", "模拟器固定返回v3.38.19及零填充；真实字段含义待抓包。"),
		.init(0x81, "扩展硬件候选81→82", "研究", "模拟器源码", "模拟器固定返回HME230_P2及零填充；非P2支持证明。"),
		.init(48, "设备短名称", "查询", "已读数据", "P1项目扩展命令；本机30→31返回ASCII P2。")
	]
	public static let expected: [UInt8: UInt8] = [
		4: 5, 6: 7, 8: 11, 10: 11, 12: 13, 14: 15, 16: 17, 18: 19, 21: 22, 28: 29, 31: 32, 35: 36, 37: 38, 40: 41,
		42: 43, 45: 46, 48: 49
	]
}

/// Calibrated on the user's P2. Other models/firmware require a new differential test.
public struct P2MediaStatus
{
	public let raw: UInt16
	public init(_ raw: UInt16)
	{
		self.raw = raw
	}
	public var coverOpen: Bool
	{
		raw & 0x10 != 0
	}
	public var paperPresent: Bool?
	{
		coverOpen ? nil : raw & 1 == 0
	}
	public var description: String
	{
		let cover = coverOpen ? "盖子打开" : "盖子闭合"
		let paper = paperPresent.map
		{
			$0 ? "有纸" : "缺纸"
		} ?? "纸张状态待合盖确认"
		let extra = raw & ~UInt16(0x11)
		return cover + " · " + paper + String(format: " · 其他位 0x%04X", extra)
	}
}
