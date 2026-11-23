import { Printer } from './ble.js';
import { mountEditor } from './editor.js';

const $ = (id) => document.getElementById(id);
const defaults = {
  left: 0,
  right: 0,
  top: 0,
  bottom: 0,
  trim: true,
  threshold: 128,
  dither: false,
  copies: 1,
  before: 0,
  after: 160,
  between: false,
  density: 100,
  interval: 10,
  chunk: 180,
};
const ranges = {
  left: [0, 287],
  right: [0, 287],
  top: [0, 1399],
  bottom: [0, 1399],
  threshold: [0, 255],
  copies: [1, 20],
  before: [0, 2000],
  after: [0, 2000],
  density: [0, 100],
  interval: [0, 1000],
  chunk: [1, 180],
};
function stored(key) {
  try {
    return JSON.parse(localStorage.getItem('paperang.web.' + key)) || {};
  } catch {
    return {};
  }
}
function save(key, value) {
  try {
    localStorage.setItem('paperang.web.' + key, JSON.stringify(value));
  } catch {}
}
function normalized(current, updates) {
  const next = { ...current };
  for (const [key, value] of Object.entries(updates)) {
    if (Object.hasOwn(ranges, key)) {
      const [min, max] = ranges[key];
      if (Number.isInteger(value) && value >= min && value <= max) next[key] = value;
    } else if (typeof defaults[key] === 'boolean' && typeof value === 'boolean') next[key] = value;
  }
  return next;
}
let settings = normalized(defaults, stored('settings'));
const prefs = { language: 'auto', mode: 'auto', style: 'morandi', ...stored('preferences') };
const dark = matchMedia('(prefers-color-scheme: dark)');
const language = () =>
  prefs.language === 'auto' ? navigator.languages?.[0] || navigator.language : prefs.language;
const t = (en, zh) => (language()?.toLowerCase().startsWith('zh') ? zh : en);
let currentBytes = null,
  busy = '',
  deviceName = '',
  dialogPage = '',
  editor;
function message(value = '') {
  $('notice').textContent = value;
  $('notice').hidden = !value;
}
function applyPreferences() {
  const root = document.documentElement;
  root.lang = t('en', 'zh-CN');
  root.dataset.style = prefs.style;
  root.dataset.mode = prefs.mode === 'auto' ? (dark.matches ? 'dark' : 'light') : prefs.mode;
  document.querySelector('meta[name=theme-color]').content = getComputedStyle(root)
    .getPropertyValue('--bg')
    .trim();
  document.querySelectorAll('[data-en]').forEach((el) => (el.textContent = t(el.dataset.en, el.dataset.zh)));
}
const printer = new Printer(() => {
  deviceName = '';
  $('armed').checked = false;
  update();
}, language);
function update() {
  const connected = printer.connected;
  $('printer-title').textContent = deviceName || 'Paperang P2';
  $('device-state').textContent =
    busy === 'connect'
      ? t('Connecting…', '正在连接…')
      : connected
        ? t('Connected', '已连接')
        : t('Disconnected', '未连接');
  $('dot').classList.toggle('online', connected);
  $('connect').disabled = !!busy;
  $('connect').lastElementChild.textContent = connected
    ? t('Disconnect', '断开连接')
    : t('Connect printer', '连接打印机');
  $('dimensions').textContent = currentBytes ? '576 × ' + currentBytes.length / 72 + ' px' : '576 px';
  $('summary').textContent =
    t(settings.copies + ' ' + (settings.copies === 1 ? 'copy' : 'copies'), settings.copies + ' 份') +
    ' · ' +
    settings.density +
    '%';
  $('preview').hidden = !currentBytes;
  $('empty').hidden = !!currentBytes;
  $('print').disabled = !currentBytes || !connected || !$('armed').checked || !!busy;
  $('print').lastElementChild.textContent = busy === 'print' ? t('Printing…', '打印中…') : t('Print', '打印');
  $('armed').disabled = !!busy;
  $('progress').hidden = busy !== 'print';
  $('print-settings').disabled = !!busy;
  $('editor').inert = busy === 'print';
  $('notice').setAttribute('aria-live', 'polite');
}
const paths = {
  bluetooth: 'm7 7 10 10-5 4V3l5 4L7 17',
  tune: 'M3 7h8m4 0h6M3 17h3m4 0h11 M15 7a2 2 0 1 1-4 0 2 2 0 0 1 4 0 M10 17a2 2 0 1 1-4 0 2 2 0 0 1 4 0',
  edit: 'M4 20l5-1 12-12-4-4L5 15z M14 6l4 4',
  receipt: 'M5 3l2 1 2-1 3 1 3-1 2 1 2-1v18l-2-1-2 1-3-1-3 1-2-1-2 1z M8 8h8 M8 12h8 M8 16h4',
  printer: 'M7 8V3h10v5 M7 17H4V9h16v8h-3 M7 14h10v7H7z M16 11h1',
};
for (const el of document.querySelectorAll('i[data-icon]')) {
  el.innerHTML =
    '<svg aria-hidden="true" viewBox="0 0 24 24"><path d="' + paths[el.dataset.icon] + '"/></svg>';
}
function applySettings(partial) {
  settings = normalized(settings, partial);
  save('settings', settings);
  editor?.setSettings({ ...settings });
  update();
}
function label(en, zh, control, parent) {
  const field = document.createElement('label');
  control.setAttribute('aria-label', t(en, zh));
  field.append(document.createTextNode(t(en, zh)), control);
  parent.append(field);
  return field;
}
function group(en, zh) {
  const h = document.createElement('h3');
  h.textContent = t(en, zh);
  const grid = document.createElement('div');
  grid.className = 'fields';
  $('dialog-body').append(h, grid);
  return grid;
}
function field(parent, key, en, zh) {
  const input = document.createElement('input');
  if (typeof defaults[key] === 'boolean') {
    input.type = 'checkbox';
    input.checked = settings[key];
  } else {
    input.type = 'number';
    [input.min, input.max] = ranges[key];
    input.step = '1';
    input.value = settings[key];
  }
  const row = label(en, zh, input, parent);
  if (input.type === 'checkbox') row.className = 'check';
  input.onchange = () => {
    if (input.type === 'checkbox') applySettings({ [key]: input.checked });
    else if (input.value && input.validity.valid) applySettings({ [key]: input.valueAsNumber });
    else {
      input.reportValidity();
      input.value = settings[key];
    }
  };
}
function preference(key, en, zh, options) {
  const select = document.createElement('select');
  for (const [value, english, chinese] of options) select.add(new Option(t(english, chinese), value));
  select.value = prefs[key];
  label(en, zh, select, $('dialog-body'));
  select.onchange = () => {
    prefs[key] = select.value;
    save('preferences', prefs);
    refresh();
    drawDialog();
    $('dialog-body')
      .querySelectorAll('select')
      [Object.keys({ language: 1, mode: 1, style: 1 }).indexOf(key)]?.focus();
  };
}
function drawDialog() {
  $('dialog-body').replaceChildren();
  $('close').setAttribute('aria-label', t('Close', '关闭'));
  if (dialogPage === 'preferences') {
    $('dialog-title').textContent = t('Preferences', '偏好设置');
    preference('language', 'Language', '界面语言', [
      ['auto', 'System', '跟随系统'],
      ['zh', '简体中文', '简体中文'],
      ['en', 'English', 'English'],
    ]);
    preference('mode', 'Appearance', '外观', [
      ['auto', 'System', '跟随系统'],
      ['light', 'Light', '浅色'],
      ['dark', 'Dark', '深色'],
    ]);
    preference('style', 'Theme', '主题', [
      ['morandi', 'Morandi', '莫兰迪'],
      ['aero', 'Frutiger Aero', 'Frutiger Aero'],
    ]);
    return;
  }
  $('dialog-title').textContent = t('Print settings', '打印设置');
  const margin = group('Margins · px', '页边距 · px');
  for (const [key, en, zh] of [
    ['top', 'Top', '上'],
    ['bottom', 'Bottom', '下'],
    ['left', 'Left', '左'],
    ['right', 'Right', '右'],
  ])
    field(margin, key, en, zh);
  const paper = group('Paper & image', '纸张与图像');
  for (const args of [
    ['density', 'Density · %', '浓度 · %'],
    ['copies', 'Copies', '份数'],
    ['before', 'Leading feed · lines', '打印前送纸 · 行'],
    ['after', 'Trailing feed · lines', '打印后送纸 · 行'],
    ['threshold', 'Threshold', '黑白阈值'],
    ['dither', 'Dithering', '图像抖动'],
    ['trim', 'Trim blank rows', '裁剪尾部空白'],
    ['between', 'Feed between copies', '每份之间送纸'],
  ])
    field(paper, ...args);
  const ble = group('Bluetooth transfer', '蓝牙传输');
  field(ble, 'interval', 'Write interval · ms', '写入间隔 · ms');
  field(ble, 'chunk', 'Chunk limit · bytes', '分块上限 · 字节');
}
function openDialog(page) {
  dialogPage = page;
  drawDialog();
  $('dialog').showModal();
}
function refresh() {
  applyPreferences();
  editor?.refreshLanguage();
  update();
}
applyPreferences();
editor = mountEditor($('editor'), $('preview'), {
  t,
  onChange: (bytes) => {
    currentBytes = bytes;
    update();
  },
  onError: message,
  onSettings: applySettings,
});
editor.setSettings({ ...settings });
$('preferences').onclick = () => openDialog('preferences');
$('print-settings').onclick = () => openDialog('print');
$('armed').onchange = update;
$('connect').onclick = async () => {
  if (busy) return;
  message();
  if (printer.connected) {
    printer.disconnect();
    deviceName = '';
    $('armed').checked = false;
    update();
    return;
  }
  busy = 'connect';
  update();
  try {
    deviceName = await printer.connect();
  } catch (e) {
    if (e.name !== 'NotFoundError') message(e.message);
  } finally {
    busy = '';
    update();
  }
};
$('print').onclick = async () => {
  if ($('print').disabled) return;
  busy = 'print';
  $('progress').value = 0;
  message();
  update();
  try {
    await printer.print(currentBytes, { ...settings }, (fraction) => {
      $('progress').value = fraction;
    });
    message(t('Print data sent.', '打印数据已发送。'));
  } catch (e) {
    message(e.message);
  } finally {
    busy = '';
    update();
  }
};
window.addEventListener('languagechange', () => {
  refresh();
  if ($('dialog').open) drawDialog();
});
dark.addEventListener('change', applyPreferences);
update();
