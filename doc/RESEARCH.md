# P2 protocol and image research

[Documentation](README.md) · [简体中文](RESEARCH.zh-CN.md)

Recorded 2026-11-04. See [pinned revisions](source-revisions.json), [protocol reference](PROTOCOL.md), and [acquisition steps](ACQUISITION.md). Downloaded third-party programs were not executed; no firmware or factory-mode writes were performed during this research.

## Physical baseline

The user selected **150 final feed lines per batch**, superseding240. It is a practical setting for this machine/loading, not a maximum image length.

A single read-only GET_STATUS0C/01 connection recorded stable closed/loaded08, open/roll-retained19, closed/empty09, and restored08. Thus0x10 means open, and0x01 means no paper only while closed. Opening also sets bit0; the UI prioritizes the open-cover warning. Background0x08 was present throughout that comparison and is uncalibrated. Old Android resource IDs and another brand's masks are not reliable labels for the remaining bits.

Stage files switched before the user completed each action, so early samples may reflect the preceding physical state. Conclusions use stable tail samples, not expected values manually substituted into the log. The app queries fresh media status before printing and blocks if no fresh state arrives.

GET_DEV_NAME30→31 came from the traditional [P1 Connect enum](https://github.com/Orangray-MDJX/paperang-p1-connect/blob/main/paperang_p1/protocol.py); a real query returned ASCII P2 plus a same-command00 ACK. Interpret those bytes as a string, not integer12880. Further unknown numbers were not blindly scanned.

## Additional functions

| Function | Request / data | Evidence and outstanding work |
|---|---|---|
| Factory status | 15→16 | ReadFF; meaning unknown |
| Set factory mode | 14 | One-byte source wrapper; enum, exit, persistence unknown; not tested |
| Density | 19 /1C→1D | Range0–100; historical75, then100; monochrome heat, not RGB color |
| Battery | 10→11 | One-byte percent; historical13 and subsequent rising samples |
| Voltage /temperature | 0E→0F /12→13 | This unit returned ACK only |
| Auto power-down | 1E /1F→20 | Read1800 seconds; SETu16LE, restart persistence unknown |
| Paper type | 2C /2A→2B | Read00; source names “type” and “gap” conflict; not the paper sensor |
| Maximum gap | 27 /28→29 | Read112; units unknown |
| Feed | 1A | u16LE lines, physically verified |
| Feed to head | 21 | u16LE in source, difference from1A untested |
| Self-test /parameters page | 1B /22 | Enumerated, not executed in the research |
| Raw raster | 00 | Physically verified |
| Compressed raster | 01 | Enum only; no verified encoder |
| Firmware data /update | 02 /03 | Not normal debugging operations |
| Bluetooth status | 17 | Candidate device event, not an established host write |
| Short name | 30→31 | Real P2 response |

“48 commands” can count requests, responses, and events, not48 independently tested functions. A factory setter wrapper does not establish safe or complete factory-mode support.

## Width and raster geometry

[Java/Kotlin P2 constants](https://github.com/hurui200320/java-paperang-p2-usb/blob/master/src/main/kotlin/info/skyblond/paperang/Constants.kt) and the [P2 model](https://github.com/mdj2812/paperang-p2-lib/blob/main/src/paperang/models/p2.json) agree on576 bits/72 bytes per row. Combined with real prints, this is the default canvas. Manufacturer nominal300 DPI implies48.768 mm mathematically; head width, paper width, and feed distance still need separate physical measurement.

RGBA must composite onto white before grayscale/threshold/dithering. Pack1=black,0=white, MSB-first, top-to-bottom. Center/pad narrower content and explicitly scale/crop larger content; do not fake wider rows by changing byte stride. Whole-row packets are1008 bytes (14×72), not1023 partially filled rows. The10-byte protocol envelope and BLE chunks are separate sizes. BLE uses the verified increasing index; a USB countdown implementation is not interchangeable.

No verified legacy GET dynamically reports head width. C2 queries do not transfer automatically. Height1400/text10,000 and8-bit packet guards are software limits; they do not demonstrate a maximum-length firmware capability.

## Marker and overwidth experiment

The original576×220 marker placed borders at x=0 and575 and ticks every48 dots, raw72 bytes/row. It was later printed at density100 and final feed150. The user confirmed complete borders and visible edge lines with paper margin remaining. That supports all576 columns, without a ruler-based millimeter calibration.

The next test used A/C576-dot rectangles and a B640-dot logical input (80 bytes/logical row). The sender retained verified72-byte physical rows and1008-byte frame payloads, without inventing a width-setting command. B's72 logical rows total5760 bytes; a pure72-byte stream interpretation would occupy80 physical rows. The combined25200-byte stream corresponds to350 standard rows, followed by150 feed lines.

A/C were complete. B had similar horizontal extent, missing outer borders, and no clear wrap observed. Therefore the test did not yield usable wider output; it did **not** establish the predicted wrap/zigzag mechanism. Keep576 as standard.

Historical artifacts: `width-calibration-576.png/.raw`, `make_width_pattern.py`, `overwidth-640-stream.raw`, `overwidth-640-logical-input.png`, `overwidth-640-expected-at-576.png`, `make_overwidth_pattern.py`, and the corresponding evidence logs. They remain outside this source tree in `PaperangStudio-Development/Legacy/PaperangP2/Research/` and adjacent `evidence/`.

## Cross-model comparison

| Project/model | Useful reference | Do not transfer directly |
|---|---|---|
| tinyprinter /oldP1 | Traditional framing, ACKs, old-app branches |384-column width and unlabeled Android resource IDs |
| Java P2 USB |72-byte rows, image packing/calibration |USB timing and packet numbering |
| mdj2812 P2 library |Models, profiles, rotation, host rendering |Claims of full coverage as hardware proof |
| Yrr0r paperang-web /P1 |Another ISSC channel, whole rows, Markdown rendering |CRC-skipping parser and its512/480 transport sizes |
| P1 Connect |30→31, protocol separation, queues, IPP |A5 media050C/050D and384 columns |
| burndown C2 |Dynamic DPI/head metadata, AirPrint design |A5/5A TLVs,1248 dots/110 mm |
| D1 /ZYB-D1 model |Model metadata,384-dot/PID5585 candidate |Claiming verified compatibility without hardware |

Names and FF00 alone do not identify firmware generation. Keep transport, protocol negotiation, model identity, and geometry separate. See [related projects](RELATED-PROJECTS.md) and [sources](SOURCES.md).

## Advanced image work

Use the stable00 raster path: decode/orient/composite → rotate → fit/crop/pad576 → threshold/dither → pack → preview/export/send identical bytes. Enforce memory/height guards, whole rows, credit backpressure, and media-fault cancellation. Feed150 once per batch. QR/barcodes should use integer module scaling and quiet zones, not photo dithering. Text, tables, Markdown, and PDF pages can share host rasterization.

The early research preceded image-import UI; the current workspace already implements image import, transforms, and dithering. Firmware long-image/buffer limits remain unmeasured. Longer jobs need independent band/completion tests before IPP/AirPrint. Factory mode, compression, and updates are not prerequisites for image printing.
