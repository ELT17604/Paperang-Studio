# Protocol acquisition and verification record

[Documentation](README.md) · [简体中文](ACQUISITION.zh-CN.md)

Compiled on 2026-11-04 from local captures, native send records, and the user's physical observations. Full historical artifacts remain outside the published source at sibling `PaperangStudio-Development/Legacy/PaperangP2/`. This record omits personal device identifiers.

## 1. Environment and failed approaches

The original task used macOS with a connected P2. Python/CLI entry points and dependencies were inspected in an isolated environment. `paperang-cli0.2.1` with `paperang-p2-lib1.2.1` lacked `_ble`; version0.4.0rc3 imported but the CLI still selected an unsuitable A5 path. Exit status zero without paper output did not establish success.

The classic serial endpoint `/dev/cu.Paperang_P2` opened and once returned an error, but did not provide stable queries. Investigation moved to GATT and community legacy implementations, without a Bluetooth reset or firmware upgrade.

## 2. GATT and handshake

Discovery identified Paperang_P2, FF00 service, FF02 writes, FF01 data, and FF03 credits. Subscribe to both notifications, send key-zero registration `0218000400219576351cdf442103` with initial CRC seed35769521, validate the response, then use seed0.

Version/model/serial/battery/status were queried separately. ACKs were distinguished from data responses. MAC query08 unexpectedly returned0B, voltage/temperature returned only ACKs, and country returned an empty data payload. Bluetooth-module GATT strings were kept separate from printer firmware.

## 3. First physical print and feed

“Hello Paperang P2!” was rendered as a 576-column one-bit raster, split into1008-byte whole-row frames with increasing indexes, then180-byte BLE chunks under credit control. The user initially saw no text, then explicitly confirmed the printed greeting. That physical observation established the working path.

Feed120 lines was too short; a second120-line feed followed. Later calibration established **150 lines once after a batch** as the user's preferred setting, replacing240. Subsequent tests used density100 on expired paper, as requested.

## 4. Cover/paper differential

After disconnecting GUI/phone clients, a single read-only session sent GET_STATUS0C/01 and retained validated frames/notifications. The user changed closed/loaded → open with roll → closed/empty → restored/closed. Stable samples were08→19→09→08, establishing cover bit4 and closed-cover paper-absent bit0. Bit0 also sets while open, so paper cannot independently be classified then. Background bit3 was not calibrated.

A source-defined short-name query30/01→31 returned ASCII P2 and entered the read-only allowlist. No command-number sweep followed.

## 5. Width tests

A576×220 marker with x=0/575 borders and48-dot ticks was printed without scaling or extra margins. The user confirmed all four borders and edge lines, with paper margin remaining. Then A/C576-dot controls surrounded a640-dot logical stream B. A/C were complete; B produced no usable wider borders and no clearly observed wrap.

Keep576 as the supported canvas. Do not claim a known640-column crop/wrap mechanism. Source scripts, PNG/raw patterns, and send logs remain in the external Research/evidence archive. Millimeter width, firmware longest image, and buffer depth were not measured.

## 6. Battery and LED

Battery13% coincided with status08; later charging samples58% and65% had status00. That supports rising estimated charge during the interval, not a direct charging-current or Boolean measurement. The user observed continued white blinking across cable unplug/replug; no synchronized protocol sampling established an insertion response. Later GUI82/100 values are additional snapshots only.

## 7. Native workspace and raster replay

The independent SwiftUI/CoreBluetooth implementation uses the same final raw raster for preview and sending. A 17-frame native image job matched offline rendering byte-for-byte. The user confirmed orientation, completeness, and 150-line tear-off feed. Features then expanded to settings, presets, image transforms/dithering, job history, media checks, and raw logs.

## 8. Engineering extension

The next update added candidate filtering, newest-first logs, real PTY, local HTTP API, clients, and a single global paper switch. External checks cover actual controlling-terminal behavior and HTTP rendering/authentication. See [architecture](ARCHITECTURE.md) for recorded scope.

ESP32 source added39/40/42/7F/81 research clues but also exposed hardcoded fields, fallthrough, and missing incoming CRC validation. HACS/P2-library/P1-Connect comparisons preserve SPP/BLE, countdown/increasing-index, and A5/legacy distinctions. Revisions are in [source-revisions.json](source-revisions.json).

## Reproduction sequence

1. Use one selected P2 connection, disconnect competing clients, and keep the cover/paper normal.
2. Subscribe FF01/FF03 and perform the verified handshake.
3. Serialize04/01,06/01,10/01,0C/01,30/01; validate length/CRC and separately record ACK, data, and timeout.
4. Perform media-state comparisons read-only, without printing while open/empty.
5. Print standard576-column text/markers at density100 with 150-line final feed; obtain a physical observation before claiming success.
6. Record requests, notifications, settings, times, and user actions separately. Keep identifiers local or redact before sharing.

## Archive map

- `Legacy/PaperangP2/evidence/`: GATT, queries, battery, feed, marker/overwidth logs.
- `Legacy/PaperangP2/Research/`: upstream snapshots, stage JSONL/summaries, profiles, and pattern generators.
- `Legacy/PaperangP2/Native/Reports/`: historical native sessions, raw sends,17-frame comparisons, and build records.
- `Validation/`: current external checks and their outputs.

These are local historical artifacts, not build dependencies. A documented past observation is not a newly repeated measurement.
