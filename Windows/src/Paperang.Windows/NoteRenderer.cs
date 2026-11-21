using Paperang.Core;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Paperang.Windows;

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
		if (s.IncludeIp) details.Add(new RelayBlock { Kind = "IP" }.Value(job));
		if (s.IncludeUa) details.Add(new RelayBlock { Kind = "UA" }.Value(job));
		if (s.IncludeDevice) details.Add("Browser supplied: " + new RelayBlock { Kind = "浏览器信息" }.Value(job));
		if (s.IncludeId)
			details.Add("#" + PrettyId(job.ShortID.Length == 0 ? job.Id.Replace("-", "") [..12].ToUpperInvariant()
			                                                   : job.ShortID));
		string footer = string.Join("\n", details);
		string message = s.IncludeText ? job.Text : "";
		string text =
		    s.Style switch { "小票" => "PAPERANG INBOX\n----------------------------\n" + message +
			                           "\n----------------------------\n" + footer,
			                 "复古电传" => "[ INCOMING TRANSMISSION ]\n" + footer + "\n\n" + message + "\n[ END ]",
			                 "访客纪念" => "A NOTE TO KEEP\n\n" + message + "\n\n" + footer + "\n— Paperang Studio —",
			                 _ => string.Join("\n\n", new[] { message, footer }.Where(x => x.Length > 0)) };
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
		                  style == "访客纪念" ? "居中" : "居左", 0, 0, spacing);
	}
	internal static Raster RenderText(string text, string fontName, float size, bool bold, string alignment, int margin,
	                                  int marginY, float spacing, WorkspaceSettings? tone = null, int? widthOverride = null, bool localBox = false)
	{
		fontName = fontName switch { "PingFang SC" => "Microsoft YaHei", "Menlo" => "Consolas", "Helvetica" => "Arial",
		                             _ => fontName };
		using var font = new Font(fontName, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
		int contentWidth = widthOverride ?? 576 - 2 * margin;
		using var format = (StringFormat)StringFormat.GenericTypographic.Clone();
		format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
		format.Trimming = StringTrimming.None;
		using var canvas = new Bitmap(576, 1400, PixelFormat.Format24bppRgb);
		using var g = Graphics.FromImage(canvas);
		g.Clear(Color.White);
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		float fontHeight = font.GetHeight(g), lineHeight = fontHeight + spacing;
		int HeightFor(int count) => (int)Math.Ceiling((count - 1) * lineHeight + fontHeight) + marginY * 2;
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
				if (HeightFor(lines.Count + 1) > 1400)
					throw new InvalidDataException("文字超过 1400 行高限制，请缩短纸条");
			}
			lines.Add(current);
			if (HeightFor(lines.Count) > 1400)
				throw new InvalidDataException("文字超过 1400 行高限制，请缩短纸条");
		}
		int height = HeightFor(lines.Count);
		for (int i = 0; i < lines.Count; i++)
		{
			float width = g.MeasureString(lines[i], font, PointF.Empty, format).Width;
			float canvasWidth = localBox ? contentWidth : 576;
			float x = alignment == "居中" ? (canvasWidth - width) / 2 : alignment == "居右" ? canvasWidth - margin - width : margin;
			g.DrawString(lines[i], font, Brushes.Black, new PointF(x, marginY + i * lineHeight), format);
		}
		var raster = new Raster(height, new byte[72 * height]).Validate();
		var gray = tone == null ? null : new double[height * 576];
		var bits =
		    canvas.LockBits(new Rectangle(0, 0, 576, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
		try
		{
			var row = new byte[bits.Stride];
			for (int y = 0; y < height; y++)
			{
				Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, row.Length);
				for (int x = 0; x < 576; x++)
				{
					double brightness = row[x * 3] * 0.0722 + row[x * 3 + 1] * 0.7152 + row[x * 3 + 2] * 0.2126;
					if (gray != null) gray[y * 576 + x] = brightness;
					if (brightness < 128)
						raster.Bytes[y * 72 + x / 8] |= (byte)(1 << (7 - x % 8));
				}
			}
		}
		finally
		{
			canvas.UnlockBits(bits);
		}
		return tone == null ? raster : tone.ConvertGray(gray!, height);
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
