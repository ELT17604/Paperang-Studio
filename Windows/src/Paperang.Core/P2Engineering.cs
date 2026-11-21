namespace Paperang.Core;

public static class P2Engineering
{
	public static bool UsesPaper(byte command) => command is 26 or 27 or 34;
	public static void Validate(byte command, byte[] payload)
	{
		bool valid = command switch
		{
			25 => payload.Length == 1 && payload[0] <= 100,
			26 => payload.Length == 2 && (payload[0] | payload[1] << 8) <= 2000,
			30 or 39 => payload.Length == 2,
			20 or 44 => payload.Length == 1,
			27 or 34 => payload.AsSpan().SequenceEqual(new byte[] { 0 }),
			_ => false
		};
		if (!valid) throw new ArgumentException("命令或载荷不在工程写入白名单；固件、压缩和未知指令未开放。");
	}
}
