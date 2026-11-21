using System.Diagnostics;
using System.Text;

namespace Paperang.Core;

public interface IBleLink : IDisposable
{
	event Action<byte[]>? Data;
	event Action<byte[]>? Flow;
	bool Connected { get; }
	int MaxWrite { get; }
	Task WriteAsync(byte[] data, CancellationToken ct);
}

/// The original FF03 credit protocol is independent of Windows GATT buffering.
public sealed class P2Session : IDisposable
{
	private readonly IBleLink link;
	private readonly FrameDecoder decoder = new();
	private readonly object sync = new();
	private int credits, packetLimit = P2Defaults.MaxBleWrite;
	private long? lastWriteStart;
	private byte? ackCommand, responseCommand;
	private TaskCompletionSource<bool>? ack;
	private TaskCompletionSource<byte[]>? response;
	private ushort? media;
	private bool disposed;
	private WorkspaceSettings transfer;
	public Action<ProtocolTrace>? Trace { get; set; }
	public double Progress { get; private set; }
	public string PartitionDetail { get; private set; } = "";
	public string Status { get; private set; } = "正在握手";
	public void ConfigureTransfer(WorkspaceSettings settings) => transfer = settings.Validate();
	public bool Ready { get; private set; }
	public P2Session(IBleLink link, WorkspaceSettings? transferSettings = null)
	{
		this.link = link;
		transfer = (transferSettings ?? new()).Validate();
		link.Data += OnData;
		link.Flow += OnFlow;
	}
	private void OnFlow(byte[] bytes)
	{
		lock (sync)
		{
			if (bytes.Length >= 2 && bytes[0] == 1)
				credits = Math.Min(65535, credits + bytes[1]);
			if (bytes.Length >= 3 && bytes[0] == 2 && (bytes[1] != 0 || bytes[2] != 0))
				packetLimit = Math.Min(packetLimit, bytes[1] | bytes[2] << 8);
		}
	}
	private void OnData(byte[] data)
	{
		lock (sync)
		{
			foreach (var f in decoder.Push(data))
			{
				Trace?.Invoke(ProtocolTrace.Capture("BLE", "RX", f.Command, f.Index, f.Payload));
				if (f.Command == 13 && f.Payload.Length == 2)
					media = (ushort)(f.Payload[0] | f.Payload[1] << 8);
				if (f.Command == ackCommand && f.Payload.Length == 1)
					ack?.TrySetResult(f.Payload[0] == 0);
				if (f.Command == responseCommand)
					response?.TrySetResult(f.Payload);
			}
		}
	}
	private async Task Send(byte command, byte[] payload, CancellationToken ct, uint seed = 0, byte index = 0,
	                        bool needsAck = true, Task<byte[]>? data = null)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(transfer.TimeoutSeconds));
		var token = timeout.Token;
		TaskCompletionSource<bool> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (sync)
		{
			ackCommand = command;
			ack = waiter;
		}
		try
		{
			var frame = Wire.Encode(command, payload, seed, index);
			Trace?.Invoke(ProtocolTrace.Capture("BLE", "TX", command, index, payload));
			int offset = 0;
			while (offset < frame.Length)
			{
				token.ThrowIfCancellationRequested();
				if (!link.Connected)
					throw new InvalidOperationException("打印机连接中断");
				if (lastWriteStart is long start)
				{
					var remaining = TimeSpan.FromMilliseconds(transfer.WritePauseMS) - Stopwatch.GetElapsedTime(start);
					if (remaining > TimeSpan.Zero)
					{
						await Task.Delay(TimeSpan.FromMilliseconds(Math.Ceiling(remaining.TotalMilliseconds)), token)
						    .ConfigureAwait(false);
						continue;
					}
				}
				int count = 0;
				lock (sync)
				{
					if (command == 0 && media is ushort status && (status & 0x11) != 0)
						throw new InvalidOperationException("检测到开盖或缺纸");
					if (credits > 0 && link.MaxWrite > 0)
					{
						credits--;
						count = Math.Min(frame.Length - offset, Math.Min(transfer.BleChunk, Math.Min(packetLimit, link.MaxWrite)));
					}
				}
				if (count == 0)
				{
					await Task.Delay(5, token).ConfigureAwait(false);
					continue;
				}
				var chunk = frame.AsSpan(offset, count).ToArray();
				lastWriteStart = Stopwatch.GetTimestamp();
				await link.WriteAsync(chunk, token).ConfigureAwait(false);
				offset += count;
			}
			if (needsAck && !await waiter.Task.WaitAsync(token).ConfigureAwait(false))
				throw new InvalidOperationException($"0x{command:X2} 返回失败 ACK");
			if (data != null)
				await data.WaitAsync(token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!ct.IsCancellationRequested)
		{
			throw new InvalidOperationException($"0x{command:X2} 超时，未收到确认或流控额度");
		}
		finally
		{
			lock (sync)
			{
				ackCommand = null;
				ack = null;
			}
		}
	}
	private async Task<byte[]> Query(byte command, CancellationToken ct)
	{
		var waiter = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
		lock (sync)
		{
			responseCommand = P2Queries.Expected(command);
			response = waiter;
		}
		try
		{
			await Send(command, [1], ct, data: waiter.Task).ConfigureAwait(false);
			return await waiter.Task.ConfigureAwait(false);
		}
		finally
		{
			lock (sync)
			{
				responseCommand = null;
				response = null;
			}
		}
	}
	public async Task HandshakeAsync(CancellationToken ct)
	{
		await Send(24, [0x21, 0x95, 0x76, 0x35], ct, 0x35769521).ConfigureAwait(false);
		string model = Encoding.UTF8.GetString(await Query(6, ct).ConfigureAwait(false)).Trim('\0', '\r', '\n');
		if (model is not("Paperang_P2" or "P2"))
			throw new InvalidOperationException("设备型号未确认是 P2");
		await RefreshAsync(ct).ConfigureAwait(false);
		await Query(28, ct).ConfigureAwait(false);
		Ready = true;
	}
	public Task<byte[]> QueryAsync(byte command, CancellationToken ct)
	{
		if (!Ready) throw new InvalidOperationException("打印机尚未就绪。");
		P2Queries.Expected(command);
		return Query(command, ct);
	}
	public async Task RefreshAsync(CancellationToken ct)
	{
		byte[] battery = await Query(16, ct).ConfigureAwait(false);
		await Query(12, ct).ConfigureAwait(false);
		ushort? value;
		lock (sync) value = media;
		Status = $"P2 · 电量 {(battery.Length == 1 ? battery[0].ToString() + "%" : "未知")} · " +
		         (value == null         ? "纸仓未知"
		          : (value & 0x10) != 0 ? "盖子打开"
		          : (value & 1) != 0    ? "缺纸"
		                                : "有纸 · 盖子闭合");
	}
	private async Task VerifyMedia(CancellationToken ct)
	{
		byte[] data = await Query(12, ct).ConfigureAwait(false);
		if (data.Length != 2 || ((data[0] | data[1] << 8) & 0x11) != 0)
			throw new InvalidOperationException("请装好纸卷并合上盖子");
	}
	public async Task FeedAsync(int lines, CancellationToken ct)
	{
		if (!Ready || lines is < 1 or > 2000)
			throw new InvalidOperationException("打印机未就绪或送纸不在 1–2000 行范围内");
		await VerifyMedia(ct).ConfigureAwait(false);
		await Send(26, [(byte)lines, (byte)(lines >> 8)], ct).ConfigureAwait(false);
	}
	public async Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct)
	{
		raster.Validate();
		if (settings.Density is < 0 or > 100 || settings.Feed is < 0 or > 2000)
			throw new ArgumentException("打印设置超出范围。");
		transfer = (settings.TransferSettings ?? new()).Validate();
		int frameBytes = transfer.PacketBytes;
		int packets = (raster.Bytes.Length + frameBytes - 1) / frameBytes;
		if (packets > 256) throw new InvalidDataException("图像分包超过 256 帧，请增大载荷或拆分打印。");
		if (!Ready)
			throw new InvalidOperationException("打印机会话尚未就绪");
		Progress = 0;
		PartitionDetail = "";
		await VerifyMedia(ct).ConfigureAwait(false);
		await ApplyDensityAsync(settings.Density, ct).ConfigureAwait(false);
		await VerifyMedia(ct).ConfigureAwait(false);
		for (int i = 0; i < packets; i++)
		{
			lock (sync) if (media == null ||
			                (media.Value & 0x11) != 0) throw new InvalidOperationException("检测到开盖或缺纸");
			await Send(0, raster.Bytes.AsSpan(i * frameBytes, Math.Min(frameBytes, raster.Bytes.Length - i * frameBytes)).ToArray(), ct,
			           index: (byte)i, needsAck: false)
			    .ConfigureAwait(false);
			Progress = (i + 1.0) / packets;
			PartitionDetail = $"第1/1份 · 分包{i + 1}/{packets}";
			if (transfer.FramePauseMS > 0) await Task.Delay(transfer.FramePauseMS, ct).ConfigureAwait(false);
		}
		await Task.Delay(TimeSpan.FromSeconds(transfer.FinishWaitSeconds), ct).ConfigureAwait(false);
		if (settings.Feed > 0)
			await Send(26, [(byte)(settings.Feed & 255), (byte)(settings.Feed >> 8)], ct).ConfigureAwait(false);
		await VerifyMedia(ct).ConfigureAwait(false);
	}
	public async Task ApplyDensityAsync(int value, CancellationToken ct)
	{
		if (!Ready || value is < 0 or > 100) throw new InvalidOperationException("打印机未就绪或浓度超出范围。");
		await Send(25, [(byte)value], ct).ConfigureAwait(false);
		byte[] density = await Query(28, ct).ConfigureAwait(false);
		if (density.Length != 1 || density[0] != value)
			throw new InvalidOperationException("浓度回读未确认");
	}
	public async Task SendEngineeringAsync(byte command, byte[] payload, CancellationToken token)
	{
		P2Engineering.Validate(command, payload);
		if (!Ready) throw new InvalidOperationException("打印机尚未就绪。");
		if (P2Engineering.UsesPaper(command)) await VerifyMedia(token).ConfigureAwait(false);
		if (command == 25) await ApplyDensityAsync(payload[0], token).ConfigureAwait(false);
		else await Send(command, payload, token).ConfigureAwait(false);
	}
	public void Dispose()
	{
		disposed = true;
		Ready = false;
		link.Data -= OnData;
		link.Flow -= OnFlow;
		link.Dispose();
	}
}
