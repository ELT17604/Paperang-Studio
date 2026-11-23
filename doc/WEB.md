# Web workspace · v0.3.3-web

[简体中文](WEB.zh-CN.md) · [Feature matrix](FEATURE-MATRIX.md) · [Static application](../Web/)

## First print

1. Open the workspace in a browser with Web Bluetooth, such as Chrome on Android, macOS or Windows. Use HTTPS on a hosted site or localhost during development. Turn on Bluetooth and the printer.
2. Start device selection and choose the P2 in the browser picker. The browser asks permission to connect to the selected device. If another client has the printer open, disconnect it there first. [Browser device access](https://developer.chrome.com/docs/capabilities/bluetooth)
3. Enter text, load an image, compose layers, draw on the canvas or import a raster1 JSON sheet. Review the monochrome preview and its dimensions. A 576-dot row occupies 72 bytes, with the leftmost pixel in the most significant bit. [raster1 file format](../Skills/paperang-local-raster/references/raster1.md)
4. Set the margins and paper feed, enable printing and press Print. Keep the page open during transmission. If a connection breaks during a job, check the printed paper before submitting another copy.

## Editing modes

| Mode | Controls |
|---|---|
| Text | Type or paste a note, choose the font and size, then adjust alignment, weight and line spacing. |
| Image | Fit a photo to the paper, preserve its pixels or fill a crop. Adjust rotation, brightness, contrast, gamma, inversion and monochrome conversion. |
| Layout | Combine text, framed labels, images and QR codes. Adjust each layer’s position, size and stacking order. |
| Draw | Use a finger, stylus or mouse. Adjust the pen width, erase strokes, undo and redo. |
| JSON | Load a raster1 sheet or apply a native workspace preset. |

For native JSON interchange, import a raster1 sheet to keep its packed dots, or a schema-1 workspace preset to apply the supported text, image and printer settings. raster1 files include width, height, bit order and a SHA-256 checksum.

## Paper and transfer settings

| Setting | Default | Effect |
|---|---:|---|
| Left, right, top and bottom margins | 0 px | Insets on the rendered sheet |
| Trim trailing blank rows | On | Removes unused content rows, then retains the configured bottom margin |
| Copies | 1 | Number of copies in a job |
| Density | 100 | P2 print intensity |
| Initial / final feed | 0 / 160 lines | Moves paper before the first copy and after printing |
| Write chunk ceiling | 180 bytes | Caps each GATT write together with the printer’s flow limit. Set 20 bytes for a link limited to the default ATT MTU. |
| Write start interval | 10 ms | Minimum interval between GATT write starts |

The Android session grants each write one flow credit. It waits for the previous write to finish, splits raster data into frames of up to 1008 bytes (14 rows), and adds no pause between raster frames. Its nominal chunk ceiling is 180 bytes, further limited by the printer and negotiated ATT MTU. Web Bluetooth does not expose the negotiated MTU to JavaScript. The selected chunk limit, browser Bluetooth stack and printer credits affect elapsed time. [Android transfer constants](../Android/src/com/elt/pprstudio/adc/P2Transfer.java) · [Android session](../Android/src/com/elt/pprstudio/adc/P2Session.java)

## Run locally or deploy

From the repository root run `python -m http.server 8080 --bind 127.0.0.1 --directory Web`, then open `http://localhost:8080/` in the browser. For a phone, use the hosted HTTPS address.

In Cloudflare Pages, import this GitHub repository, select the `main` production branch and leave the root directory at the repository root. Use no framework preset, `exit 0` as the build command and `Web` as the output directory. The application is served as static files. The connected browser handles editing and Bluetooth printing. [Cloudflare Pages static HTML guide](https://developers.cloudflare.com/pages/framework-guides/deploy-anything/)

The workspace follows the browser language and system appearance. Select Preferences → Theme → Frutiger Aero for a glass workspace over a blue-sky lake landscape. The light and dark appearances use separate glass tints. Select Morandi for the muted theme.

## Native implementation references

The following native code paths informed the browser edition. The [feature matrix](FEATURE-MATRIX.md) lists the capabilities of each distribution.

| Client | Editing and raster preparation | Device handling |
|---|---|---|
| MacClient | Styled text, label, image and QR layers are clipped to their frames and composited into a 576-dot bitmap. RAW and schema-1 raster1 imports validate geometry and byte counts. [Composition](../Sources/PaperangLab/PaperComposition.swift) · [Raster import](../Sources/PaperangLab/WorkspaceRaster.swift) | [CoreBluetooth session](../Sources/PaperangLab/Bluetooth.swift) |
| WinClient | Font, alignment, image fit, rotation, tone and dithering controls feed the workspace preview. JSON import distinguishes native settings presets from raster data. [Workspace controls](../Windows/src/Paperang.WinClient/WorkspaceEditing.cs) · [JSON import](../Windows/src/Paperang.WinClient/WorkspaceJson.cs) | [Discovery](../Windows/src/Paperang.WinClient/PrinterDiscovery.cs) · [P2 session](../Windows/src/Paperang.Core/P2Session.cs) |
| AndroidClient | Text and graphics are rasterized at paper width. Freehand strokes have adjustable width and undo. Automatic paper length removes trailing blank rows. [Rendering](../Android/src/com/elt/pprstudio/adc/PaperRenderer.java) · [Drawing](../Android/src/com/elt/pprstudio/adc/DrawingEditor.java) · [Raster processing](../Android/src/com/elt/pprstudio/adc/Raster.java) | FF00 service, model handshake, media check, density readback, credit-based writes and feed. [P2 session](../Android/src/com/elt/pprstudio/adc/P2Session.java) |
