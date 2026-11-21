namespace Paperang.Core;

public sealed record PaperLayer(Raster Image, int X, int Y, int Width, int Height,
	int SourceWidth = 576, System.Text.Json.JsonElement? Source = null);

public static class PaperLayout
{
	public static Raster Render(int height, IEnumerable<PaperLayer> layers)
	{
		if (height is < 1 or > 1400)
			throw new ArgumentOutOfRangeException(nameof(height));
		var output = new Raster(height, new byte[height * 72]);
		foreach (var layer in layers)
		{
			layer.Image.Validate();
			if (layer.X < 0 || layer.Y < 0 || layer.Width is < 1 or > 576 || layer.Height is < 1 or > 1400 ||
			    layer.SourceWidth is < 1 or > 576 ||
			    layer.X > 576 - layer.Width || layer.Y > height - layer.Height)
				throw new InvalidDataException("图层超出纸面，请调整位置、尺寸或纸张高度。");
			for (int row = 0; row < layer.Height; row++)
			{
				int sourceRow = row * layer.Image.Height / layer.Height;
				for (int column = 0; column < layer.Width; column++)
				{
					int sourceColumn = column * layer.SourceWidth / layer.Width, targetColumn = layer.X + column;
					int target = (layer.Y + row) * 72 + targetColumn / 8;
					byte mask = (byte)(128 >> (targetColumn % 8));
					if ((layer.Image.Bytes[sourceRow * 72 + sourceColumn / 8] & (128 >> (sourceColumn % 8))) != 0)
						output.Bytes[target] |= mask;
					else
						output.Bytes[target] &= (byte)~mask;
				}
			}
		}
		return output;
	}
}
