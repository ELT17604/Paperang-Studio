using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Paperang.Core;

/// A registered public key is the identity; short-lived bearer sessions are never saved.
public sealed class OwnerAuthentication(string path, Uri server) : IDisposable
{
	private readonly SemaphoreSlim gate = new(1, 1);
	private volatile bool disposed;
	private string token = "";
	private DateTimeOffset expires;
	public static JsonElement Read(string path, Uri server)
	{
		using var file = File.OpenRead(path);
		if (file.Length > 32768)
			throw new InvalidDataException("所有者文件超过 32 KiB");
		using var document = JsonDocument.Parse(file);
		var root = document.RootElement;
		if (!Uri.TryCreate(root.GetProperty("server").GetString(), UriKind.Absolute, out var bound) || bound != server)
			throw new InvalidDataException("所有者私钥绑定的服务器与当前连接不一致");
		if (!Guid.TryParse(root.GetProperty("id").GetString(), out _))
			throw new InvalidDataException("公钥尚未配对：请先在服务器登记公钥并写入凭证 id");
		return root.Clone();
	}
	public static string Fingerprint(string path, Uri server)
	{
		var root = Read(path, server);
		using var rsa = RSA.Create();
		rsa.ImportFromPem(root.GetProperty("privateKeyPem").GetString());
		return root.GetProperty("id").GetString() + ":" +
		       Convert.ToHexStringLower(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()));
	}
	public async Task<string> GetTokenAsync(HttpClient http, CancellationToken ct)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		await gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (token.Length > 0 && DateTimeOffset.UtcNow < expires)
				return token;
			var key = Read(path, server);
			using var rsa = RSA.Create();
			rsa.ImportFromPem(key.GetProperty("privateKeyPem").GetString());
			if (rsa.KeySize < 3072)
				throw new InvalidDataException("所有者 RSA 私钥至少需要 3072 位");
			async Task<JsonElement> Post(string endpoint, object body)
			{
				using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
				request.Headers.Add("X-Paperang-Native", "1");
				request.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, RelayClient.Json));
				request.Content.Headers.ContentType = new("application/json");
				using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
				if (!response.IsSuccessStatusCode)
					throw new RelayHttpException((int)response.StatusCode);
				await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
				byte[] buffer = new byte[16385];
				int total = 0, n;
				while (total < buffer.Length &&
				       (n = await stream.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false)) > 0)
					total += n;
				if (total > 16384)
					throw new InvalidDataException("鉴权响应过大");
				using var json = JsonDocument.Parse(buffer.AsMemory(0, total));
				return json.RootElement.Clone();
			}
			var challenge =
			    await Post("v2/auth/challenge", new { id = key.GetProperty("id").GetString() }).ConfigureAwait(false);
			if (challenge.GetProperty("algorithm").GetString() != "RSA-PSS-SHA256")
				throw new InvalidDataException("不支持的所有者签名算法");
			byte[] signature = rsa.SignData(Encoding.UTF8.GetBytes(challenge.GetProperty("message").GetString()!),
			                                HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
			var proof = await Post("v2/auth/prove", new { challenge = challenge.GetProperty("challenge").GetString(),
			                                              signature = Convert.ToBase64String(signature) })
			                .ConfigureAwait(false);
			if (proof.GetProperty("role").GetString() != "C")
				throw new InvalidDataException("所有者私钥未获得 C 权限");
			token = proof.GetProperty("token").GetString() ??
			        throw new InvalidDataException("缺少原生管理会话；请更新客户端和服务器");
			int age = Math.Clamp(proof.GetProperty("maxAge").GetInt32(), 1, 600);
			expires = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, age - 60));
			return token;
		}
		finally
		{
			gate.Release();
		}
	}
	public void Dispose()
	{
		disposed = true;
		token = "";
	} // No native wait handle is allocated; an in-flight cancellation must still release this managed gate.
}
