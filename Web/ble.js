const SERVICE = 0xff00;
const RX = 0xff01;
const TX = 0xff02;
const FLOW = 0xff03;
const FRAME_BYTES = 1008;
const MAX_CHUNK = 180;
const IO_TIMEOUT = 6000;
const FINISH_WAIT = 2000;

const messages = {
  unavailable: [
    'Open in a browser with Web Bluetooth over HTTPS or localhost.',
    '请通过 HTTPS 或 localhost 在支持网页蓝牙的浏览器中打开。',
  ],
  offline: ['Printer disconnected.', '打印机已断开。'],
  busy: ['Printer is busy.', '打印机正忙。'],
  timeout: ['Printer timed out.', '打印机响应超时。'],
  model: ['Select a Paperang P2.', '请选择 Paperang P2。'],
  writer: ['P2 write characteristic unavailable.', '设备缺少 P2 写入特征。'],
  rejected: ['Printer rejected the command.', '打印机拒绝了指令。'],
  paper: ['Load a roll of paper.', '请装入纸卷。'],
  lid: ['Close the printer cover.', '请合上纸仓盖。'],
  media: ['Invalid paper status.', '纸仓状态响应无效。'],
  density: ['Print density readback failed.', '打印浓度回读失败。'],
  options: ['Check print settings.', '请检查打印参数。'],
  raster: ['Invalid print bitmap.', '打印点阵格式无效。'],
};

function crc32(bytes, seed) {
  let crc = seed ^ -1;
  for (const byte of bytes) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit++) {
      crc = (crc >>> 1) ^ (crc & 1 ? 0xedb88320 : 0);
    }
  }
  return (crc ^ -1) >>> 0;
}

export function frame(command, index, payload, seed = 0) {
  const bytes = new Uint8Array(payload.length + 10);
  const view = new DataView(bytes.buffer);
  bytes.set([2, command, index]);
  view.setUint16(3, payload.length, true);
  bytes.set(payload, 5);
  view.setUint32(5 + payload.length, crc32(payload, seed), true);
  bytes[bytes.length - 1] = 3;
  return bytes;
}

export class Decoder {
  bytes = new Uint8Array();

  push(fragment, receive) {
    const bytes = new Uint8Array(this.bytes.length + fragment.length);
    bytes.set(this.bytes);
    bytes.set(fragment, this.bytes.length);
    const view = new DataView(bytes.buffer);
    let start = 0;
    while (bytes.length - start >= 10) {
      const length = view.getUint16(start + 3, true);
      if (bytes[start] !== 2 || length > 1023) {
        start++;
        continue;
      }
      const end = start + 10 + length;
      if (bytes.length < end) break;
      if (bytes[end - 1] !== 3) {
        start++;
        continue;
      }
      const payload = bytes.subarray(start + 5, end - 5);
      if (view.getUint32(end - 5, true) === crc32(payload, 0)) receive(bytes[start + 1], payload);
      start = end;
    }
    this.bytes = bytes.slice(start);
  }
}

function notification(event) {
  const value = event.target.value;
  return new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
}

class Session {
  controller = new AbortController();
  changes = new EventTarget();
  decoder = new Decoder();
  credits = 0;
  printerLimit = 20;
  lastWrite = -Infinity;
  interval = 10;
  chunk = MAX_CHUNK;
  pending = null;
  media = -1;

  constructor(error) {
    this.error = error;
  }

  // Bound writes and credit/ACK waits independently. A slow user-selected pace
  // must not time out a healthy frame that is still making progress.
  wait(start, timeout = IO_TIMEOUT) {
    const signal = this.controller.signal;
    signal.throwIfAborted();
    return new Promise((resolve, reject) => {
      let cleanup;
      const finish = (complete, value) => {
        clearTimeout(timer);
        signal.removeEventListener('abort', cancel);
        cleanup?.();
        complete(value);
      };
      const cancel = () => finish(reject, signal.reason);
      const timer = setTimeout(() => finish(reject, this.error('timeout')), timeout);
      signal.addEventListener('abort', cancel, { once: true });
      try {
        cleanup = start(
          (value) => finish(resolve, value),
          (error) => finish(reject, error),
        );
      } catch (error) {
        finish(reject, error);
      }
    });
  }

  io(action, timeout) {
    return this.wait((resolve, reject) => {
      Promise.resolve(action()).then(resolve, reject);
    }, timeout);
  }

  delay(ms) {
    if (ms <= 0) return;
    return this.wait((resolve) => {
      const timer = setTimeout(resolve, Math.ceil(ms));
      return () => clearTimeout(timer);
    }, ms + IO_TIMEOUT);
  }

  async until(predicate) {
    const deadline = performance.now() + IO_TIMEOUT;
    while (!predicate()) {
      const remaining = deadline - performance.now();
      if (remaining <= 0) throw this.error('timeout');
      await this.wait((resolve) => {
        this.changes.addEventListener('change', resolve, { once: true });
        return () => this.changes.removeEventListener('change', resolve);
      }, remaining);
    }
  }

  async subscribe(service, id, handler) {
    const characteristic = await this.io(() => service.getCharacteristic(id));
    characteristic.addEventListener(
      'characteristicvaluechanged',
      (event) => {
        handler(notification(event));
        this.changes.dispatchEvent(new Event('change'));
      },
      { signal: this.controller.signal },
    );
    await this.io(() => characteristic.startNotifications());
  }

  async open() {
    const server = await this.io(() => this.device.gatt.connect(), 20000);
    const service = await this.io(() => server.getPrimaryService(SERVICE));
    this.writer = await this.io(() => service.getCharacteristic(TX));
    if (!this.writer.properties.writeWithoutResponse) throw this.error('writer');
    await this.subscribe(service, RX, (bytes) =>
      this.decoder.push(bytes, (cmd, data) => this.receive(cmd, data)),
    );
    await this.subscribe(service, FLOW, (bytes) => {
      if (bytes[0] === 1 && bytes.length >= 2) this.credits += bytes[1];
      if (bytes[0] === 2 && bytes.length >= 3) {
        const limit = bytes[1] | (bytes[2] << 8);
        if (limit > 0) this.printerLimit = Math.min(MAX_CHUNK, limit);
      }
    });
    await this.send(24, [0x21, 0x95, 0x76, 0x35], { seed: 0x35769521 });
    const model = new TextDecoder()
      .decode(await this.query(6))
      .replace(/\0+$/, '')
      .trim();
    if (!['Paperang_P2', 'P2'].includes(model)) throw this.error('model');
    await this.query(12);
    await this.query(16);
    await this.query(28);
  }

  receive(command, payload) {
    if (command === 13) this.media = payload.length === 2 ? payload[0] | (payload[1] << 8) : -1;
    const pending = this.pending;
    if (!pending) return;
    if (pending.ackRequired && command === pending.command && payload.length === 1) {
      pending.ack = payload[0];
    }
    if (command === pending.responseCode) pending.response = payload;
  }

  checkMedia() {
    if (this.media < 0) throw this.error('media');
    if (this.media & 0x10) throw this.error('lid');
    if (this.media & 1) throw this.error('paper');
  }

  async send(command, payload, { index = 0, seed = 0, responseCode = -1, ackRequired = true } = {}) {
    const pending = { command, responseCode, ackRequired, ack: null, response: null };
    this.pending = pending;
    const bytes = frame(command, index, payload, seed);
    try {
      for (let offset = 0; offset < bytes.length; ) {
        await this.until(() => this.credits > 0 || (command === 0 && this.media & 0x11));
        await this.delay(this.lastWrite + this.interval - performance.now());
        if (command === 0) this.checkMedia();
        const count = Math.min(this.chunk, this.printerLimit, bytes.length - offset);
        this.credits--;
        this.lastWrite = performance.now();
        await this.io(() => this.writer.writeValueWithoutResponse(bytes.subarray(offset, offset + count)));
        offset += count;
      }
      await this.until(() => {
        if (ackRequired && pending.ack !== null && pending.ack !== 0) throw this.error('rejected');
        return (!ackRequired || pending.ack === 0) && (responseCode < 0 || pending.response !== null);
      });
      return pending.response;
    } finally {
      this.pending = null;
    }
  }

  query(command) {
    return this.send(command, [1], { responseCode: command + 1 });
  }

  async feed(lines) {
    if (lines) await this.send(26, [lines & 255, lines >>> 8]);
  }
}

function integer(value, min, max) {
  return Number.isInteger(value) && value >= min && value <= max;
}

export class Printer {
  session = null;
  ready = false;
  printing = false;

  constructor(onDisconnect = () => {}, language = () => globalThis.navigator?.language || 'en') {
    this.onDisconnect = onDisconnect;
    this.language = language;
  }

  error(key) {
    const lang = typeof this.language === 'function' ? this.language() : this.language;
    return new Error(messages[key][/^zh/i.test(lang) ? 1 : 0]);
  }

  get connected() {
    return this.ready && Boolean(this.session?.device?.gatt?.connected);
  }

  async connect() {
    if (this.session) throw this.error('busy');
    if (!globalThis.navigator?.bluetooth?.requestDevice) throw this.error('unavailable');
    const session = new Session((key) => this.error(key));
    this.session = session;
    try {
      const device = await navigator.bluetooth.requestDevice({
        filters: [
          { services: [SERVICE] },
          { namePrefix: 'Paperang' },
          { namePrefix: 'PAPERANG' },
          { namePrefix: 'paperang' },
          { name: 'P2' },
        ],
        optionalServices: [SERVICE],
      });
      session.controller.signal.throwIfAborted();
      session.device = device;
      device.addEventListener('gattserverdisconnected', () => this.close(session), {
        signal: session.controller.signal,
      });
      await session.open();
      session.controller.signal.throwIfAborted();
      this.ready = true;
      return device.name || 'Paperang P2';
    } catch (error) {
      this.close(session);
      throw error;
    }
  }

  close(session) {
    if (!session || session !== this.session) return;
    const notify = this.ready;
    this.session = null;
    this.ready = false;
    this.printing = false;
    session.controller.abort(this.error('offline'));
    session.device?.gatt?.disconnect();
    if (notify) this.onDisconnect();
  }

  disconnect() {
    this.close(this.session);
  }

  async print(bytes, options = {}, onProgress = () => {}) {
    if (!this.connected) throw this.error('offline');
    if (this.printing) throw this.error('busy');
    if (!(bytes instanceof Uint8Array) || !bytes.length || bytes.length % 72) throw this.error('raster');
    const {
      density = 100,
      copies = 1,
      before = 0,
      after = 160,
      between = false,
      interval = 10,
      chunk = MAX_CHUNK,
    } = options;
    if (
      !integer(density, 0, 100) ||
      !integer(copies, 1, 20) ||
      !integer(before, 0, 2000) ||
      !integer(after, 0, 2000) ||
      !integer(interval, 0, 1000) ||
      !integer(chunk, 1, MAX_CHUNK) ||
      typeof between !== 'boolean'
    )
      throw this.error('options');
    const session = this.session;
    const raster = bytes.slice();
    this.printing = true;
    session.interval = interval;
    session.chunk = chunk;
    try {
      await session.query(12);
      session.checkMedia();
      await session.send(25, [density]);
      const reading = await session.query(28);
      if (reading.length !== 1 || reading[0] !== density) throw this.error('density');
      await session.feed(before);
      for (let copy = 0; copy < copies; copy++) {
        await session.query(12);
        session.checkMedia();
        for (let start = 0; start < raster.length; start += FRAME_BYTES) {
          const end = Math.min(start + FRAME_BYTES, raster.length);
          await session.send(0, raster.subarray(start, end), {
            index: start / FRAME_BYTES,
            ackRequired: false,
          });
          onProgress((copy + end / raster.length) / copies);
        }
        await session.delay(FINISH_WAIT);
        session.checkMedia();
        if (between || copy === copies - 1) await session.feed(after);
      }
      await session.query(12);
      session.checkMedia();
    } catch (error) {
      this.close(session);
      throw error;
    } finally {
      if (session === this.session) this.printing = false;
    }
  }
}
