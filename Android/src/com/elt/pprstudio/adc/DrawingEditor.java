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
        var hint = app.label("单指或触控笔绘制 · 双指平移或缩放 · 尺标 mm", 13);
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
        page.addView(app.actionRow(app.button("关闭", dialog::cancel), app.button("适合纸张", pad::resetViewport), app.button("完成", () -> {
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
        private final PaperRuler ruler;
        private final int gutter;
        private final int rulerHeight;
        private final Paint rulerSurface = new Paint();
        private final int rulerInk;
        private PaperGesture gesture;
        private float zoom = 1, offsetX, offsetY;
        private boolean navigating;
        private Path active;
        private Bitmap base;
        private float width = 5;
        private float activeWidth;
        private float startX, startY;
        private boolean moved;

        Pad(MainActivity app) {
            super(app);
            setContentDescription("手绘画布，顶部宽度和右侧长度毫米尺标");
            ruler = new PaperRuler(app);
            gutter = app.dp(43);
            rulerHeight = app.dp(28);
            rulerSurface.setColor(app.getColor(android.R.color.system_accent2_200));
            rulerInk = app.getColor(android.R.color.system_accent2_900);
            surface.setColor(Color.WHITE);
            edge.setColor(0xff26303b);
            pen.setColor(Color.BLACK);
            pen.setStrokeCap(Paint.Cap.ROUND);
            pen.setStrokeJoin(Paint.Join.ROUND);
        }

        private float viewportWidth() { return Math.max(0, getWidth() - gutter); }

        private float viewportHeight() { return Math.max(0, getHeight() - rulerHeight); }

        private float scale() {
            return zoom * Math.min(viewportWidth() / Raster.WIDTH, viewportHeight() / Raster.MAX_HEIGHT);
        }

        private float left() { return (viewportWidth() - Raster.WIDTH * scale()) / 2 + offsetX; }
        private float top() { return rulerHeight + (viewportHeight() - Raster.MAX_HEIGHT * scale()) / 2 + offsetY; }

        private void constrainViewport() {
            float x = Math.max(0, (Raster.WIDTH * scale() - viewportWidth()) / 2);
            float y = Math.max(0, (Raster.MAX_HEIGHT * scale() - viewportHeight()) / 2);
            offsetX = PaperPlacement.clamp(offsetX, -x, x);
            offsetY = PaperPlacement.clamp(offsetY, -y, y);
        }

        void resetViewport() {
            zoom = 1;
            offsetX = offsetY = 0;
            invalidate();
        }

        @Override protected void onSizeChanged(int w, int h, int oldw, int oldh) {
            constrainViewport();
        }

        @Override protected void onDraw(Canvas canvas) {
            if (scale() <= 0) return;
            canvas.save();
            canvas.clipRect(0, rulerHeight, viewportWidth(), getHeight());
            canvas.translate(left(), top());
            canvas.drawRect(2, 3, Raster.WIDTH * scale() + 2, Raster.MAX_HEIGHT * scale() + 3, edge);
            canvas.scale(scale(), scale());
            canvas.clipRect(0, 0, Raster.WIDTH, Raster.MAX_HEIGHT);
            canvas.drawRect(0, 0, Raster.WIDTH, Raster.MAX_HEIGHT, surface);
            drawInk(canvas);
            canvas.restore();
            canvas.drawRect(0, 0, getWidth(), rulerHeight, rulerSurface);
            canvas.drawRect(viewportWidth(), rulerHeight, getWidth(), getHeight(), rulerSurface);
            canvas.save();
            canvas.clipRect(0, 0, viewportWidth(), rulerHeight);
            ruler.drawWidth(canvas, left(), scale(), viewportWidth(), rulerHeight - 1, rulerInk);
            canvas.restore();
            canvas.save();
            canvas.clipRect(viewportWidth(), rulerHeight, getWidth(), getHeight());
            canvas.translate(0, rulerHeight);
            ruler.draw(canvas, viewportWidth() + gutter * .27f, top() - rulerHeight, scale(), Raster.MAX_HEIGHT,
                    getHeight() - rulerHeight, 0, rulerInk);
            canvas.restore();
            edge.setColor(rulerInk);
            edge.setTextSize(rulerHeight * .38f);
            edge.setTextAlign(Paint.Align.CENTER);
            canvas.drawText("mm", viewportWidth() + gutter / 2f, rulerHeight * .65f, edge);
            edge.setColor(0xff26303b);
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
            int action = event.getActionMasked();
            if (action == MotionEvent.ACTION_DOWN) {
                if (event.getX() >= viewportWidth() || event.getY() < rulerHeight) return false;
                navigating = false;
                gesture = null;
            } else if (action == MotionEvent.ACTION_POINTER_DOWN) {
                active = null;
                navigating = true;
                gesture = event.getPointerCount() == 2 ? gesture(event) : null;
                invalidate();
                return true;
            } else if (action == MotionEvent.ACTION_POINTER_UP) {
                gesture = null;
                return true;
            } else if (navigating) {
                if (action == MotionEvent.ACTION_MOVE && event.getPointerCount() == 2) {
                    PaperGesture current = gesture(event);
                    if (gesture != null) {
                        float x = (gesture.x() - left()) / scale();
                        float y = (gesture.y() - top()) / scale();
                        zoom = current.zoomFrom(gesture, zoom, gutter * .28f);
                        offsetX += current.x() - left() - x * scale();
                        offsetY += current.y() - top() - y * scale();
                        constrainViewport();
                        invalidate();
                    }
                    gesture = current;
                }
                return true;
            }
            float rawX = (event.getX() - left()) / scale();
            float rawY = (event.getY() - top()) / scale();
            float x = PaperPlacement.clamp(rawX, 0, Raster.WIDTH - 1);
            float y = PaperPlacement.clamp(rawY, 0, Raster.MAX_HEIGHT - 1);
            switch (event.getActionMasked()) {
                case MotionEvent.ACTION_DOWN -> {
                    if (rawX < 0 || rawX >= Raster.WIDTH || rawY < 0 || rawY >= Raster.MAX_HEIGHT) return true;
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
                                PaperPlacement.clamp((event.getHistoricalY(i) - top()) / scale(), 0, Raster.MAX_HEIGHT - 1));
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
                case MotionEvent.ACTION_CANCEL -> active = null;
                default -> { }
            }
            invalidate();
            return true;
        }

        private PaperGesture gesture(MotionEvent event) {
            return PaperGesture.between(event.getX(0), event.getY(0), event.getX(1), event.getY(1));
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
