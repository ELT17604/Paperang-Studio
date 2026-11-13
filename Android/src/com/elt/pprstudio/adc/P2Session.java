package com.elt.pprstudio.adc;

import android.annotation.SuppressLint;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothGatt;
import android.bluetooth.BluetoothGattCallback;
import android.bluetooth.BluetoothGattCharacteristic;
import android.bluetooth.BluetoothGattDescriptor;
import android.bluetooth.BluetoothProfile;
import android.bluetooth.BluetoothStatusCodes;
import android.content.Context;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.UUID;
import java.util.function.Consumer;

@SuppressLint("MissingPermission")
final class P2Session extends BluetoothGattCallback {
    static UUID uuid(int id) {
        return UUID.fromString(String.format(java.util.Locale.ROOT, "0000%04x-0000-1000-8000-00805f9b34fb", id));
    }

    static final UUID SERVICE = uuid(0xff00);
    private static final UUID RX = uuid(0xff01);
    private static final UUID TX = uuid(0xff02);
    private static final UUID FLOW = uuid(0xff03);
    private static final UUID CCCD = uuid(0x2902);

    private final Handler main = new Handler(Looper.getMainLooper());
    private final Consumer<String> update;
    private final Wire.Decoder decoder = new Wire.Decoder();
    private final Runnable deadline = () -> fail("蓝牙操作超时，请重新连接");
    private BluetoothGatt gatt;
    private BluetoothGattCharacteristic writer;
    private BluetoothGattCharacteristic flow;
    private BluetoothGattDescriptor subscribing;
    private int credits;
    private int mtu = P2Transfer.DEFAULT_MTU;
    private int printerLimit = P2Transfer.MAX_CHUNK;
    private boolean discovering;
    private long lastWriteAt;
    private int media = -1;
    private boolean ready;
    private boolean printing;
    private boolean writing;
    private boolean pacing;
    private byte[] raster;
    private PrintOptions options;
    private Consumer<String> finished;
    private int copy;
    private boolean rasterStarted;
    private Pending pending;

    private static final class Pending {
        final int command;
        final int responseCode;
        final byte[] frame;
        final boolean requireAck;
        final Consumer<byte[]> complete;
        int offset;
        boolean ack;
        byte[] response;

        Pending(int command, int responseCode, byte[] frame, boolean requireAck, Consumer<byte[]> complete) {
            this.command = command;
            this.responseCode = responseCode;
            this.frame = frame;
            this.requireAck = requireAck;
            this.complete = complete;
        }
    }

    P2Session(Context context, BluetoothDevice device, Consumer<String> update) {
        this.update = update;
        gatt = device.connectGatt(context, false, this, BluetoothDevice.TRANSPORT_LE);
        if (gatt == null) throw new IllegalStateException("无法启动 GATT 连接");
        arm(20000);
    }

    boolean isOpen() {
        return gatt != null;
    }

    boolean canPrint() {
        return ready && !printing && gatt != null;
    }

    void close() {
        main.removeCallbacksAndMessages(null);
        BluetoothGatt old = gatt;
        gatt = null;
        pending = null;
        raster = null;
        finish("连接中断，实际出纸待核对");
        if (old != null) {
            try {
                old.disconnect();
            } catch (SecurityException ignored) {
                // The user may revoke Nearby Devices access during the session.
            } finally {
                old.close();
            }
        }
    }

    private void fail(String message) {
        String prefix = printing && rasterStarted ? "打印中断，部分内容可能已送达。请核对纸面。\n" : "";
        finish(message);
        close();
        update.accept(prefix + message);
    }

    private void arm(long millis) {
        main.removeCallbacks(deadline);
        main.postDelayed(deadline, millis);
    }

    private void event(BluetoothGatt source, Runnable action) {
        main.post(() -> {
            if (gatt != source) return;
            try {
                action.run();
            } catch (SecurityException error) {
                fail("附近设备权限已撤销");
            }
        });
    }

    @Override
    public void onConnectionStateChange(BluetoothGatt source, int status, int state) {
        event(source, () -> {
            if (status != BluetoothGatt.GATT_SUCCESS || state == BluetoothProfile.STATE_DISCONNECTED) {
                fail("蓝牙连接已断开（" + status + "）");
            } else if (state == BluetoothProfile.STATE_CONNECTED) {
                if (source.requestMtu(P2Transfer.REQUEST_MTU)) {
                    main.postDelayed(() -> event(source, this::discoverServices), 1800);
                } else {
                    discoverServices();
                }
            }
        });
    }

    @Override
    public void onMtuChanged(BluetoothGatt source, int negotiated, int status) {
        event(source, () -> {
            if (status == BluetoothGatt.GATT_SUCCESS && negotiated >= P2Transfer.DEFAULT_MTU) mtu = negotiated;
            discoverServices();
        });
    }

    private void discoverServices() {
        if (discovering) return;
        discovering = true;
        if (!gatt.discoverServices()) fail("无法开始发现 GATT 服务");
    }

    @Override
    public void onServicesDiscovered(BluetoothGatt source, int status) {
        event(source, () -> {
            var service = source.getService(SERVICE);
            if (status != BluetoothGatt.GATT_SUCCESS || service == null) {
                fail("设备没有 Paperang FF00 服务");
                return;
            }
            writer = service.getCharacteristic(TX);
            flow = service.getCharacteristic(FLOW);
            var receiver = service.getCharacteristic(RX);
            if (writer == null || (writer.getProperties() & BluetoothGattCharacteristic.PROPERTY_WRITE_NO_RESPONSE) == 0
                    || flow == null || receiver == null) {
                fail("设备缺少 P2 蓝牙特征");
                return;
            }
            subscribe(receiver);
        });
    }

    private void subscribe(BluetoothGattCharacteristic characteristic) {
        subscribing = characteristic.getDescriptor(CCCD);
        if (subscribing == null || !gatt.setCharacteristicNotification(characteristic, true)) {
            fail("无法订阅 P2 通知");
            return;
        }
        byte[] value = (characteristic.getProperties() & BluetoothGattCharacteristic.PROPERTY_NOTIFY) != 0
                ? BluetoothGattDescriptor.ENABLE_NOTIFICATION_VALUE : BluetoothGattDescriptor.ENABLE_INDICATION_VALUE;
        if (gatt.writeDescriptor(subscribing, value) != BluetoothStatusCodes.SUCCESS) fail("通知配置发送失败");
    }

    @Override
    public void onDescriptorWrite(BluetoothGatt source, BluetoothGattDescriptor descriptor, int status) {
        event(source, () -> {
            if (descriptor != subscribing) return;
            if (status != BluetoothGatt.GATT_SUCCESS) {
                fail("打印机拒绝通知配置（" + status + "）");
                return;
            }
            if (!FLOW.equals(descriptor.getCharacteristic().getUuid())) {
                subscribe(flow);
                return;
            }
            send(24, 0, new byte[]{0x21, (byte) 0x95, 0x76, 0x35}, 0x35769521, true, -1,
                    ignored -> query(6, model -> {
                        String name = new String(model, StandardCharsets.UTF_8).trim();
                        if (!name.equals("Paperang_P2") && !name.equals("P2")) {
                            fail("型号未通过 Paperang P2 验证");
                            return;
                        }
                        readDevice();
                    }));
        });
    }

    @Override
    public void onCharacteristicChanged(BluetoothGatt source, BluetoothGattCharacteristic characteristic, byte[] value) {
        byte[] bytes = value.clone();
        event(source, () -> {
            if (FLOW.equals(characteristic.getUuid())) {
                if (bytes.length >= 2 && bytes[0] == 1) credits += bytes[1] & 255;
                if (bytes.length >= 3 && bytes[0] == 2 && Wire.u16(bytes, 1) > 0) {
                    printerLimit = Math.min(printerLimit, Wire.u16(bytes, 1));
                }
                pump();
            } else if (RX.equals(characteristic.getUuid())) {
                decoder.push(bytes, this::receive);
                pump();
            }
        });
    }

    private void receive(int command, byte[] payload) {
        if (command == 13 && payload.length == 2) media = Wire.u16(payload, 0);
        if (pending == null) return;
        if (pending.requireAck && command == pending.command && payload.length == 1) {
            if (payload[0] != 0) {
                fail(String.format("打印机拒绝命令 0x%02X", command));
                return;
            }
            pending.ack = true;
        }
        if (command == pending.responseCode) pending.response = payload;
    }

    private void send(int command, int index, byte[] payload, int seed, boolean ack, int response,
                      Consumer<byte[]> complete) {
        pending = new Pending(command, response, Wire.frame(command, index, payload, seed), ack, complete);
        arm(6000);
        pump();
    }

    private void query(int command, Consumer<byte[]> complete) {
        send(command, 0, new byte[]{1}, 0, true, command + 1, complete);
    }

    private void pump() {
        Pending current = pending;
        if (gatt == null || current == null || writing || pacing) return;
        if (current.offset == current.frame.length) {
            if ((!current.requireAck || current.ack) && (current.responseCode < 0 || current.response != null)) {
                pending = null;
                main.removeCallbacks(deadline);
                current.complete.accept(current.response);
            }
            return;
        }
        if (printing && current.command == 0 && (media & 0x11) != 0) {
            fail(mediaText());
            return;
        }
        if (credits == 0) return;
        int end = Math.min(current.offset + P2Transfer.chunkSize(mtu, printerLimit), current.frame.length);
        byte[] data = Arrays.copyOfRange(current.frame, current.offset, end);
        lastWriteAt = SystemClock.uptimeMillis();
        int result = gatt.writeCharacteristic(writer, data, BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE);
        if (result != BluetoothStatusCodes.SUCCESS) {
            fail("蓝牙写入失败（" + result + "）");
            return;
        }
        if (current.command == 0) rasterStarted = true;
        credits--;
        current.offset = end;
        writing = true;
    }

    @Override
    public void onCharacteristicWrite(BluetoothGatt source, BluetoothGattCharacteristic characteristic, int status) {
        event(source, () -> {
            if (!TX.equals(characteristic.getUuid()) || !writing) return;
            writing = false;
            if (status != BluetoothGatt.GATT_SUCCESS) {
                fail("GATT 写入失败（" + status + "）");
                return;
            }
            long delay = P2Transfer.remainingDelay(SystemClock.uptimeMillis() - lastWriteAt);
            if (delay == 0) {
                pump();
                return;
            }
            pacing = true;
            main.postDelayed(() -> event(source, () -> {
                pacing = false;
                pump();
            }), delay);
        });
    }

    void refreshDevice() {
        if (!canPrint()) return;
        ready = false;
        update.accept("正在读取打印机状态");
        event(gatt, this::readDevice);
    }

    private void readDevice() {
        query(12, paper -> {
            if (paper.length != 2) {
                fail("纸仓状态响应格式有误");
                return;
            }
            query(16, battery -> query(28, density -> {
                ready = true;
                update.accept("Paperang P2 · " + mediaText() + " · 电量 " + reading(battery, "%")
                        + " · 浓度 " + reading(density, "")
                        + "\nBLE 每次最多 " + P2Transfer.chunkSize(mtu, printerLimit) + " 字节");
            }));
        });
    }

    private String reading(byte[] payload, String suffix) {
        return payload.length == 1 ? (payload[0] & 255) + suffix : "未知";
    }

    void applyDensity(int density) {
        if (!canPrint()) return;
        printing = true;
        rasterStarted = false;
        update.accept("正在设置并回读打印浓度");
        event(gatt, () -> verifyDensity(density, () -> complete("打印浓度已确认 · " + density)));
    }

    private void verifyDensity(int density, Runnable next) {
        send(25, 0, new byte[]{(byte) density}, 0, true, -1, ignored ->
                query(28, value -> {
                    if (value.length != 1 || (value[0] & 255) != density) {
                        fail("浓度回读未确认，已停止发送");
                        return;
                    }
                    next.run();
                }));
    }

    void print(byte[] bytes, PrintOptions settings, Consumer<String> outcome) {
        if (!canPrint()) return;
        printing = true;
        raster = bytes;
        options = settings;
        finished = outcome;
        copy = 1;
        rasterStarted = false;
        update.accept("正在检查纸仓");
        event(gatt, () -> checkMedia(() ->
                verifyDensity(settings.density(), () -> feed(settings.before(), this::beginCopy))));
    }

    void feedPaper(int lines, Consumer<String> outcome) {
        if (!canPrint()) return;
        printing = true;
        finished = outcome;
        rasterStarted = false;
        update.accept("正在检查纸仓");
        event(gatt, () -> checkMedia(() -> feed(lines, () -> complete("送纸 " + lines + " 行指令已确认"))));
    }

    private void checkMedia(Runnable next) {
        query(12, paper -> {
            if (paper.length != 2 || (media & 0x11) != 0) {
                String message = paper.length == 2 ? mediaText() : "无法取得纸仓状态";
                if (rasterStarted) fail(message);
                else complete(message);
                return;
            }
            next.run();
        });
    }

    private void feed(int lines, Runnable next) {
        if (lines == 0) {
            next.run();
            return;
        }
        send(26, 0, new byte[]{(byte) lines, (byte) (lines >>> 8)}, 0, true, -1, ignored -> next.run());
    }

    private void beginCopy() {
        checkMedia(() -> sendRaster(0));
    }

    private void sendRaster(int offset) {
        if (offset == raster.length) {
            BluetoothGatt source = gatt;
            main.postDelayed(() -> event(source, () -> {
                if ((media & 0x11) != 0) {
                    fail(mediaText());
                    return;
                }
                feed(copy == options.copies() || options.between() ? options.after() : 0, () -> {
                    if (copy < options.copies()) {
                        copy++;
                        beginCopy();
                    } else {
                        query(12, ignored -> complete("已发送，请核对纸面"));
                    }
                });
            }), 2000);
            return;
        }
        int frameBytes = P2Transfer.FRAME_BYTES;
        int end = Math.min(offset + frameBytes, raster.length);
        int frame = offset / frameBytes;
        update.accept("第 " + copy + " / " + options.copies() + " 份 · 分包 "
                + (frame + 1) + " / " + ((raster.length + frameBytes - 1) / frameBytes));
        send(0, frame, Arrays.copyOfRange(raster, offset, end), 0, false, -1, ignored -> sendRaster(end));
    }

    private void complete(String status) {
        printing = false;
        raster = null;
        finish(status);
        update.accept(status);
    }

    private void finish(String status) {
        Consumer<String> callback = finished;
        finished = null;
        if (callback != null) callback.accept(status);
    }

    private String mediaText() {
        if (media < 0) return "纸仓状态未知";
        if ((media & 0x10) != 0) return "盖子打开，请合上盖子";
        return (media & 1) != 0 ? "缺纸，请装好纸卷" : "纸仓就绪";
    }
}
