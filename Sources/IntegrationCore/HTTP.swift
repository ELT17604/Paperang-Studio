import Foundation
public struct HTTPRequest: Sendable
{
	public let method: String
	public let target: String
	public let headers: [String: String]
	public let body: Data
	public var path: String
	{
		String(target.split(separator: "?", maxSplits: 1).first ?? "")
	}
	public var query: [String: String]
	{
		let c = URLComponents(string: "http://localhost" + target)
		return Dictionary((c?.queryItems ?? []).map
		{
			($0.name, $0.value ?? "")
		}, uniquingKeysWith:
		{
			$1
		})
	}
}
public enum HTTPParseError: Error
{ case malformed, tooLarge, unsupported }
public enum HTTPParser
{
	public static let maxBody = 2 * 1024 * 1024
	public static func parse(_ buffer: Data) throws -> HTTPRequest?
	{
		guard buffer.count <= maxBody + 16384 else
		{
			throw HTTPParseError.tooLarge
		}
		guard let end = buffer.range(of: Data("\r\n\r\n".utf8)) else
		{
			if buffer.count > 16384
			{
				throw HTTPParseError.tooLarge
			}
			return nil
		}
		guard end.lowerBound <= 16384, let header = String(data: buffer[..<end.lowerBound], encoding: .utf8) else
		{
			throw HTTPParseError.malformed
		}
		let lines = header.components(separatedBy: "\r\n")
		let first = (lines.first ?? "").split(separator: " ")
		guard first.count == 3, first[2] == "HTTP/1.1", ["GET", "POST", "OPTIONS"].contains(String(first[0])),
			first[1].hasPrefix("/"), !first[1].hasPrefix("//")
		else
		{
			throw HTTPParseError.malformed
		}
		var headers = [String: String]()
		for line in lines.dropFirst()
		{
			guard let colon = line.firstIndex(of: ":"), !line.hasPrefix(" "), !line.hasPrefix("\t") else
			{
				throw HTTPParseError.malformed
			}
			let name = line[..<colon].lowercased()
			let value = line[line.index(after: colon)...].trimmingCharacters(in: .whitespaces)
			guard !name.isEmpty, headers[name] == nil else
			{
				throw HTTPParseError.malformed
			}
			headers[name] = value
		}
		guard headers["transfer-encoding"] == nil else
		{
			throw HTTPParseError.unsupported
		}
		let rawLength = headers["content-length"] ?? "0"
		guard !rawLength.isEmpty, rawLength.allSatisfy(
		{
			$0.isASCII && $0.isNumber
		}), let length = Int(rawLength),
			length >= 0
		else
		{
			throw HTTPParseError.malformed
		}
		guard length <= maxBody else
		{
			throw HTTPParseError.tooLarge
		}
		if first[0] == "POST" && headers["content-length"] == nil
		{
			throw HTTPParseError.malformed
		}
		guard buffer.count >= end.upperBound + length else
		{
			return nil
		}
		guard buffer.count == end.upperBound + length else
		{
			throw HTTPParseError.unsupported
		}
		return HTTPRequest(
			method: String(first[0]), target: String(first[1]), headers: headers, body: Data(buffer[end.upperBound...]))
	}
	public static func authenticate(_ request: HTTPRequest, token: String, port: UInt16, origins: Set<String>) -> Bool
	{
		guard let host = request.headers["host"],
			Set(["127.0.0.1:\(port)", "localhost:\(port)"]).contains(host.lowercased())
		else
		{
			return false
		}
		if let origin = request.headers["origin"], !origins.contains(origin)
		{
			return false
		}
		if request.method == "OPTIONS"
		{
			return request.headers["origin"] != nil
		}
		guard let supplied = request.headers["authorization"], supplied.count == token.count + 7 else
		{
			return false
		}
		let a = Array(supplied.utf8), b = Array(("Bearer " + token).utf8)
		return zip(a, b).reduce(UInt8(0))
		{
			$0 | ($1.0 ^ $1.1)
		} == 0
	}
}
public enum DeviceMatcher
{
	public static func paperang(_ name: String) -> Bool
	{
		let s = name.lowercased()
		return ["paperang", "miaomiaoji", "喵喵机"].contains
		{
			s.contains($0)
		}
	}
}
