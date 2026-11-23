export const WIDTH = 576,
  STRIDE = 72,
  MAX_ROWS = 1400;
const bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

export function convert(pixels, width, height, s) {
  const levels = new Float32Array(width * height);
  for (let i = 0; i < levels.length; i++) {
    const p = i * 4;
    const gray = 0.2126 * pixels[p] + 0.7152 * pixels[p + 1] + 0.0722 * pixels[p + 2];
    const tone = Math.max(0, Math.min(255, (gray - 127.5) * s.contrast + 127.5 + s.brightness * 2.55));
    const value = 255 * (tone / 255) ** (1 / s.gamma);
    levels[i] = s.invert ? 255 - value : value;
  }
  const rows = new Uint8Array(STRIDE * (s.top + height + s.bottom));
  let last = -1;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const i = y * width + x;
      const threshold =
        s.method === 'ordered' ? (bayer[(y % 4) * 4 + (x % 4)] + 0.5) * 16 + s.threshold - 128 : s.threshold;
      const black = levels[i] < threshold;
      if (black) {
        const col = s.left + x;
        rows[(s.top + y) * STRIDE + (col >> 3)] |= 128 >> col % 8;
        last = y;
      }
      if (s.method !== 'floyd') continue;
      const error = (levels[i] - (black ? 0 : 255)) / 16;
      if (x + 1 < width) levels[i + 1] += error * 7;
      if (y + 1 === height) continue;
      if (x > 0) levels[i + width - 1] += error * 3;
      levels[i + width] += error * 5;
      if (x + 1 < width) levels[i + width + 1] += error;
    }
  }
  if (last < 0) return null;
  return s.trim ? rows.subarray(0, (s.top + last + 1 + s.bottom) * STRIDE) : rows;
}

export function paintRaster(canvas, bytes) {
  canvas.width = WIDTH;
  canvas.height = bytes ? bytes.length / STRIDE : 1;
  const ctx = canvas.getContext('2d');
  const image = ctx.createImageData(canvas.width, canvas.height);
  image.data.fill(255);
  if (bytes)
    for (let i = 0; i < bytes.length * 8; i++) {
      if (bytes[i >> 3] & (128 >> i % 8)) image.data.fill(0, i * 4, i * 4 + 3);
    }
  ctx.putImageData(image, 0, 0);
}

function base64(bytes) {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 4096) binary += String.fromCharCode(...bytes.subarray(i, i + 4096));
  return btoa(binary);
}
async function checksum(bytes) {
  const hash = new Uint8Array(await crypto.subtle.digest('SHA-256', bytes));
  return Array.from(hash, (n) => n.toString(16).padStart(2, '0')).join('');
}
export async function encodeRaster(bytes) {
  return {
    type: 'raster1',
    schemaVersion: 1,
    width: WIDTH,
    height: bytes.length / STRIDE,
    encoding: 'base64',
    bitOrder: 'msb-first',
    blackIsOne: true,
    data: base64(bytes),
    sha256: await checksum(bytes),
  };
}
export async function decodeRaster(obj, t) {
  if (
    obj.type !== 'raster1' ||
    obj.schemaVersion !== 1 ||
    obj.width !== WIDTH ||
    !Number.isInteger(obj.height) ||
    obj.height < 1 ||
    obj.height > MAX_ROWS ||
    obj.encoding !== 'base64' ||
    obj.bitOrder !== 'msb-first' ||
    obj.blackIsOne !== true ||
    typeof obj.data !== 'string' ||
    obj.data.length !== 4 * Math.ceil((obj.height * STRIDE) / 3) ||
    !/^[A-Za-z0-9+/]*={0,2}$/.test(obj.data)
  )
    throw Error(t('Invalid raster1 file.', 'raster1 文件格式无效。'));
  const bytes = Uint8Array.from(atob(obj.data), (c) => c.charCodeAt(0));
  if (
    base64(bytes) !== obj.data ||
    bytes.length !== obj.height * STRIDE ||
    (await checksum(bytes)) !== obj.sha256
  )
    throw Error(t('Bitmap length or checksum mismatch.', '点阵长度或校验值不符。'));
  return bytes;
}
export function readPreset(o, t) {
  function invalid(key) {
    throw Error(t('Invalid preset field: ', '预设字段无效：') + key);
  }
  function num(key, min, max, integer = true) {
    const v = o[key];
    if (
      typeof v !== 'number' ||
      !Number.isFinite(v) ||
      v < min ||
      v > max ||
      (integer && !Number.isInteger(v))
    )
      invalid(key);
    return v;
  }
  function bool(key) {
    if (typeof o[key] !== 'boolean') invalid(key);
    return o[key];
  }
  function choice(key, map) {
    if (!Object.hasOwn(map, o[key])) invalid(key);
    return map[o[key]];
  }
  num('version', 1, 1);
  const left = num('marginX', 0, 120),
    top = num('marginY', 0, 200);
  if (typeof o.fontName !== 'string' || !o.fontName.trim() || o.fontName.length > 256) invalid('fontName');
  const rotation = num('rotation', 0, 270);
  if (rotation % 90) invalid('rotation');
  const method = choice('conversion', { 阈值: 'threshold', 'Floyd–Steinberg': 'floyd', 有序抖动: 'ordered' });
  return {
    external: {
      left,
      right: left,
      top,
      bottom: top,
      threshold: num('threshold', 0, 255),
      dither: method === 'floyd',
      before: num('feedBefore', 0, 2000),
      after: num('feedAfter', 0, 2000),
      between: bool('feedBetweenCopies'),
      density: num('density', 0, 100),
      copies: num('copies', 1, 20),
      interval: num('writePauseMS', 0, 1000),
      chunk: num('bleChunk', 1, 180),
    },
    internal: {
      size: num('fontSize', 8, 96, false),
      font: o.fontName,
      bold: bool('bold'),
      space: num('lineSpacing', 0, 40, false),
      align: choice('alignment', { 居左: 'left', 居中: 'center', 居右: 'right' }),
      width: num('contentWidth', 48, WIDTH - left * 2),
      fit: choice('imageFit', { 适配宽度: 'fit', 保持像素: 'original', 裁切填满: 'crop' }),
      rotation,
      crop: num('cropHeight', 24, 1400),
      method,
      brightness: num('brightness', -100, 100, false),
      contrast: num('contrast', 0.2, 3, false),
      gamma: num('gamma', 0.2, 3, false),
      invert: bool('invert'),
    },
  };
}
