using System.Net.Http.Headers;
using System.Text.Json;

namespace Paperang.Core;

public sealed class RelayHttpException(int status) : Exception
($"中转 HTTP {status}：" +
 (status is 401 or 403 ? "凭证无效或权限不足：M 只执行打印；管理需要已登记的所有者私钥/C 权限" : "请求未被服务器接受"))
{
	public int Status { get; } = status;
}

public sealed class RelayClient : IDisposable
{
	public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
	private readonly HttpClient http;
	private readonly OwnerAuthentication? owner;
	public RelayClient(AppSettings settings, HttpMessageHandler? handler = null)
	{
		http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) {
			BaseAddress = settings.Validate(),
			Timeout = TimeSpan.FromSeconds(settings.AuthenticationMode == "owner" ? 15 : 4)
		};
		if (settings.AuthenticationMode == "owner")
			owner = new(settings.OwnerKeyFile, settings.Validate());
		else
			http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.DeviceKey);
		http.DefaultRequestHeaders.UserAgent.ParseAdd("PaperangStudio/" + AppVersion.Current);
		http.DefaultRequestHeaders.Add("X-Paperang-Content-Types", "text,raster1");
		http.DefaultRequestHeaders.CacheControl = new() { NoCache = true, NoStore = true };
	}
	public async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body, CancellationToken ct,
	                                            IReadOnlyDictionary<string, string>? headers = null)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
		deadline.CancelAfter(TimeSpan.FromSeconds(owner != null ? 15 : 4));
		ct = deadline.Token;
		using var req = new HttpRequestMessage(method, path);
		if (owner != null)
			req.Headers.Authorization = new("Bearer", await owner.GetTokenAsync(http, ct).ConfigureAwait(false));
		req.Headers.TryAddWithoutValidation("X-Paperang-Native", "1");
		if (headers != null)
			foreach (var item in headers)
			{
				req.Headers.Remove(item.Key);
				req.Headers.Add(item.Key, item.Value);
			}
		if (body != null)
		{
			// Upstream explicitly rejects Transfer-Encoding: chunked.
			req.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, Json));
			req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		}
		using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
		if (!res.IsSuccessStatusCode)
			throw new RelayHttpException((int)res.StatusCode);
		if (res.Content.Headers.ContentLength > 1048576)
			throw new InvalidDataException("服务器响应超过 1 MB");
		await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
		using var memory = new MemoryStream();
		byte[] buffer = new byte[8192];
		int n;
		while ((n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
		{
			if (memory.Length + n > 1048576)
				throw new InvalidDataException("服务器响应超过 1 MB");
			memory.Write(buffer, 0, n);
		}
		using var json = JsonDocument.Parse(memory.ToArray());
		if (json.RootElement.ValueKind != JsonValueKind.Object)
			throw new InvalidDataException("服务器未返回 JSON 对象");
		return json.RootElement.Clone();
	}
	public Task<JsonElement> Get(string path, CancellationToken ct) => RequestAsync(HttpMethod.Get, path, null, ct);
	public Task<JsonElement> Post(string path, object body, CancellationToken ct) => RequestAsync(HttpMethod.Post, path,
	                                                                                              body, ct);
	public void Dispose()
	{
		http.Dispose();
		owner?.Dispose();
	}
}
