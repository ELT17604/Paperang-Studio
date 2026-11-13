package com.elt.pprstudio.adc;

import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;

final class PaperRuler {
    static final float DOTS_PER_MM = 300f / 25.4f;

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final MainActivity app;

    PaperRuler(MainActivity app) {
        this.app = app;
        paint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        paint.setTextSize(app.dp(10));
    }

    void draw(Canvas canvas, float x, float top, float scale, int rows, int height, int printedRows) {
        if (scale <= 0 || rows <= 0) return;
        float bottom = top + rows * scale;
        paint.setColor(app.getColor(android.R.color.system_neutral2_600));
        canvas.drawLine(x, Math.max(0, top), x, Math.min(height, bottom), paint);
        int step = DOTS_PER_MM * scale >= app.dp(6) ? 1 : DOTS_PER_MM * scale >= app.dp(2) ? 5 : 10;
        int labelStep = Math.max(step, DOTS_PER_MM * scale * 10 >= app.dp(16) ? 10 : 20);
        for (int mm = 0; mm * DOTS_PER_MM <= rows; mm += step) {
            float y = top + mm * DOTS_PER_MM * scale;
            if (y < 0 || y > height) continue;
            boolean label = mm % labelStep == 0;
            canvas.drawLine(x - app.dp(label ? 7 : 4), y, x, y, paint);
            if (label) canvas.drawText(Integer.toString(mm), x + app.dp(3), Math.max(app.dp(10), y + app.dp(4)), paint);
        }
        if (printedRows > 0) {
            float y = top + printedRows * scale;
            if (y >= 0 && y <= height) {
                paint.setColor(app.getColor(android.R.color.system_accent1_700));
                paint.setStrokeWidth(app.dp(2));
                canvas.drawLine(x - app.dp(10), y, x + app.dp(5), y, paint);
                paint.setStrokeWidth(0);
            }
        }
    }
}
