using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Paperang.Core;

/// One relay loop, one printer operation. A persisted claim is never printed again.
public sealed class RelayEngine(IPrinter printer, IRasterizer renderer, IJournal journal, Func<AppSettings, RelayClient>? clientFactory = null)
{
    private CancellationTokenSource? lifetime;
    private Task? loop;
    private Task? printing;
    private PendingResult? pending;
    private AppSettings settings = new();
    private volatile bool allowed;
    private volatile bool reconnectPrinter;
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    public bool Allowed => allowed;
    public bool Online { get; private set; }
    public bool Running => loop is { IsCompleted: false };
    public bool Busy => printing is { IsCompleted: false };
    public int Retries { get; private set; }
    public int Queued { get; private set; }
    public string Message { get; private set; } = "请从托盘打开设置，连接服务器和 P2";
    public string PrintMessage { get; private set; } = "空闲";
    public string PrinterStatus => printer.Status;
    public bool PrinterReady => printer.Ready;
    public double Progress => printer.Progress;
    public event Action? Changed;
    public void SetAllowed(bool value) { allowed = value; Changed?.Invoke(); }
    public void InvalidatePrinter() => reconnectPrinter = true;
    public void ApplyTemplates(List<RelayTemplate> templates, string id) => settings = settings with { Templates = templates, TemplateId = id, Style = "自定义" };
    public async Task StartAsync(AppSettings value)
    {
        value.Validate();
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
        await StopCoreAsync().ConfigureAwait(false);
        pending = journal.Load();
        string fingerprint = Fingerprint(value.DeviceKey);
        if (pending != null && (pending.Server != value.Validate().AbsoluteUri || pending.KeyFingerprint != fingerprint))
            throw new InvalidOperationException("有尚未回报的任务，请先用原服务器与设备密钥连接完成回报");
        settings = value; allowed = value.AutoAllowPrint; Retries = 0;
        lifetime = new CancellationTokenSource();
        // Run outside the UI synchronization context: rendering and persistence must not block the tray.
        loop = Task.Run(() => RunAsync(lifetime.Token));
        Changed?.Invoke();
        }
        finally { lifecycle.Release(); }
    }
    public async Task StopAsync()
    {
        await lifecycle.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync().ConfigureAwait(false); }
        finally { lifecycle.Release(); }
    }
    private async Task StopCoreAsync()
    {
        lifetime?.Cancel();
        if (loop != null) { try { await loop.ConfigureAwait(false); } catch (OperationCanceledException) { } }
        lifetime?.Dispose(); lifetime = null; loop = null; Online = false;
        printer.Disconnect();
        Changed?.Invoke();
    }
    private async Task RunAsync(CancellationToken ct)
    {
        using var relay = clientFactory?.Invoke(settings) ?? new RelayClient(settings);
        int failures = 0;
        DateTime nextPrinter = DateTime.MinValue, nextRefresh = DateTime.MinValue;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (printing is { IsCompleted: true }) { await printing.ConfigureAwait(false); printing = null; }
                    if (!Busy) await FlushAsync(relay, ct).ConfigureAwait(false);
                    if (Busy && pending is { State: "unknown" } flight)
                        await relay.Post($"v1/device/jobs/{flight.JobId}/result", new { claim = flight.Claim, state = "printing", detail = printer.PartitionDetail }, ct).ConfigureAwait(false);
                    await relay.Post("v1/device/status", new { status = Status() }, ct).ConfigureAwait(false);
                    var response = await relay.Get("v1/device/jobs", ct).ConfigureAwait(false);
                    var jobs = response.GetProperty("jobs").Deserialize<RelayJob[]>(RelayClient.Json) ?? [];
                    foreach (var j in jobs) j.Validate();
                    Online = true; failures = 0; Retries = 0;
                    Queued = jobs.Count(x => x.State == "queued");
                    Message = $"中转在线 · 待处理 {Queued} 项";
                    if (reconnectPrinter && !Busy) { printer.Disconnect(); reconnectPrinter = false; nextPrinter = DateTime.MinValue; }
                    if (!Busy && !printer.Ready && settings.PrinterAddress.Length > 0 && DateTime.UtcNow >= nextPrinter)
                    {
                        try { await printer.ConnectAsync(settings, ct).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch { PrintMessage = "打印机连接失败；30 秒后重试，请检查电源和蓝牙"; }
                        nextPrinter = DateTime.UtcNow.AddSeconds(30);
                    }
                    if (!Busy && printer.Ready && DateTime.UtcNow >= nextRefresh)
                    {
                        try { await printer.RefreshAsync(ct).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch { printer.Disconnect(); PrintMessage = "打印机失去响应，等待重连"; }
                        nextRefresh = DateTime.UtcNow.AddSeconds(30);
                    }
                    if (allowed && printer.Ready && !Busy && pending == null)
                    {
                        var job = jobs.Where(x => x.State == "queued").OrderBy(x => x.Created).FirstOrDefault();
                        if (job != null) await ClaimAsync(relay, job, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Online = false; failures++; Retries = Math.Max(0, failures - 1);
                    Message = $"中转不可用 · 重连 {Retries}/5 · {SafeMessage(ex)}";
                    if (failures >= 6) { Message = "连续 5 次重连失败，已断开；请从托盘重新连接"; break; }
                }
                Changed?.Invoke();
                await Task.Delay(2000, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            // If a relay outage exhausts retries during a print, let that print settle,
            // keeping its outcome durable for the next explicit reconnection.
            if (printing != null) { try { await printing.ConfigureAwait(false); } catch (OperationCanceledException) { } printing = null; }
            Online = false; Changed?.Invoke();
        }
    }
    private async Task ClaimAsync(RelayClient relay, RelayJob job, CancellationToken ct)
    {
        var jobSettings = settings;
        try
        {
            if (job.Truncated || job.Content is { } c && !c.TryGetProperty("data", out _))
            {
                var full = (await relay.Get("v1/device/jobs/" + job.Id, ct).ConfigureAwait(false)).Deserialize<RelayJob>(RelayClient.Json)?.Validate();
                if (full == null || full.Id != job.Id || full.Truncated) throw new InvalidDataException("未取得完整任务，禁止打印摘要");
                job = full;
            }
            var raster = renderer.Render(job, jobSettings).Validate();
            if (!allowed || ct.IsCancellationRequested) return;
            JsonElement claim;
            try { claim = await relay.Post($"v1/device/jobs/{job.Id}/claim", new { }, ct).ConfigureAwait(false); }
            catch (RelayHttpException e) when (e.Status == 409) { return; }
            string token = claim.GetProperty("claim").GetString() ?? throw new InvalidDataException("缺少认领凭据");
            pending = new(settings.Validate().AbsoluteUri, Fingerprint(settings.DeviceKey), job.Id, token);
            // Persist unknown BEFORE allowing any physical write.
            journal.Save(pending);
            await relay.Post($"v1/device/jobs/{job.Id}/result", new { claim = token, state = "printing" }, ct).ConfigureAwait(false);
            if (!allowed) { PrintMessage = "领取后已暂停，任务按不确定结果回报，不会重打"; return; }
            printing = PrintAsync(raster, jobSettings, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            allowed = false; PrintMessage = "自动打印已暂停：" + SafeMessage(ex);
            // Claim stays unknown; a failed report must not result in a second print.
        }
    }
    private async Task PrintAsync(Raster raster, AppSettings jobSettings, CancellationToken ct)
    {
        try
        {
            PrintMessage = "正在发送纸条"; Changed?.Invoke();
            await printer.PrintAsync(raster, jobSettings, ct).ConfigureAwait(false);
            pending = pending! with { State = "sent" };
            journal.Save(pending);
            PrintMessage = "数据已发送，请核对纸面";
        }
        catch (Exception ex)
        {
            allowed = false;
            printer.Disconnect();
            PrintMessage = "结果不确定，已暂停；请核对纸面 · " + SafeMessage(ex);
        }
        Changed?.Invoke();
    }
    private async Task FlushAsync(RelayClient relay, CancellationToken ct)
    {
        if (pending == null) return;
        await relay.Post($"v1/device/jobs/{pending.JobId}/result", new {
            claim = pending.Claim, state = pending.State,
            detail = pending.State == "sent" ? "Data sent; physical output not independently confirmed" : "Inspect paper before any manual retry"
        }, ct).ConfigureAwait(false);
        journal.Save(null); pending = null;
    }
    public object Status() => new {
        appVersion = "0.2.3-win_lite", apiVersion = "v1", trayMode = true,
        networkPrintAllowed = allowed, automaticNetworkConsent = allowed,
        relayAutomaticPrinting = allowed, relayContentTypes = new[] { "text", "raster1" },
        connected = printer.Ready, busy = Busy, printer = settings.PrinterName,
        state = printer.Status, progress = printer.Progress, jobStatus = PrintMessage,
        printingEnabled = allowed, workspacePrintingEnabled = false, charging = "unknown"
    };
    public static string Fingerprint(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    private static string SafeMessage(Exception e) => e switch {
        RelayHttpException => e.Message, HttpRequestException => "网络连接失败", TaskCanceledException => "请求超时",
        _ => e is ArgumentException or InvalidDataException or InvalidOperationException ? e.Message : "操作失败，请检查设置与连接"
    };
}
