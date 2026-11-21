namespace Paperang.Core;

public static class P2Queries
{
	public static readonly IReadOnlyDictionary<byte, byte> Responses = new Dictionary<byte, byte>
	{
		[4] = 5, [6] = 7, [8] = 11, [10] = 11, [12] = 13, [14] = 15, [16] = 17,
		[18] = 19, [21] = 22, [28] = 29, [31] = 32, [35] = 36, [37] = 38,
		[40] = 41, [42] = 43, [45] = 46, [48] = 49
	};

	public static byte Expected(byte command) => Responses.TryGetValue(command, out byte expected) ? expected
		: throw new ArgumentException("只允许协议目录中的已知 GET 查询命令。");
}
