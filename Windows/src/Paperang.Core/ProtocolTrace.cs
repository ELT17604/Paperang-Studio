namespace Paperang.Core;

public sealed record ProtocolTrace(DateTimeOffset Time, string Transport, string Direction, byte Command, byte Index,
                                   int PayloadBytes, string PayloadHex)
{
	public static ProtocolTrace Capture(string transport, string direction, byte command, byte index, byte[] payload) =>
		new(DateTimeOffset.UtcNow, transport, direction, command, index, payload.Length,
			command == 0 ? "[图像内容省略]" : Convert.ToHexString(payload.AsSpan(0, Math.Min(payload.Length, 256))));
	public override string ToString() => $"{Time.LocalDateTime:HH:mm:ss.fff} · {Transport} {Direction} · 0x{Command:X2} #{Index} · {PayloadBytes} B · {PayloadHex}";
}
