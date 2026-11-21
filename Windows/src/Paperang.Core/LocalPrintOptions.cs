namespace Paperang.Core;

public sealed record LocalPrintOptions(int Density = 100, int Copies = 1, int Before = 0,
                                       int After = P2Defaults.FinalFeed, bool BetweenCopies = false, WorkspaceSettings? TransferSettings = null)
{
	public async Task SendAsync(IPrinter printer, Raster raster, AppSettings connection, CancellationToken token)
	{
		if (Density is < 0 or > 100 || Copies is < 1 or > 20 || Before is < 0 or > 2000 || After is < 0 or > 2000)
			throw new ArgumentException("浓度、份数或送纸设置超出范围");
		raster.Validate();
		var transfer = (TransferSettings ?? connection.TransferSettings ?? new()).Validate();
		int frameBytes = transfer.PacketBytes;
		if ((raster.Bytes.Length + frameBytes - 1) / frameBytes > 256)
			throw new InvalidDataException("当前分包大小会超过 256 个协议帧，请增大图像载荷或拆分任务。");
		printer.ConfigureTransfer(transfer);
		if (Before > 0)
		{
			await printer.ApplyDensityAsync(Density, token).ConfigureAwait(false);
			await printer.FeedAsync(Before, token).ConfigureAwait(false);
		}
		for (int copy = 1; copy <= Copies; copy++)
		{
			token.ThrowIfCancellationRequested();
			int feed = copy == Copies || BetweenCopies ? After : 0;
			await printer.PrintAsync(raster, connection with { Density = Density, Feed = feed, TransferSettings = TransferSettings ?? connection.TransferSettings }, token).ConfigureAwait(false);
		}
	}
}
