package com.elt.pprstudio.adc;

record PaperGesture(float x, float y, float span) {
    static PaperGesture between(float x1, float y1, float x2, float y2) {
        return new PaperGesture((x1 + x2) / 2, (y1 + y2) / 2, (float) Math.hypot(x2 - x1, y2 - y1));
    }

    float zoomFrom(PaperGesture previous, float zoom, float minSpan) {
        if (previous.span < minSpan || span < minSpan) return zoom;
        return PaperPlacement.clamp(zoom * span / previous.span, 1, 5);
    }
}
