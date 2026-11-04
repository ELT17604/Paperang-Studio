'use strict';
const $ = id => document.getElementById(id);
const storageKey = 'etel.latestReceipt.v1';
const historyKey = 'etel.receipts.v1';
const normalizeID = value => value.trim().replace(/-/g, '').toUpperCase();
const stageMap = {
  queued: {step: 1, title: 'In the inbox', detail: 'Waiting for the recipient to accept your note.'},
  claimed: {step: 2, title: 'Note accepted', detail: 'The recipient has accepted your note.'},
  printing: {step: 3, title: 'Sending to the printer', detail: 'Your note is being sent. Paper output is not yet confirmed.'},
  sent: {step: 4, title: 'Sent to the printer', detail: 'Waiting for the recipient to confirm the paper output.'},
  confirmed: {step: 5, title: 'On paper', detail: 'The recipient has confirmed the printed note.'},
  unknown: {title: 'Output needs checking', detail: 'The printer did not confirm the outcome. The recipient needs to check the paper.'},
  rejected: {title: 'Note declined', detail: 'The recipient declined this note.'}
};
let policy = null, connectionOK = false, sending = false, currentJob = null, pollTimer, trackingVersion = 0, lookupVersion = 0;
let receipts = [];
let drawMode = false, drawHasInk = false, supportsRaster = false;
let drawActive = false, drawUndo = [];
const canvas = $('draw-canvas'), drawContext = canvas.getContext('2d', {willReadFrequently: true});
const countCharacters = () => Array.from($('text').value).length;
const request = (path, options = {}) => fetch(path, {...options, cache: 'no-store', signal: AbortSignal.timeout(8000)});
function jobPollDelay() {
  if (document.hidden) return 4000;
  if (['claimed', 'printing'].includes(currentJob?.state)) return 250;
  if (currentJob?.state === 'queued') return 500;
  return 1000; // Keep watching sent jobs for the recipient's confirmation.
}
function error(message = '') {
  $('error').textContent = message;
  $('error').hidden = !message;
}
function updateForm() {
  const keyed = Boolean($('key').value.trim());
  const limit = policy?.[keyed ? 'authenticated_max_length' : 'anonymous_max_length'];
  const length = countCharacters() + (drawMode ? canvas.height : 0);
  $('count').textContent = `${length.toLocaleString('en-US')} / ${limit ? limit.toLocaleString('en-US') : '—'}`;
  $('count').classList.toggle('over', Boolean(limit && length > limit));
  const inFlight = currentJob?.receipt && ['queued', 'claimed', 'printing'].includes(currentJob.state);
  $('send').disabled = sending || !connectionOK || !policy || !length || (drawMode ? (!drawHasInk || !supportsRaster) : !$('text').value.trim()) || length > limit || Boolean(inFlight);
  for (const id of ['compose-mode','draw-height','pen-size','draw-clear','draw-undo','text','key']) $(id).disabled = sending;
  $('send-label').textContent = sending ? 'Sending…' : inFlight ? 'Note in progress' : 'Send note';
}
function saveReceipt() {
  // Keep receipt capabilities in this tab, never content, metadata or access keys.
  const saved = Object.fromEntries(['id', 'shortID', 'receipt', 'lookupID', 'state', 'step', 'progress'].filter(key => currentJob?.[key] !== undefined).map(key => [key, currentJob[key]]));
  if (saved.receipt && saved.shortID) {
    receipts = [{id: saved.id, shortID: saved.shortID, receipt: saved.receipt}, ...receipts.filter(item => item.shortID !== saved.shortID)].slice(0, 20);
  }
  try {
    sessionStorage.setItem(storageKey, JSON.stringify(saved));
    sessionStorage.setItem(historyKey, JSON.stringify(receipts));
  } catch { /* Private mode may disable storage. */ }
}
function renderProgress() {
  const progress = currentJob.progress;
  const counted = progress && Number.isInteger(progress.completed) && Number.isInteger(progress.total) && progress.total > 0 && progress.completed >= 0 && progress.completed <= progress.total;
  // One five-segment workflow. Only Printing (40–60) has measured subprogress;
  // this value is a stage position, never an estimate of elapsed print time.
  const fractionalStage = currentJob.state === 'printing' || (currentJob.state === 'unknown' && counted);
  const fraction = counted ? progress.completed / progress.total : 0;
  const position = fractionalStage ? 40 + fraction * 20 : currentJob.step * 20;
  $('progress').max = 100;
  $('progress').value = position;
  $('progress').setAttribute('aria-label', 'Delivery workflow');
  $('progress-label').textContent = 'Delivery progress';
  if (fractionalStage) {
    const counts = counted ? `${progress.completed} / ${progress.total} partitions · ${Math.round(fraction * 1000) / 10}%` : 'Waiting for partition counts';
    $('progress-count').textContent = counts;
    $('progress').setAttribute('aria-valuetext', `Printing stage. ${counts}. Paper output is not confirmed.`);
  } else {
    $('progress-count').textContent = ['Queued', 'Accepted', 'Printing', 'Sent', 'Confirmed'][currentJob.step - 1];
    $('progress').setAttribute('aria-valuetext', stageMap[currentJob.state]?.detail || 'Receipt unavailable');
  }
}
function renderDetails() {
  const format = value => Number.isFinite(value) ? new Date(value * 1000).toLocaleString('en-US', {dateStyle: 'medium', timeStyle: 'short'}) : '—';
  $('task-created').textContent = format(currentJob.created);
  $('task-updated').textContent = format(currentJob.updated);
  const readable = currentJob.detailsAvailable === true && typeof currentJob.text === 'string';
  $('task-text').hidden = !readable;
  $('task-text').textContent = readable ? (currentJob.content?.type === 'raster1' ? `Drawing · ${currentJob.content.width} × ${currentJob.content.height} pixels${currentJob.text ? '\n'+currentJob.text : ''}` : currentJob.text) : '';
  $('detail-access').textContent = readable ? 'Note content' : 'Content is private. Use this tab’s original receipt or the submission key used to send this note.';
}
function renderJob(job) {
  const state = stageMap[job.state];
  if (!state) throw new Error('Unrecognized delivery status. Please check again later.');
  const remembered = currentJob?.id === job.id || currentJob?.shortID === job.shortID;
  const previousStep = job.state === 'unknown' ? (job.lastStage === 'printing' || job.progress ? 3 : job.lastStage === 'claimed' ? 2 : Math.max(2, remembered ? (currentJob?.step || 2) : 2)) : (remembered ? (currentJob?.step || 1) : 1);
  currentJob = {...currentJob, ...job, step: state.step || previousStep};
  if (job.detailsAvailable === false) {
    delete currentJob.text;
    delete currentJob.metadata;
    delete currentJob.device;
  }
  $('delivery').hidden = false;
  $('delivery').dataset.tone = ['unknown', 'rejected'].includes(job.state) ? 'warning' : 'normal';
  $('result').textContent = state.title;
  $('detail').textContent = state.detail;
  $('reference').textContent = currentJob.shortID ? `#${currentJob.shortID.match(/.{1,4}/g).join('·')}` : '';
  renderProgress();
  renderDetails();
  [...document.querySelectorAll('.stages li')].forEach((item, index) => {
    item.dataset.reached = String(index < currentJob.step);
    if (index === currentJob.step - 1) item.setAttribute('aria-current', 'step');
    else item.removeAttribute('aria-current');
  });
  saveReceipt();
  updateForm();
}
async function refreshStatus() {
  try {
    const response = await request('/v1/public/status');
    if (!response.ok) throw new Error();
    const data = await response.json();
    policy = data.policy;
    supportsRaster = Array.isArray(data.capabilities?.contentTypes) && data.capabilities.contentTypes.includes('raster1');
    $('compose-mode').querySelector('option[value="raster1"]').disabled = !supportsRaster;
    connectionOK = true;
    const status = data.status;
    $('dot').className = status.online && status.printerConnected ? 'online' : '';
    $('printer').textContent = status.online ? (status.printerConnected ? (status.busy ? 'Printer busy' : (status.automaticPrinting ? 'Printer online · automatic printing' : 'Printer online')) : 'Printer disconnected') : 'Offline · notes can wait';
    $('key').required = !policy.allow_anonymous;
    $('key-label').textContent = policy.allow_anonymous ? 'Use a submission key' : 'Submission key required';
    if (!policy.allow_anonymous) $('key-options').open = true;
  } catch {
    connectionOK = false;
    $('dot').className = '';
    $('printer').textContent = 'Connection unavailable';
  }
  updateForm();
  setTimeout(refreshStatus, 2500);
}
async function pollJob() {
  clearTimeout(pollTimer);
  if (!currentJob) return;
  const version = trackingVersion;
  const shortID = currentJob.lookupID || currentJob.shortID;
  const path = shortID ? `/v1/jobs/lookup/${normalizeID(shortID)}` : (currentJob.receipt && currentJob.id ? `/v1/jobs/${currentJob.id}` : null);
  if (!path) return;
  const headers = {'X-Paperang-Content-Summary': '1'};
  if (currentJob.receipt) headers['X-Job-Receipt'] = currentJob.receipt;
  // Lookups may use the submission key already entered in the form. It stays in memory.
  if (currentJob.lookupID && $('key').value.trim()) headers.Authorization = `Bearer ${$('key').value.trim()}`;
  let terminal = false;
  try {
    const response = await request(path, {headers});
    if (trackingVersion !== version) return;
    if (response.status === 404) {
      $('result').textContent = 'Note unavailable';
      $('detail').textContent = 'This note has expired or is no longer available.';
      currentJob.state = 'expired';
      saveReceipt();
      updateForm();
      return;
    }
    if (response.status === 401) {
      $('detail').textContent = 'The submission key is invalid or revoked. Replace or clear it, then track the ID again.';
      return;
    }
    if (!response.ok) throw new Error();
    const job = await response.json();
    if (trackingVersion !== version) return;
    renderJob(job);
    terminal = ['confirmed', 'unknown', 'rejected'].includes(job.state);
  } catch {
    if (trackingVersion !== version) return;
    $('detail').textContent = 'Connection interrupted. Your note has not been resent. Reconnecting…';
  }
  if (!terminal && trackingVersion === version) pollTimer = setTimeout(pollJob, jobPollDelay());
}
$('lookup').addEventListener('submit', async event => {
  event.preventDefault();
  const shortID = normalizeID($('lookup-id').value);
  const showError = message => {
    $('lookup-error').textContent = message;
    $('lookup-error').hidden = !message;
  };
  showError('');
  if (!/^[A-F0-9]{12}$/.test(shortID)) {
    showError('Enter the 12-character ID printed on the note.');
    return;
  }
  if (sending) return;
  const version = ++lookupVersion;
  const saved = receipts.find(item => normalizeID(item.shortID) === shortID);
  const headers = {'X-Paperang-Content-Summary': '1'};
  if (saved) headers['X-Job-Receipt'] = saved.receipt;
  if ($('key').value.trim()) headers.Authorization = `Bearer ${$('key').value.trim()}`;
  $('track').disabled = true;
  try {
    const response = await request(`/v1/jobs/lookup/${shortID}`, {headers});
    if (version !== lookupVersion) return;
    if (response.status === 404) throw new Error('Note not found. Check the ID; older notes may have expired.');
    if (response.status === 401) throw new Error('The submission key is invalid or revoked. Replace or clear it and try again.');
    if (!response.ok) throw new Error('Unable to look up this note. Please try again.');
    const job = await response.json();
    if (version !== lookupVersion) return;
    clearTimeout(pollTimer);
    trackingVersion++;
    currentJob = {...job, lookupID: shortID, ...(saved ? {receipt: saved.receipt} : {}), step: 1};
    renderJob(job);
    if (!['confirmed', 'unknown', 'rejected'].includes(job.state)) pollTimer = setTimeout(pollJob, jobPollDelay());
  } catch (err) {
    if (version === lookupVersion) showError(err.message || 'Unable to look up this note.');
  } finally {
    if (version === lookupVersion) $('track').disabled = false;
  }
});
$('note').addEventListener('submit', async event => {
  event.preventDefault();
  if ($('send').disabled) return;
  const key = $('key').value.trim();
  const text = $('text').value;
  error();
  sending = true;
  ++lookupVersion;
  $('track').disabled = true;
  updateForm();
  let receivedResponse = false;
  try {
    const headers = {'Content-Type': 'application/json'};
    if (key) headers.Authorization = `Bearer ${key}`;
    const body = {text, browser: {timezone: Intl.DateTimeFormat().resolvedOptions().timeZone, screen: `${screen.width}×${screen.height}`, platform: navigator.platform, language: navigator.language}};
    if (drawMode) body.content = await drawingContent();
    const response = await request('/v1/jobs', {method: 'POST', headers, body: JSON.stringify(body)});
    receivedResponse = true;
    const job = await response.json();
    if (!response.ok) {
      if (response.status === 401) throw new Error('This submission key is invalid or revoked. Replace it, or remove it to send anonymously if allowed.');
      if (response.status === 403) throw new Error('Use a submission key. Device keys cannot send notes.');
      if (response.status === 429) throw new Error(`Hourly limit reached.${job.resetAt ? ` Try again after ${new Date(job.resetAt * 1000).toLocaleTimeString('en-US', {hour: 'numeric', minute: '2-digit'})}.` : ' Please try again later.'}`);
      throw new Error(job.error || 'Your note could not be sent. Please try again later.');
    }
    ++lookupVersion;
    clearTimeout(pollTimer);
    trackingVersion++;
    currentJob = {...job, step: 1};
    renderJob(job);
    $('text').value = '';
    if (drawMode) clearDrawing();
    pollTimer = setTimeout(pollJob, 250);
  } catch (err) {
    error(receivedResponse ? err.message : 'Connection interrupted. Your note may have been received. Check before sending it again.');
  } finally {
    sending = false;
    $('track').disabled = false;
    updateForm();
  }
});

function clearDrawing() {
  drawContext.fillStyle = '#fff';drawContext.fillRect(0, 0, canvas.width, canvas.height);
  drawHasInk = false;drawActive = false;drawUndo = [];updateForm();
}
function drawSnapshot() {
  drawUndo.push({pixels: drawContext.getImageData(0, 0, canvas.width, canvas.height), ink: drawHasInk});
  if (drawUndo.length > 10) drawUndo.shift();
}
function drawPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return [(event.clientX - rect.left) * canvas.width / rect.width, (event.clientY - rect.top) * canvas.height / rect.height];
}
canvas.addEventListener('pointerdown', event => {
  if (sending || drawActive) return;
  event.preventDefault();drawSnapshot();canvas.setPointerCapture(event.pointerId);drawActive = true;
  const [x,y] = drawPoint(event);drawContext.strokeStyle = '#000';drawContext.fillStyle = '#000';drawContext.lineWidth = Number($('pen-size').value);drawContext.lineCap = 'round';drawContext.lineJoin = 'round';
  drawContext.beginPath();drawContext.arc(x,y,drawContext.lineWidth/2,0,Math.PI*2);drawContext.fill();drawContext.beginPath();drawContext.moveTo(x,y);drawHasInk = true;updateForm();
});
canvas.addEventListener('pointermove', event => {
  if (!drawActive || sending) return;event.preventDefault();const [x,y] = drawPoint(event);drawContext.lineTo(x,y);drawContext.stroke();
});
for (const kind of ['pointerup','pointercancel','lostpointercapture']) canvas.addEventListener(kind, () => {drawActive = false;});
$('draw-undo').addEventListener('click', () => {if (sending) return;const previous=drawUndo.pop();if(previous){drawContext.putImageData(previous.pixels,0,0);drawHasInk=previous.ink;updateForm();}});
$('draw-clear').addEventListener('click', () => {if(!sending)clearDrawing();});
$('draw-height').addEventListener('change', () => {if(sending)return;const image=drawContext.getImageData(0,0,canvas.width,canvas.height);canvas.height=Number($('draw-height').value);drawContext.fillStyle='#fff';drawContext.fillRect(0,0,canvas.width,canvas.height);drawContext.putImageData(image,0,0);drawUndo=[];drawHasInk=drawContext.getImageData(0,0,canvas.width,canvas.height).data.some((v,i)=>i%4!==3 && v<128);updateForm();});
$('compose-mode').addEventListener('change', () => {if(sending)return;drawMode=$('compose-mode').value==='raster1';$('drawing').hidden=!drawMode;$('text').required=!drawMode;$('text').placeholder=drawMode?'Optional caption…':'Something worth putting on paper…';updateForm();});
async function drawingContent() {
  // Snapshot pixels before any await. Always composite white and pack MSB first.
  const width=canvas.width,height=canvas.height,pixels=drawContext.getImageData(0,0,width,height).data;
  const data=new Uint8Array(width/8*height);
  for(let i=0;i<width*height;i++) {const offset=i*4;const gray=(pixels[offset]*0.2126+pixels[offset+1]*0.7152+pixels[offset+2]*0.0722)*(pixels[offset+3]/255)+255*(1-pixels[offset+3]/255);if(gray<128)data[i>>3]|=1<<(7-(i%8));}
  // Base64 stays small (at most 134400 characters at the protocol's height limit).
  let raw='';for(let start=0;start<data.length;start+=8192)raw+=String.fromCharCode(...data.subarray(start,start+8192));
  const result={schemaVersion:1,type:'raster1',width,height,encoding:'base64',bitOrder:'msb-first',blackIsOne:true,data:btoa(raw)};
  if(globalThis.crypto?.subtle)result.sha256=Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',data))).map(v=>v.toString(16).padStart(2,'0')).join('');
  return result;
}
clearDrawing();

$('text').addEventListener('input', updateForm);
$('key').addEventListener('input', updateForm);
try {
  const history = JSON.parse(sessionStorage.getItem(historyKey) || '[]');
  if (Array.isArray(history)) receipts = history.filter(item => typeof item?.shortID === 'string' && /^[A-F0-9]{12}$/.test(item.shortID) && typeof item.receipt === 'string' && item.receipt.length < 150).slice(0, 20);
  const saved = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
  const hasReceipt = typeof saved?.receipt === 'string' && saved.receipt.length < 150 && /^[a-f0-9-]{36,40}$/.test(saved.id);
  const hasLookup = typeof saved?.lookupID === 'string' && /^[A-F0-9]{12}$/.test(saved.lookupID);
  if (saved && (hasReceipt || hasLookup) && stageMap[saved.state]) {
    currentJob = saved;
    renderJob(saved);
    // Fetch even final receipts once after reload to restore private details.
    pollTimer = setTimeout(pollJob, 0);
  }
} catch { /* A malformed or unavailable saved receipt should not block the form. */ }
updateForm();
refreshStatus();
