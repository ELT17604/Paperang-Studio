import PrintCore
import Foundation
import Security

struct RelaySavedConnection: Codable
{
	let server: String
	let credential: String
	var authMode: String?
	var ownerPath: String?
}
struct RelaySavedClaim: Codable
{
	let server: String
	let fingerprint: String
	let job: String
	let claim: String
	let state: String
}

/// Atomic Keychain updates; secrets are never stored in UserDefaults or logs.
enum RelayVault
{
	static var service = "local.paperang.relay.v2"
	static func read<T: Decodable>(_ account: String, as: T.Type) throws -> T?
	{
		let query: [String: Any] = [
			kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
			kSecAttrAccount as String: account, kSecReturnData as String: true,
			kSecMatchLimit as String: kSecMatchLimitOne
		]
		var result: CFTypeRef?
		let status = SecItemCopyMatching(query as CFDictionary, &result)
		if status == errSecItemNotFound
		{
			return nil
		}
		guard status == errSecSuccess, let bytes = result as? Data else
		{
			throw PrintFailure("无法读取中转 Keychain 数据")
		}
		return try JSONDecoder().decode(T.self, from: bytes)
	}
	static func save<T: Encodable>(_ account: String, _ value: T) throws
	{
		let query: [String: Any] = [
			kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
			kSecAttrAccount as String: account
		]
		let bytes = try JSONEncoder().encode(value)
		let status = SecItemUpdate(query as CFDictionary, [kSecValueData as String: bytes] as CFDictionary)
		if status == errSecItemNotFound
		{
			var attributes = query
			attributes[kSecValueData as String] = bytes
			attributes[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
			guard SecItemAdd(attributes as CFDictionary, nil) == errSecSuccess else
			{
				throw PrintFailure("无法保存中转 Keychain 数据")
			}
		} else if status != errSecSuccess
		{
			throw PrintFailure("无法更新中转 Keychain 数据")
		}
	}
	static func remove(_ account: String) throws
	{
		let status = SecItemDelete(
			[
				kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service,
				kSecAttrAccount as String: account
			] as CFDictionary)
		guard status == errSecSuccess || status == errSecItemNotFound else
		{
			throw PrintFailure("无法清除中转 Keychain 数据")
		}
	}
}
