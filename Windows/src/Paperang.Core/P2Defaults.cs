namespace Paperang.Core;

public static class P2Defaults
{
	public const int FinalFeed = 160;
	public const int FrameBytes = 1008;
	public const int MaxBleWrite = 180;
	public static readonly TimeSpan BleWriteInterval = TimeSpan.FromMilliseconds(10);
	public static readonly TimeSpan BleCommandTimeout = TimeSpan.FromSeconds(6);
	public static readonly TimeSpan FinishWait = TimeSpan.FromSeconds(2);
}
