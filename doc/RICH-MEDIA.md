# Remote rich media and drawing

[简体中文](RICH-MEDIA.zh-CN.md) · [Remote printing](REMOTE-PRINTING.md)

## Implemented

The relay supports legacy text jobs and schema-versioned `raster1` content. The mobile-friendly website offers **Content → Drawing**, finger/stylus/mouse input, adjustable pen width and canvas height, undo (10 strokes), clear, and an optional caption. Sending freezes the captured composer, packs its white-composited image into MSB-first monochrome rows, and uses the existing job lifecycle, receipts and short IDs. A dot counts as drawing. Closing/reloading loses the drawing draft; receipts retain their existing session behavior. This is a raster drawing editor, not a vector document editor or general photo uploader.

The Mac must run this updated build. It advertises `X-Paperang-Content-Types: text,raster1`; old clients see only text jobs and cannot claim raster jobs. Upgrade both server and app before use. The server advertises `capabilities.contentTypes` and `contentSchemaVersion` at `/v2/public/status`; the website disables Drawing when this capability is absent.

The Mac lists metadata only, downloads full content on selection/print, verifies dimensions, exact byte count and SHA-256, and previews the exact raster that will be submitted. Built-in styles print the drawing at its native 576-dot width with selected text fields/caption appended as a footer. In custom templates, the Body block scales the image proportionally into its configured margins, using one-bit nearest-neighbor sampling; margin 0 preserves the original raster exactly. Local density and feed remain authoritative. Custom templates apply to both text and image jobs. Their Body block renders the fitted image first, then its optional caption; other blocks retain their configured order and typography. Combined image and footer must fit 1400 rows; reduce image height or disable footer fields if necessary. Copying a drawing to the workspace copies its rendered image and footer for normal local image controls.

## POST /v2/jobs

Existing `{ "text": "hello" }` is unchanged. A drawing uses the same endpoint, submit key/anonymous rules, and reply `{id,shortID,receipt,state,resetAt}`:

```json
{
  "text": "Optional caption",
  "content": {
    "schemaVersion": 1,
    "type": "raster1",
    "width": 576,
    "height": 384,
    "encoding": "base64",
    "bitOrder": "msb-first",
    "blackIsOne": true,
    "data": "<base64 of exactly 72 * height bytes>",
    "sha256": "<optional lowercase SHA-256 hex of decoded bytes>"
  },
  "browser": {"timezone": "Asia/Shanghai"}
}
```

Rows run top-to-bottom; bits run left-to-right, high bit first, 1 black / 0 white. Every row is 72 bytes. Height is an integer 1–1400, additionally bounded by the caller's message allowance. Browser preview uses white background and threshold 128. SHA-256 is calculated in the browser when Web Crypto is available; the server always computes it and rejects a supplied mismatch. The Mac always requires the server-provided hash. The client does not accept protocol commands, executable markup, remote image URLs or unknown content versions/types.

Quota units: **one text character + one raster row**. A job must fit the anonymous/authenticated `*_max_length`; the same units debit `*_hourly_chars`, atomically with job-count admission. Thus the default anonymous limit 500 allows a 384-row image plus 116 caption characters. This also limits paper length when text is empty. There is no separate media flag to install: existing quota CLI flags remain effective. The page shows used/allowed units and disables sending when over limit. Default hourly job/IP limits still apply.

Only POST `/v2/jobs` accepts up to **160 KiB (163840 bytes)** JSON; all other JSON requests remain 64 KiB. The largest raw raster is 100800 bytes, base64 134400 characters. Ensure reverse proxy body limits allow at least 160 KiB. The bounded JSON envelope avoids multipart uploads and a second upload lifecycle for these small print-ready bitmaps. Base64 has 33% wire overhead, but one-bit raster is 1/24 of RGB before that overhead (a 576×384 image is 27648 bytes raw).

SQLite stores `payload` as binary BLOB and `content_meta` separately. Lists omit binary payload from the database SELECT and response; `content` metadata and `truncated:true` prompt full download. Full device detail and authorized short-ID lookup include base64 data. Public short-ID lookup and receipt-only status expose only `content:{type:"raster1"}`, never image bytes. Only receipt, matching submission key, target device key or administrator can access private full content via the documented lookup/detail routes. Schema migration preserves existing tasks. Image payload follows the same retention, access, backup and deletion policy as the message body.

## Task state and races

Submission invalidates pending lookups before sending and when accepting the response. Tracking versions reject stale polling responses. A slow lookup cannot replace the new receipt or stop its polling. During submission, tracking controls are disabled. The server records `lastStage` when a task is claimed/sending and preserves it when expiring to unknown. Unknown tasks render the last known accepted/sending stage; older records without that information fall back to Accepted with an explicit uncertain-outcome label, never Queued. Transport completion and manual physical-paper confirmation remain separate.
