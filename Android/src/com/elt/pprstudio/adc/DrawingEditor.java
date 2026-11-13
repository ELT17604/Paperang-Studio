package com.elt.pprstudio.adc;

import android.app.Dialog;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.view.MotionEvent;
import android.view.View;
import android.widget.LinearLayout;
import java.util.ArrayList;
import java.util.function.Consumer;

final class DrawingEditor {
    private record Stroke(Path path, float width) {}

    static void open(MainActivity app, byte[] previous, Consumer<byte[]> use) {
        Dialog dialog = new Dialog(app, android.R.style.Theme_Material_Light_NoActionBar);
        LinearLayout page = app.column();
        page.setBackgroundColor(app.getColor(android.R.color.system_neutral1_10));
        page.setPadding(app.dp(12), app.dp(8), app.dp(12), app.dp(12));
        var heading = app.label("手绘纸条", 24);
        page.addView(heading);
        var pad = new Pad(app);
        if (previous != null) pad.base = Raster.bitmap(previous, Color.TRANSPARENT);
        var hint = app.label("用手指或触控笔绘制", 13);
        page.addView(hint);
        var stage = new android.widget.FrameLayout(app);
        stage.setBackground(app.shape(0xff485361, 24));
        stage.setClipToOutline(true);
        stage.setPadding(app.dp(12), app.dp(10), app.dp(12), app.dp(10));
        stage.addView(pad, new android.widget.FrameLayout.LayoutParams(-1, -1));
        page.addView(stage, new LinearLayout.LayoutParams(-1, 0, 1));
        page.addView(app.actionRow(app.button("撤销", pad::undo), app.button("清空", () -> {
            if (pad.strokes.isEmpty() && pad.base == null) return;
            new StudioDialog(app).setTitle("清空手绘？")
                    .setNegativeButton("取消", null).setPositiveButton("清空", (d, which) -> {
                        pad.strokes.clear();
                        pad.base = null;
                        pad.invalidate();
                    }).show();
        }), app.button("笔宽", () -> new StudioDialog(app).setTitle("笔宽")
                .setItems(new String[]{"细笔 · 2 点", "中笔 · 5 点", "粗笔 · 12 点"},
                        (d, which) -> pad.width = new int[]{2, 5, 12}[which]).show())));
        page.addView(app.actionRow(app.button("关闭", dialog::cancel), app.button("完成", () -> {
            byte[] bytes;
            try {
                bytes = pad.raster();
            } catch (IllegalArgumentException error) {
                hint.setText(error.getMessage());
                return;
            }
            dialog.dismiss();
            use.accept(bytes);
        })));
        dialog.setContentView(page);
        dialog.show();
        dialog.getWindow().setLayout(-1, -1);
        if (android.animation.ValueAnimator.areAnimatorsEnabled()) {
            stage.setAlpha(0);
            stage.animate().alpha(1).setDuration(180).start();
        }
    }

    static final class Pad extends View {
        private final ArrayList<Stroke> strokes = new ArrayList<>();
        private final Paint pen = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint surface = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint edge = new Paint(Paint.ANTI_ALIAS_FLAG);
        private Path active;
        private Bitmap base;
        private float width = 5;
        private float activeWidth;
        private float startX, startY;
        private boolean moved;

        Pad(MainActivity app) {
            super(app);
            setContentDescription("手绘画布");
            surface.setColor(Color.WHITE);
            edge.setColor(0xff26303b);
            pen.setColor(Color.BLACK);
            pen.setStrokeCap(Paint.Cap.ROUND);
            pen.setStrokeJoin(Paint.Join.ROUND);
        }

        private float scale() {
            return Math.min(getWidth() / (float) Raster.WIDTH, getHeight() / (float) Raster.MAX_HEIGHT);
        }

        private float left() {
            return (getWidth() - Raster.WIDTH * scale()) / 2;
        }

        @Override protected void onDraw(Canvas canvas) {
            if (scale() <= 0) return;
            canvas.save();
            canvas.translate(left(), 0);
            canvas.drawRect(2, 3, Raster.WIDTH * scale() + 2, Raster.MAX_HEIGHT * scale() + 3, edge);
            canvas.scale(scale(), scale());
            canvas.clipRect(0, 0, Raster.WIDTH, Raster.MAX_HEIGHT);
            canvas.drawRect(0, 0, Raster.WIDTH, Raster.MAX_HEIGHT, surface);
            drawInk(canvas);
            canvas.restore();
        }

        private void drawInk(Canvas canvas) {
            if (base != null) {
                canvas.drawBitmap(base, null, new android.graphics.Rect(0, 0, Raster.WIDTH, base.getHeight()), null);
            }
            pen.setStyle(Paint.Style.STROKE);
            for (Stroke stroke : strokes) {
                pen.setStrokeWidth(stroke.width());
                canvas.drawPath(stroke.path(), pen);
            }
            if (active != null) {
                pen.setStrokeWidth(activeWidth);
                canvas.drawPath(active, pen);
            }
        }

        @Override public boolean onTouchEvent(MotionEvent event) {
            if (scale() <= 0) return false;
            float rawX = (event.getX() - left()) / scale();
            float rawY = event.getY() / scale();
            float x = PaperPlacement.clamp(rawX, 0, Raster.WIDTH - 1);
            float y = PaperPlacement.clamp(rawY, 0, Raster.MAX_HEIGHT - 1);
            switch (event.getActionMasked()) {
                case MotionEvent.ACTION_DOWN -> {
                    if (rawX < 0 || rawX >= Raster.WIDTH || rawY < 0 || rawY >= Raster.MAX_HEIGHT) return false;
                    active = new Path();
                    active.moveTo(x, y);
                    startX = x;
                    startY = y;
                    moved = false;
                    activeWidth = width;
                }
                case MotionEvent.ACTION_MOVE -> {
                    if (active == null || event.getPointerCount() != 1) return true;
                    for (int i = 0; i < event.getHistorySize(); i++) {
                        active.lineTo(PaperPlacement.clamp((event.getHistoricalX(i) - left()) / scale(), 0, Raster.WIDTH - 1),
                                PaperPlacement.clamp(event.getHistoricalY(i) / scale(), 0, Raster.MAX_HEIGHT - 1));
                    }
                    active.lineTo(x, y);
                    moved = true;
                }
                case MotionEvent.ACTION_UP -> {
                    if (active == null) return true;
                    if (!moved && x == startX && y == startY) active.lineTo(x + .1f, y);
                    else active.lineTo(x, y);
                    strokes.add(new Stroke(active, activeWidth));
                    active = null;
                    performClick();
                }
                case MotionEvent.ACTION_POINTER_DOWN, MotionEvent.ACTION_CANCEL -> active = null;
                default -> { }
            }
            invalidate();
            return true;
        }

        @Override public boolean performClick() {
            super.performClick();
            return true;
        }

        void undo() {
            if (!strokes.isEmpty()) strokes.remove(strokes.size() - 1);
            invalidate();
        }

        byte[] raster() {
            Bitmap image = PaperRenderer.paper(Raster.MAX_HEIGHT);
            drawInk(new Canvas(image));
            return Raster.trimBottom(PaperRenderer.pack(image, PaperRenderer.Tone.DEFAULT));
        }
    }
}
