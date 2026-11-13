package com.elt.pprstudio.adc;

import org.json.JSONException;
import org.json.JSONObject;

record WorkspacePreset(PaperRenderer.LayoutOptions layout, PaperRenderer.Tone tone, PrintOptions print) {
    private static final String[] ALIGNMENTS = {"居左", "居中", "居右"};
    private static final String[] FITS = {"适配宽度", "保持像素", "裁切填满"};
    private static final String[] METHODS = {"阈值", "Floyd–Steinberg", "有序抖动"};

    JSONObject json() throws JSONException {
        return new JSONObject().put("version", 1).put("fontName", layout.family()).put("fontSize", layout.fontSize())
                .put("bold", layout.bold()).put("lineSpacing", layout.spacing()).put("alignment", ALIGNMENTS[layout.align()])
                .put("marginX", layout.marginX()).put("marginY", layout.marginY()).put("contentWidth", layout.width())
                .put("imageFit", FITS[layout.fit()]).put("cropHeight", layout.cropHeight()).put("rotation", layout.rotation())
                .put("conversion", METHODS[tone.method()]).put("threshold", tone.threshold()).put("brightness", tone.brightness())
                .put("contrast", tone.contrast()).put("gamma", tone.gamma()).put("invert", tone.invert())
                .put("density", print.density()).put("copies", print.copies()).put("feedBefore", print.before())
                .put("feedAfter", print.after()).put("feedBetweenCopies", print.between())
                .put("packetBytes", 1008).put("bleChunk", 180).put("writePauseMS", 10)
                .put("framePauseMS", 0).put("timeoutSeconds", 6).put("finishWaitSeconds", 2);
    }

    static WorkspacePreset read(JSONObject json) throws JSONException {
        integer(json, "version", 1, 1);
        int margin = integer(json, "marginX", 0, 120);
        int rotation = integer(json, "rotation", 0, 270);
        if (rotation % 90 != 0) throw new IllegalArgumentException("无效旋转角度");
        var layout = new PaperRenderer.LayoutOptions(margin, integer(json, "marginY", 0, 200),
                integer(json, "contentWidth", 48, 576 - margin * 2), index(ALIGNMENTS, json.getString("alignment")),
                (int) Math.round(range(json, "fontSize", 8, 96)), (int) Math.round(range(json, "lineSpacing", 0, 40)),
                json.getString("fontName"), json.getBoolean("bold"), rotation,
                index(FITS, json.getString("imageFit")), integer(json, "cropHeight", 24, 1400));
        var tone = new PaperRenderer.Tone(index(METHODS, json.getString("conversion")),
                (int) Math.round(range(json, "threshold", 0, 255)), (int) Math.round(range(json, "brightness", -100, 100)),
                range(json, "contrast", .2, 3), range(json, "gamma", .2, 3), json.getBoolean("invert"));
        var print = new PrintOptions(integer(json, "copies", 1, 20), integer(json, "density", 0, 100),
                integer(json, "feedBefore", 0, 2000), integer(json, "feedAfter", 0, 2000), json.getBoolean("feedBetweenCopies"));
        return new WorkspacePreset(layout, tone, print);
    }

    private static int integer(JSONObject json, String key, int min, int max) throws JSONException {
        double value = range(json, key, min, max);
        if (value != Math.rint(value)) throw new IllegalArgumentException("预设字段须为整数 · " + key);
        return (int) value;
    }

    private static double range(JSONObject json, String key, double min, double max) throws JSONException {
        Object value = json.get(key);
        if (value instanceof Number number && number.doubleValue() >= min && number.doubleValue() <= max) {
            return number.doubleValue();
        }
        throw new IllegalArgumentException("预设字段超出范围 · " + key);
    }

    private static int index(String[] values, String value) {
        int position = java.util.Arrays.asList(values).indexOf(value);
        if (position < 0) throw new IllegalArgumentException("未知预设选项 · " + value);
        return position;
    }
}
