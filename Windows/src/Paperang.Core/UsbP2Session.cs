using System.Text;

namespace Paperang.Core;

public interface IUsbLink : IDisposable
{
	bool Connected { get; }
	Task WriteAsync(byte[] data, CancellationToken ct);
	/// Empty bytes mean a quiet read timeout, not disconnection.
	Task<byte[]> ReadAsync(TimeSpan timeout, CancellationToken ct);
}

public sealed class UsbP2Session(IUsbLink link, WorkspaceSettings? transferSettings = null) : IDisposable
{
	public void ConfigureTransfer(WorkspaceSettings settings) => transfer = settings.Validate();
	public const uint Seed = 0x35769521;
	public Action<ProtocolTrace>? Trace { get; set; }
	private uint sessionSeed = Seed;
	private FrameDecoder decoder = new(Seed, true);
	private readonly SemaphoreSlim gate = new(1, 1);
	private bool disposed;
	private WorkspaceSettings transfer = (transferSettings ?? new()).Validate();
	public bool Ready { get; private set; }
	public double Progress { get; private set; }
	public string PartitionDetail { get; private set; } = "";
	public string Status { get; private set; } = "正在连接 P2";
	private async Task<List<Frame>> Read(TimeSpan timeout, CancellationToken ct)
	{
		var frames = decoder.Push(await link.ReadAsync(timeout, ct).ConfigureAwait(false)).ToList();
		foreach (var frame in frames) Trace?.Invoke(ProtocolTrace.Capture("USB", "RX", frame.Command, frame.Index, frame.Payload));
		return frames;
	}
	private async Task Drain(CancellationToken ct)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(500);
		while (DateTime.UtcNow < deadline)
		{
			var data = await link.ReadAsync(TimeSpan.FromMilliseconds(60), ct).ConfigureAwait(false);
			if (data.Length == 0)
			{
				decoder = new(sessionSeed, true);
				return;
			}
			decoder.Push(data).ToArray();
		}
		throw new InvalidOperationException("USB 接收持续占用，请断开其他打印客户端");
	}
	private async Task<byte[]> Request(byte command, byte[] payload, byte? response, CancellationToken ct)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		await Drain(ct).ConfigureAwait(false);
		Trace?.Invoke(ProtocolTrace.Capture("USB", "TX", command, 0, payload));
		await link.WriteAsync(Wire.Encode(command, payload, sessionSeed), ct).ConfigureAwait(false);
		long started = System.Diagnostics.Stopwatch.GetTimestamp();
		byte[]? value = null;
		bool ack = false;
		while (System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalSeconds < transfer.TimeoutSeconds)
		{
			foreach (var f in await Read(TimeSpan.FromMilliseconds(150), ct).ConfigureAwait(false))
			{
				if (f.Command == command && f.Payload.Length == 1)
				{
					if (f.Payload[0] != 0)
						throw new InvalidOperationException($"USB 0x{command:X2} 返回失败 ACK");
					ack = true;
				}
				if (response != null && f.Command == response)
					value = f.Payload;
			}
			if (response == null && ack)
				return [];
			// A data reply is sufficient even when firmware delays its ACK.
			if (value != null)
				return value;
		}
		if (!ack)
			throw new TimeoutException($"USB 0x{command:X2} 超时，未取得确认");
		throw new InvalidOperationException($"USB 0x{command:X2} 仅 ACK，未返回数据");
	}
	private Task<byte[]> Query(byte command, CancellationToken ct) => Request(command, [1], P2Queries.Expected(command), ct);
	public async Task<byte[]> QueryAsync(byte command, CancellationToken ct)
	{
		P2Queries.Expected(command);
		await gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			if (!Ready) throw new InvalidOperationException("打印机尚未就绪。");
			return await Query(command, ct).ConfigureAwait(false);
		}
		finally { gate.Release(); }
	}
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
			try
			{
				identity = await Query(6, ct).ConfigureAwait(false);
			}
			catch (TimeoutException)
			{
				// BLE key-zero registration persists across USB on this P2.
				// Only a read-only model query is repeated, never a physical write.
				sessionSeed = 0;
				decoder = new(sessionSeed, true);
				identity = await Query(6, ct).ConfigureAwait(false);
			}
			var model = Encoding.ASCII.GetString(identity).Trim('\0', '\r', '\n');
			if (model is not("Paperang_P2" or "P2"))
				throw new InvalidOperationException("USB 设备型号未确认是 P2");
			await RefreshCore(ct).ConfigureAwait(false);
			var density = await Query(28, ct).ConfigureAwait(false);
			if (density.Length != 1 || density[0] > 100)
				throw new InvalidOperationException("USB 浓度响应无效");
			Ready = true;
		}
		finally
		{
			gate.Release();
		}
	}
	private async Task RefreshCore(CancellationToken ct)
	{
		var battery = await Query(16, ct).ConfigureAwait(false);
		var media = await Query(12, ct).ConfigureAwait(false);
		if (media.Length != 2)
			throw new InvalidOperationException("USB 纸仓响应无效");
		int raw = media[0] | media[1] << 8;
		Status = $"P2 · 电量 {(battery.Length == 1 && battery[0] <= 100 ? battery[0] + "%" : "未知")} · " +
		         ((raw & 0x10) != 0 ? "盖子打开"
		          : (raw & 1) != 0  ? "缺纸"
		                            : "有纸 · 盖子闭合");
	}
	public async Task RefreshAsync(CancellationToken ct)
	{
		await gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await RefreshCore(ct).ConfigureAwait(false);
		}
		finally
		{
			gate.Release();
		}
	}
	public async Task FeedAsync(int lines, CancellationToken ct)
	{
		if (!Ready || lines is < 1 or > 2000)
			throw new InvalidOperationException("打印机未就绪或送纸不在 1–2000 行范围内");
		await gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			await Media(ct).ConfigureAwait(false);
			await Request(26, [(byte)lines, (byte)(lines >> 8)], null, ct).ConfigureAwait(false);
		}
		finally
		{
			gate.Release();
		}
	}
	public async Task SendEngineeringAsync(byte command, byte[] payload, CancellationToken token)
	{
		P2Engineering.Validate(command, payload);
		await gate.WaitAsync(token).ConfigureAwait(false);
		try
		{
			if (!Ready) throw new InvalidOperationException("打印机尚未就绪。");
			if (P2Engineering.UsesPaper(command)) await Media(token).ConfigureAwait(false);
			if (command == 25) await DensityCore(payload[0], token).ConfigureAwait(false);
			else await Request(command, payload, null, token).ConfigureAwait(false);
		}
		finally { gate.Release(); }
	}
	public async Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct)
	{
		raster.Validate();
		if (settings.Density is < 0 or > 100 || settings.Feed is < 0 or > 2000)
			throw new ArgumentException("打印设置超出范围");
		await gate.WaitAsync(ct).ConfigureAwait(false);
		try
		{
			if (!Ready || !link.Connected)
				throw new InvalidOperationException("USB 打印机会话尚未就绪");
			transfer = (settings.TransferSettings ?? new()).Validate();
			int frameBytes = transfer.PacketBytes;
			int packets = (raster.Bytes.Length + frameBytes - 1) / frameBytes;
			if (packets > 256) throw new InvalidDataException("USB 图像分包超过 256 帧，请增大载荷或拆分任务。");
			Progress = 0;
			PartitionDetail = "";
			await Media(ct).ConfigureAwait(false);
			await DensityCore(settings.Density, ct).ConfigureAwait(false);
			for (int i = 0; i < packets; i++)
			{
				await Media(ct).ConfigureAwait(false);
				// USB uses a remaining-packet countdown, not the BLE index.
				var payload = raster.Bytes.AsSpan(i * frameBytes, Math.Min(frameBytes, raster.Bytes.Length - i * frameBytes)).ToArray();
				Trace?.Invoke(ProtocolTrace.Capture("USB", "TX", 0, (byte)(packets - i - 1), payload));
				await link.WriteAsync(Wire.Encode(0, payload, sessionSeed, (byte)(packets - i - 1)), ct)
				    .ConfigureAwait(false);
				Progress = (i + 1.0) / packets;
				PartitionDetail = $"第1/1份 · 分包{i + 1}/{packets}";
				if (transfer.FramePauseMS > 0) await Task.Delay(transfer.FramePauseMS, ct).ConfigureAwait(false);
			}
			await Task.Delay(TimeSpan.FromSeconds(transfer.FinishWaitSeconds), ct).ConfigureAwait(false);
			if (settings.Feed > 0)
				await Request(26, [(byte)settings.Feed, (byte)(settings.Feed >> 8)], null, ct).ConfigureAwait(false);
			await Media(ct).ConfigureAwait(false);
		}
		finally
		{
			gate.Release();
		}
	}
	private async Task DensityCore(int density, CancellationToken ct)
	{
		if (!Ready || density is < 0 or > 100) throw new InvalidOperationException("打印机未就绪或浓度超出范围。");
		await Request(25, [(byte)density], null, ct).ConfigureAwait(false);
		var value = await Query(28, ct).ConfigureAwait(false);
		if (value.Length != 1 || value[0] != density) throw new InvalidOperationException("浓度回读未确认。");
	}
	public async Task ApplyDensityAsync(int density, CancellationToken ct)
	{
		await gate.WaitAsync(ct).ConfigureAwait(false);
		try { await DensityCore(density, ct).ConfigureAwait(false); }
		finally { gate.Release(); }
	}
	public void Dispose()
	{
		disposed = true;
		Ready = false;
		link.Dispose();
	}
}
