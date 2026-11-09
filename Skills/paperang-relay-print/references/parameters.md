# Parameters and examples

Current sender implements **v0.3.1 Relay API v2** / raster1 schema 1, compatible with v0.3.1 MacClient and WinLite receivers. Historical relay v1 is retired (410). The Mac-local API v2 is a separate interface.

| Option | Meaning / range |
| --- | --- |
| `--mode text` | Submit text for the receiver's current template; image/typography options do not apply. |
| `--mode raster` | Render text locally; images always use raster1. |
| `--caption-file PATH` | Optional UTF-8 image caption, rendered by the receiver; local preview shows the packed image only. |
| `--font PATH` | TTF/OTF/TTC; defaults to Microsoft YaHei or an available system CJK font. Missing CJK font is an error. |
| `--font-size 8..96` | Pixel size, default 24. |
| `--align left/center/right` | Alignment within margins. |
| `--line-spacing 0..40` | Extra rows between text lines, default 4. |
| `--margin 0..120` | Left/right white margin, default 12 dots. |
| `--margin-y 0..200` | Top/bottom white rows, default 12. |
| `--max-height 1..1400` | Final raster limit, default **1000** to leave some receiver-footer space; never silently crop text. |
| `--fit contain/width/cover` | Fit within width/height, fit width (error if too tall), or center-crop (requires height). |
| `--height 1..1400` | Image canvas height including vertical margins. |
| `--rotate 0/90/180/270` | Clockwise after EXIF orientation. |
| `--threshold 0..255` | Black/white threshold, default 128. |
| `--dither none/floyd/bayer` | Threshold, Floyd–Steinberg, ordered Bayer. |
| `--brightness -100..100` | Additive grayscale brightness. |
| `--contrast 0.2..3` | Mid-gray contrast, default 1. |
| `--gamma 0.2..3` | Gamma, default 1. |
| `--invert` | Invert grayscale before monochrome conversion. |
| `--copies 1..20` | Explicitly requested copies as distinct submissions, with separate receipts. |

Images: first supported raster frame, at most 64 MiB and 40 million source pixels. Transparency composites on white. Payloads include dimensions, exact packed bytes and SHA-256. Text wrapping keeps common combining/ZWJ clusters together; glyph coverage depends on the font.

## Client-only controls

POST `/v2/jobs` has **no per-job thermal density, paper feed, template, copies or client-configuration fields**. The helper realizes copies as separate explicitly requested jobs. Do not pretend unsupported fields override the client.

Density 0–100, feed 0–2000 rows and active template belong to local receiver settings. The receiver may shrink raster1 into its Body block and append sender/time/ID. For exact pre-rendered layout, the user must select a body-only template with margin/gap zero and set density/feed locally. Do not silently replace a template for one remote job. Darker image pixels do not change thermal density.

The **combined image + local footer** must fit 1400 rows. A 1400-row upload can fail with a footer. Reduce height/fontsize or explicitly adjust the template. Do not split into more printed jobs without user scope.

## Examples

Global options (`--server`, `--key-file`, `--allow-http`, `--config`) go **before** the command. Paths below are examples, not real secrets.

```text
relay_print.py prepare --text-file C:/work/note.txt --mode text --output C:/work/job.json
relay_print.py prepare --text-file C:/work/note.txt --mode raster --font-size 30 --align center --margin 20 --output C:/work/job.json --preview C:/work/preview.png
relay_print.py prepare --image C:/work/photo.png --fit contain --max-height 800 --rotate 90 --dither floyd --contrast 1.1 --output C:/work/job.json --preview C:/work/preview.png
relay_print.py prepare --image C:/work/label.png --fit cover --height 300 --threshold 150 --output C:/work/job.json --preview C:/work/preview.png
relay_print.py submit --job C:/work/job.json --receipt C:/work/receipt.json --confirm-print
relay_print.py submit --job C:/work/job.json --receipt C:/work/copies.json --copies 2 --confirm-print
relay_print.py --server https://example.com wait --receipt C:/work/copies.001.json --timeout 120
relay_print.py status
relay_print.py configure --server-url https://example.com --submit-key-file C:/private/submit.keys.json
```

Configure stores only endpoint and key-file path, never the key. An A/B random key is required; a saved Windows endpoint alone does not authorize submission. Default config: `%LOCALAPPDATA%/ELT/PaperangRelaySkill/config.json` or `~/.config/paperang-relay-print/config.json`. `PAPERANG_RELAY_URL` and `PAPERANG_RELAY_SUBMIT_KEY` can override configuration.

Quota units are **text characters + raster rows**, not image bytes. Public policy gives the actual per-job and hourly allowances; anonymous default 500 units is often too short for large photos. Supply a submit key or reduce size, rather than bypassing quota with extra anonymous jobs.

Each attempt file contains server, job IDs and acceptance/status-readable flags, not a bearer receipt. A cannot wait for status; B wait requires its matching identity key. Wait supports queued/claimed/printing/sent/confirmed/unknown/rejected. A bounded wait ending while queued is not failure or permission to resubmit. No POST is retried automatically, including 429 and timeouts.
