import {
  WIDTH,
  MAX_ROWS,
  STRIDE,
  convert,
  paintRaster,
  decodeRaster,
  encodeRaster,
  readPreset,
} from './raster.js';
import qrcode from './vendor/qrcode.mjs';

qrcode.stringToBytes = (value) => Array.from(new TextEncoder().encode(value));
const modes = {
  text: ['Text', '文本'],
  image: ['Image', '图片'],
  layout: ['Layout', '高级排版'],
  draw: ['Draw', '手绘'],
  json: ['JSON', 'JSON'],
};
const kinds = { text: modes.text, label: ['Label', '标签'], image: modes.image, qr: ['QR code', '二维码'] };
const families = [
  ['sans-serif', 'Sans serif', '无衬线'],
  ['serif', 'Serif', '衬线'],
  ['monospace', 'Monospace', '等宽'],
];
const alignments = [
  ['left', 'Left', '左对齐'],
  ['center', 'Center', '居中'],
  ['right', 'Right', '右对齐'],
];
const defaultStyle = { font: 'sans-serif', size: 30, space: 4, bold: false, align: 'left' };
const styleFont = (font) =>
  ['serif', 'sans-serif', 'monospace'].includes(font) ? font : JSON.stringify(font);
function makeCanvas(w, h) {
  const cv = document.createElement('canvas');
  cv.width = w;
  cv.height = h;
  const ctx = cv.getContext('2d', { willReadFrequently: true });
  ctx.fillStyle = 'white';
  ctx.fillRect(0, 0, w, h);
  ctx.fillStyle = 'black';
  return cv;
}
function font(ctx, style) {
  ctx.font = (style.bold ? 'bold ' : '') + style.size + 'px ' + styleFont(style.font);
}
function lines(ctx, value, width) {
  const result = [];
  for (const paragraph of value.replaceAll('\r\n', '\n').split('\n')) {
    let line = '';
    for (const char of paragraph) {
      if (line && ctx.measureText(line + char).width > width) {
        result.push(line);
        line = '';
      }
      line += char;
    }
    result.push(line);
  }
  return result;
}
function drawText(ctx, content, x, y, width, style) {
  font(ctx, style);
  ctx.textBaseline = 'top';
  ctx.textAlign = style.align;
  const start = x + (style.align === 'left' ? 0 : style.align === 'center' ? width / 2 : width);
  const step = Math.ceil(style.size * 1.22 + style.space);
  for (const [i, line] of lines(ctx, content, width).entries()) ctx.fillText(line, start, y + i * step);
}
function drawPhoto(
  ctx,
  img,
  x,
  y,
  width,
  height,
  cover = false,
  rotation = 0,
  original = false,
  align = 'center',
) {
  const sideways = rotation % 180 !== 0,
    w = sideways ? img.height : img.width,
    h = sideways ? img.width : img.height;
  const scale = cover
    ? Math.max(width / w, height / h)
    : Math.min(original ? 1 : Infinity, width / w, height / h);
  const shift = align === 'left' ? 0 : align === 'right' ? 1 : 0.5;
  ctx.save();
  ctx.beginPath();
  ctx.rect(x, y, width, height);
  ctx.clip();
  ctx.translate(x + (width - w * scale) * shift + (w * scale) / 2, y + height / 2);
  ctx.rotate((rotation * Math.PI) / 180);
  ctx.drawImage(
    img,
    (-img.width * scale) / 2,
    (-img.height * scale) / 2,
    img.width * scale,
    img.height * scale,
  );
  ctx.restore();
}
function drawMark(ctx, layer, t) {
  if (!layer.value.trim()) return;
  let payload = layer.value.trim();
  if (layer.format === 'url') {
    let parsed;
    try {
      parsed = new URL(payload);
    } catch {}
    if (!parsed || !['https:', 'http:'].includes(parsed.protocol))
      throw Error(t('Enter an HTTP or HTTPS URL.', '请输入 HTTP 或 HTTPS 链接。'));
    payload = parsed.href;
  } else if (layer.format === 'email') {
    if (!/^[^@\s?]+@[^@\s?]+$/.test(payload))
      throw Error(t('Enter a valid email address.', '请输入有效的邮件地址。'));
    payload = 'mailto:' + payload;
  }
  const code = qrcode(0, layer.correction);
  try {
    code.addData(payload, 'Byte');
    code.make();
  } catch {
    throw Error(t('QR content is too long.', '二维码内容过长。'));
  }
  const size = code.getModuleCount(),
    step = Math.floor(Math.min(layer.w, layer.h) / (size + 8));
  if (step < 3)
    throw Error(t('Enlarge the QR layer or shorten its content.', '请放大二维码图层或缩短内容。'));
  ctx.fillStyle = 'white';
  ctx.fillRect(0, 0, layer.w, layer.h);
  ctx.fillStyle = 'black';
  const left = Math.floor((layer.w - size * step) / 2),
    top = Math.floor((layer.h - size * step) / 2);
  for (let row = 0; row < size; row++)
    for (let col = 0; col < size; col++) {
      if (!code.isDark(row, col)) continue;
      const x = left + col * step,
        y = top + row * step;
      const fixed =
        row === 6 || col === 6 || (row < 9 && (col < 9 || col >= size - 9)) || (col < 9 && row >= size - 9);
      if (fixed || layer.shape === 'square') ctx.fillRect(x, y, step, step);
      else {
        ctx.beginPath();
        if (layer.shape === 'dot') ctx.arc(x + step / 2, y + step / 2, step / 2, 0, 2 * Math.PI);
        else ctx.roundRect(x, y, step, step, step * 0.22);
        ctx.fill();
      }
    }
  if (layer.border) {
    ctx.strokeStyle = 'black';
    ctx.strokeRect(0.5, 0.5, layer.w - 1, layer.h - 1);
  }
}
function drawLayer(ctx, l, t) {
  ctx.save();
  try {
    ctx.translate(l.x, l.y);
    ctx.beginPath();
    ctx.rect(0, 0, l.w, l.h);
    ctx.clip();
    if (l.kind === 'qr') drawMark(ctx, l, t);
    else if (l.kind === 'image') drawPhoto(ctx, l.image, 0, 0, l.w, l.h);
    else {
      const pad = l.kind === 'label' ? 8 : 0;
      if (pad) {
        ctx.fillStyle = 'white';
        ctx.fillRect(0, 0, l.w, l.h);
        ctx.strokeStyle = 'black';
        ctx.lineWidth = 2;
        ctx.strokeRect(1, 1, l.w - 2, l.h - 2);
      }
      ctx.fillStyle = 'black';
      drawText(ctx, l.value, pad, pad, Math.max(1, l.w - 2 * pad), l);
    }
  } finally {
    ctx.restore();
  }
}
function drawStroke(ctx, s) {
  ctx.strokeStyle = ctx.fillStyle = s.erase ? 'white' : 'black';
  ctx.lineWidth = s.size;
  ctx.lineCap = ctx.lineJoin = 'round';
  ctx.beginPath();
  ctx.moveTo(...s.points[0]);
  if (s.points.length === 1) {
    ctx.arc(...s.points[0], s.size / 2, 0, 2 * Math.PI);
    ctx.fill();
  } else {
    for (let i = 1; i < s.points.length; i++) ctx.lineTo(...s.points[i]);
    ctx.stroke();
  }
}
async function loadPhoto(file, t) {
  if (file.size > 16 * 1024 * 1024)
    throw Error(t('Choose an image under 16 MB.', '请选择小于 16 MB 的图片。'));
  const url = URL.createObjectURL(file),
    img = new Image();
  try {
    img.src = url;
    await img.decode();
  } catch {
    throw Error(t('Unable to read this image.', '无法读取此图片。'));
  } finally {
    URL.revokeObjectURL(url);
  }
  if (img.width * img.height > 40000000)
    throw Error(t('Image exceeds 40 megapixels.', '图片超过 4000 万像素。'));
  return img;
}

export function mountEditor(container, preview, { t, onChange, onError, onSettings }) {
  let mode = 'text',
    value = '',
    photo = null,
    photoName = '',
    raw = null,
    rawName = '',
    jsonText = '',
    selected = -1;
  let layoutHeight = 540,
    drawHeight = 360,
    penSize = 6,
    erase = false,
    stage = null,
    output = null,
    scheduled = 0,
    revision = 0;
  let statusMessage = '',
    lastError = '',
    gesture = null;
  const layers = [],
    strokes = [],
    redo = [];
  const s = {
    ...defaultStyle,
    width: 576,
    fit: 'fit',
    rotation: 0,
    crop: 400,
    method: 'threshold',
    brightness: 0,
    contrast: 1,
    gamma: 1,
    invert: false,
    threshold: 128,
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    trim: true,
  };
  const node = (tag, cls, txt) => {
    const el = document.createElement(tag);
    if (cls) el.className = cls;
    if (txt !== undefined) el.textContent = txt;
    return el;
  };
  const tabs = node('nav', 'ed-tabs'),
    panel = node('div', 'ed-panel'),
    notice = node('div', 'ed-notice');
  notice.setAttribute('role', 'status');
  container.append(tabs, panel, notice);
  const group = (parent, cls) => {
    const el = node('div', cls);
    parent.append(el);
    return el;
  };
  function button(parent, txt, act, active = false) {
    const el = node('button', 'ed-btn', txt);
    el.type = 'button';
    if (active) el.classList.add('selected');
    el.onclick = act;
    parent.append(el);
    return el;
  }
  function field(parent, title, input) {
    const label = node('label', 'ed-field');
    label.append(node('span', null, title), input);
    parent.append(label);
    return input;
  }
  function number(parent, title, obj, key, min, max, step = 1) {
    const input = node('input');
    Object.assign(input, { type: 'number', value: obj[key], min, max, step });
    input.onchange = () => {
      if (!input.checkValidity() || input.value === '') {
        input.value = obj[key];
        return;
      }
      obj[key] = Number(input.value);
      schedule();
    };
    return field(parent, title, input);
  }
  function select(parent, title, obj, key, choices) {
    const input = node('select');
    for (const [id, en, zh] of choices) input.add(new Option(t(en, zh), id));
    input.value = obj[key];
    input.onchange = () => {
      obj[key] = input.value;
      schedule();
    };
    return field(parent, title, input);
  }
  function check(parent, title, obj, key) {
    const input = node('input');
    input.type = 'checkbox';
    input.checked = obj[key];
    input.onchange = () => {
      obj[key] = input.checked;
      schedule();
    };
    return field(parent, title, input);
  }
  function area(parent, title, content, change, hint, rows = 5) {
    const el = node('textarea', 'ed-text');
    el.rows = rows;
    el.setAttribute('aria-label', title);
    el.value = content;
    el.placeholder = hint;
    el.oninput = () => {
      change(el.value);
      schedule();
    };
    parent.append(el);
    return el;
  }
  function fileButton(parent, title, accept, act) {
    const el = node('input');
    el.type = 'file';
    el.accept = accept;
    el.hidden = true;
    const btn = button(parent, title, () => el.click());
    parent.append(el);
    el.onchange = async () => {
      const file = el.files[0];
      el.value = '';
      if (!file) return;
      const token = ++revision;
      try {
        await act(file, () => revision === token);
      } catch (e) {
        if (revision === token) onError(e.message);
      }
    };
    return btn;
  }
  function textControls(parent, style) {
    const g = group(parent, 'ed-fields');
    const choices = families.some((f) => f[0] === style.font)
      ? families
      : [...families, [style.font, style.font, style.font]];
    select(g, t('Typeface', '字体'), style, 'font', choices);
    number(g, t('Size (px)', '字号 (px)'), style, 'size', 8, 96);
    select(g, t('Alignment', '对齐'), style, 'align', alignments);
    number(g, t('Line gap (px)', '行距 (px)'), style, 'space', 0, 40);
    check(g, t('Bold', '粗体'), style, 'bold');
  }
  function fold(parent, name) {
    const d = node('details', 'ed-fold');
    d.append(node('summary', null, name));
    parent.append(d);
    return d;
  }
  function tones(parent) {
    const g = group(fold(parent, t('Image adjustments', '图像调整')), 'ed-fields');
    number(g, t('Brightness', '亮度'), s, 'brightness', -100, 100);
    number(g, t('Contrast', '对比度'), s, 'contrast', 0.2, 3, 0.1);
    number(g, t('Gamma', '伽马'), s, 'gamma', 0.2, 3, 0.1);
    check(g, t('Invert', '反相'), s, 'invert');
    const selector = select(g, t('Conversion', '转换方式'), s, 'method', [
      ['threshold', 'Threshold', '阈值'],
      ['floyd', 'Floyd–Steinberg', '误差扩散'],
      ['ordered', 'Ordered', '有序抖动'],
    ]);
    selector.onchange = () => {
      const method = selector.value;
      onSettings?.({ dither: method === 'floyd' });
      s.method = method;
      schedule();
    };
  }
  function addLayer(kind, image = null, name = '') {
    const offset = Math.min(12 * layers.length, 100);
    const layer = {
      ...defaultStyle,
      kind,
      x: offset,
      y: offset,
      w: kind === 'qr' ? 216 : 280,
      h: kind === 'qr' ? 216 : kind === 'image' ? 180 : 76,
      value:
        kind === 'label'
          ? t('Label', '标签')
          : kind === 'text'
            ? t('Text', '文本')
            : kind === 'qr'
              ? 'Paperang Studio'
              : '',
      image,
      name,
      correction: 'M',
      format: 'text',
      shape: 'square',
      border: false,
    };
    layers.push(layer);
    selected = layers.length - 1;
    build();
  }
  function surface(height) {
    stage = makeCanvas(WIDTH, height);
    stage.className = 'ed-stage';
    stage.setAttribute('aria-label', t('Paper canvas', '编辑画布'));
    panel.append(stage);
    stage.onpointerdown = startGesture;
    stage.onpointermove = moveGesture;
    stage.onpointerup = finishGesture;
    stage.onpointercancel = finishGesture;
    stage.onlostpointercapture = finishGesture;
  }
  function point(e) {
    const r = stage.getBoundingClientRect();
    return [
      Math.max(0, Math.min(575, Math.round(((e.clientX - r.left) * WIDTH) / r.width))),
      Math.max(0, Math.min(stage.height - 1, Math.round(((e.clientY - r.top) * stage.height) / r.height))),
    ];
  }
  function startGesture(e) {
    if (gesture || e.button !== 0) return;
    const [x, y] = point(e);
    if (mode === 'layout') {
      selected = layers.findLastIndex((l) => x >= l.x && x < l.x + l.w && y >= l.y && y < l.y + l.h);
      if (selected < 0) {
        build();
        return;
      }
      const l = layers[selected];
      gesture = { id: e.pointerId, dx: x - l.x, dy: y - l.y };
    } else {
      const stroke = { size: penSize, erase, points: [[x, y]] };
      strokes.push(stroke);
      redo.length = 0;
      gesture = { id: e.pointerId, stroke };
    }
    stage.setPointerCapture(e.pointerId);
    schedule();
  }
  function moveGesture(e) {
    if (!gesture || gesture.id !== e.pointerId) return;
    const [x, y] = point(e);
    if (mode === 'draw') gesture.stroke.points.push([x, y]);
    else {
      const l = layers[selected];
      l.x = Math.max(0, Math.min(WIDTH - l.w, x - gesture.dx));
      l.y = Math.max(0, Math.min(layoutHeight - l.h, y - gesture.dy));
    }
    schedule();
  }
  function finishGesture(e) {
    if (gesture?.id !== e.pointerId) return;
    gesture = null;
    build();
  }
  function buildLayout() {
    const toolbar = group(panel, 'ed-tools');
    for (const k of ['text', 'label', 'qr']) button(toolbar, '+ ' + t(...kinds[k]), () => addLayer(k));
    fileButton(toolbar, '+ ' + t(...kinds.image), 'image/*', async (file, current) => {
      const im = await loadPhoto(file, t);
      if (current()) addLayer('image', im, file.name);
    });
    surface(layoutHeight);
    const props = group(panel, 'ed-fields');
    const height = number(
      props,
      t('Paper height (px)', '纸高 (px)'),
      { height: layoutHeight },
      'height',
      120,
      1400,
    );
    height.onchange = () => {
      if (height.checkValidity() && height.value) {
        layoutHeight = Number(height.value);
        build();
      }
    };
    if (!layers.length) return;
    const menu = node('select');
    layers.forEach((l, i) => menu.add(new Option(i + 1 + ' · ' + (l.name || t(...kinds[l.kind])), i)));
    menu.selectedIndex = selected;
    menu.onchange = () => {
      selected = menu.selectedIndex;
      build();
    };
    field(props, t('Layer', '图层'), menu);
    if (selected < 0) return;
    const l = layers[selected];
    const row = group(panel, 'ed-tools');
    function shift(by) {
      [layers[selected], layers[selected + by]] = [layers[selected + by], layers[selected]];
      selected += by;
      build();
    }
    button(row, t('Bring forward', '上移一层'), () => shift(1)).disabled = selected === layers.length - 1;
    button(row, t('Send backward', '下移一层'), () => shift(-1)).disabled = selected === 0;
    button(row, t('Duplicate', '复制图层'), () => {
      layers.splice(selected + 1, 0, {
        ...l,
        x: Math.min(WIDTH - 1, l.x + 8),
        y: Math.min(layoutHeight - 1, l.y + 8),
      });
      selected++;
      build();
    });
    button(row, t('Delete layer', '删除图层'), () => {
      layers.splice(selected, 1);
      selected = Math.min(selected, layers.length - 1);
      build();
    });
    if (l.kind !== 'image')
      area(
        panel,
        t('Layer content', '图层内容'),
        l.value,
        (v) => (l.value = v),
        t('Layer content', '图层内容'),
        2,
      ).maxLength = 10000;
    const geo = group(panel, 'ed-fields');
    number(geo, 'X', l, 'x', 0, 575);
    number(geo, 'Y', l, 'y', 0, 1399);
    number(geo, t('Width (px)', '宽 (px)'), l, 'w', 1, 576);
    number(geo, t('Height (px)', '高 (px)'), l, 'h', 1, 1400);
    if (l.kind === 'qr') {
      select(geo, t('Content type', '内容类型'), l, 'format', [
        ['text', 'Text', '文本'],
        ['url', 'URL', '网址'],
        ['email', 'Email', '电子邮件'],
      ]);
      select(
        geo,
        t('Correction', '纠错等级'),
        l,
        'correction',
        ['L', 'M', 'Q', 'H'].map((k) => [k, k, k]),
      );
      select(geo, t('Modules', '码点'), l, 'shape', [
        ['square', 'Square', '方形'],
        ['rounded', 'Rounded', '圆角'],
        ['dot', 'Dots', '圆点'],
      ]);
      check(geo, t('Border', '外框'), l, 'border');
    }
    if (l.kind === 'text' || l.kind === 'label') textControls(fold(panel, t('Typography', '文字样式')), l);
  }
  function buildDraw() {
    const opts = group(panel, 'ed-fields'),
      state = { size: penSize, height: drawHeight, tool: erase ? 'erase' : 'pen' };
    const tool = select(opts, t('Tool', '工具'), state, 'tool', [
      ['pen', 'Pen', '画笔'],
      ['erase', 'Eraser', '橡皮'],
    ]);
    tool.onchange = () => {
      erase = tool.value === 'erase';
    };
    const size = number(opts, t('Brush (px)', '笔宽 (px)'), state, 'size', 1, 96);
    size.onchange = () => {
      if (size.checkValidity() && size.value) penSize = Number(size.value);
    };
    const height = number(opts, t('Height (px)', '高度 (px)'), state, 'height', 32, 1400);
    height.onchange = () => {
      if (height.checkValidity() && height.value) {
        drawHeight = Number(height.value);
        build();
      }
    };
    surface(drawHeight);
    const g = group(panel, 'ed-tools');
    button(g, t('Undo', '撤销'), () => {
      redo.push(strokes.pop());
      build();
    }).disabled = !strokes.length;
    button(g, t('Redo', '重做'), () => {
      strokes.push(redo.pop());
      build();
    }).disabled = !redo.length;
    button(g, t('Add to layout', '插入排版'), () => {
      const cv = makeCanvas(WIDTH, drawHeight),
        ctx = cv.getContext('2d');
      strokes.forEach((stroke) => drawStroke(ctx, stroke));
      mode = 'layout';
      addLayer('image', cv, t('Drawing', '手绘'));
    }).disabled = !strokes.length;
  }
  async function importJSON(text, name, current) {
    if (new TextEncoder().encode(text).length > 144000)
      throw Error(t('JSON exceeds 144000 bytes.', 'JSON 超过 144000 字节。'));
    let data;
    try {
      data = JSON.parse(text);
    } catch {
      throw Error(t('Invalid JSON syntax.', 'JSON 语法无效。'));
    }
    if (!data || typeof data !== 'object')
      throw Error(t('Expected a raster1 file or preset.', '请选择 raster1 文件或参数预设。'));
    if (data.type === 'raster1') {
      const bytes = await decodeRaster(data, t);
      if (!current()) return;
      raw = bytes;
      rawName = name;
      statusMessage = 'raster';
    } else {
      const p = readPreset(data, t);
      if (!current()) return;
      onSettings?.(p.external);
      Object.assign(s, p.external, p.internal);
      statusMessage = 'preset';
    }
    build();
  }
  function clearImportedSheet() {
    raw = null;
    rawName = '';
    statusMessage = '';
    panel.querySelector('.ed-file')?.remove();
    render();
  }
  function build() {
    gesture = null;
    stage = null;
    tabs.replaceChildren();
    panel.replaceChildren();
    for (const [id, labels] of Object.entries(modes)) {
      const tab = button(
        tabs,
        t(...labels),
        () => {
          if (mode !== id) {
            mode = id;
            revision++;
            statusMessage = '';
            build();
          }
        },
        id === mode,
      );
      tab.setAttribute('aria-pressed', String(id === mode));
    }
    if (mode === 'text') {
      const a = area(
        panel,
        t('Note text', '文本内容'),
        value,
        (v) => (value = v),
        t('Write your note…', '在这里输入文本…'),
        7,
      );
      a.maxLength = 10000;
      textControls(panel, s);
    } else if (mode === 'image') {
      const g = group(panel, 'ed-tools');
      fileButton(g, t('Choose image', '选择图片'), 'image/*', async (file, current) => {
        const im = await loadPhoto(file, t);
        if (current()) {
          photo = im;
          photoName = file.name;
          build();
        }
      });
      if (photo) g.append(node('span', 'ed-file', photoName));
      const f = group(panel, 'ed-fields');
      select(f, t('Sizing', '缩放'), s, 'fit', [
        ['fit', 'Fit paper', '适应纸张'],
        ['original', 'Original pixels', '保持像素'],
        ['crop', 'Crop to fill', '裁切填满'],
      ]);
      select(f, t('Alignment', '对齐'), s, 'align', alignments);
      select(
        f,
        t('Rotate', '旋转'),
        s,
        'rotation',
        [0, 90, 180, 270].map((v) => [String(v), v + '°', v + '°']),
      );
      number(f, t('Content width (px)', '内容宽 (px)'), s, 'width', 48, 576);
      number(f, t('Crop height (px)', '裁切高 (px)'), s, 'crop', 24, 1400);
      tones(panel);
    } else if (mode === 'layout') buildLayout();
    else if (mode === 'draw') buildDraw();
    else {
      const g = group(panel, 'ed-tools');
      fileButton(g, t('Open JSON', '打开 JSON'), '.json,application/json', async (file, current) => {
        clearImportedSheet();
        if (file.size > 144000) throw Error(t('JSON exceeds 144000 bytes.', 'JSON 超过 144000 字节。'));
        await importJSON(await file.text(), file.name, current);
      });
      if (rawName) g.append(node('span', 'ed-file', rawName));
      const a = area(
        panel,
        t('Raster or preset JSON', '点阵或预设 JSON'),
        jsonText,
        (v) => {
          revision++;
          jsonText = v;
          clearImportedSheet();
        },
        t('Paste raster1 or preset JSON…', '粘贴 raster1 或参数预设 JSON…'),
      );
      a.maxLength = 144000;
      button(panel, t('Import JSON', '导入 JSON'), async () => {
        clearImportedSheet();
        const token = ++revision;
        try {
          await importJSON(jsonText, '', () => token === revision);
        } catch (e) {
          if (token === revision) onError(e.message);
        }
      });
    }
    if (mode !== 'json') {
      const btn = button(panel, t('Export bitmap JSON', '导出点阵 JSON'), async () => {
        if (!output) return;
        try {
          const blob = new Blob([JSON.stringify(await encodeRaster(output))], { type: 'application/json' });
          const url = URL.createObjectURL(blob),
            a = node('a');
          a.href = url;
          a.download = 'paperang-raster1.json';
          a.click();
          setTimeout(() => URL.revokeObjectURL(url), 30000);
        } catch (e) {
          onError(e.message);
        }
      });
      btn.classList.add('ed-export');
    }
    render();
  }
  function contentCanvas(width, maxHeight) {
    if (mode === 'text') {
      if (!value.trim()) return null;
      const measure = makeCanvas(1, 1).getContext('2d');
      font(measure, s);
      const n = lines(measure, value, width).length;
      const h = n * Math.ceil(s.size * 1.22 + s.space) - s.space;
      if (h > maxHeight)
        throw Error(
          t(
            'Text exceeds 1400 rows. Reduce the size or split the note.',
            '文本超过 1400 行，请缩小字号或拆分文本。',
          ),
        );
      const cv = makeCanvas(width, h);
      drawText(cv.getContext('2d'), value, 0, 0, width, s);
      return cv;
    }
    if (mode === 'image') {
      if (!photo) return null;
      const rotated = s.rotation % 180 !== 0;
      const w = rotated ? photo.height : photo.width,
        h = rotated ? photo.width : photo.height;
      const widthScale = s.fit === 'original' ? Math.min(1, width / w) : width / w;
      const visible =
        s.fit === 'crop'
          ? Math.min(s.crop, maxHeight)
          : Math.max(1, Math.min(maxHeight, Math.ceil(h * widthScale)));
      const cv = makeCanvas(width, visible);
      drawPhoto(
        cv.getContext('2d'),
        photo,
        0,
        0,
        width,
        visible,
        s.fit === 'crop',
        s.rotation,
        s.fit === 'original',
        s.align,
      );
      return cv;
    }
    const source = makeCanvas(WIDTH, mode === 'draw' ? drawHeight : layoutHeight),
      ctx = source.getContext('2d');
    if (mode === 'draw') strokes.forEach((stroke) => drawStroke(ctx, stroke));
    else layers.forEach((layer) => drawLayer(ctx, layer, t));
    const sc = stage.getContext('2d');
    sc.drawImage(source, 0, 0);
    if (mode === 'layout' && selected >= 0) {
      const l = layers[selected];
      sc.save();
      sc.strokeStyle = '#37786d';
      sc.lineWidth = 2;
      sc.setLineDash([7, 5]);
      sc.strokeRect(l.x + 1, l.y + 1, l.w - 2, l.h - 2);
      sc.restore();
    }
    if (mode === 'draw' ? !strokes.length : !layers.length) return null;
    const scale = Math.min(width / WIDTH, maxHeight / source.height),
      h = Math.max(1, Math.ceil(source.height * scale));
    const cv = makeCanvas(width, h);
    cv.getContext('2d').drawImage(source, 0, 0, WIDTH * scale, source.height * scale);
    return cv;
  }
  function render() {
    cancelAnimationFrame(scheduled);
    scheduled = 0;
    let message = '';
    try {
      if (mode === 'json') {
        output = raw?.some((b) => b) ? raw : null;
        if (statusMessage === 'preset') message = t('Preset applied', '已应用预设');
        if (raw && !output) message = t('The bitmap is blank.', '点阵为空白。');
      } else {
        const available = WIDTH - s.left - s.right,
          maxHeight = MAX_ROWS - s.top - s.bottom;
        if (available < 1 || maxHeight < 1)
          throw Error(t('Margins leave no printable area.', '边距超过纸张可打印范围。'));
        const width = Math.min(available, s.width);
        const cv = contentCanvas(width, maxHeight);
        const offset =
          mode === 'text' || mode === 'image'
            ? s.align === 'center'
              ? Math.floor((available - width) / 2)
              : s.align === 'right'
                ? available - width
                : 0
            : 0;
        output = cv
          ? convert(cv.getContext('2d').getImageData(0, 0, cv.width, cv.height).data, cv.width, cv.height, {
              ...s,
              left: s.left + offset,
            })
          : null;
      }
    } catch (e) {
      output = null;
      message = e.message;
    }
    if (lastError !== message || notice.textContent !== message) notice.textContent = message;
    lastError = message;
    notice.hidden = !message;
    paintRaster(preview, output);
    onChange(output);
    const exp = panel.querySelector('.ed-export');
    if (exp) exp.disabled = !output;
  }
  function schedule() {
    if (!scheduled) {
      output = null;
      onChange(null);
      scheduled = requestAnimationFrame(render);
    }
  }
  build();
  return {
    refreshLanguage: build,
    setSettings(options) {
      if ('dither' in options && (options.dither || s.method !== 'ordered'))
        s.method = options.dither ? 'floyd' : 'threshold';
      Object.assign(s, options);
      render();
    },
  };
}
