# Local HTTP API v1

[Documentation](README.md) · [简体中文](API.zh-CN.md) · [OpenAPI 3.0.3](openapi.json)

The app hosts a Network.framework HTTP service at `http://127.0.0.1:8176`. It is disabled by default and binds only to loopback. GUI, API, and terminal operations share the same printer connection and busy lock.

## Startup and authentication

Open **Local API** (`本地 API`), configure the port (1024–65535), token, and allowed web origins, then start. Tokens must contain 32–256 printable, non-whitespace ASCII characters. The default token is random, not written to the repository or general logs, and regenerated on app restart. Stop the service before changing the port, token, or origins.

All actual requests require:

```http
Authorization: Bearer <local-token>
```

POST additionally requires `Content-Type: application/json` and a JSON object, including `{}` for parameterless operations. Host must be `127.0.0.1:port` or `localhost:port`. Browser Origin must exactly match the allowlist; defaults are `http://localhost:3000` and `http://127.0.0.1:3000`. Wildcards and `null` are not accepted.

OPTIONS preflight is token-free only for an allowed Origin. CORS permits Authorization/Content-Type and advertises private-network access. Browser local-network and HTTPS-to-HTTP policies may impose additional requirements. Do not embed tokens in public source, URLs, or remote services. There is no shell execution, TTY input, arbitrary file read, or unknown raw-command endpoint.

Explicit command-line startup:

```sh
'/path/Paperang Studio.app/Contents/MacOS/PaperangLab' \
  --api --api-port 8176 --api-token-file /absolute/private-token-file
```

The token file is UTF-8, with surrounding whitespace trimmed; use private file permissions such as 0600. Add `--api-offline` for a headless rendering service without Bluetooth initialization. Scanning/connecting and paper use remain unavailable in offline mode. Exiting stops the service; a port conflict fails startup without changing system settings.

## Endpoints

| Method and path | Input | Result |
|---|---|---|
| GET `/v1/status` | None | `apiVersion`, `connected`, `busy`, `printer`, `state`, `lastError`, `progress`, `jobStatus`, `charging`, `readings`, `printingEnabled` |
| GET `/v1/devices` | Optional `?all=true` | `{scanning, devices:[{id,name,rssi,paperangCandidate}]}`; latest scan, candidates by default |
| POST `/v1/scan` | `{}` | 202 `{accepted,scanning}`; about 10 seconds, no automatic connection; poll devices |
| POST `/v1/connect` | `{"id":"discovered UUID"}` | 202 accepted; poll status until `connected:true`. The UUID is local to CoreBluetooth, not a MAC |
| POST `/v1/disconnect` | `{}` | `{connected:false}`; 409 during a print, cancel first |
| POST `/v1/query` | `{"command":12}` | `{command,payloadHex,hasData,status}`; allowlisted read-only GET, decimal integer command |
| GET `/v1/protocol` | None | Model, width 576, 72 bytes/row, nominal DPI 300, default feed 150, command evidence catalog |
| GET `/v1/settings` | None | `{defaults:PrintSettings,note}`; request settings merge over defaults, not current GUI edits |
| POST `/v1/render` | RenderInput | Offline `{width,height,bytes,previewPngBase64}`; `includeRaw:true` adds `rawBase64` |
| POST `/v1/jobs` | RenderInput plus `allowPaper`/`title` | 202 `{id,state:"accepted",note}`; poll the job; 409 when busy, no automatic queue/retry |
| GET `/v1/jobs` | None | `{jobs:[Job]}`, newest first, latest 30 session jobs |
| GET `/v1/jobs/{id}` | None | One Job |
| POST `/v1/jobs/{id}/cancel` | `{}` | Only the active sending job; stops/disconnects, cannot recall printer-buffered data |
| POST `/v1/feed` | `{"lines":150,"allowPaper":true}` | Integer 0–2000; `{acknowledged:true,lines,physicalCompletion:"unknown"}` after media check and ACK |
| GET `/v1/logs` | Optional `?limit=100`, clamped 1–500 | `{order:"newest_first",logs:[{timestamp,type,text}]}`; may include local device information |
| GET `/v1/openapi` | None | Bundled OpenAPI document |

### Readings and jobs

A reading is `{command,name,value,rawHex,sampledAt}`. Values are cached samples with ISO8601 times; POST query for a new sample. An ACK without field data gives `hasData:false` and `payloadHex:null`. `charging` is `"unknown"`; a rising battery estimate is not treated as a real-time charging flag. Research catalog entries are not automatically callable.

Job fields: `{id,title,detail,state,createdAt}`. The wire values of `state` currently match the Chinese GUI:

| Wire value | Meaning |
|---|---|
| `发送中` | Sending |
| `已发送，待核对纸面` | Sent; check physical output |
| `未完成` | Incomplete |
| `已停止` | Stopped |

HTTP 202 means accepted, not mechanically complete. “Sent” means the data/ACK sequence finished. Progress belongs to the current global job. Jobs are not durable across app restarts, and failures are not automatically replayed.

### RenderInput and paper permission

Provide exactly one of `text` (string) or `imageBase64` (the Base64-encoded image file, without a data-URL prefix). Optional fields are partial `settings` and a real Boolean `includeRaw`. Jobs also accept a `title` truncated to 120 characters and `allowPaper:true`.

Images are decoded with ImageIO; paths and remote URLs are not accepted. The 2 MiB JSON-body cap gives a practical image payload of roughly 1.5 MiB before Base64 encoding. Source images are additionally limited to 40 million pixels and decoded previews to a 4096-pixel edge. Text allows 10,000 characters; final output height is at most 1400 rows. These are software limits. PNG and raw output describe the same 576-column raster.

Both jobs and feed require the sidebar's **global paper switch** and an actual JSON Boolean `allowPaper:true`; numeric `1` and string `"true"` do not count. Starting the API does not enable printing. Before printing, the app checks fresh cover/paper state, sets density, and reads it back.

## All settings

Geometry uses output dots/pixels, pauses use milliseconds, waits use seconds, and feed uses device lines. Unknown fields produce 400. Preserve the enum strings below exactly; English documentation does not translate the API's wire values.

| Field | Default | Range / values |
|---|---|---|
| version | 1 | Only 1 |
| fontName / fontSize / bold | PingFang SC / 30 / false | Installed font name; size 8–96 |
| lineSpacing / alignment | 4 / `居中` | 0–40; `居左`, `居中`, `居右` (left, center, right) |
| marginX / marginY | 12 / 12 | 0–120 / 0–200 |
| contentWidth | 552 | 48–576 and `contentWidth + 2 × marginX ≤ 576` |
| imageFit | `适配宽度` | `适配宽度`, `保持像素`, `裁切填满` (fit width, preserve pixels, crop to fill) |
| cropHeight / rotation | 400 / 0 | 24–1400; 0/90/180/270 |
| conversion | `阈值` | `阈值`, `Floyd–Steinberg`, `有序抖动` (threshold, error diffusion, ordered) |
| threshold / brightness | 128 / 0 | 0–255 / -100–100 |
| contrast / gamma | 1 / 1 | Each 0.2–3 |
| invert | false | Boolean |
| density / copies | 100 / 1 | Integers 0–100 / 1–20 |
| feedBefore / feedAfter | 0 / 150 | Integers 0–2000 |
| feedBetweenCopies | false | Feed between copies |
| packetBytes | 1008 | 72–1008, multiple of 72 |
| bleChunk | 180 | Integer 20–180, also limited by CoreBluetooth |
| writePauseMS / framePauseMS | 10 / 0 | Integers 5–200 / 0–1000 |
| timeoutSeconds / finishWaitSeconds | 6 / 2 | Integers 3–30 / 1–15 |

## Errors and HTTP limits

Errors use `{"error":{"code":"paper_disabled","message":"..."}}`. Handle HTTP status and `code`; messages are user-facing and may be Chinese.

| Status | Meaning |
|---|---|
| 400 | Invalid HTTP/JSON/settings/rendering input or failed protocol operation |
| 403 | Rejected token, Host, Origin, or paper permission |
| 404 | Unknown route/job |
| 409 | Busy, not ready, offline, or invalid connection state |
| 415 | POST Content-Type mismatch |
| 429 | Rate limit |
| 503 | Service not ready |

HTTP/1.1 supports GET/POST/OPTIONS, at most 8 connections, 12 seconds per connection, and 20 authenticated requests/second. Headers are limited to 16 KiB, bodies to 2 MiB. Chunked transfer, pipelining, persistent connections, and duplicate headers are rejected. Responses use `no-store`. Polling every 0.5–1 second is sufficient; do not retry failures indefinitely.

## Examples

Copy the token in the GUI and store it in the local `PAPERANG_TOKEN` environment variable.

```sh
curl --noproxy '*' -H "Authorization: Bearer $PAPERANG_TOKEN" \
  http://127.0.0.1:8176/v1/status
curl --noproxy '*' -H "Authorization: Bearer $PAPERANG_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"text":"Hello P2","settings":{"fontSize":30},"includeRaw":true}' \
  http://127.0.0.1:8176/v1/render
```

Python (make `Clients/` available on the module path):

```python
import os
from paperang_client import PaperangClient
p = PaperangClient(os.environ['PAPERANG_TOKEN'])
preview = p.render('Hello Paperang P2!', {'density': 100, 'feedAfter': 150})
# Connect and enable the global paper switch before explicitly printing.
job = p.print_text('Hello Paperang P2!', allow_paper=True)
print(p.wait(job['id']))
```

Browser/Node (a browser origin must be allowlisted):

```js
import {PaperangClient} from './Clients/paperang-client.js';
const p = new PaperangClient(tokenEnteredLocallyByUser);
const preview = await p.render('Hello P2', {fontSize: 24});
imageElement.src = `data:image/png;base64,${preview.previewPngBase64}`;
// Submit after a user print action; the global switch must also be on.
const job = await p.printText('Hello P2', {allowPaper: true});
console.log(await p.job(job.id));
```

Clients expose a generic request method for other endpoints. Python bypasses system HTTP proxies for loopback; JavaScript uses fetch. The `/v1` path is the API contract version, separate from app version 0.2.3 and OpenAPI format version 3.0.3.

## v0.2.3 tray consent

Tray mode authorizes network jobs independently of workspace paper consent. A job may omit allowPaper in active tray mode; explicit false is always refused. Feed still requires explicit true. Status adds appVersion/trayMode/networkPrintAllowed/automaticNetworkConsent/workspacePrintingEnabled; printingEnabled is effective network consent. [Complete behavior](TRAY-MODE.md).
