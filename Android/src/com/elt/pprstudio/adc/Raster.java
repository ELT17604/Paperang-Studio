package com.elt.pprstudio.adc;

import android.graphics.Bitmap;
import android.graphics.Color;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.Arrays;
import java.util.Base64;
import org.json.JSONObject;

final class Raster {
    static final int WIDTH = 576;
    static final int STRIDE = WIDTH / 8;
    static final int MAX_HEIGHT = 1400;

    static byte[] read(InputStream stream, boolean raw) throws Exception {
        int limit = raw ? STRIDE * MAX_HEIGHT : 144000;
        byte[] input = stream.readNBytes(limit + 1);
        if (input.length == 0 || input.length > limit) throw new IllegalArgumentException("文件为空或超出大小上限");
        if (raw) {
            if (input.length % STRIDE != 0) throw new IllegalArgumentException("RAW 文件必须每行 72 字节");
            return input;
        }
        JSONObject json = new JSONObject(new String(input, StandardCharsets.UTF_8));
        int height = json.optInt("height");
        if (!"raster1".equals(json.opt("type")) || !Integer.valueOf(1).equals(json.opt("schemaVersion"))
                || !Integer.valueOf(WIDTH).equals(json.opt("width")) || !Integer.valueOf(height).equals(json.opt("height"))
                || height < 1 || height > MAX_HEIGHT || !"base64".equals(json.opt("encoding"))
                || !"msb-first".equals(json.opt("bitOrder")) || !Boolean.TRUE.equals(json.opt("blackIsOne"))) {
            throw new IllegalArgumentException("不支持该 raster1 版本、尺寸或编码");
        }
        String encoded = json.getString("data");
        byte[] bytes = Base64.getDecoder().decode(encoded);
        if (bytes.length != STRIDE * height || !Base64.getEncoder().encodeToString(bytes).equals(encoded)) {
            throw new IllegalArgumentException("Base64 格式或位图长度不正确");
        }
        if (!checksum(bytes).equals(json.opt("sha256"))) throw new IllegalArgumentException("SHA-256 校验失败");
        return bytes;
    }

    static byte[] json(byte[] bytes) throws Exception {
        if (bytes.length == 0 || bytes.length % STRIDE != 0 || bytes.length > STRIDE * MAX_HEIGHT) {
            throw new IllegalArgumentException("位图尺寸无效");
        }
        return new JSONObject().put("type", "raster1").put("schemaVersion", 1)
                .put("width", WIDTH).put("height", bytes.length / STRIDE).put("encoding", "base64")
                .put("bitOrder", "msb-first").put("blackIsOne", true)
                .put("data", Base64.getEncoder().encodeToString(bytes)).put("sha256", checksum(bytes))
                .toString().getBytes(StandardCharsets.UTF_8);
    }

    private static String checksum(byte[] bytes) throws java.security.NoSuchAlgorithmException {
        StringBuilder hash = new StringBuilder();
        for (byte value : MessageDigest.getInstance("SHA-256").digest(bytes)) {
            hash.append(Character.forDigit((value & 255) >>> 4, 16));
            hash.append(Character.forDigit(value & 15, 16));
        }
        return hash.toString();
    }

    static byte[] trimBottom(byte[] bytes) {
        int last = bytes.length - 1;
        while (last >= 0 && bytes[last] == 0) last--;
        if (last < 0) throw new IllegalArgumentException("纸面没有可打印的内容");
        return Arrays.copyOf(bytes, (last / STRIDE + 1) * STRIDE);
    }

    static Bitmap preview(byte[] bytes) {
        return bitmap(bytes, Color.WHITE);
    }

    static Bitmap bitmap(byte[] bytes, int background) {
        int[] pixels = new int[bytes.length * 8];
        Arrays.fill(pixels, background);
        for (int i = 0; i < pixels.length; i++) {
            if ((bytes[i / 8] & (0x80 >>> (i % 8))) != 0) pixels[i] = Color.BLACK;
        }
        return Bitmap.createBitmap(pixels, WIDTH, bytes.length / STRIDE, Bitmap.Config.ARGB_8888);
    }
}
