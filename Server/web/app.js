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
const countCharacters = () => Array.from($('text').value).length;
const request = (path, options = {}) => fetch(path, {...options, cache: 'no-store', signal: AbortSignal.timeout(8000)});
function error(message = '') {
  $('error').textContent = message;
  $('error').hidden = !message;
}
function updateForm() {
  const keyed = Boolean($('key').value.trim());
  const limit = policy?.[keyed ? 'authenticated_max_length' : 'anonymous_max_length'];
  const length = countCharacters();
  $('count').textContent = `${length.toLocaleString('en-US')} / ${limit ? limit.toLocaleString('en-US') : '—'}`;
  $('count').classList.toggle('over', Boolean(limit && length > limit));
  const inFlight = currentJob?.receipt && ['queued', 'claimed', 'printing'].includes(currentJob.state);
  $('send').disabled = sending || !connectionOK || !policy || !length || !$('text').value.trim() || length > limit || Boolean(inFlight);
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
  if (counted) {
    const percent = Math.round(progress.completed / progress.total * 1000) / 10;
    $('progress').max = progress.total;
    $('progress').value = progress.completed;
    $('progress-label').textContent = 'Partitions sent';
    $('progress-count').textContent = `${progress.completed} / ${progress.total} · ${percent}%`;
    $('progress').setAttribute('aria-label', 'Raster partitions sent to printer');
    $('progress').setAttribute('aria-valuetext', `${progress.completed} of ${progress.total} partitions sent. Paper output requires confirmation.`);
  } else if (currentJob.state === 'printing') {
    $('progress').removeAttribute('value');
    $('progress-label').textContent = 'Sending to printer';
    $('progress-count').textContent = 'Waiting for counts';
    $('progress').setAttribute('aria-label', 'Raster transmission');
    $('progress').setAttribute('aria-valuetext', 'Sending; partition counts have not been reported.');
  } else {
    $('progress').max = 5;
    $('progress').value = currentJob.step;
    $('progress-label').textContent = 'Delivery stage';
    $('progress-count').textContent = `${currentJob.step} of 5`;
    $('progress').setAttribute('aria-label', 'Delivery stage');
    $('progress').setAttribute('aria-valuetext', stageMap[currentJob.state]?.detail || 'Receipt unavailable');
  }
}
function renderDetails() {
  const format = value => Number.isFinite(value) ? new Date(value * 1000).toLocaleString('en-US', {dateStyle: 'medium', timeStyle: 'short'}) : '—';
  $('task-created').textContent = format(currentJob.created);
  $('task-updated').textContent = format(currentJob.updated);
  const readable = currentJob.detailsAvailable === true && typeof currentJob.text === 'string';
  $('task-text').hidden = !readable;
  $('task-text').textContent = readable ? currentJob.text : '';
  $('detail-access').textContent = readable ? 'Note content' : 'Content is private. Use this tab’s original receipt or the submission key used to send this note.';
}
function renderJob(job) {
  const state = stageMap[job.state];
  if (!state) throw new Error('Unrecognized delivery status. Please check again later.');
  const previousStep = currentJob?.step || 1;
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
    connectionOK = true;
    const status = data.status;
    $('dot').className = status.online && status.printerConnected ? 'online' : '';
    $('printer').textContent = status.online ? (status.printerConnected ? (status.busy ? 'Printer busy' : 'Printer online') : 'Printer disconnected') : 'Offline · notes can wait';
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
  const headers = {};
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
  if (!terminal && trackingVersion === version) pollTimer = setTimeout(pollJob, 2000);
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
  const version = ++lookupVersion;
  const saved = receipts.find(item => normalizeID(item.shortID) === shortID);
  const headers = {};
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
    if (!['confirmed', 'unknown', 'rejected'].includes(job.state)) pollTimer = setTimeout(pollJob, 2000);
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
  updateForm();
  let receivedResponse = false;
  try {
    const headers = {'Content-Type': 'application/json'};
    if (key) headers.Authorization = `Bearer ${key}`;
    const response = await request('/v1/jobs', {method: 'POST', headers, body: JSON.stringify({text, browser: {timezone: Intl.DateTimeFormat().resolvedOptions().timeZone, screen: `${screen.width}×${screen.height}`, platform: navigator.platform, language: navigator.language}})});
    receivedResponse = true;
    const job = await response.json();
    if (!response.ok) {
      if (response.status === 401) throw new Error('This submission key is invalid or revoked. Replace it, or remove it to send anonymously if allowed.');
      if (response.status === 403) throw new Error('Use a submission key. Device keys cannot send notes.');
      if (response.status === 429) throw new Error(`Hourly limit reached.${job.resetAt ? ` Try again after ${new Date(job.resetAt * 1000).toLocaleTimeString('en-US', {hour: 'numeric', minute: '2-digit'})}.` : ' Please try again later.'}`);
      throw new Error(job.error || 'Your note could not be sent. Please try again later.');
    }
    clearTimeout(pollTimer);
    trackingVersion++;
    currentJob = {...job, step: 1};
    renderJob(job);
    $('text').value = '';
    pollTimer = setTimeout(pollJob, 1000);
  } catch (err) {
    error(receivedResponse ? err.message : 'Connection interrupted. Your note may have been received. Check before sending it again.');
  } finally {
    sending = false;
    updateForm();
  }
});
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
