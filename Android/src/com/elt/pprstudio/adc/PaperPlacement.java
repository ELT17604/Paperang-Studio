package com.elt.pprstudio.adc;

record PaperPlacement(float x, float y, float width, float height) {
    PaperPlacement drag(float dx, float dy, int corner, int paperWidth, int paperHeight, boolean snap, float tolerance) {
        if (corner < 0) {
            return new PaperPlacement(
                    place(x + dx, paperWidth - width, snap, tolerance),
                    place(y + dy, paperHeight - height, snap, tolerance), width, height);
        }
        boolean left = (corner & 1) == 0;
        boolean top = (corner & 2) == 0;
        float l = left ? clamp(x + dx, 0, x + width - 12) : x;
        float t = top ? clamp(y + dy, 0, y + height - 12) : y;
        float r = left ? x + width : clamp(x + width + dx, x + 12, paperWidth);
        float b = top ? y + height : clamp(y + height + dy, y + 12, paperHeight);
        return new PaperPlacement(l, t, r - l, b - t);
    }

    private static float place(float value, float limit, boolean snap, float tolerance) {
        float position = clamp(value, 0, limit);
        if (!snap) return position;
        float center = limit / 2;
        return Math.abs(position - center) <= tolerance ? center : position;
    }

    static float clamp(float value, float min, float max) {
        return Math.max(min, Math.min(max, value));
    }
}
