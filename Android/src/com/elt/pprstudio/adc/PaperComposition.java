package com.elt.pprstudio.adc;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.text.SpannableString;
import java.util.ArrayList;
import java.util.List;

final class PaperComposition
{
	static final int TEXT = 0, LABEL = 1, PHOTO = 2, QR = 3;

	static final class Layer
	{
		final int kind;
		final RectF bounds = new RectF();
		CharSequence text = "";
		Bitmap image;
		PaperRenderer.LayoutOptions style = PaperRenderer.LayoutOptions.DEFAULT;
		QrMark qr = QrMark.DEFAULT;

		Layer(int kind, float top, Bitmap image)
		{
			this.kind = kind;
			this.image = image;
			bounds.set(24, top, kind == QR ? 244 : 304, top + (kind == QR ? 220 : kind == PHOTO ? 180 : 80));
			text = kind == LABEL ? "标签" : kind == QR ? "Paperang Studio" : "输入文字";
		}

		Layer snapshot()
		{
			Layer copy = new Layer(kind, 0, image);
			copy.bounds.set(bounds);
			copy.text = new SpannableString(text);
			copy.style = style;
			copy.qr = qr;
			return copy;
		}
	}

	int height = Raster.MAX_HEIGHT;
	boolean autoLength = true;
	final List<Layer> layers = new ArrayList<>();

	PaperComposition snapshot()
	{
		PaperComposition copy = new PaperComposition();
		copy.height = height;
		copy.autoLength = autoLength;
		for (Layer layer : layers)
			copy.layers.add(layer.snapshot());
		return copy;
	}

	byte[] raster(PaperRenderer.Tone tone)
	{
		if (layers.isEmpty())
			throw new IllegalArgumentException("请至少插入一个对象");
		Bitmap bitmap = PaperRenderer.paper(height);
		draw(new Canvas(bitmap), true);
		byte[] bytes = PaperRenderer.pack(bitmap, tone);
		return autoLength ? Raster.trimBottom(bytes) : bytes;
	}

	void draw(Canvas canvas, boolean strict)
	{
		Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
		canvas.drawColor(Color.WHITE);
		for (Layer layer : layers)
		{
			RectF box = layer.bounds;
			canvas.save();
			canvas.clipRect(box);
			try
			{
				switch (layer.kind)
				{
				case TEXT, LABEL ->
				{
					if (layer.kind == LABEL)
					{
						paint.setColor(Color.WHITE);
						canvas.drawRoundRect(box, 8, 8, paint);
						paint.setColor(Color.BLACK);
						paint.setStyle(Paint.Style.STROKE);
						paint.setStrokeWidth(2);
						canvas.drawRoundRect(box.left + 1, box.top + 1, box.right - 1, box.bottom - 1, 8, 8, paint);
						paint.setStyle(Paint.Style.FILL);
					}
					int pad = layer.kind == LABEL ? 8 : 2;
					canvas.translate(box.left + pad, box.top + pad);
					PaperRenderer.layout(layer.text, Math.max(1, (int)box.width() - 2 * pad), layer.style).draw(canvas);
				}
				case PHOTO ->
				{
					Bitmap photo = layer.image;
					if (photo == null)
						break;
					float scale = Math.min(box.width() / photo.getWidth(), box.height() / photo.getHeight());
					float w = photo.getWidth() * scale / 2;
					float h = photo.getHeight() * scale / 2;
					canvas.drawBitmap(
					    photo, null,
					    new RectF(box.centerX() - w, box.centerY() - h, box.centerX() + w, box.centerY() + h), paint);
				}
				case QR -> layer.qr.draw(canvas, box, layer.text.toString());
				default -> throw new IllegalArgumentException("未知图层类型");
				}
			}
			catch (IllegalArgumentException error)
			{
				if (strict)
					throw error;
				paint.setColor(Color.RED);
				paint.setTextSize(16);
				canvas.drawText(error.getMessage(), box.left + 4, box.top + 20, paint);
			}
			finally
			{
				canvas.restore();
			}
		}
	}
}
