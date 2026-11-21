using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Paperang.Core;
using Paperang.Windows;
using Windows.ApplicationModel.DataTransfer;

namespace Paperang.WinClient;

internal sealed partial class WorkspaceWindow
{
	private readonly StackPanel apiPage = new() { Spacing = 12 };
	private readonly NumberBox apiPort = Number("监听端口", 1024, 65535, 8766);
	private readonly PasswordBox apiToken = new() { Header = "Bearer 访问令牌", Password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)) };
	private readonly TextBox apiOrigins = new() { Header = "允许的网页 Origin（逗号分隔）", PlaceholderText = "https://example.com" };
	private readonly TextBlock apiState = Paragraph("本地 API 未监听");
	private LoopbackServer? apiServer;
	private PrinterChoice[] apiDevices = [];
	private sealed record LocalLog(DateTimeOffset Timestamp, string Type, string Text);
	private readonly List<LocalLog> apiEvents = [];
	private static readonly string[] LocalGetRoutes = ["/v2/status", "/v2/devices", "/v2/protocol", "/v2/settings", "/v2/jobs", "/v2/logs", "/v2/openapi"];
	private static readonly string[] LocalPostRoutes = ["/v2/scan", "/v2/connect", "/v2/disconnect", "/v2/query", "/v2/render", "/v2/jobs", "/v2/feed"];

	private void BuildLocalApi()
	{
		apiPage.Children.Add(Heading("本地 API"));
		apiPage.Children.Add(Paragraph("默认关闭，仅监听 127.0.0.1。每个请求需要访问令牌；打印和送纸还需全局允许耗纸及 allowPaper:true。关闭工作台窗口会停止监听。"));
		foreach (var field in new FrameworkElement[] { apiPort, apiToken, apiOrigins, apiState }) apiPage.Children.Add(field);
		apiPage.Children.Add(SimpleAction("复制访问令牌", () =>
		{
			var clipboard = new DataPackage();
			clipboard.SetText(apiToken.Password);
			Clipboard.SetContent(clipboard);
		}));
		apiPage.Children.Add(SimpleAction("重新生成令牌", () =>
		{
			if (apiServer != null) throw new InvalidOperationException("请先停止 API。");
			apiToken.Password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
		}));
		apiPage.Children.Add(SimpleAction("启动本地 API", () =>
		{
			if (apiServer != null) throw new InvalidOperationException("API 已启动。");
			var server = new LoopbackServer(Value(apiPort), apiToken.Password,
				apiOrigins.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), DispatchLocalApi);
			try { server.Start(); }
			catch { server.Dispose(); throw; }
			apiServer = server;
			apiPort.IsEnabled = apiToken.IsEnabled = apiOrigins.IsEnabled = false;
			apiState.Text = $"正在监听 http://127.0.0.1:{server.Port}";
		}));
		apiPage.Children.Add(SimpleAction("停止 API", StopLocalApi));
		apiPage.Children.Add(Paragraph("GET：status、devices、protocol、settings、jobs、logs、openapi\nPOST：scan、connect、disconnect、query、render、jobs、feed\n任务查询：GET /v2/jobs/{id}；停止任务：POST /v2/jobs/{id}/cancel"));
		Closed += (_, _) => StopLocalApi();
	}
	private void StopLocalApi()
	{
		apiServer?.Dispose();
		apiServer = null;
		apiPort.IsEnabled = apiToken.IsEnabled = apiOrigins.IsEnabled = true;
		apiState.Text = "本地 API 未监听";
	}
	private Task<LocalResponse> DispatchLocalApi(LocalRequest request, CancellationToken token)
	{
		var completion = new TaskCompletionSource<LocalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (!DispatcherQueue.TryEnqueue(async () =>
		{
			try { token.ThrowIfCancellationRequested(); completion.TrySetResult(await HandleLocalApi(request, token)); }
			catch (Exception error) { completion.TrySetResult(new(400, LoopbackServer.Error("request", error.Message))); }
		}))
			completion.TrySetResult(new(503, LoopbackServer.Error("shutdown", "应用正在退出。")));
		return completion.Task.WaitAsync(token);
	}
	private async Task<LocalResponse> HandleLocalApi(LocalRequest request, CancellationToken token)
	{
		string path = request.Target.Split('?', 2)[0];
		string? Query(string name) => request.Target.Split('?', 2).ElementAtOrDefault(1)?.Split('&')
			.Select(part => part.Split('=', 2)).LastOrDefault(pair => pair[0] == name)?.ElementAtOrDefault(1);
		int logLimit = Math.Clamp(int.TryParse(Query("limit"), out int requestedLimit) ? requestedLimit : 100, 1, 500);
		if (apiEvents.Count >= 500) apiEvents.RemoveAt(0);
		apiEvents.Add(new(DateTimeOffset.UtcNow, "API", request.Method + " " + path));
		if (path == "/v1" || path.StartsWith("/v1/", StringComparison.Ordinal))
			return new(410, LoopbackServer.Error("api_v1_retired", "Use local API v2"));
		if (request.Method == "GET")
		{
			object? value = path switch
			{
				"/v2/status" => new { apiVersion = "v2", appVersion = Program.Version, connected = trayHost?.Connected ?? false,
					busy = trayHost?.Working ?? false, printingEnabled = trayHost?.AllowPaper ?? false,
					workspacePrintingEnabled = trayHost?.AllowPaper ?? false, state = trayHost?.DeviceStatus ?? "未连接",
					progress = trayHost?.Progress ?? 0, physicalCompletion = "unknown" },
				"/v2/devices" => new { scanning = false, devices = apiDevices.Where(device => Query("all") == "true" || BlePrinter.IsCandidate(device.Name))
					.Select(device => new { id = device.Address.ToString("X12"), name = device.Name, rssi = device.Rssi, paperangCandidate = BlePrinter.IsCandidate(device.Name) }) },
				"/v2/settings" => new { defaults = new WorkspaceSettings(), note = "每次请求可传部分 settings；宽度固定 576" },
				"/v2/protocol" => new { model = "Paperang_P2", widthDots = 576, bytesPerRow = 72, nominalDPI = 300,
					defaultFeedLines = 160, commands = P2Command.All.Select(item => new { command = (int)item.Code, name = item.Title,
						kind = item.Kind, evidence = item.Evidence, description = item.Detail,
						expectedResponse = P2Queries.Responses.TryGetValue(item.Code, out byte response) ? (int?)response : null }) },
				"/v2/jobs" => new { jobs = (trayHost?.Jobs ?? []).Select(LocalJob) },
				"/v2/logs" => new { order = "newest_first", logs = apiEvents.Concat((trayHost?.Traces ?? [])
					.Select(trace => new LocalLog(trace.Time, trace.Transport + " " + trace.Direction, trace.ToString())))
					.OrderByDescending(entry => entry.Timestamp).Take(logLimit).ToArray() },
				"/v2/openapi" => LocalOpenApi(),
				_ => null
			};
			if (value != null) return new(200, value);
			if (path.StartsWith("/v2/jobs/", StringComparison.Ordinal) && Guid.TryParse(path[9..], out var id))
			{
				var job = trayHost?.Jobs.FirstOrDefault(job => job.Id == id);
				if (job != null) return new(200, LocalJob(job));
			}
			return new(404, LoopbackServer.Error("not_found", "未知路由或任务。"));
		}
		if (request.Method != "POST") return new(405, LoopbackServer.Error("method", "不支持此方法。"));
		using var json = JsonDocument.Parse(request.Body);
		var body = json.RootElement;
		if (body.ValueKind != JsonValueKind.Object) return new(400, LoopbackServer.Error("body", "JSON 需为对象。"));
		if (path is "/v2/jobs" or "/v2/feed" &&
		    (!body.TryGetProperty("allowPaper", out var consent) || consent.ValueKind != JsonValueKind.True || !tray.AllowPaper))
			return new(403, LoopbackServer.Error("paper_disabled", "请开启全局耗纸并指定 allowPaper:true。"));
		if (path == "/v2/scan")
		{
			if (tray.Working) return new(409, LoopbackServer.Error("busy", "打印机正忙。"));
			apiDevices = await BlePrinter.ScanAsync(true, token);
			return new(200, new { accepted = true, scanning = false, count = apiDevices.Length });
		}
		if (path == "/v2/connect")
		{
			string id = body.GetProperty("id").GetString() ?? "";
			var device = apiDevices.FirstOrDefault(device => device.Address.ToString("X12") == id);
			if (device == null || tray.Connected || tray.Working) return new(409, LoopbackServer.Error("connection", "请先扫描并选择设备。"));
			tray.SaveSettings(tray.Settings() with { PrinterAddress = id, PrinterName = device.Name, PrinterTransport = "bluetooth" });
			await tray.UseLocalPrinterAsync(null, new(), token);
			return new(200, new { accepted = true, id, connected = tray.Connected });
		}
		if (path == "/v2/disconnect") { await tray.DisconnectAsync(); return new(200, new { connected = false }); }
		if (path == "/v2/query")
		{
			byte command = body.GetProperty("command").GetByte();
			if (!tray.Connected || tray.Working) return new(409, LoopbackServer.Error("busy", "设备未就绪或正忙。"));
			var payload = await tray.QueryAsync(command, token);
			return new(200, new { command, payloadHex = Convert.ToHexString(payload), hasData = true });
		}
		if (path is "/v2/render" or "/v2/jobs")
		{
			var settings = ParseLocalSettings(body);
			var raster = await RenderLocalBody(body, settings, token);
			token.ThrowIfCancellationRequested();
			if (path == "/v2/render")
			{
				using var bitmap = NoteRenderer.Preview(raster);
				using var bytes = new MemoryStream();
				bitmap.Save(bytes, System.Drawing.Imaging.ImageFormat.Png);
				var result = new Dictionary<string, object> { ["width"] = 576, ["height"] = raster.Height, ["bytes"] = raster.Bytes.Length,
					["previewPngBase64"] = Convert.ToBase64String(bytes.ToArray()) };
				if (body.TryGetProperty("includeRaw", out var raw) && raw.ValueKind == JsonValueKind.True)
					result["rawBase64"] = Convert.ToBase64String(raster.Bytes);
				return new(200, result);
			}
			if (!tray.Connected || tray.Working) return new(409, LoopbackServer.Error("busy", "设备未连接或正忙。"));
			var id = Guid.NewGuid();
			var options = new LocalPrintOptions(settings.Density, settings.Copies, settings.FeedBefore, settings.FeedAfter, settings.FeedBetweenCopies, settings);
			var job = tray.UseLocalPrinterAsync(raster, options, default, requestedId: id);
			if (job.IsCompleted) await job;
			else _ = ObserveLocalJob(job);
			return new(202, new { id, state = "accepted", note = "接收不等于纸面完成，请轮询任务。" });
		}
		if (path == "/v2/feed")
		{
			int lines = body.GetProperty("lines").GetInt32();
			if (lines is < 0 or > 2000) throw new InvalidDataException("送纸行数应为 0–2000。");
			if (!tray.Connected || tray.Working) return new(409, LoopbackServer.Error("busy", "设备未连接或正忙。"));
			if (lines > 0) await tray.UseLocalPrinterAsync(null, new(), token, lines);
			return new(200, new { acknowledged = true, lines, physicalCompletion = "unknown" });
		}
		if (path.StartsWith("/v2/jobs/", StringComparison.Ordinal) && path.EndsWith("/cancel", StringComparison.Ordinal) &&
		    Guid.TryParse(path[9..^7], out var cancelled) && tray.Jobs.FirstOrDefault()?.Id == cancelled && tray.Working)
		{
			tray.CancelCurrent();
			return new(200, new { id = cancelled, state = "cancelled", note = "已发送的数据无法撤回。" });
		}
		return new(404, LoopbackServer.Error("not_found", "未知路由。"));
	}
	private static object LocalJob(ClientJob job) => new { id = job.Id, title = job.Title, detail = job.Detail,
		state = job.State switch { "发送中" => "printing", "已发送" => "sent", "连接中" => "accepted", _ => "unknown" },
		stateLabel = job.State, createdAt = job.Created, physicalCompletion = "unknown" };
	private async Task ObserveLocalJob(Task task)
	{
		try { await task; }
		catch (Exception) { apiState.Text = "API 任务未正常完成；请在打印记录中核对结果。"; }
	}
	private static WorkspaceSettings ParseLocalSettings(JsonElement body)
	{
		if (!body.TryGetProperty("settings", out var patch)) return new();
		if (patch.ValueKind != JsonValueKind.Object) throw new InvalidDataException("settings 需为对象。");
		var keys = JsonSerializer.SerializeToElement(new WorkspaceSettings(), RelayClient.Json).EnumerateObject().Select(item => item.Name).ToHashSet();
		if (patch.EnumerateObject().Any(item => !keys.Contains(item.Name))) throw new InvalidDataException("settings 包含未知字段。");
		var settings = JsonSerializer.Deserialize<WorkspaceSettings>(patch, RelayClient.Json)!.Validate();
		return settings;
	}
	private static Task<Raster> RenderLocalBody(JsonElement body, WorkspaceSettings settings, CancellationToken token)
	{
		bool hasText = body.TryGetProperty("text", out var text), hasImage = body.TryGetProperty("imageBase64", out var image);
		if (hasText == hasImage) throw new InvalidDataException("text 和 imageBase64 必须且只能提供一个。");
		if (hasImage)
		{
			var bytes = Convert.FromBase64String(image.GetString() ?? "");
			return Task.Run(() => WorkspaceImage.Render(bytes, settings), token);
		}
		string value = text.GetString() ?? "";
		if (string.IsNullOrWhiteSpace(value) || value.Length > 10000) throw new InvalidDataException("文字须为 1–10000 字符。");
		return Task.Run(() => NoteRenderer.RenderText(value, settings.FontName, (float)settings.FontSize, settings.Bold,
			settings.Alignment, settings.MarginX, settings.MarginY, (float)settings.LineSpacing, settings, settings.ContentWidth), token);
	}
	private static object LocalOpenApi()
	{
		var paths = new Dictionary<string, object>();
		object Operation(bool post) => new Dictionary<string, object>
		{
			["responses"] = new Dictionary<string, object> { ["200"] = new { description = "请求完成" }, ["202"] = new { description = "任务已接收" },
				["400"] = new { description = "参数无效" }, ["401"] = new { description = "令牌无效" },
				["403"] = new { description = "来源或耗纸授权不足" }, ["409"] = new { description = "设备状态冲突" } },
			["description"] = post ? "JSON 请求；耗纸操作需 allowPaper:true" : "只读本机状态"
		};
		foreach (string route in LocalGetRoutes) paths[route] = new Dictionary<string, object> { ["get"] = Operation(false) };
		foreach (string route in LocalPostRoutes)
		{
			if (paths.TryGetValue(route, out var existing)) ((Dictionary<string, object>)existing)["post"] = Operation(true);
			else paths[route] = new Dictionary<string, object> { ["post"] = Operation(true) };
		}
		var parameter = new { name = "id", @in = "path", required = true, schema = new { type = "string", format = "uuid" } };
		paths["/v2/jobs/{id}"] = new { get = Operation(false), parameters = new[] { parameter } };
		paths["/v2/jobs/{id}/cancel"] = new { post = Operation(true), parameters = new[] { parameter } };
		return new { openapi = "3.1.0", info = new { title = "Paperang WinClient Local API", version = Program.Version },
			servers = new[] { new { url = "http://127.0.0.1:8766" } }, paths,
			security = new[] { new { bearer = Array.Empty<string>() } },
			components = new { securitySchemes = new { bearer = new { type = "http", scheme = "bearer" } } } };
	}
}
