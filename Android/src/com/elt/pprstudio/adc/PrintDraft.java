package com.elt.pprstudio.adc;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.style.AbsoluteSizeSpan;
import android.text.style.StyleSpan;
import android.text.style.TypefaceSpan;
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.Base64;
import org.json.JSONArray;
import org.json.JSONObject;

record PrintDraft(int mode, WorkspacePreset preset, String text, Bitmap image, PaperComposition paper) {
    byte[] encode() throws Exception {
        var json = new JSONObject().put("version", 1).put("mode", mode).put("preset", preset.json());
        if (mode == 0) json.put("text", text);
        if (mode == 1) json.put("image", image(image));
        if (mode == 2) {
            var layers = new JSONArray();
            for (var layer : paper.layers) {
                var box = layer.bounds;
                var qr = layer.qr;
                layers.put(new JSONObject().put("kind", layer.kind)
                        .put("bounds", new JSONArray().put(box.left).put(box.top).put(box.right).put(box.bottom))
                        .put("text", layer.text.toString()).put("spans", spans(layer.text))
                        .put("align", layer.style.align()).put("image", image(layer.image))
                        .put("qrFormat", qr.format()).put("qrCorrection", qr.correction().name())
                        .put("qrShape", qr.shape()).put("qrBorder", qr.border()));
            }
            json.put("height", paper.height).put("autoLength", paper.autoLength).put("layers", layers);
        }
        return json.toString().getBytes(StandardCharsets.UTF_8);
    }

    static PrintDraft decode(byte[] bytes) throws Exception {
        var json = new JSONObject(new String(bytes, StandardCharsets.UTF_8));
        if (json.getInt("version") != 1) throw new IOException("无法读取任务版本");
        int mode = json.getInt("mode");
        if (mode < 0 || mode > 4) throw new IOException("无法读取任务模式");
        PaperComposition paper = null;
        if (mode == 2) {
            paper = new PaperComposition();
            paper.height = json.getInt("height");
            paper.autoLength = json.getBoolean("autoLength");
            var layers = json.getJSONArray("layers");
            for (int i = 0; i < layers.length(); i++) {
                var item = layers.getJSONObject(i);
                var layer = new PaperComposition.Layer(item.getInt("kind"), 0, bitmap(item.getString("image")));
                var box = item.getJSONArray("bounds");
                layer.bounds.set((float) box.getDouble(0), (float) box.getDouble(1),
                        (float) box.getDouble(2), (float) box.getDouble(3));
                var content = new SpannableString(item.getString("text"));
                var spans = item.getJSONArray("spans");
                for (int j = 0; j < spans.length(); j++) {
                    var span = spans.getJSONArray(j);
                    Object style = switch (span.getInt(0)) {
                        case 0 -> new TypefaceSpan(span.getString(3));
                        case 1 -> new AbsoluteSizeSpan(span.getInt(3));
                        case 2 -> new StyleSpan(span.getInt(3));
                        default -> throw new IOException("无法读取文字样式");
                    };
                    content.setSpan(style, span.getInt(1), span.getInt(2), Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
                }
                layer.text = content;
                layer.style = new PaperRenderer.LayoutOptions(0, 0, (int) layer.bounds.width(), item.getInt("align"),
                        30, 4, "sans-serif", false, 0, 0, 400);
                layer.qr = new QrMark(item.getInt("qrFormat"), ErrorCorrectionLevel.valueOf(item.getString("qrCorrection")),
                        item.getInt("qrShape"), item.getBoolean("qrBorder"));
                paper.layers.add(layer);
            }
        }
        return new PrintDraft(mode, WorkspacePreset.read(json.getJSONObject("preset")), json.optString("text"),
                bitmap(json.optString("image")), paper);
    }

    private static JSONArray spans(CharSequence text) throws Exception {
        var result = new JSONArray();
        if (!(text instanceof Spanned styled)) return result;
        for (Object span : styled.getSpans(0, text.length(), Object.class)) {
            int kind;
            Object value;
            if (span instanceof TypefaceSpan typeface) {
                kind = 0;
                value = typeface.getFamily();
            } else if (span instanceof AbsoluteSizeSpan size) {
                kind = 1;
                value = size.getSize();
            } else if (span instanceof StyleSpan style) {
                kind = 2;
                value = style.getStyle();
            } else continue;
            result.put(new JSONArray().put(kind).put(styled.getSpanStart(span)).put(styled.getSpanEnd(span)).put(value));
        }
        return result;
    }

    private static String image(Bitmap bitmap) throws IOException {
        if (bitmap == null) return "";
        var out = new ByteArrayOutputStream();
        if (!bitmap.compress(Bitmap.CompressFormat.PNG, 100, out)) throw new IOException("无法保存任务图片");
        return Base64.getEncoder().encodeToString(out.toByteArray());
    }

    private static Bitmap bitmap(String data) throws IOException {
        if (data.isEmpty()) return null;
        byte[] bytes = Base64.getDecoder().decode(data);
        Bitmap bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.length);
        if (bitmap == null) throw new IOException("无法读取任务图片");
        return bitmap;
    }
}
