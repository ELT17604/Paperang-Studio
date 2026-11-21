using System.Runtime.InteropServices;
using Paperang.Core;

namespace Paperang.Windows;

public static class RichTextRenderer
{
	public static Raster Render(string rtf, int width, int height, WorkspaceSettings settings, bool label = false)
	{
		if (rtf.Length > 200_000 || width is < 24 or > 576 || height is < 24 or > 1400)
			throw new InvalidDataException("富文本内容或对象尺寸超出上限。");
		if (new[] { "\\object", "\\objdata", "\\field", "\\pict" }.Any(tag => rtf.Contains(tag, StringComparison.OrdinalIgnoreCase)))
			throw new InvalidDataException("文字对象只接受文字样式，请通过图片工具添加图像。");
		using var control = new RichTextBox { DetectUrls = false, ReadOnly = true, Rtf = rtf };
		if (control.TextLength > 10000) throw new InvalidDataException("单个文字对象最多 10000 字符。");
		using var bitmap = new Bitmap(576, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
		bitmap.SetResolution(96, 96);
		using var graphics = Graphics.FromImage(bitmap);
		graphics.Clear(Color.White);
		IntPtr context = graphics.GetHdc();
		IntPtr range = Marshal.AllocHGlobal(Marshal.SizeOf<FormatRange>());
		try
		{
			var format = new FormatRange { Device = context, Target = context,
				Region = new() { Right = width * 15, Bottom = height * 15 },
				Page = new() { Right = 576 * 15, Bottom = height * 15 }, Range = new() { Start = 0, End = -1 } };
			Marshal.StructureToPtr(format, range, false);
			int fitted = SendMessage(control.Handle, 0x439, (IntPtr)1, range).ToInt32();
			if (fitted < control.TextLength) throw new InvalidDataException("文字超出对象范围，请增加对象宽高或减小字号。");
		}
		finally
		{
			SendMessage(control.Handle, 0x439, IntPtr.Zero, IntPtr.Zero);
			Marshal.FreeHGlobal(range);
			graphics.ReleaseHdc(context);
		}
		if (label)
		{
			using var border = new System.Drawing.Drawing2D.GraphicsPath();
			border.AddArc(0, 0, 12, 12, 180, 90);
			border.AddArc(width - 13, 0, 12, 12, 270, 90);
			border.AddArc(width - 13, height - 13, 12, 12, 0, 90);
			border.AddArc(0, height - 13, 12, 12, 90, 90);
			border.CloseFigure();
			graphics.DrawPath(Pens.Black, border);
		}
		var gray = new double[576 * height];
		var bits = bitmap.LockBits(new Rectangle(0, 0, 576, height), System.Drawing.Imaging.ImageLockMode.ReadOnly,
			System.Drawing.Imaging.PixelFormat.Format24bppRgb);
		try
		{
			var row = new byte[Math.Abs(bits.Stride)];
			for (int position = 0; position < height; position++)
			{
				Marshal.Copy(bits.Scan0 + position * bits.Stride, row, 0, row.Length);
				for (int column = 0; column < 576; column++)
					gray[position * 576 + column] = .0722 * row[column * 3] + .7152 * row[column * 3 + 1] + .2126 * row[column * 3 + 2];
			}
		}
		finally { bitmap.UnlockBits(bits); }
		return settings.ConvertGray(gray, height);
	}
	[StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
	[StructLayout(LayoutKind.Sequential)] private struct Range { public int Start, End; }
	[StructLayout(LayoutKind.Sequential)] private struct FormatRange { public IntPtr Device, Target; public Rect Region, Page; public Range Range; }
	[DllImport("user32.dll", EntryPoint = "SendMessageW")]
	private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr parameter, IntPtr data);
}
