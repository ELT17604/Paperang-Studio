package com.elt.pprstudio.adc;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;

final class PaperRenderer
{
	private static final int WIDTH = Raster.WIDTH;
	private static final int[] BAYER = {0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5};

	record Tone(int method, int threshold, int brightness, double contrast, double gamma, boolean invert)
	{
		static final Tone DEFAULT = new Tone(0, 128, 0, 1, 1, false);
	}

	record LayoutOptions(int marginX, int marginY, int width, int align, int fontSize, int spacing, String family,
	                     boolean bold, int rotation, int fit, int cropHeight)
	{
		static final LayoutOptions DEFAULT = new LayoutOptions(12, 12, 552, 1, 30, 4, "sans-serif", false, 0, 0, 400);

		int origin()
		{
			return switch (align)
			{
				case 0 -> marginX;
				case 2 -> WIDTH - marginX - width;
				default -> (WIDTH - width) / 2;
			};
		}
	}

	static StaticLayout layout(CharSequence text, int width, LayoutOptions options)
	{
		TextPaint paint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
		paint.setColor(Color.BLACK);
		paint.setTextSize(options.fontSize());
		paint.setTypeface(Typeface.create(options.family(), options.bold() ? Typeface.BOLD : Typeface.NORMAL));
		Layout.Alignment alignment = switch (options.align())
		{
			case 0 -> Layout.Alignment.ALIGN_NORMAL;
			case 2 -> Layout.Alignment.ALIGN_OPPOSITE;
			default -> Layout.Alignment.ALIGN_CENTER;
		};
		return StaticLayout.Builder.obtain(text, 0, text.length(), paint, width)
		    .setAlignment(alignment)
		    .setLineSpacing(options.spacing(), 1)
		    .build();
	}

	static Bitmap paper(int height)
	{
		if (height < 1 || height > Raster.MAX_HEIGHT)
		{
			throw new IllegalArgumentException("预览超过 1400 行，请缩小内容或使用裁切");
		}
		Bitmap image = Bitmap.createBitmap(WIDTH, height, Bitmap.Config.ARGB_8888);
		image.eraseColor(Color.WHITE);
		return image;
	}

	static byte[] text(CharSequence text, LayoutOptions options, Tone tone)
	{
		if (text.toString().isBlank() || text.length() > 10000)
			throw new IllegalArgumentException("请输入 1–10000 字符");
		StaticLayout layout = layout(text, options.width(), options);
		Bitmap image = paper(layout.getHeight() + options.marginY() * 2);
		Canvas canvas = new Canvas(image);
		canvas.translate(options.origin(), options.marginY());
		layout.draw(canvas);
		return pack(image, tone);
	}

	static byte[] image(Bitmap source, LayoutOptions options, Tone tone)
	{
		Bitmap image = source;
		if (options.rotation() != 0)
		{
			Matrix rotation = new Matrix();
			rotation.setRotate(options.rotation());
			image = Bitmap.createBitmap(source, 0, 0, source.getWidth(), source.getHeight(), rotation, true);
		}
		double widthScale = (double)options.width() / image.getWidth();
		double scale = switch (options.fit())
		{
			case 1 -> Math.min(1, widthScale);
			case 2 -> Math.max(widthScale, (double)options.cropHeight() / image.getHeight());
			default -> widthScale;
		};
		float w = (float)(scale * image.getWidth());
		float h = (float)(scale * image.getHeight());
		int visible = options.fit() == 2 ? options.cropHeight() : (int)Math.ceil(h);
		Bitmap output = paper(visible + options.marginY() * 2);
		Canvas canvas = new Canvas(output);
		float x = options.origin();
		float y = options.marginY();
		canvas.clipRect(x, y, x + options.width(), y + visible);
		x += switch (options.align())
		{
			case 0 -> 0;
			case 2 -> options.width() - w;
			default -> (options.width() - w) / 2;
		};
		y += (visible - h) / 2;
		canvas.drawBitmap(image, null, new RectF(x, y, x + w, y + h), new Paint(Paint.FILTER_BITMAP_FLAG));
		return pack(output, tone);
	}

	static byte[] pack(Bitmap image, Tone tone)
	{
		int height = image.getHeight();
		int[] colors = new int[WIDTH * height];
		image.getPixels(colors, 0, WIDTH, 0, 0, WIDTH, height);
		double[] levels = new double[colors.length];
		for (int i = 0; i < colors.length; i++)
		{
			int color = colors[i];
			double gray = 0.2126 * Color.red(color) + 0.7152 * Color.green(color) + 0.0722 * Color.blue(color);
			double adjusted =
			    Math.max(0, Math.min(255, (gray - 127.5) * tone.contrast() + 127.5 + tone.brightness() * 2.55));
			double corrected = Math.pow(adjusted / 255, 1 / tone.gamma()) * 255;
			levels[i] = tone.invert() ? 255 - corrected : corrected;
		}
		byte[] bytes = new byte[height * Raster.STRIDE];
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < WIDTH; x++)
			{
				int i = y * WIDTH + x;
				double threshold = tone.method() == 2 ? (BAYER[(y % 4) * 4 + x % 4] + .5) * 16 + tone.threshold() - 128
				                                      : tone.threshold();
				boolean black = levels[i] < threshold;
				if (black)
					bytes[i / 8] |= (byte)(0x80 >>> (x % 8));
				if (tone.method() == 1)
				{
					double error = (levels[i] - (black ? 0 : 255)) / 16;
					if (x + 1 < WIDTH)
						levels[i + 1] += error * 7;
					if (y + 1 < height)
					{
						if (x > 0)
							levels[i + WIDTH - 1] += error * 3;
						levels[i + WIDTH] += error * 5;
						if (x + 1 < WIDTH)
							levels[i + WIDTH + 1] += error;
					}
				}
			}
		}
		return bytes;
	}
}
