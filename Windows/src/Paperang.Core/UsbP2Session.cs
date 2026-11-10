using System.Text;

namespace Paperang.Core;

public interface IUsbLink : IDisposable
{
    bool Connected { get; }
    Task WriteAsync(byte[] data, CancellationToken ct);
    /// Empty bytes mean a quiet read timeout, not disconnection.
    Task<byte[]> ReadAsync(TimeSpan timeout, CancellationToken ct);
}

public sealed class UsbP2Session(IUsbLink link) : IDisposable
{
    public const uint Seed = 0x35769521;
    private uint sessionSeed = Seed;
    private FrameDecoder decoder = new(Seed, true);
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;
    public bool Ready { get; private set; }
    public double Progress { get; private set; }
    public string PartitionDetail { get; private set; } = "";
    public string Status { get; private set; } = "正在连接 P2";
    private async Task<List<Frame>> Read(TimeSpan timeout, CancellationToken ct) => decoder.Push(await link.ReadAsync(timeout, ct).ConfigureAwait(false)).ToList();
    private async Task Drain(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < deadline)
        {
            var data = await link.ReadAsync(TimeSpan.FromMilliseconds(60), ct).ConfigureAwait(false);
            if (data.Length == 0) { decoder = new(sessionSeed, true); return; }
            decoder.Push(data).ToArray();
        }
        throw new InvalidOperationException("USB 接收持续占用，请断开其他打印客户端");
    }
    private async Task<byte[]> Request(byte command, byte[] payload, byte? response, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await Drain(ct).ConfigureAwait(false);
        await link.WriteAsync(Wire.Encode(command, payload, sessionSeed), ct).ConfigureAwait(false);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        byte[]? value = null;
        bool ack = false;
        while (DateTime.UtcNow < deadline)
        {
            foreach (var f in await Read(TimeSpan.FromMilliseconds(150), ct).ConfigureAwait(false))
            {
                if (f.Command == command && f.Payload.Length == 1)
                {
                    if (f.Payload[0] != 0) throw new InvalidOperationException($"USB 0x{command:X2} 返回失败 ACK");
                    ack = true;
                }
                if (response != null && f.Command == response) value = f.Payload;
            }
            if (response == null && ack) return [];
            // A data reply is sufficient even when firmware delays its ACK.
            if (value != null) return value;
        }
        if (!ack) throw new TimeoutException($"USB 0x{command:X2} 超时，未取得确认");
        throw new InvalidOperationException($"USB 0x{command:X2} 仅 ACK，未返回数据");
    }
    private Task<byte[]> Query(byte command, CancellationToken ct) => Request(command, [1], (byte)(command + 1), ct);
    private async Task Media(CancellationToken ct)
    {
        var value = await Query(12, ct).ConfigureAwait(false);
        if (value.Length != 2 || ((value[0] | value[1] << 8) & 0x11) != 0)
            throw new InvalidOperationException("请装好纸卷并合上盖子");
    }
    public async Task ConnectAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            byte[] identity;
            try { identity = await Query(6, ct).ConfigureAwait(false); }
            catch (TimeoutException)
            {
                // BLE key-zero registration persists across USB on this P2.
                // Only a read-only model query is repeated, never a physical write.
                sessionSeed = 0; decoder = new(sessionSeed, true);
                identity = await Query(6, ct).ConfigureAwait(false);
            }
            var model = Encoding.ASCII.GetString(identity).Trim('\0', '\r', '\n');
            if (model is not ("Paperang_P2" or "P2")) throw new InvalidOperationException("USB 设备型号未确认是 P2");
            await RefreshCore(ct).ConfigureAwait(false);
            var density = await Query(28, ct).ConfigureAwait(false);
            if (density.Length != 1 || density[0] > 100) throw new InvalidOperationException("USB 浓度响应无效");
            Ready = true;
        }
        finally { gate.Release(); }
    }
    private async Task RefreshCore(CancellationToken ct)
    {
        var battery = await Query(16, ct).ConfigureAwait(false);
        var media = await Query(12, ct).ConfigureAwait(false);
        if (media.Length != 2) throw new InvalidOperationException("USB 纸仓响应无效");
        int raw = media[0] | media[1] << 8;
        Status = $"P2 · 电量 {(battery.Length == 1 && battery[0] <= 100 ? battery[0] + "%" : "未知")} · " +
            ((raw & 0x10) != 0 ? "盖子打开" : (raw & 1) != 0 ? "缺纸" : "有纸 · 盖子闭合");
    }
    public async Task RefreshAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { await RefreshCore(ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    public async Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct)
    {
        raster.Validate();
        if (settings.Density is < 0 or > 100 || settings.Feed is < 0 or > 2000) throw new ArgumentException("打印设置超出范围");
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!Ready || !link.Connected) throw new InvalidOperationException("USB 打印机会话尚未就绪");
            Progress = 0; PartitionDetail = "";
            await Media(ct).ConfigureAwait(false);
            await Request(25, [(byte)settings.Density], null, ct).ConfigureAwait(false);
            var density = await Query(28, ct).ConfigureAwait(false);
            if (density.Length != 1 || density[0] != settings.Density) throw new InvalidOperationException("浓度回读未确认");
            int packets = (raster.Bytes.Length + 1007) / 1008;
            for (int i = 0; i < packets; i++)
            {
                await Media(ct).ConfigureAwait(false);
                // USB uses a remaining-packet countdown, not the BLE index.
                var payload = raster.Bytes.AsSpan(i * 1008, Math.Min(1008, raster.Bytes.Length - i * 1008)).ToArray();
                await link.WriteAsync(Wire.Encode(0, payload, sessionSeed, (byte)(packets - i - 1)), ct).ConfigureAwait(false);
                Progress = (i + 1.0) / packets;
                PartitionDetail = $"第1/1份 · 分包{i + 1}/{packets}";
            }
            await Task.Delay(2000, ct).ConfigureAwait(false);
            if (settings.Feed > 0) await Request(26, [(byte)settings.Feed, (byte)(settings.Feed >> 8)], null, ct).ConfigureAwait(false);
            await Media(ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    public void Dispose() { disposed = true; Ready = false; link.Dispose(); }
}
