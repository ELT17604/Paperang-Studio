using Paperang.Core;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Paperang.WinLite;

internal sealed class NoteRenderer : IRasterizer
{
	public sealed record RenderedBlock(string Id, Raster Raster, int Gap);
	public List<RenderedBlock> Parts(RelayJob job, RelayTemplate template)
	{
		template.Validate();
		job.Validate();
		var image = job.Image();
		var parts = new List<RenderedBlock>();
		foreach (var b in template.Blocks.Where(x => x.Enabled))
		{
			string text = b.Value(job);
			bool hasText = !string.IsNullOrWhiteSpace(text);
			if (!hasText && !(b.Kind == "正文" && image != null))
				continue;
			Raster TextImage() => RenderText(text, b.Font, (float)b.Size, b.Bold, b.Alignment, b.Margin, 0, 3);
			Raster raster;
			if (b.Kind == "正文" && image != null)
				raster =
				    hasText ? Raster.Stack([(image.Fitting(b.Margin), 0), (TextImage(), 4)]) : image.Fitting(b.Margin);
			else
				raster = TextImage();
			parts.Add(new(b.Id, raster, b.Gap));
		}
		return parts;
	}
	public Raster Render(RelayJob job, AppSettings s)
	{
		job.Validate();
		if (job.Truncated)
			throw new InvalidDataException("禁止打印摘要");
		if (s.Style == "自定义")
		{
			var template = s.Templates.FirstOrDefault(x => x.Id == s.TemplateId) ?? s.Templates[0];
			return Raster.Stack(Parts(job, template).Select(x => (x.Raster, x.Gap)));
		}
		var image = job.Image();
		var details = new List<string>();
		if (s.IncludeSender)
			details.Add("From: " + job.Sender);
		if (s.IncludeTime)
			details.Add(DateTimeOffset.UnixEpoch.AddSeconds(job.Created).ToString("yyyy-MM-ddTHH:mm:ssZ"));
		if (s.IncludeId)
			details.Add("#" + PrettyId(job.ShortID.Length == 0 ? job.Id.Replace("-", "") [..12].ToUpperInvariant()
			                                                   : job.ShortID));
		string footer = string.Join("\n", details);
		string text =
		    s.Style switch { "小票" => "PAPERANG INBOX\n----------------------------\n" + job.Text +
			                           "\n----------------------------\n" + footer,
			                 "复古电传" => "[ INCOMING TRANSMISSION ]\n" + footer + "\n\n" + job.Text + "\n[ END ]",
			                 "访客纪念" => "A NOTE TO KEEP\n\n" + job.Text + "\n\n" + footer + "\n— Paperang Studio —",
			                 _ => string.Join("\n\n", new[] { job.Text, footer }.Where(x => x.Length > 0)) };
		if (string.IsNullOrWhiteSpace(text))
			return image ?? throw new InvalidDataException("没有可打印内容");
		Raster rendered = TextRaster(text, s.Style);
		if (image == null)
			return rendered;
		int height = image.Height + 8 + rendered.Height;
		var result = new Raster(height, new byte[height * 72]).Validate();
		image.Bytes.CopyTo(result.Bytes, 0);
		rendered.Bytes.CopyTo(result.Bytes, (image.Height + 8) * 72);
		return result;
	}
	private static string PrettyId(string id) => id.Length == 12 ? $"{id[..4]}-{id[4..8]}-{id[8..]}" : id;
	private static Raster TextRaster(string text, string style)
	{
		float size = style switch { "小票" => 19, "复古电传" => 15, "访客纪念" => 27,
		                            _ => 24 };
		int spacing = style switch { "小票" => 3, "复古电传" => 2, "访客纪念" => 7,
		                             _ => 4 };
		return RenderText(text, style is "小票" or "复古电传" ? "Consolas" : "Microsoft YaHei", size, false,
		                  style == "访客纪念" ? "居中" : "居左", 12, 12, spacing);
	}
	private static Raster RenderText(string text, string fontName, float size, bool bold, string alignment, int margin,
	                                 int marginY, int spacing)
	{
		fontName = fontName switch { "PingFang SC" => "Microsoft YaHei", "Menlo" => "Consolas", "Helvetica" => "Arial",
		                             _ => fontName };
		using var font = new Font(fontName, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
		int contentWidth = 576 - 2 * margin;
		using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
		format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
		format.Trimming = StringTrimming.None;
		using var canvas = new Bitmap(576, 1400, PixelFormat.Format24bppRgb);
		using var g = Graphics.FromImage(canvas);
		g.Clear(Color.White);
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		float lineHeight = font.GetHeight(g) + spacing;
		var lines = new List<string>();
		foreach (string paragraph in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
		{
			string current = "";
			// Enumerate grapheme clusters so emoji/combining marks and surrogate pairs stay intact.
			var parts = System.Globalization.StringInfo.GetTextElementEnumerator(paragraph);
			while (parts.MoveNext())
			{
				string element = parts.GetTextElement();
				if (current.Length > 0 &&
				    g.MeasureString(current + element, font, PointF.Empty, format).Width > contentWidth)
				{
					lines.Add(current);
					current = "";
				}
				current += element;
				if ((lines.Count + 1) * lineHeight + marginY * 2 + 4 > 1400)
					throw new InvalidDataException("文字超过 1400 行高限制，请缩短纸条");
			}
			lines.Add(current);
			if (lines.Count * lineHeight + marginY * 2 + 4 > 1400)
				throw new InvalidDataException("文字超过 1400 行高限制，请缩短纸条");
		}
		int height = (int)Math.Ceiling(lines.Count * lineHeight) + marginY * 2 + 4;
		for (int i = 0; i < lines.Count; i++)
		{
			float width = g.MeasureString(lines[i], font, PointF.Empty, format).Width;
			float x = alignment == "居中" ? (576 - width) / 2 : alignment == "居右" ? 576 - margin - width : margin;
			g.DrawString(lines[i], font, Brushes.Black, new PointF(x, marginY + i * lineHeight), format);
		}
		var raster = new Raster(height, new byte[72 * height]).Validate();
		var bits =
		    canvas.LockBits(new Rectangle(0, 0, 576, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
		try
		{
			var row = new byte[bits.Stride];
			for (int y = 0; y < height; y++)
			{
				Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, row.Length);
				for (int x = 0; x < 576; x++)
					if (row[x * 3] * 0.0722 + row[x * 3 + 1] * 0.7152 + row[x * 3 + 2] * 0.2126 < 128)
						raster.Bytes[y * 72 + x / 8] |= (byte)(1 << (7 - x % 8));
			}
		}
		finally
		{
			canvas.UnlockBits(bits);
		}
		return raster;
	}
	public static Bitmap Preview(Raster raster)
	{
		raster.Validate();
		var bitmap = new Bitmap(576, raster.Height, PixelFormat.Format24bppRgb);
		var bits = bitmap.LockBits(new Rectangle(0, 0, 576, raster.Height), ImageLockMode.WriteOnly,
		                           PixelFormat.Format24bppRgb);
		try
		{
			var row = new byte[bits.Stride];
			for (int y = 0; y < raster.Height; y++)
			{
				for (int x = 0; x < 576; x++)
				{
					byte gray = (raster.Bytes[y * 72 + x / 8] & (1 << (7 - x % 8))) == 0 ? (byte)255 : (byte)0;
					row[x * 3] = row[x * 3 + 1] = row[x * 3 + 2] = gray;
				}
				Marshal.Copy(row, 0, bits.Scan0 + y * bits.Stride, row.Length);
			}
		}
		finally
		{
			bitmap.UnlockBits(bits);
		}
		return bitmap;
	}
}
