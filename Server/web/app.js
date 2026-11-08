'use strict';
const $ = id => document.getElementById(id);
let identity=null, policy=null, sending=false, connectionOK=false;
let drawMode=false,drawHasInk=false,supportsRaster=false,drawActive=false,drawUndo=[];
const canvas=$('draw-canvas'),drawContext=canvas.getContext('2d',{willReadFrequently:true});
const countCharacters=()=>Array.from($('text').value).length;
function error(message=''){$('error').textContent=message;$('error').hidden=!message;}
async function request(path,body){
 const response=await fetch('/v2/'+path,{method:body?'POST':'GET',credentials:'same-origin',cache:'no-store',signal:AbortSignal.timeout(8000),headers:body?{'Content-Type':'application/json','X-CSRF-Token':identity?.csrf||''}:{},body:body?JSON.stringify(body):undefined});
 const value=await response.json();if(!response.ok){const error=new Error(value.error||`HTTP ${response.status}`);error.status=response.status;throw error;}return value;
}
function browserID(){let value=localStorage.getItem('paperang.browser.v2');if(!/^psb_[a-f0-9]{32}$/.test(value||'')){value='psb_'+Array.from(crypto.getRandomValues(new Uint8Array(16)),n=>n.toString(16).padStart(2,'0')).join('');localStorage.setItem('paperang.browser.v2',value);}return value;}
function updateForm(){
 const limit=policy?.[identity?.anonymous?'anonymous_max_length':'authenticated_max_length'];
 const length=countCharacters()+(drawMode?canvas.height:0);$('count').textContent=`${length} / ${limit||'—'}`;
 $('send').disabled=sending||!identity||!connectionOK||!limit||length>limit||(drawMode?!drawHasInk:!$('text').value.trim());$('send-label').textContent=sending?'Sending…':'Send note';for(const id of ['text','compose-mode','draw-height','pen-size','draw-clear','draw-undo'])$(id).disabled=sending;
 $('identity-status').textContent=identity?`${identity.name} · ${identity.role} · ${identity.user_id}`:'Identity not ready — submission blocked';
}
function showHistory(){const list=$('history-list');list.replaceChildren();try{const rows=JSON.parse(localStorage.getItem('paperang.history.v2')||'[]');if(!Array.isArray(rows))return;for(const row of rows.slice(0,20)){if(!/^[A-F0-9]{12}$/.test(row.shortID||''))continue;const button=document.createElement('button');button.type='button';button.textContent=row.shortID;button.addEventListener('click',()=>lookup(row.shortID));list.append(button);}}catch{}}
function remember(job){try{const saved=JSON.parse(localStorage.getItem('paperang.history.v2')||'[]');const rows=Array.isArray(saved)?saved:[];localStorage.setItem('paperang.history.v2',JSON.stringify([{id:job.id,shortID:job.shortID},...rows.filter(x=>x.id!==job.id)].slice(0,20)));showHistory();}catch{}}
function renderJob(job){$('delivery').hidden=false;$('result').textContent=job.accepted?'Accepted by inbox':job.state;$('reference').textContent=job.shortID||job.id;$('detail').textContent=job.accepted?'Acceptance receipt only. A permissions do not include delivery status.':'Status only. Content and delivery metadata are available only in the owner’s C management panel.';$('task-created').textContent=job.created?new Date(job.created*1000).toLocaleString():'—';$('task-updated').textContent=job.updated?new Date(job.updated*1000).toLocaleString():'—';$('task-text').hidden=true;$('task-text').textContent='';$('detail-access').textContent='Content is never returned to this webpage.';}
async function lookup(id){try{renderJob(await request('jobs/lookup/'+id.trim().replace(/-/g,'').toUpperCase()));error();}catch(e){error(e.message==='status_read_forbidden'?'A allows submission only; delivery status is unavailable.':e.message);}}
$('lookup').addEventListener('submit',e=>{e.preventDefault();lookup($('lookup-id').value);});
$('login').addEventListener('click',async()=>{try{await request('auth/login',{credential:$('key').value.trim()});$('key').value='';identity=await request('auth/me');error();updateForm();}catch(e){error(e.message);}});
$('logout').addEventListener('click',async()=>{try{await request('auth/logout',{});identity=null;await initialize();}catch(e){error(e.message);}});
$('note').addEventListener('submit',async e=>{e.preventDefault();if($('send').disabled)return;sending=true;updateForm();error();try{const body={text:$('text').value,request_id:crypto.randomUUID()};if(drawMode)body.content=await drawingContent();const job=await request('jobs',body);remember(job);renderJob(job);$('text').value='';if(drawMode)clearDrawing();if(job.statusReadable)renderJob(await request('jobs/'+job.id));}catch(e){error(`${e.message}. A network failure may hide an accepted task; do not resend automatically.`);}finally{sending=false;updateForm();}});
async function initialize(){try{const data=await request('public/status');policy=data.policy;supportsRaster=data.capabilities.contentTypes.includes('raster1');$('compose-mode').querySelector('option[value="raster1"]').disabled=!supportsRaster;try{identity=await request('auth/me');}catch(e){if(e.status!==401)throw e;if(policy.allow_anonymous){await request('auth/anonymous',{browser_id:browserID()});identity=await request('auth/me');}}connectionOK=true;$('printer').textContent='Inbox available';$('dot').className='online';}catch(e){error(e.message);}updateForm();}
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


$('text').addEventListener('input',updateForm);
showHistory(); initialize();
