import assert from 'node:assert/strict';
import test from 'node:test';
import { Printer, frame, Decoder } from '../../Web/ble.js';

const handshake = '0218000400219576351cdf442103';

test('seeded handshake, corrupted and fragmented protocol frames', () => {
  assert.equal(Buffer.from(frame(24, 0, [0x21, 0x95, 0x76, 0x35], 0x35769521)).toString('hex'), handshake);
  const decoder = new Decoder();
  const valid = frame(0, 22, new Uint8Array(1008).fill(0xa5));
  const corrupt = valid.slice();
  corrupt[100] ^= 1;
  const packets = [];
  const receive = (code, value) => packets.push([code, value]);
  decoder.push(corrupt, receive);
  for (let i = 0; i < valid.length; i += 20) decoder.push(valid.subarray(i, i + 20), receive);
  assert.deepEqual(packets, [[0, new Uint8Array(1008).fill(0xa5)]]);
});

class Characteristic extends EventTarget {
  properties = { writeWithoutResponse: true };
  async startNotifications() {
    this.start?.();
  }
  notify(bytes) {
    this.value = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
    this.dispatchEvent(new Event('characteristicvaluechanged'));
  }
}

class Device extends EventTarget {
  name = 'Paperang_P2';
  incoming = Buffer.alloc(0);
  commands = [];
  chunks = [];
  media = 0;
  density = 55;
  credit = true;
  model = 'Paperang_P2';
  rx = new Characteristic();
  tx = new Characteristic();
  flow = new Characteristic();

  constructor() {
    super();
    const service = {
      getCharacteristic: async (id) =>
        new Map([
          [0xff01, this.rx],
          [0xff02, this.tx],
          [0xff03, this.flow],
        ]).get(id),
    };
    this.gatt = {
      connected: false,
      connect: async () => {
        this.gatt.connected = true;
        return {
          getPrimaryService: async (id) => {
            assert.equal(id, 0xff00);
            return service;
          },
        };
      },
      disconnect: () => {
        this.gatt.connected = false;
        this.dispatchEvent(new Event('gattserverdisconnected'));
      },
    };
    this.flow.start = () => {
      this.flow.notify(Uint8Array.of(2, 53, 0));
      this.flow.notify(Uint8Array.of(1, 1));
    };
    this.tx.writeValueWithoutResponse = async (bytes) => {
      this.chunks.push(bytes.length);
      assert.ok(bytes.length <= 53);
      if (this.credit) this.flow.notify(Uint8Array.of(1, 1));
      this.incoming = Buffer.concat([this.incoming, bytes]);
      if (this.incoming.length < 5) return;
      const length = this.incoming.readUInt16LE(3);
      if (this.incoming.length < length + 10) return;
      const raw = this.incoming;
      this.incoming = Buffer.alloc(0);
      assert.equal(raw.length, length + 10);
      const cmd = raw[1];
      const data = raw.subarray(5, -5);
      this.commands.push({ cmd, index: raw[2], data });
      if (cmd === 24) assert.equal(raw.toString('hex'), handshake);
      if (cmd === 0) return;
      if (cmd === 25) this.density = data[0];
      const replies = { 6: Buffer.from(this.model), 12: [this.media, 0], 16: [85], 28: [this.density] };
      // The protocol response can precede the command ACK and write promise resolution.
      if (replies[cmd]) this.rx.notify(frame(cmd + 1, 0, replies[cmd]));
      this.rx.notify(frame(cmd, 0, [0]));
    };
  }
}

function mock(device) {
  Object.defineProperty(navigator, 'bluetooth', {
    configurable: true,
    value: {
      requestDevice: async (options) => {
        assert.ok(options.optionalServices.includes(0xff00));
        return device;
      },
    },
  });
}

test('session performs complete P2 print, chunks, density readback, feed and disconnect', async () => {
  const device = new Device();
  mock(device);
  let disconnects = 0;
  const printer = new Printer(() => disconnects++, 'en');
  assert.equal(await printer.connect(), device.name);
  assert.equal(printer.connected, true);
  const bytes = Uint8Array.from({ length: 72 * 15 }, (_, i) => i & 255);
  const progress = [];
  const job = printer.print(bytes, { density: 76, before: 3, interval: 0, chunk: 40 }, (fraction) =>
    progress.push(fraction),
  );
  await assert.rejects(printer.print(bytes), /busy/);
  await job;
  assert.equal(device.density, 76);
  assert.deepEqual(
    device.commands.map((p) => p.cmd),
    [24, 6, 12, 16, 28, 12, 25, 28, 26, 12, 0, 0, 26, 12],
  );
  const raster = device.commands.filter((p) => p.cmd === 0);
  assert.deepEqual(
    raster.map((p) => [p.index, p.data.length]),
    [
      [0, 1008],
      [1, 72],
    ],
  );
  assert.deepEqual(Buffer.concat(raster.map((p) => p.data)), Buffer.from(bytes));
  assert.deepEqual(
    device.commands.filter((p) => p.cmd === 26).map((p) => p.data.readUInt16LE()),
    [3, 160],
  );
  assert.equal(progress.at(-1), 1);
  assert.equal(Math.max(...device.chunks), 40);
  printer.disconnect();
  assert.equal(disconnects, 1);
  assert.equal(printer.connected, false);
});

test('cover status and wrong model stop print before raster', async () => {
  const device = new Device();
  mock(device);
  const printer = new Printer(undefined, 'en');
  device.model = 'P1';
  await assert.rejects(printer.connect(), /Select a Paperang P2/);
  device.model = 'P2';
  await printer.connect();
  device.media = 16;
  await assert.rejects(printer.print(new Uint8Array(72)), /cover/);
  assert.ok(!device.commands.some((p) => p.cmd === 0));
  assert.equal(printer.connected, false);
});

test('credit starvation pauses until disconnect, without replay', async () => {
  const device = new Device();
  mock(device);
  const printer = new Printer(undefined, 'en');
  await printer.connect();
  device.credit = false;
  const count = device.chunks.length;
  const job = printer.print(new Uint8Array(72));
  await new Promise((resolve) => setTimeout(resolve, 50));
  assert.equal(device.chunks.length, count + 1);
  device.gatt.disconnect();
  await assert.rejects(job, /disconnected/);
  assert.equal(printer.connected, false);
  assert.ok(!device.commands.some((p) => p.cmd === 0));
});
