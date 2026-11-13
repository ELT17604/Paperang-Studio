package com.elt.pprstudio.adc;

import android.graphics.Canvas;
import android.graphics.ColorFilter;
import android.graphics.Paint;
import android.graphics.PixelFormat;
import android.graphics.RectF;
import android.graphics.drawable.Drawable;

final class PaperSurface extends Drawable {
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final int paper;
    private final int edge;
    private final float unit;

    PaperSurface(int paper, int edge, float unit) {
        this.paper = paper;
        this.edge = edge;
        this.unit = unit;
    }

    @Override public void draw(Canvas canvas) {
        var bounds = getBounds();
        float end = bounds.bottom - 5 * unit;
        paint.setColor(edge);
        canvas.drawRoundRect(new RectF(bounds.left + unit, bounds.top + unit,
                bounds.right - unit, end + 4 * unit), 8 * unit, 8 * unit, paint);
        paint.setColor(paper);
        canvas.drawRoundRect(new RectF(bounds.left, bounds.top, bounds.right, end), 8 * unit, 8 * unit, paint);
        for (float x = bounds.left + 8 * unit; x < bounds.right - 6 * unit; x += 9 * unit) {
            canvas.drawCircle(x, end - unit, 1.25f * unit, paint);
        }
        paint.setColor(edge);
        paint.setStrokeWidth(unit);
        for (float x = bounds.left + 8 * unit; x < bounds.right - 6 * unit; x += 9 * unit) {
            canvas.drawLine(x, end - 5 * unit, x + 4 * unit, end - 5 * unit, paint);
        }
    }

    @Override public void setAlpha(int alpha) {}
    @Override public void setColorFilter(ColorFilter filter) {}
    @Override public int getOpacity() { return PixelFormat.TRANSLUCENT; }
}
