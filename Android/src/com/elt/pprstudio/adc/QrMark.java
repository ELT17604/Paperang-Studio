package com.elt.pprstudio.adc;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import com.google.zxing.EncodeHintType;
import com.google.zxing.WriterException;
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel;
import com.google.zxing.qrcode.encoder.Encoder;
import java.net.URI;
import java.net.URISyntaxException;
import java.util.Map;

record QrMark(int format, ErrorCorrectionLevel correction, int shape, boolean border)
{
	static final QrMark DEFAULT = new QrMark(0, ErrorCorrectionLevel.M, 0, false);

	private String payload(String value)
	{
		if (value.isBlank() || value.length() > 3000)
			throw new IllegalArgumentException("请输入有效的二维码内容");
		try
		{
			if (format == 1)
			{
				URI url = new URI(value.strip());
				if (url.getHost() == null ||
				    !("http".equalsIgnoreCase(url.getScheme()) || "https".equalsIgnoreCase(url.getScheme())))
					throw new URISyntaxException(value, "HTTP(S)");
				return url.toASCIIString();
			}
			if (format == 2)
			{
				String address = value.strip();
				if (!address.matches("[^@\\s?]+@[^@\\s?]+"))
					throw new URISyntaxException(value, "email");
				return new URI("mailto", address, null).toASCIIString();
			}
			return value;
		}
		catch (URISyntaxException error)
		{
			throw new IllegalArgumentException(format == 1 ? "请输入有效的 HTTP(S) 链接" : "请输入有效的邮件地址");
		}
	}

	private com.google.zxing.qrcode.encoder.ByteMatrix encode(String text)
	{
		try
		{
			return Encoder.encode(payload(text), correction, Map.of(EncodeHintType.CHARACTER_SET, "UTF-8")).getMatrix();
		}
		catch (WriterException error)
		{
			throw new IllegalArgumentException("二维码内容过长或无法编码", error);
		}
	}

	private int moduleSize(int count, float side)
	{
		int size = (int)((side - (border ? 4 : 0)) / (count + 8));
		if (size < 3)
			throw new IllegalArgumentException("二维码太密，请放大图层或缩短内容");
		return size;
	}

	void validate(String text, float side)
	{
		moduleSize(encode(text).getWidth(), side);
	}

	void draw(Canvas canvas, RectF box, String text)
	{
		var matrix = encode(text);
		int count = matrix.getWidth();
		int step = moduleSize(count, Math.min(box.width(), box.height()));
		float x = (float)Math.floor(box.centerX() - count * step / 2f);
		float y = (float)Math.floor(box.centerY() - count * step / 2f);
		Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
		paint.setColor(Color.WHITE);
		canvas.drawRect(box, paint);
		paint.setColor(Color.BLACK);
		for (int row = 0; row < count; row++)
		{
			for (int col = 0; col < count; col++)
			{
				if (matrix.get(col, row) != 1)
					continue;
				float left = x + col * step;
				float top = y + row * step;
				boolean function = row == 6 || col == 6 || (col < 9 && row < 9) || (col < 9 && row >= count - 9) ||
				                   (col >= count - 9 && row < 9);
				if (function || shape == 0)
					canvas.drawRect(left, top, left + step, top + step, paint);
				else if (shape == 1)
					canvas.drawRoundRect(left, top, left + step, top + step, step * .22f, step * .22f, paint);
				else
					canvas.drawOval(left, top, left + step, top + step, paint);
			}
		}
		if (border)
		{
			paint.setStyle(Paint.Style.STROKE);
			canvas.drawRect(box.left + 1, box.top + 1, box.right - 1, box.bottom - 1, paint);
		}
	}
}
