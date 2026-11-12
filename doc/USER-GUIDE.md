# User guide

[MacClient 0.3.3 upgrade](RELEASE-v0.3.3-macclient.md) · [QR, freeform layout and local bitmap import](MACCLIENT-WORKSPACE.zh-CN.md)


[MacClient 0.3.1 fix3 upgrade / 升级与验证](MACCLIENT-v0.3.1.md)
> v0.3.0: historical observations/screenshots below remain recorded evidence. Current relay authentication, history access, dependencies and validation status are defined by [API v2](AUTH-v2.md), [upgrade](UPGRADE-v0.3.0.zh-CN.md) and [matrix](FEATURE-MATRIX.md). Local API v2 and BLE/template records are separate contracts.

[Documentation](README.md) · [简体中文](USER-GUIDE.zh-CN.md)

## First connection

Build with `./scripts/build.sh`, then open the sibling `PaperangStudio-Development/Release/Paperang Studio.app`. The app displays as Paperang Studio and retains the `local.paperang.lab` bundle identifier. It starts with local previews and does not automatically scan, connect, or print.

Open **Printer** (`打印机`) → **Scan nearby devices** (`扫描附近设备`) → connect to `Paperang_P2`. After the handshake, the app checks the model and reads battery, media status, and density. The default list shows Paperang-family names; **Show all Bluetooth devices** (`展示全部蓝牙设备`) also reveals renamed or unrelated devices. Candidate matching is a discovery aid, not device authentication.

## Print workspace

The left pane edits settings; the right pane shows the **final one-bit raster sent to the printer**. Preview details include dimensions, byte count, and black-pixel coverage. Export PNG or raw raster bytes from the preview.

| Control | Behavior |
|---|---|
| Text | Chinese/English, explicit newlines, wrapping, system fonts, size 8–96, bold, line spacing, and alignment |
| Image | PNG, JPEG, HEIC, TIFF, BMP, and the first GIF frame; orientation handling and white compositing for transparency |
| Fit | Fit width, preserve pixels (oversized input still fits the content area), or crop to fill a chosen height |
| Rotation | 0°, 90°, 180°, or 270° |
| Geometry | Fixed 576-dot output; `contentWidth + 2 × marginX ≤ 576` |
| Monochrome | Threshold, Floyd–Steinberg, or ordered dithering; brightness, contrast, gamma, and inversion |

GUI image import allows up to 64 MB and 40 million source pixels; decoded previews have a maximum edge of 4096 pixels. Text is limited to 10,000 characters and one rendered batch to 1400 rows. Invalid geometry blocks preview/printing with an explanation. These are application limits, not measured firmware limits.

## Printing and feed

Enable **Global paper use** (`全局允许耗纸`) once in the sidebar. It controls the workspace, paper terminal, API, and feed actions for the current app run. Turning it off pauses automatic terminal printing and stops an active print job; buffered printer data cannot be recalled.

- Density: 0–100, default 100; written and read back before each print.
- Copies: 1–20. Feed before/after: 0–2000 device lines.
- Default final feed: 150 lines, physically confirmed as convenient on the tested unit.
- With feed after every copy disabled, the final feed occurs once after the whole batch.
- **Feed only** uses the current feed-after value.

The app checks fresh cover/paper status before printing and stops subsequent packets on a media fault. Each job uses a snapshot of its raster and settings. Progress shows copies and packets. **Stop and disconnect** cancels future sends, not data already accepted by the device. “Sent” is a transport result; check the paper. Failed jobs are not retried automatically.

## Presets and history

Default, photo, label, and line-art presets change local parameters without immediately writing the printer. Import/export JSON includes layout, image conversion, density, copies, feed, and transport settings; versions and ranges are validated. Examples are in `Configurations/`. Text and image paths are not embedded in presets.

History keeps the current session's latest 30 jobs, including title, settings summary, time, and send status. Pending jobs are not restored or replayed at startup.

## Engineering tools

Enable **Show engineering tools** (`显示工程界面`) in the sidebar.

1. **Device fields:** known GET commands, interpreted values, raw bytes, sampling times, and GATT properties.
2. **Protocol catalog:** command numbers, categories, evidence levels, and descriptions.
3. **Raw commands:** allowlisted GETs use payload `01`. Engineering writes require separate unlock and per-write confirmation; payloads are validated. Firmware, unknown commands, and compressed data are not accepted for blind sends.
4. **Transport settings:** raster payload 72–1008 bytes in whole rows, BLE chunks 20–180 bytes within the device limit, pauses, timeouts, and finish waits. Excessive packet counts are rejected.
5. **Logs:** newest first, with frames, BLE chunks, notifications, CRC results, and errors. JSON export remains chronological and includes settings, readings, and jobs.

The tested P2 uses 576 dots/72 bytes per row and nominal 300 DPI. Cover-open is `0x0010`; paper-absent is `0x0001` only when closed. Charging flags, LED control, compression, and factory-mode semantics remain research topics documented in the [protocol reference](PROTOCOL.md).

## Build and additional features

The build script packages and ad-hoc signs the app, with all outputs outside the source directory. Validation programs and historical evidence are also external; they are not run by the current build script. See [architecture](ARCHITECTURE.md), [paper terminal](TERMINAL.md), and [HTTP API](API.md).

## Refreshing Bluetooth discovery

A scan runs for12 seconds, shows the number of actual broadcasts, and cancels its previous timer so it cannot stop a later scan. Results distinguish live BLE advertising from recalled device identifiers and system-connected peripherals. History is not proof of online status; connection still performs the normal model/handshake checks. If all broadcasts are missing, disconnect any session and use Reinitialize scanning (`重新初始化扫描`). This recreates only the app's central manager, without resetting system Bluetooth. Avoid simultaneously running old copies of the app.

## Menu bar mode (v0.2.3)

Use **进入托盘模式** in the sidebar/File menu for background network printing. [Lifecycle, consent and restoration](TRAY-MODE.md).
