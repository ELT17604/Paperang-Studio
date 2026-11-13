package com.elt.pprstudio.adc;

record PrintOptions(int copies, int density, int before, int after, boolean between) {
    static final PrintOptions DEFAULT = new PrintOptions(1, 100, 0, 150, false);

    PrintOptions {
        if (copies < 1 || copies > 20 || density < 0 || density > 100
                || before < 0 || before > 2000 || after < 0 || after > 2000) {
            throw new IllegalArgumentException("份数须为 1–20，浓度须为 0–100，送纸须为 0–2000 行");
        }
    }
}
