// Read-only production probe. Pass the configured HTTPS Relay root as argument.
// Does not authenticate, submit, claim, cancel, or change any server data.
import Foundation
@main struct Probe
{
	static func main() async throws
	{
		guard CommandLine.arguments.count == 2, let root = URL(string: CommandLine.arguments[1]),
			root.scheme == "https", root.user == nil, root.password == nil, root.query == nil, root.fragment == nil
		else
		{
			throw URLError(.badURL)
		}
		var request = URLRequest(url: root.appendingPathComponent("v2/public/status"))
		request.timeoutInterval = 15
		let session = URLSession(configuration: .ephemeral)
		defer
		{
			session.invalidateAndCancel()
		}
		let (data, response) = try await session.data(for: request)
		guard (response as? HTTPURLResponse)?.statusCode == 200,
			let object = try JSONSerialization.jsonObject(with: data) as? [String: Any],
			let capabilities = object["capabilities"] as? [String: Any], capabilities["authVersion"] as? Int == 2
		else
		{
			throw URLError(.badServerResponse)
		}
		let result: [String: Any] = [
			"http": 200, "version": object["version"] ?? "", "status": object["status"] ?? [:],
			"capabilities": capabilities
		]
		print(
			String(data: try JSONSerialization.data(withJSONObject: result, options: [.sortedKeys]), encoding: .utf8)!)
	}
}
