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
	private int credits, packetLimit = 20;
	private byte? ackCommand, responseCommand;
	private TaskCompletionSource<bool>? ack;
	private TaskCompletionSource<byte[]>? response;
	private ushort? media;
	private bool disposed;
	public double Progress { get; private set; }
	public string PartitionDetail { get; private set; } = "";
	public string Status { get; private set; } = "正在握手";
	public bool Ready { get; private set; }
	public P2Session(IBleLink link)
	{
		this.link = link;
		link.Data += OnData;
		link.Flow += OnFlow;
	}
	private void OnFlow(byte[] bytes)
	{
		lock (sync)
		{
			if (bytes.Length >= 2 && bytes[0] == 1)
				credits = Math.Min(65535, credits + bytes[1]);
			if (bytes.Length >= 3 && bytes[0] == 2)
				packetLimit = Math.Clamp(bytes[1] | bytes[2] << 8, 1, 180);
		}
	}
	private void OnData(byte[] data)
	{
		lock (sync)
		{
			foreach (var f in decoder.Push(data))
			{
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
	                        bool needsAck = true)
	{
		ObjectDisposedException.ThrowIf(disposed, this);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(6));
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
			int offset = 0;
			while (offset < frame.Length)
			{
				token.ThrowIfCancellationRequested();
				if (!link.Connected)
					throw new InvalidOperationException("打印机连接中断");
				int count = 0;
				lock (sync)
				{
					if (credits > 0)
					{
						credits--;
						count = Math.Min(frame.Length - offset, Math.Min(packetLimit, link.MaxWrite));
					}
				}
				if (count == 0)
				{
					await Task.Delay(5, token).ConfigureAwait(false);
					continue;
				}
				await link.WriteAsync(frame.AsSpan(offset, count).ToArray(), token).ConfigureAwait(false);
				offset += count;
				await Task.Delay(10, token).ConfigureAwait(false);
			}
			if (needsAck && !await waiter.Task.WaitAsync(token).ConfigureAwait(false))
				throw new InvalidOperationException($"0x{command:X2} 返回失败 ACK");
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
			responseCommand = (byte)(command + 1);
			response = waiter;
		}
		try
		{
			await Send(command, [1], ct).ConfigureAwait(false);
			return await waiter.Task.WaitAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			throw new InvalidOperationException($"0x{command:X2} 仅 ACK，未返回数据");
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
	public async Task PrintAsync(Raster raster, AppSettings settings, CancellationToken ct)
	{
		raster.Validate();
		if (!Ready)
			throw new InvalidOperationException("打印机会话尚未就绪");
		Progress = 0;
		PartitionDetail = "";
		await VerifyMedia(ct).ConfigureAwait(false);
		await Send(25, [(byte)settings.Density], ct).ConfigureAwait(false);
		byte[] density = await Query(28, ct).ConfigureAwait(false);
		if (density.Length != 1 || density[0] != settings.Density)
			throw new InvalidOperationException("浓度回读未确认");
		await VerifyMedia(ct).ConfigureAwait(false);
		int packets = (raster.Bytes.Length + 1007) / 1008;
		for (int i = 0; i < packets; i++)
		{
			lock (sync) if (media == null ||
			                (media.Value & 0x11) != 0) throw new InvalidOperationException("检测到开盖或缺纸");
			await Send(0, raster.Bytes.AsSpan(i * 1008, Math.Min(1008, raster.Bytes.Length - i * 1008)).ToArray(), ct,
			           index: (byte)i, needsAck: false)
			    .ConfigureAwait(false);
			Progress = (i + 1.0) / packets;
			PartitionDetail = $"第1/1份 · 分包{i + 1}/{packets}";
		}
		await Task.Delay(2000, ct).ConfigureAwait(false);
		if (settings.Feed > 0)
			await Send(26, [(byte)(settings.Feed & 255), (byte)(settings.Feed >> 8)], ct).ConfigureAwait(false);
		await VerifyMedia(ct).ConfigureAwait(false);
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
