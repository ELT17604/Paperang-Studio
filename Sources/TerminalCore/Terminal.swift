import Foundation
import Darwin
import PTYBridge
/// A real controlling PTY. Reads and writes are owned by one serial queue.
public final class PTYSession: @unchecked Sendable
{
	public private(set) var name = ""
	public private(set) var pid: Int32 = 0
	private var fd: Int32 = -1
	private let queue = DispatchQueue(label: "local.paperang.pty")
	private var source: DispatchSourceRead?
	private var processSource: DispatchSourceProcess?
	private var closed = false
	private var started = false
	private var outputBytes = 0
	public var onOutput: ((Data) -> Void)?
	public var onExit: ((Int32) -> Void)?
	public init()
	{
	}
	public func start(
		directory: String, columns: Int = 72, rows: Int = 24, loadProfile: Bool = false, helperURL: URL? = nil
	) throws
	{
		guard !started, fd < 0, (20...200).contains(columns), (5...100).contains(rows) else
		{
			throw NSError(domain: "PTY", code: Int(EINVAL))
		}
		var master: Int32 = -1
		var child: Int32 = 0
		var path = [CChar](repeating: 0, count: 128)
		let capacity = path.count
		let helper =
			helperURL
			?? URL(fileURLWithPath: CommandLine.arguments[0]).deletingLastPathComponent().appendingPathComponent(
				"PaperangPTYHost")
		let result = helper.path.withCString
		{
			h in
			directory.withCString
			{
				paper_tty_start(
					h, $0, Int32(columns), Int32(rows), loadProfile ? 1 : 0, &master, &child, &path, capacity)
			}
		}
		guard result == 0 else
		{
			throw NSError(domain: NSPOSIXErrorDomain, code: Int(result))
		}
		started = true
		fd = master
		pid = child
		name = String(cString: path)
		closed = false
		let read = DispatchSource.makeReadSource(fileDescriptor: master, queue: queue)
		source = read
		read.setEventHandler
		{ [weak self] in self?.readAvailable() }
		read.resume()
		let process = DispatchSource.makeProcessSource(identifier: child, eventMask: .exit, queue: queue)
		processSource = process
		process.setEventHandler
		{
			[self] in
			var status: Int32 = 0
			waitpid(child, &status, 0)
			self.readAvailable()
			self.finish()
			self.onExit?(status)
		}
		process.resume()
	}
	private func readAvailable()
	{
		guard !closed, fd >= 0 else
		{
			return
		}
		var bytes = [UInt8](repeating: 0, count: 8192)
		while true
		{
			let n = Darwin.read(fd, &bytes, bytes.count)
			if n > 0
			{
				outputBytes += n
				if outputBytes > 1_000_000
				{
					onOutput?(Data("\r\n[单次会话输出达到1MB，已停止shell。]\r\n".utf8))
					paper_tty_stop(fd, pid)
					fd = -1
					closed = true
					source?.cancel()
					source = nil
					break
				}
				onOutput?(Data(bytes.prefix(n)))
			} else
			{
				if n < 0 && errno == EINTR
				{
					continue
				}
				break
			}
		}
	}
	private var inputBuffer = Data()
	public func write(_ data: Data)
	{
		queue.async
		{
			[weak self] in
			guard let self, !self.closed, self.fd >= 0 else
			{
				return
			}
			guard self.inputBuffer.count + data.count <= 65536 else
			{
				return
			}
			self.inputBuffer.append(data)
			self.flushInput()
		}
	}
	private func flushInput()
	{
		guard !closed, fd >= 0, !inputBuffer.isEmpty else
		{
			return
		}
		let n = inputBuffer.withUnsafeBytes
		{
			Darwin.write(fd, $0.baseAddress!, $0.count)
		}
		if n > 0
		{
			inputBuffer.removeFirst(n)
		}
		if !inputBuffer.isEmpty
		{
			queue.asyncAfter(deadline: .now() + 0.03)
		{ [weak self] in self?.flushInput() } }
	}
	public func resize(columns: Int, rows: Int)
	{
		guard (20...200).contains(columns), (5...100).contains(rows) else
		{
			return
		}
		queue.async
		{
			[weak self] in
			guard let self, self.fd >= 0 else
			{
				return
			}
			_ = paper_tty_resize(self.fd, Int32(columns), Int32(rows))
		}
	}
	public func stop()
	{
		queue.async
		{
			[weak self] in
			guard let self, !self.closed else
			{
				return
			}
			paper_tty_stop(self.fd, self.pid)
			self.fd = -1
			self.source?.cancel()
			self.source = nil
			self.closed = true
		}
	}
	private func finish()
	{
		source?.cancel()
		source = nil
		processSource?.cancel()
		processSource = nil
		if fd >= 0
		{
			Darwin.close(fd)
			fd = -1
		}
		closed = true
	}
	deinit
	{
		if fd >= 0
	{
		paper_tty_stop(fd, pid)
	} }
}
/// UTF-8 and ANSI stream normalizer for a paper/line terminal, not a full-screen emulator.
public struct PaperTerminalBuffer
{
	public private(set) var lines = [String]()
	private var characters = [Character]()
	public private(set) var current: String
	{
		get
		{
			String(characters)
		}
		set
		{
			characters = Array(newValue)
		}
	}
	public private(set) var pending = [String]()
	private var cursor = 0
	private var bytes = [UInt8]()
	private var state = 0
	private var escape = ""
	private var printedPartial = ""
	public init()
	{
	}
	public mutating func feed(_ data: Data)
	{
		bytes.append(contentsOf: data)
		var i = 0
		while i < bytes.count
		{
			let b = bytes[i]
			let n: Int = b < 0x80 ? 1 : (b & 0xe0 == 0xc0 ? 2 : (b & 0xf0 == 0xe0 ? 3 : (b & 0xf8 == 0xf0 ? 4 : 1)))
			if i + n > bytes.count
			{
				break
			}
			let text = String(decoding: bytes[i..<i + n], as: UTF8.self)
			for ch in text
			{
				consume(ch)
			}
			i += n
		}
		bytes.removeFirst(i)
	}
	private mutating func consume(_ c: Character)
	{
		if state == 1
		{
			if c == "["
			{
				state = 2
				escape = ""
			} else if c == "]" || c == "P" || c == "_"
			{
				state = 3
			} else
			{
				state = 0
			}
			return
		}
		if state == 2
		{
			escape.append(c)
			if let a = c.asciiValue, (0x40...0x7e).contains(a)
			{
				applyEscape()
				state = 0
			}
			if escape.count > 128
			{
				state = 0
			}
			return
		}
		if state == 3
		{
			if c == "\u{7}"
			{
				state = 0
			} else if c == "\u{1b}"
			{
				state = 4
			}
			return
		}
		if state == 4
		{
			state = c == "\\" ? 0 : 3
			return
		}
		switch c
		{
		case "\u{1b}": state = 1
		case "\r": cursor = 0
		case "\n": commit()
		case "\u{8}", "\u{7f}": cursor = max(0, cursor - 1)
		case "\t":
			let count = 8 - cursor % 8
			for _ in 0..<count
			{
				put(" ")
			}
		default: if c.unicodeScalars.allSatisfy(
		{
			$0.value >= 32
		})
		{
			put(c)
		}
		}
	}
	private mutating func put(_ c: Character)
	{
		guard cursor < 512 else
		{
			return
		}
		while characters.count < cursor
		{
			characters.append(" ")
		}
		if cursor < characters.count
		{
			characters[cursor] = c } else
		{
			characters.append(c)
		}
		cursor += 1
	}
	private mutating func commit()
	{
		lines.append(current)
		if lines.count > 1500
		{
			lines.removeFirst(lines.count - 1500)
		}
		let remainder = current.hasPrefix(printedPartial) ? String(current.dropFirst(printedPartial.count)) : current
		pending.append(remainder)
		printedPartial = ""
		current = ""
		cursor = 0
	}
	private mutating func applyEscape()
	{
		guard let last = escape.last else
		{
			return
		}
		let number = Int(escape.dropLast()) ?? 1
		switch last
		{
		case "K":
			if escape == "2K"
			{
				current = ""
				cursor = 0
			} else
			{
				current = String(current.prefix(cursor))
			}
		case "G": cursor = max(0, min(511, number - 1))
		case "C": cursor = min(511, cursor + max(1, number))
		case "D": cursor = max(0, cursor - max(1, number))
		default: break
		}
	}
	public var transcript: String
	{
		(lines + [current]).joined(separator: "\n")
	}
	public var pendingCount: Int
	{
		pending.reduce(0)
		{
			$0 + $1.count + 1
		} + max(0, current.count - printedPartial.count)
	}
	public mutating func take(maxCharacters: Int = 1800, includePartial: Bool = true) -> String
	{
		var result = ""
		while let first = pending.first
		{
			if !result.isEmpty && result.count + first.count + 1 > maxCharacters
			{
				break
			}
			let room = maxCharacters - result.count
			if first.count + 1 > room
			{
				let prefix = String(first.prefix(max(1, room)))
				result += prefix
				pending[0] = String(first.dropFirst(prefix.count))
				break
			}
			result += first + "\n"
			pending.removeFirst()
		}
		if pending.isEmpty && includePartial
		{
			let tail = current.hasPrefix(printedPartial) ? String(current.dropFirst(printedPartial.count)) : current
			if !tail.isEmpty && result.count + tail.count <= maxCharacters
			{
				result += tail
				printedPartial = current
			}
		}
		return result
	}
	public mutating func prepend(_ text: String)
	{
		pending.insert(contentsOf: text.components(separatedBy: "\n"), at: 0)
	}
	public mutating func clearPending()
	{
		pending = []
		printedPartial = current
	}
}
