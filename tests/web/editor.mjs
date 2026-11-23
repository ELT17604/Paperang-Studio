import assert from 'node:assert/strict';
import { test } from 'node:test';
import { convert, encodeRaster, decodeRaster, readPreset } from '../../Web/raster.js';
const t = (en) => en;
const tone = {
  contrast: 1,
  brightness: 0,
  gamma: 1,
  invert: false,
  threshold: 128,
  method: 'threshold',
  left: 0,
  top: 0,
  bottom: 0,
  trim: true,
};
function pixels(width, height, black) {
  const result = new Uint8Array(width * height * 4).fill(255);
  for (const pos of black) result.fill(0, pos * 4, pos * 4 + 3);
  return result;
}
test('576 columns, MSB-first, trimming and padding', () => {
  const image = pixels(576, 3, [0, 7, 8, 575]);
  const data = convert(image, 576, 3, { ...tone, top: 1, bottom: 2 });
  assert.equal(data.length, 72 * 4);
  assert.equal(data[72], 0x81);
  assert.equal(data[73], 0x80);
  assert.equal(data[143], 1);
  assert.equal(data.filter(Boolean).length, 3);
  assert.equal(convert(image, 576, 3, { ...tone, trim: false }).length, 216);
  assert.equal(convert(pixels(1, 1, [0]), 1, 1, { ...tone, left: 15 })[1], 1);
  assert.equal(convert(pixels(2, 2, []), 2, 2, tone), null);
});
test('JSON round-trip and rejected corrupt imports', async () => {
  const bytes = convert(pixels(576, 3, [1, 577]), 576, 3, tone);
  const obj = await encodeRaster(bytes);
  assert.deepEqual(await decodeRaster(obj, t), bytes);
  for (const changes of [
    { data: 'AAAA' },
    { height: 1401 },
    { sha256: '0'.repeat(64) },
    { bitOrder: 'lsb-first' },
    { schemaVersion: '1' },
  ]) {
    await assert.rejects(() => decodeRaster({ ...obj, ...changes }, t));
  }
});
test('native workspace preset enums and parameter ranges', () => {
  const p = {
    version: 1,
    fontName: 'sans-serif',
    fontSize: 30,
    bold: false,
    lineSpacing: 4,
    alignment: '居中',
    marginX: 0,
    marginY: 0,
    contentWidth: 576,
    imageFit: '适配宽度',
    cropHeight: 400,
    rotation: 90,
    conversion: '有序抖动',
    threshold: 128,
    brightness: 0,
    contrast: 1,
    gamma: 1,
    invert: false,
    density: 100,
    copies: 1,
    feedBefore: 0,
    feedAfter: 160,
    feedBetweenCopies: false,
    bleChunk: 180,
    writePauseMS: 10,
  };
  const result = readPreset(p, t);
  assert.equal(result.internal.method, 'ordered');
  assert.equal(result.internal.rotation, 90);
  assert.equal(result.external.after, 160);
  for (const changes of [
    { gamma: 0 },
    { rotation: 45 },
    { marginX: 40 },
    { threshold: '128' },
    { bold: 1 },
    { imageFit: null },
  ])
    assert.throws(() => readPreset({ ...p, ...changes }, t));
});
