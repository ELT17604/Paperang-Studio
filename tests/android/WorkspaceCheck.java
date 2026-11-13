package com.elt.pprstudio.adc;

import com.google.zxing.BinaryBitmap;
import com.google.zxing.RGBLuminanceSource;
import com.google.zxing.common.HybridBinarizer;
import com.google.zxing.qrcode.QRCodeReader;
import com.google.zxing.qrcode.QRCodeWriter;
import com.google.zxing.BarcodeFormat;
import com.google.zxing.EncodeHintType;
import java.util.Arrays;
import java.util.HexFormat;
import java.util.Map;

public final class WorkspaceCheck {
    public static void main(String[] args) throws Exception {
        byte[] handshake = Wire.frame(24, 0, new byte[]{0x21, (byte) 0x95, 0x76, 0x35}, 0x35769521);
        require(HexFormat.of().formatHex(handshake).equals("0218000400219576351cdf442103"), "seeded handshake");

        byte[] payload = new byte[1008];
        Arrays.fill(payload, (byte) 0xa5);
        byte[] frame = Wire.frame(0, 3, payload, 0);
        require(Wire.u16(frame, 3) == 1008 && frame[2] == 3, "raster frame");
        byte[] bad = frame.clone();
        bad[100] ^= 1;
        Wire.Decoder decoder = new Wire.Decoder();
        decoder.push(bad, (cmd, data) -> { throw new AssertionError("invalid CRC"); });
        int[] count = {0};
        for (int offset = 0; offset < frame.length; offset += 20) {
            decoder.push(Arrays.copyOfRange(frame, offset, Math.min(offset + 20, frame.length)), (cmd, data) -> {
                require(cmd == 0 && Arrays.equals(data, payload), "fragmented frame");
                count[0]++;
            });
        }
        require(count[0] == 1, "decoder count");
        require(PrintOptions.DEFAULT.after() == 150, "default feed");
        rejects(() -> new PrintOptions(0, 100, 0, 150, false));
        rejects(() -> new PrintOptions(20, 101, 0, 150, false));
        rejects(() -> new PrintOptions(20, 100, 0, 2001, false));

        String text = "Paperang Studio · 中文";
        int size = 220;
        var qr = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size,
                Map.of(EncodeHintType.CHARACTER_SET, "UTF-8"));
        int[] pixels = new int[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) pixels[y * size + x] = qr.get(x, y) ? 0xff000000 : 0xffffffff;
        }
        var bitmap = new BinaryBitmap(new HybridBinarizer(new RGBLuminanceSource(size, size, pixels)));
        require(new QRCodeReader().decode(bitmap).getText().equals(text), "QR UTF-8");
        PaperPlacement box = new PaperPlacement(20, 40, 100, 80);
        require(box.drag(-99, 999, -1, 576, 720, false, 7)
                .equals(new PaperPlacement(0, 640, 100, 80)), "drag clamps to paper");
        require(box.drag(215, 276, -1, 576, 720, true, 7)
                .equals(new PaperPlacement(238, 320, 100, 80)), "center snap");
        require(box.drag(215, 276, -1, 576, 720, false, 7)
                .equals(new PaperPlacement(235, 316, 100, 80)), "free placement");
        require(box.drag(999, 999, 0, 576, 720, true, 7)
                .equals(new PaperPlacement(108, 108, 12, 12)), "minimum size and fixed opposite corner");
        require(box.drag(999, 999, 3, 576, 720, true, 7)
                .equals(new PaperPlacement(20, 40, 556, 680)), "resize clamps to paper");
        require(P2Transfer.chunkSize(23, 180) == 20, "default ATT payload");
        require(P2Transfer.chunkSize(185, 180) == 180, "Mac-sized payload");
        require(P2Transfer.chunkSize(517, 64) == 64, "printer flow limit");
        require(P2Transfer.chunkSize(100, 180) == 97, "negotiated MTU limit");
        require(P2Transfer.remainingDelay(3) == 7 && P2Transfer.remainingDelay(12) == 0, "write-to-write pacing");
        var start = PaperGesture.between(20, 40, 100, 40);
        var pan = PaperGesture.between(50, 75, 130, 75);
        require(pan.x() - start.x() == 30 && pan.y() - start.y() == 35
                && pan.zoomFrom(start, 2, 12) == 2, "two-finger pan without pinch");
        require(PaperGesture.between(0, 40, 160, 40).zoomFrom(start, 2, 12) == 4, "pinch zoom");
        require(PaperGesture.between(0, 40, 500, 40).zoomFrom(start, 2, 12) == 5, "zoom clamp");
        System.out.println("PASS Android wire framing, fragmented CRC, print settings, QR dependency, paper placement and BLE pacing");
    }

    private static void rejects(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("out-of-range print options");
    }

    private static void require(boolean condition, String name) {
        if (!condition) throw new AssertionError(name);
    }
}
