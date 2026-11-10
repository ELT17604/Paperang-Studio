namespace Paperang.Core;

/// Select a transport only at connection boundaries. Never replay a failed print.
public sealed class PreferredPrinter(IPrinter usb, IPrinter bluetooth) : IPrinter
{
    private IPrinter? active;
    private string status = "打印机：未连接";
    public string ActiveTransport => active == usb ? "usb" : active == bluetooth ? "bluetooth" : "";
    public bool Ready => active?.Ready == true;
    public string Status => active == null ? status : (active == usb ? "USB · " : "蓝牙 · ") + active.Status;
    public double Progress => active?.Progress ?? 0;
    public string PartitionDetail => active?.PartitionDetail ?? "";
    public async Task ConnectAsync(AppSettings settings, CancellationToken ct)
    {
        Disconnect();
        ct.ThrowIfCancellationRequested();
        string usbError = "";
        if (settings.PrinterTransport == "auto")
        {
            status = "正在尝试 USB 连接…";
            try
            {
                await usb.ConnectAsync(settings, ct).ConfigureAwait(false);
                if (!usb.Ready) throw new InvalidOperationException("USB 会话未就绪");
                ct.ThrowIfCancellationRequested();
                bluetooth.Disconnect(); active = usb; return;
            }
            catch (OperationCanceledException) { usb.Disconnect(); throw; }
            catch (Exception e) { usb.Disconnect(); usbError = "USB：" + e.Message + "；"; }
        }
        if (string.IsNullOrEmpty(settings.PrinterAddress))
        {
            status = usbError + "蓝牙：请先扫描并选择 P2";
            throw new InvalidOperationException(status);
        }
        status = usbError + "正在连接记住的蓝牙 P2…";
        try
        {
            await bluetooth.ConnectAsync(settings, ct).ConfigureAwait(false);
            if (!bluetooth.Ready) throw new InvalidOperationException("蓝牙会话未就绪");
            ct.ThrowIfCancellationRequested(); active = bluetooth;
        }
        catch (OperationCanceledException) { bluetooth.Disconnect(); throw; }
        catch (Exception e) { bluetooth.Disconnect(); status = usbError + "蓝牙连接失败：" + e.Message; throw new InvalidOperationException(status, e); }
    }
    public Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct) =>
        active?.PrintAsync(raster, settings, ct) ?? throw new InvalidOperationException("打印机未连接");
    public Task RefreshAsync(CancellationToken ct) => active?.RefreshAsync(ct) ?? Task.CompletedTask;
    public void Disconnect() { active = null; usb.Disconnect(); bluetooth.Disconnect(); status = "打印机：未连接"; }
    public void Dispose() { Disconnect(); usb.Dispose(); bluetooth.Dispose(); }
}
