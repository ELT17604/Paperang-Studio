using System.Net.Http.Headers;
using System.Text.Json;

namespace Paperang.Core;

public sealed class RelayHttpException(int status) : Exception($"中转 HTTP {status}：" + (status is 401 or 403 ? "设备密钥无效或权限不足" : "请求未被服务器接受"))
{ public int Status { get; } = status; }

public sealed class RelayClient : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient http;
    public RelayClient(AppSettings settings, HttpMessageHandler? handler = null)
    {
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = settings.Validate(), Timeout = TimeSpan.FromSeconds(4) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.DeviceKey);
        http.DefaultRequestHeaders.Add("X-Paperang-Content-Types", "text,raster1");
        http.DefaultRequestHeaders.CacheControl = new() { NoCache = true, NoStore = true };
    }
    public async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(4));
        ct = deadline.Token;
        using var req = new HttpRequestMessage(method, path);
        if (body != null)
        {
            // Upstream explicitly rejects Transfer-Encoding: chunked.
            req.Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(body, Json));
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) throw new RelayHttpException((int)res.StatusCode);
        if (res.Content.Headers.ContentLength > 1048576) throw new InvalidDataException("服务器响应超过 1 MB");
        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var memory = new MemoryStream();
        byte[] buffer = new byte[8192]; int n;
        while ((n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + n > 1048576) throw new InvalidDataException("服务器响应超过 1 MB");
            memory.Write(buffer, 0, n);
        }
        using var json = JsonDocument.Parse(memory.ToArray());
        if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("服务器未返回 JSON 对象");
        return json.RootElement.Clone();
    }
    public Task<JsonElement> Get(string path, CancellationToken ct) => RequestAsync(HttpMethod.Get, path, null, ct);
    public Task<JsonElement> Post(string path, object body, CancellationToken ct) => RequestAsync(HttpMethod.Post, path, body, ct);
    public void Dispose() => http.Dispose();
}
