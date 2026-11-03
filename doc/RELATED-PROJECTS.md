# Related project comparison

[Documentation](README.md) · [简体中文](RELATED-PROJECTS.zh-CN.md)

Reviewed 2026-11-04 using the supplied repository/PyPI pages and local source snapshots. These projects provide engineering references, but no newly confirmed charging Boolean or LED control for this particular P2.

## paperang-hacs

Sources: [README](https://github.com/mdj2812/paperang-hacs), [device locks](https://github.com/mdj2812/paperang-hacs/blob/95211c336ef8646eb9a0a50c2574a184ab3bfa66/custom_components/paperang/core/blocking.py), [telemetry](https://github.com/mdj2812/paperang-hacs/blob/95211c336ef8646eb9a0a50c2574a184ab3bfa66/custom_components/paperang/core/state.py). Pinned commit `95211c336ef8646eb9a0a50c2574a184ab3bfa66`, README release v1.5.2.

Seven services cover text, image, QR, pickup code, self-test, status, and feed. Six telemetry entities include battery/status/voltage/temperature/density/connection. Per-device locks serialize polling and printing; static identity is separated from dynamic telemetry. Paperang Studio similarly shares a busy lock and timestamped readings.

Its fallback uses SDP FEE7 through Linux bluetoothctl for paired/renamed devices. FEE7 is not exclusive to Paperang, and classic SDP discovery is not a CoreBluetooth identity check. Studio retains name filtering plus Show all. A sensor definition does not prove this P2 returns voltage/temperature. No HA installation or automatic paper-use rules were added; later HA integration can use Studio's API.

## paperang-p2-lib

Sources: [PyPI 1.2.1](https://pypi.org/project/paperang-p2-lib/) and [repository](https://github.com/mdj2812/paperang-p2-lib). Its Bluetooth path is **classic SPP**, not a drop-in CoreBluetooth BLE backend.

Useful ideas include Transport/Protocol/Printer separation, JSON image profiles, CJK fallback,90-degree labels, QR and pickup-code host rendering. Its576-dot width,72-byte rows, and1008-byte payloads agree with local observations.

Preserve differences: its default CRC seed35769521 versus Studio's post-registration seed0; packetRemain countdown versus the verified BLE increasing index; `_send_get` assumes cmd+1 but the local08→0B response is exceptional; density usesu16LE there versus verifiedu8 here. “48 commands” includes responses/events and is not48 physical validations.

Voltage returnsu16LE millivolts only when at least2 response bytes exist, otherwiseNone; temperature likewise requires data. The factory setter does not document a validated mode/exit sequence. Historical CLI/import failures are recorded in [acquisition](ACQUISITION.md); the library was not reinstalled/run for this comparison.

## paperang-p1-connect

Sources: [README](https://github.com/Orangray-MDJX/paperang-p1-connect), [traditional enum](https://github.com/Orangray-MDJX/paperang-p1-connect/blob/9ec2cb535ec8ac09a7dc1ce2464bc2a114d569bf/paperang_p1/protocol.py), and [A5 specification](https://github.com/Orangray-MDJX/paperang-p1-connect/blob/main/docs/protocol-a5.md). Local commit `9ec2cb535ec8ac09a7dc1ce2464bc2a114d569bf`; the current README was also reread during the investigation.

Useful architecture includes durable queues, held media faults, unknown interrupted sends, no automatic replay, cancellation boundaries, and HTTP/IPP/MCP separation. BLE-touch-then-RFCOMM wake behavior is another device-specific clue. Its traditional30→31 helped obtain the real P2 short name.

Its primary hardware is P1 A5 firmware 01.03.18,384 columns/48 bytes. A5 sessions, paper-width/media queries, two-meter images, and wake timing cannot simply transfer to this legacy576-column P2. Studio does not currently implement IPP, MCP, durable queues, or verified deep-sleep wake.

## Adopted and future work

Current design uses layered Swift modules, an evidence catalog, sampled telemetry, one device lock, task IDs/cancellation without auto-retry, JSON settings, and local HTTP. Future steps can add host-rendered QR/labels, durable task states, PDF/banded long images, then IPP/MCP. Keep final output576 columns and do not require factory/firmware writes for those features.
