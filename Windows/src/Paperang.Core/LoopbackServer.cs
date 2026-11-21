using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Paperang.Core;

public sealed record LocalRequest(string Method, string Target, byte[] Body);
public sealed record LocalResponse(int Status, object Body);

public sealed class LoopbackServer : IDisposable
{
	private readonly TcpListener listener;
	private readonly byte[] tokenHash;
	private readonly HashSet<string> origins;
	private readonly Func<LocalRequest, CancellationToken, Task<LocalResponse>> handler;
	private readonly CancellationTokenSource lifetime = new();
	private readonly SemaphoreSlim connections = new(8, 8);
	private Task? accepting;
	public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

	public LoopbackServer(int port, string token, IEnumerable<string> allowedOrigins,
		Func<LocalRequest, CancellationToken, Task<LocalResponse>> handler)
	{
		if (port is < 0 or > 65535 || token.Length is < 32 or > 256 || token.Any(char.IsWhiteSpace))
			throw new ArgumentException("API 端口或访问令牌无效。");
		this.handler = handler;
		origins = new(allowedOrigins, StringComparer.Ordinal);
		foreach (string origin in origins)
			if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
			    uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
			    origin.EndsWith('/') || origin.Any(char.IsControl))
				throw new ArgumentException("Origin 必须是完整的 http(s) 来源，不含路径、通配符或结尾斜线。");
		tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
		listener = new(IPAddress.Loopback, port);
	}

	public void Start()
	{
		if (accepting != null) throw new InvalidOperationException("API 已启动。");
		listener.Start(16);
		accepting = Accept();
	}
	private async Task Accept()
	{
		while (!lifetime.IsCancellationRequested)
		{
			TcpClient client;
			try { client = await listener.AcceptTcpClientAsync(lifetime.Token).ConfigureAwait(false); }
			catch (Exception) when (lifetime.IsCancellationRequested) { break; }
			if (lifetime.IsCancellationRequested) { client.Dispose(); break; }
			if (!connections.Wait(0))
			{
				client.Dispose();
				continue;
			}
			_ = Handle(client);
		}
	}
	private async Task Handle(TcpClient client)
	{
		using (client)
		using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
		{
			deadline.CancelAfter(TimeSpan.FromSeconds(20));
			var token = deadline.Token;
			await using var stream = client.GetStream();
			string? origin = null;
			try
			{
				using var header = new MemoryStream();
				var single = new byte[1];
				int tail = 0;
				while (tail != 0x0D0A0D0A)
				{
					if (header.Length >= 16384) throw new InvalidDataException("请求头过大。");
					if (await stream.ReadAsync(single, token).ConfigureAwait(false) == 0) return;
					header.WriteByte(single[0]);
					tail = (tail << 8) | single[0];
				}
				string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n", StringSplitOptions.None);
				string[] requestLine = lines[0].Split(' ');
				if (requestLine.Length != 3 || requestLine[2] != "HTTP/1.1" ||
				    !requestLine[1].StartsWith('/') || requestLine[1].StartsWith("//"))
					throw new InvalidDataException("HTTP 请求行无效。");
				var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (string line in lines.Skip(1).Where(line => line.Length != 0))
				{
					int separator = line.IndexOf(':');
					if (separator <= 0 || !headers.TryAdd(line[..separator], line[(separator + 1)..].Trim()))
						throw new InvalidDataException("HTTP 请求头重复或无效。");
				}
				if (!headers.TryGetValue("Host", out string? host) || host != $"127.0.0.1:{Port}" && host != $"localhost:{Port}")
				{
					await Reply(stream, new(403, Error("host", "只允许本机 Host。")), null, token);
					return;
				}
				headers.TryGetValue("Origin", out origin);
				if (origin != null && !origins.Contains(origin))
				{
					await Reply(stream, new(403, Error("origin", "来源未获允许。")), null, token);
					return;
				}
				if (requestLine[0] == "OPTIONS")
				{
					await Reply(stream, new(200, new { accepted = true }), origin, token);
					return;
				}
				string bearer = headers.GetValueOrDefault("Authorization", "");
				bool authorized = bearer.StartsWith("Bearer ", StringComparison.Ordinal) &&
					CryptographicOperations.FixedTimeEquals(tokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(bearer[7..])));
				if (!authorized)
				{
					await Reply(stream, new(401, Error("authentication", "访问令牌无效。")), origin, token);
					return;
				}
				if (headers.ContainsKey("Transfer-Encoding")) throw new InvalidDataException("不支持分块请求。");
				int length = 0;
				if (headers.TryGetValue("Content-Length", out string? rawLength) &&
				    (!int.TryParse(rawLength, out length) || length < 0 || length > 2 * 1024 * 1024))
				{
					await Reply(stream, new(413, Error("body", "请求大小超过上限。")), origin, token);
					return;
				}
				if (requestLine[0] == "POST" && (!headers.TryGetValue("Content-Type", out string? type) ||
				    !type.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase)))
					throw new InvalidDataException("POST 请求需使用 application/json。");
				var body = new byte[length];
				await stream.ReadExactlyAsync(body, token).ConfigureAwait(false);
				var response = await handler(new(requestLine[0], requestLine[1], body), token).ConfigureAwait(false);
				await Reply(stream, response, origin, token);
			}
			catch (Exception error) when (error is InvalidDataException or JsonException or ArgumentException)
			{
				try { await Reply(stream, new(400, Error("request", error.Message)), origin, token); }
				catch (Exception) { }
			}
			catch (Exception)
			{
				if (!token.IsCancellationRequested)
					try { await Reply(stream, new(500, Error("internal", "本机请求未完成。")), origin, token); }
					catch (Exception) { }
			}
			finally { connections.Release(); }
		}
	}
	public static object Error(string code, string message) => new { error = new { code, message } };
	private static async Task Reply(NetworkStream stream, LocalResponse response, string? origin, CancellationToken token)
	{
		byte[] body = JsonSerializer.SerializeToUtf8Bytes(response.Body, RelayClient.Json);
		string cors = origin == null ? "" : $"Access-Control-Allow-Origin: {origin}\r\nVary: Origin\r\nAccess-Control-Allow-Methods: GET, POST, OPTIONS\r\nAccess-Control-Allow-Headers: Authorization, Content-Type\r\n";
		string header = $"HTTP/1.1 {response.Status} Response\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n{cors}\r\n";
		await stream.WriteAsync(Encoding.ASCII.GetBytes(header), token).ConfigureAwait(false);
		await stream.WriteAsync(body, token).ConfigureAwait(false);
	}
	public void Dispose()
	{
		lifetime.Cancel();
		listener.Stop();
	}
}
