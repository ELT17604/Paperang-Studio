using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using Paperang.Core;
using Paperang.Windows;
using QRCoder;

namespace Paperang.WinClient;

internal static class WorkspaceFiles
{
	internal static Raster Qr(string value, int width, string correction, string format = "纯文字", string shape = "方形", bool border = false)
	{
		if (string.IsNullOrWhiteSpace(value))
			throw new InvalidDataException("请输入二维码内容。");
		if (width is < 64 or > 576 || !new[] { "L", "M", "Q", "H" }.Contains(correction) ||
		    !new[] { "纯文字", "网页链接", "电子邮件" }.Contains(format) ||
		    !new[] { "方形", "圆角", "圆点" }.Contains(shape))
			throw new InvalidDataException("二维码参数无效。");
		if (format == "网页链接" && (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var url) ||
		    url.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(url.Host)))
			throw new InvalidDataException("二维码链接需以 http:// 或 https:// 开头。");
		if (format == "电子邮件")
		{
			value = value.Trim();
			if (!value.Contains('@') || value.Any(char.IsWhiteSpace))
				throw new InvalidDataException("请输入邮件地址。");
			value = "mailto:" + Uri.EscapeDataString(value).Replace("%40", "@");
		}
		else if (format == "网页链接") value = value.Trim();
		using var code = QRCodeGenerator.GenerateQrCode(value, Enum.Parse<QRCodeGenerator.ECCLevel>(correction), eciMode: QRCodeGenerator.EciMode.Utf8);
		var matrix = code.ModuleMatrix;
		int padding = border ? 2 : 0;
		int scale = (width - padding * 2) / matrix.Count;
		if (scale < 3)
			throw new InvalidDataException("二维码太密，请增大尺寸或缩短内容。");
		int height = matrix.Count * scale + padding * 2, left = (576 - height) / 2;
		var bytes = new byte[height * 72];
		for (int row = 0; row < height; row++)
			for (int column = 0; column < height; column++)
			{
				bool frame = border && (row == 0 || column == 0 || row == height - 1 || column == height - 1);
				int localRow = row - padding, localColumn = column - padding;
				bool inside = localRow >= 0 && localColumn >= 0 && localRow < matrix.Count * scale && localColumn < matrix.Count * scale;
				bool black = inside && matrix[localRow / scale][localColumn / scale];
				if (black && shape != "方形")
				{
					int moduleRow = localRow / scale, moduleColumn = localColumn / scale;
					bool structural = (moduleRow < 12 && (moduleColumn < 12 || moduleColumn >= matrix.Count - 12)) ||
						(moduleRow >= matrix.Count - 12 && moduleColumn < 12) || moduleRow == 10 || moduleColumn == 10;
					if (!structural)
					{
						double horizontal = Math.Abs((localColumn % scale + .5) / scale - .5);
						double vertical = Math.Abs((localRow % scale + .5) / scale - .5);
						black = shape == "圆点" ? horizontal * horizontal + vertical * vertical <= .25 :
							Math.Pow(Math.Max(0, horizontal - .28), 2) + Math.Pow(Math.Max(0, vertical - .28), 2) <= .22 * .22;
					}
				}
				if (!black && !frame) continue;
				int position = left + column;
				bytes[row * 72 + position / 8] |= (byte)(128 >> (position % 8));
			}
		return new(height, bytes);
	}

	internal static Raster Open(string path, int threshold) => Open(path, new WorkspaceSettings { Threshold = threshold });

	internal static Raster Open(string path, WorkspaceSettings settings)
	{
		using var input = File.OpenRead(path);
		string extension = Path.GetExtension(path).ToLowerInvariant();
		long limit = extension == ".raw" ? 72 * 1400 : extension == ".json" ? 144_000 : 16 * 1024 * 1024;
		if (input.Length == 0 || input.Length > limit)
			throw new InvalidDataException("文件为空或超过大小上限。");
		if (extension == ".json")
		{
			using var json = JsonDocument.Parse(input);
			var raster = new RelayJob { Content = json.RootElement }.Image()
			             ?? throw new InvalidDataException("文件不是 raster1 JSON。");
			if (Convert.ToBase64String(raster.Bytes) != json.RootElement.GetProperty("data").GetString())
				throw new InvalidDataException("Base64 编码不规范。");
			return raster;
		}
		if (extension == ".raw")
		{
			var bytes = new byte[checked((int)input.Length)];
			input.ReadExactly(bytes);
			return new Raster(bytes.Length / 72, bytes).Validate();
		}
		return WorkspaceImage.Render(path, settings);
	}

	internal static void Export(string path, Raster raster)
	{
		raster.Validate();
		switch (Path.GetExtension(path).ToLowerInvariant())
		{
			case ".raw": File.WriteAllBytes(path, raster.Bytes); break;
			case ".png":
				using (var bitmap = NoteRenderer.Preview(raster)) bitmap.Save(path, ImageFormat.Png);
				break;
			case ".json":
				File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(new
				{
					type = "raster1", schemaVersion = 1, width = 576, height = raster.Height, encoding = "base64",
					bitOrder = "msb-first", blackIsOne = true, data = Convert.ToBase64String(raster.Bytes),
					sha256 = Convert.ToHexStringLower(SHA256.HashData(raster.Bytes))
				}));
				break;
			default: throw new InvalidDataException("请选择 PNG、RAW 或 raster1 JSON。");
		}
	}
}
