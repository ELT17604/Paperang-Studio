namespace Paperang.Core;

public sealed record WorkspaceSettings
{
	public int Version { get; init; } = 1;
	public string FontName { get; init; } = "Microsoft YaHei";
	public double FontSize { get; init; } = 30;
	public bool Bold { get; init; }
	public double LineSpacing { get; init; } = 4;
	public string Alignment { get; init; } = "居中";
	public int MarginX { get; init; }
	public int MarginY { get; init; }
	public int ContentWidth { get; init; } = 576;
	public string ImageFit { get; init; } = "适配宽度";
	public int CropHeight { get; init; } = 400;
	public int Rotation { get; init; }
	public string Conversion { get; init; } = "阈值";
	public double Threshold { get; init; } = 128;
	public double Brightness { get; init; }
	public double Contrast { get; init; } = 1;
	public double Gamma { get; init; } = 1;
	public bool Invert { get; init; }
	public int Density { get; init; } = 100;
	public int Copies { get; init; } = 1;
	public int FeedBefore { get; init; }
	public int FeedAfter { get; init; } = P2Defaults.FinalFeed;
	public bool FeedBetweenCopies { get; init; }
	public int PacketBytes { get; init; } = 1008;
	public int BleChunk { get; init; } = 180;
	public int WritePauseMS { get; init; } = 10;
	public int FramePauseMS { get; init; }
	public int TimeoutSeconds { get; init; } = 6;
	public int FinishWaitSeconds { get; init; } = 2;

	public WorkspaceSettings Validate()
	{
		static bool Range(double value, double minimum, double maximum) =>
			double.IsFinite(value) && value >= minimum && value <= maximum;
		if (Version != 1 || string.IsNullOrWhiteSpace(FontName) || FontName.Length > 120 ||
		    !Range(FontSize, 8, 96) || !Range(LineSpacing, 0, 40) ||
		    MarginX is < 0 or > 120 || MarginY is < 0 or > 200 ||
		    ContentWidth is < 48 or > 576 || ContentWidth + MarginX * 2 > 576 ||
		    !new[] { "居左", "居中", "居右" }.Contains(Alignment) ||
		    !new[] { "适配宽度", "保持像素", "裁切填满" }.Contains(ImageFit) ||
		    CropHeight is < 24 or > 1400 || Rotation is not (0 or 90 or 180 or 270) ||
		    !new[] { "阈值", "Floyd–Steinberg", "有序抖动" }.Contains(Conversion) ||
		    !Range(Threshold, 0, 255) || !Range(Brightness, -100, 100) ||
		    !Range(Contrast, .2, 3) || !Range(Gamma, .2, 3) ||
		    Density is < 0 or > 100 || Copies is < 1 or > 20 ||
		    FeedBefore is < 0 or > 2000 || FeedAfter is < 0 or > 2000 ||
		    PacketBytes is < 72 or > 1008 || PacketBytes % 72 != 0 ||
		    BleChunk is < 20 or > 180 || WritePauseMS is < 5 or > 200 ||
		    FramePauseMS is < 0 or > 1000 || TimeoutSeconds is < 3 or > 30 || FinishWaitSeconds is < 1 or > 15)
			throw new InvalidDataException("预设包含无效参数；内容宽度与两侧边距合计不能超过 576 点。");
		return this;
	}

	public static WorkspaceSettings Preset(string name) => name switch
	{
		"照片" => new() { Conversion = "Floyd–Steinberg", Contrast = 1.1 },
		"标签" => new() { FontSize = 42, Bold = true },
		"线稿" => new() { Threshold = 160 },
		_ => new()
	};

	public Raster ConvertGray(double[] gray, int height)
	{
		Validate();
		if (height is < 1 or > 1400 || gray.Length != height * 576)
			throw new InvalidDataException("灰度画布必须为 576 点宽、1–1400 行。");
		var values = new double[gray.Length];
		for (int index = 0; index < values.Length; index++)
		{
			double adjusted = Math.Clamp((gray[index] - 127.5) * Contrast + 127.5 + Brightness * 2.55, 0, 255);
			double corrected = Math.Pow(adjusted / 255, 1 / Gamma) * 255;
			values[index] = Invert ? 255 - corrected : corrected;
		}
		int[] bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
		var raster = new Raster(height, new byte[height * 72]);
		for (int row = 0; row < height; row++)
			for (int column = 0; column < 576; column++)
			{
				int index = row * 576 + column;
				double cutoff = Conversion == "有序抖动" ? (bayer[row % 4 * 4 + column % 4] + .5) * 16 + Threshold - 128 : Threshold;
				bool black = values[index] < cutoff;
				if (black) raster.Bytes[row * 72 + column / 8] |= (byte)(128 >> (column % 8));
				if (Conversion != "Floyd–Steinberg") continue;
				double error = values[index] - (black ? 0 : 255);
				if (column < 575) values[index + 1] += error * 7 / 16;
				if (row + 1 >= height) continue;
				if (column > 0) values[index + 575] += error * 3 / 16;
				values[index + 576] += error * 5 / 16;
				if (column < 575) values[index + 577] += error / 16;
			}
		return raster;
	}
}
