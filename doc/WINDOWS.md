# Windows Client

[简体中文](WINDOWS.zh-CN.md) · [Build](../Windows/README.md) · [Documentation](README.md)

Paperang Studio WinClient runs on Windows 10 2004+ and Windows 11. It supports the legacy Paperang P2 over USB or Bluetooth. The installer includes the application runtimes; keep adjacent files together when using a portable build.

## Connect

1. Open **Printer**. Choose automatic USB selection or Bluetooth.
2. For Bluetooth, switch on the P2 and scan. Devices appear immediately; select and connect while scanning continues, or stop the scan first. Signal updates retain your selection.
3. Check the connection card. Selection alone does not connect. Connected status identifies USB/Bluetooth, the device and battery/media readings; failures retain their reason.
4. Refresh readings or disconnect on this page. Density/feed actions require a connected printer.

## Edit and print

The main column contains the content editor and settings. The side preview updates automatically and can be enlarged. Select text, image, QR, imported raster or layout tools. Add independently prepared content to a layout, adjust objects, then confirm the layout before printing/exporting.

**Import file**, **Import JSON** and file drop support PNG/JPEG/BMP/GIF/TIFF, RAW, and Mac/Android schema 1 raster1 or complete parameter-preset JSON. Presets restore layout, image processing, density, copies, feed and transport settings. Portable font names map to installed Windows fonts. Invalid files leave the current content intact; importing never sends to a printer.

Defaults are 576 printable dots, zero margins, density 100 and final feed 160 lines. BLE defaults match Android: 1008-byte raster packets, writes up to 180 bytes, 10 ms minimum write interval, no extra frame pause, 6 s response timeout and 2 s finish wait. Device flow control may increase elapsed time. Existing saved preferences remain explicit choices. Continuous terminal batches default to zero extra feed.

Enable the global paper switch, review copies/feed and choose **Print**. An interrupted send may have reached the printer; check the paper before deciding whether to retry. Uncertain Relay jobs are retained and never replayed automatically.

## Remote printing and tools

- **Remote printing:** configure the server and paired owner file or scoped M credential. Choose automatic reception or a queued task. Full unclaimed-content preview requires a C management login.
- **Templates:** edit ordered blocks, preview a sample, import/export JSON and save/apply.
- **Management:** independently enter the server and C credential, or use a paired owner file. Sessions expire; signing out clears the credential/detail view. Pairing never uploads private keys.
- **Local API:** explicitly start the loopback service; Windows defaults to port 8766. Requests require its token, and paper operations also require the global switch and `allowPaper:true`. See [API platform differences](API.md).
- **Paper terminal:** manual text or a Windows ConPTY shell; preview/export/discard/flush buffered output. Automatic paper output is opt-in.
- **Settings:** sign-in startup, credential-free configuration export, version and project links.

Closing the workspace leaves the receiver in the tray, but closes its window-owned API/Shell. Use Quit to exit. Saved configuration uses current-user Windows encryption; library content is saved only when explicitly requested. Compatible historical storage identifiers preserve existing preferences and pending results.

## Build and validation

See [Windows build instructions](../Windows/README.md), [implementation and acceptance scope](WINCLIENT.md) and [legacy WinLite](WINLITE-LEGACY.md). WinClient source on main is not a claim of completed Mac feature parity: HEIC import, exact API response/schema parity, some tray shortcuts and hardware/installer acceptance remain documented limitations.
