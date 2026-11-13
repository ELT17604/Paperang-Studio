package com.elt.pprstudio.adc;

import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;

final class PaperRuler
{
	static final float DOTS_PER_MM = 300f / 25.4f;

	private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
	private final MainActivity app;

	PaperRuler(MainActivity app)
	{
		this.app = app;
		paint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
		paint.setTextSize(app.dp(10));
	}

	void draw(Canvas canvas, float x, float top, float scale, int rows, int height, int printedRows)
	{
		draw(canvas, x, top, scale, rows, height, printedRows, app.getColor(android.R.color.system_neutral2_600));
	}

	void draw(Canvas canvas, float x, float top, float scale, int rows, int height, int printedRows, int color)
	{
		if (scale <= 0 || rows <= 0)
			return;
		float bottom = top + rows * scale;
		paint.setColor(color);
		canvas.drawLine(x, Math.max(0, top), x, Math.min(height, bottom), paint);
		int step = DOTS_PER_MM * scale >= app.dp(6) ? 1 : DOTS_PER_MM * scale >= app.dp(2) ? 5 : 10;
		int labelStep = Math.max(step, DOTS_PER_MM * scale * 10 >= app.dp(16) ? 10 : 20);
		for (int mm = 0; mm * DOTS_PER_MM <= rows; mm += step)
		{
			float y = top + mm * DOTS_PER_MM * scale;
			if (y < 0 || y > height)
				continue;
			boolean label = mm % labelStep == 0;
			canvas.drawLine(x - app.dp(label ? 7 : 4), y, x, y, paint);
			if (label)
				canvas.drawText(Integer.toString(mm), x + app.dp(3), Math.max(app.dp(10), y + app.dp(4)), paint);
		}
		if (printedRows > 0)
		{
			float y = top + printedRows * scale;
			if (y >= 0 && y <= height)
			{
				paint.setColor(app.getColor(android.R.color.system_accent1_700));
				paint.setStrokeWidth(app.dp(2));
				canvas.drawLine(x - app.dp(10), y, x + app.dp(5), y, paint);
				paint.setStrokeWidth(0);
			}
		}
	}
	void drawWidth(Canvas canvas, float left, float scale, float width, float baseline, int color)
	{
		if (scale <= 0)
			return;
		paint.setColor(color);
		canvas.drawLine(Math.max(0, left), baseline, Math.min(width, left + Raster.WIDTH * scale), baseline, paint);
		float mmPixels = DOTS_PER_MM * scale;
		int step = mmPixels >= app.dp(6) ? 1 : mmPixels >= app.dp(2) ? 5 : 10;
		int labelStep = mmPixels * 10 >= app.dp(22) ? 10 : 20;
		for (int mm = 0; mm * DOTS_PER_MM <= Raster.WIDTH; mm += step)
		{
			float x = left + mm * mmPixels;
			if (x < 0 || x > width)
				continue;
			boolean label = mm % labelStep == 0;
			canvas.drawLine(x, baseline - app.dp(label ? 7 : 4), x, baseline, paint);
			if (label)
			{
				String number = Integer.toString(mm);
				float textX =
				    PaperPlacement.clamp(x - paint.measureText(number) / 2, 0, width - paint.measureText(number));
				canvas.drawText(number, textX, baseline - app.dp(9), paint);
			}
		}
	}
}
