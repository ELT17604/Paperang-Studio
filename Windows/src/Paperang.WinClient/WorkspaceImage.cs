using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Paperang.Core;

namespace Paperang.WinClient;

internal static class WorkspaceImage
{
	internal static Raster Render(string path, WorkspaceSettings settings)
	{
		using var source = Image.FromFile(path);
		return RenderSource(source, settings);
	}
	internal static Raster Render(byte[] encoded, WorkspaceSettings settings)
	{
		if (encoded.Length > 2 * 1024 * 1024) throw new InvalidDataException("图片超过 2 MB。");
		using var input = new MemoryStream(encoded, false);
		using var source = Image.FromStream(input);
		return RenderSource(source, settings);
	}
	private static Raster RenderSource(Image source, WorkspaceSettings settings)
	{
		using var paper = RenderPaper(source, settings);
		return settings.ConvertGray(Gray(paper), paper.Height);
	}
	internal static byte[] CaptureGray(string path, WorkspaceSettings settings)
	{
		using var source = Image.FromFile(path);
		using var paper = RenderPaper(source, settings);
		var gray = Gray(paper);
		var bits = paper.LockBits(new Rectangle(0, 0, 576, paper.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
		try
		{
			var row = new byte[Math.Abs(bits.Stride)];
			for (int y = 0; y < paper.Height; y++)
			{
				for (int x = 0; x < 576; x++) row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = (byte)Math.Clamp(Math.Round(gray[y * 576 + x]), 0, 255);
				Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, row.Length);
			}
		}
		finally { paper.UnlockBits(bits); }
		using var output = new MemoryStream();
		paper.Save(output, ImageFormat.Png);
		if (output.Length > 2 * 1024 * 1024) throw new InvalidDataException("图片对象超过 2 MB，请缩小或裁切后再添加。");
		return output.ToArray();
	}
	private static Bitmap RenderPaper(Image source, WorkspaceSettings settings)
	{
		settings.Validate();
		if ((long)source.Width * source.Height > 40_000_000 || source.Width > 40000 || source.Height > 40000)
			throw new InvalidDataException("图片超过 4000 万像素或边长超过 40000 点。");
		if (source.PropertyIdList.Contains(0x112))
		{
			int orientation = source.GetPropertyItem(0x112)?.Value?.FirstOrDefault() ?? 1;
			source.RotateFlip(orientation switch
			{
				2 => RotateFlipType.RotateNoneFlipX, 3 => RotateFlipType.Rotate180FlipNone,
				4 => RotateFlipType.Rotate180FlipX, 5 => RotateFlipType.Rotate90FlipX,
				6 => RotateFlipType.Rotate90FlipNone, 7 => RotateFlipType.Rotate270FlipX,
				8 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone
			});
		}
		source.RotateFlip(settings.Rotation switch
		{
			90 => RotateFlipType.Rotate90FlipNone, 180 => RotateFlipType.Rotate180FlipNone,
			270 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone
		});
		double scale = (double)settings.ContentWidth / source.Width;
		if (settings.ImageFit == "保持像素") scale = Math.Min(1, scale);
		if (settings.ImageFit == "裁切填满") scale = Math.Max(scale, (double)settings.CropHeight / source.Height);
		int visible = settings.ImageFit == "裁切填满" ? settings.CropHeight : (int)Math.Ceiling(source.Height * scale);
		int height = visible + settings.MarginY * 2;
		if (height is < 1 or > 1400)
			throw new InvalidDataException("输出超过 1400 行，请缩小内容宽度或选择裁切填满。");
		var paper = new Bitmap(576, height, PixelFormat.Format24bppRgb);
		try
		{
			using var graphics = Graphics.FromImage(paper);
			graphics.Clear(Color.White);
			graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
			double left = settings.Alignment == "居左" ? settings.MarginX :
				settings.Alignment == "居右" ? 576 - settings.MarginX - settings.ContentWidth : (576 - settings.ContentWidth) / 2.0;
			double width = source.Width * scale, drawnHeight = source.Height * scale;
			double offset = settings.Alignment == "居左" ? 0 :
				settings.Alignment == "居右" ? settings.ContentWidth - width : (settings.ContentWidth - width) / 2;
			graphics.SetClip(new RectangleF((float)left, settings.MarginY, settings.ContentWidth, visible));
			graphics.DrawImage(source, new RectangleF((float)(left + offset),
				(float)(settings.MarginY + (visible - drawnHeight) / 2), (float)width, (float)drawnHeight));
			return paper;
		}
		catch { paper.Dispose(); throw; }
	}

	internal static double[] Gray(Bitmap bitmap)
	{
		var gray = new double[576 * bitmap.Height];
		var bits = bitmap.LockBits(new Rectangle(0, 0, 576, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
		try
		{
			var row = new byte[Math.Abs(bits.Stride)];
			for (int position = 0; position < bitmap.Height; position++)
			{
				Marshal.Copy(bits.Scan0 + position * bits.Stride, row, 0, row.Length);
				for (int column = 0; column < 576; column++)
					gray[position * 576 + column] = row[column * 3] * .0722 + row[column * 3 + 1] * .7152 + row[column * 3 + 2] * .2126;
			}
		}
		finally { bitmap.UnlockBits(bits); }
		return gray;
	}
}
