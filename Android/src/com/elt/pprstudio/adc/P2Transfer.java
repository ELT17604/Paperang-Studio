package com.elt.pprstudio.adc;

final class P2Transfer {
    static final int DEFAULT_MTU = 23;
    static final int REQUEST_MTU = 185;
    static final int MAX_CHUNK = 180;
    static final int FRAME_BYTES = 1008;
    static final int WRITE_INTERVAL_MS = 10;

    static int chunkSize(int mtu, int printerLimit) {
        return Math.max(1, Math.min(MAX_CHUNK, Math.min(mtu - 3, printerLimit)));
    }

    static long remainingDelay(long elapsedMs) {
        return Math.max(0, WRITE_INTERVAL_MS - elapsedMs);
    }
}
