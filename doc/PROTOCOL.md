# Paperang P2 legacy protocol reference

[Documentation](README.md) · [简体中文](PROTOCOL.zh-CN.md)

Recorded on 2026-11-04 for the physically tested legacy `02…03` P2. Other firmware, A5/5A devices, and C2 models need separate verification. [Acquisition](ACQUISITION.md) describes how these observations were obtained; [research](RESEARCH.md) distinguishes hardware results from source-only clues.

## BLE transport

Use a CoreBluetooth central, discover `Paperang_P2`, and connect after user selection. Do not hardcode a UUID from another Mac.

| UUID, expanded as `0000xxxx-0000-1000-8000-00805f9b34fb` | Purpose |
|---|---|
| FF00 | Working service on the tested unit |
| FF02 | Host writes, respecting characteristic properties and CoreBluetooth backpressure |
| FF01 | Device data notifications; parse as a byte stream |
| FF03 | Transport credits and size, not a business-protocol ACK |

Subscribe to FF01 and FF03 before handshaking. FF03 `01 NN` adds NN write credits; `02 LL HH` reports a u16LE packet size. An observed initial allocation was 2 credits and 182 bytes. The app conservatively uses 180-byte BLE chunks, additionally bounded by `maximumWriteValueLength`.

Each BLE write consumes credit. Wait for both credits and CoreBluetooth readiness; stop on timeout. Defaults are 30 ms between BLE writes, 80 ms between raster frames, and a 6-second timeout. These are software stability settings, not firmware constants. Other discovered services included ISSC/NUS-style services, FEE7, and FF80; they were not written. Classic SPP is a separate transport, discussed in the [emulator analysis](ESP32-EMULATOR.md).

## Frame and CRC

```
02 | command:u8 | index:u8 | payloadLength:u16LE | payload | crc32:u32LE | 03
```

The envelope is 10 bytes and payload at most 1023. CRC covers **payload only**. It is zlib-compatible reflected CRC32, reflected polynomial `EDB88320`, initial/final XOR `FFFFFFFF`. `Wire.crc(data,seed:)` matches `zlib.crc32(data,seed)`. Notifications can split or combine frames: use length, terminator, and CRC rather than notification boundaries.

### Confirmed handshake

The initial seed is `0x35769521`. Register session key zero using payload `21 95 76 35` and the complete frame:

```
02 18 00 04 00 21 95 76 35 1C DF 44 21 03
```

Observed confirmation:

```
02 18 00 01 00 00 8D EF 02 D2 03
```

After registration, sends and responses use seed zero. The app only uses this verified key-zero sequence; an emulator's unverified shift expression is not a general key-negotiation specification.

### ACK versus data

A common success ACK repeats the request command with a one-byte `00` payload. Queries use payload `01`; field data normally uses a different response command. Voltage and temperature queries on this unit returned ACK only, which does not mean 0 V or 0°C. An ACK also does not establish mechanical print completion.

MAC query `08` actually returned data under `0B`, overlapping serial-number query `0A→0B`. Serialize requests and associate data with the in-flight query; do not identify the field by response command alone. Preserve raw bytes alongside interpretations.

## Raster, density, and feed

- Fixed **576 dots**, **72 bytes/row**, top-to-bottom, MSB-first within each byte, 1=black and 0=white.
- Composite transparency onto white, lay out/scale on the host, then threshold or dither. Fonts and grayscale processing are host-side; the head prints monochrome raster data.
- Command `00` sends raw raster. Split into whole rows: `floor(1023/72) × 72 = 1008` bytes, 14 rows/frame. The verified BLE path increments the 8-bit index from zero. Split each protocol frame again into BLE chunks; these are separate layers.
- Set density with `19`, one byte 0–100; read `1C→1D`. Default/test density is 100; an earlier value was 75. The setting may persist in the device.
- Feed with `1A` and a u16LE line count. 150 lines is `96 00`, confirmed convenient after a batch on this unit. Do not add final feed after every image packet. Earlier 120/240-line experiments were superseded by 150.
- The 576-column outer edges were physically visible. A 640-dot logical input did not produce usable wider output; the exact cropping/stream interpretation remains unknown. Nominal 300 DPI gives a theoretical width of 48.768 mm, not a ruler measurement.
- Render height 1400, text 10,000 characters, and at most 256 image frames are application guards, not measured firmware limits.

## Cover, paper, and battery

| User-confirmed physical state | SENT_STATUS payload |
|---|---|
| Closed, paper loaded | `08 00` |
| Open, roll left inside | `19 00` |
| Closed, empty | `09 00` |
| Paper restored, closed | `08 00` |

`status & 0x10` means cover open. **Only while closed**, `status & 1` means paper absent. Paper presence is unknown while open. Other bits remain uncalibrated. Bit `0x08` disappeared as the battery estimate rose, but is not an independently established low-battery or charging flag.

Battery `10→11` is a one-byte estimated percentage. Historical readings were 13→58→65, with later GUI samples of 82/100. There is no confirmed charging Boolean, standard Battery Service, or LED-control command. See [charging research](CHARGING-AND-LED.md).

## Recorded device fields

| Field | Request → data response | Historical payload / interpretation |
|---|---|---|
| Printer firmware | 04→05 | `09 00 01`; version ordering unknown |
| Model | 06→07 | ASCII `Paperang_P2` |
| MAC | 08→0B | 12 ASCII hexadecimal characters; identifier retained locally only |
| Serial | 0A→0B | ASCII; original retained in local archive |
| Status | 0C→0D | u16LE, mapping above |
| Voltage | 0E→0F | ACK only on this unit |
| Battery | 10→11 | u8 percentage |
| Temperature | 12→13 | ACK only on this unit |
| Factory status | 15→16 | `FF`, unknown meaning |
| Density | 1C→1D | `4B`=75, later `64`=100 |
| Auto power-down | 1F→20 | `08 07`=1800 seconds; persistence across restart untested |
| Board | 23→24 | ASCII `V1.00.01` with trailing space |
| Hardware | 25→26 | `00 00 00`, unknown meaning |
| Maximum gap | 28→29 | `70`=112, unknown units |
| Paper type | 2A→2B | `00`; naming differs across projects |
| Country | 2D→2E | Empty data payload, distinct from ACK-only |
| Short name | 30→31 | ASCII `P2` |
| GATT 2A29 / 2A24 | read | `ISSC` / `BT5050`, Bluetooth-module identity |
| GATT 2A27 / 2A26 | read | `5050_SPP` / `2030030`, not printer firmware |
| GATT FEC9 | read | Empty bytes |

## Command catalog

All numbers below are hexadecimal. Data responses and unsolicited events are not automatically host commands. The app allowlists established GETs; source-only write definitions are not proof of verified device behavior.

| Request | Function / response | Evidence and behavior |
|---|---|---|
| 00 | Raw raster | Physical output verified; 576 dots, complete rows |
| 01 | Compressed raster | Enumeration only; encoder/format unconfirmed, not sent |
| 02 / 03 | Firmware transfer / USB update | Writes disabled |
| 04 | Firmware →05 | Data read |
| 06 | Model →07 | Data read |
| 08 | MAC →0B on this unit | Source expected09; in-flight association required |
| 0A | Serial →0B | Data read |
| 0C | Status →0D | Physical differential calibration |
| 0E | Voltage →0F | ACK only |
| 10 | Battery →11 | Data read |
| 12 | Temperature →13 | ACK only |
| 14 | Set factory mode | Source definition; mode values, exit, and persistence unknown |
| 15 | Factory status →16 | Read `FF`; meaning unknown |
| 17 | Bluetooth status event | Source enumeration; not a verified LED command |
| 18 | CRC registration | Verified handshake above |
| 19 | Set density | u8 0–100; density100 physically used |
| 1A | Feed | u16LE lines; physically verified |
| 1B | Self-test page | Source definition; not automatically executed |
| 1C | Density →1D | Data read |
| 1E | Set auto power-down | u16LE seconds; persistence unknown |
| 1F | Auto power-down →20 | Read1800 seconds |
| 21 | Feed to head | u16LE in source; difference from1A not physically confirmed |
| 22 | Print default parameters | Source definition; not automatically executed |
| 23 | Board version →24 | Data read |
| 25 | Hardware info →26 | Raw data read; meaning unknown |
| 27 | Set maximum gap | Source definition; units unknown |
| 28 | Maximum gap →29 | Read112; units unknown |
| 2A | Paper type →2B | Read00; naming conflicts |
| 2C | Set paper type | Source definition; values/persistence not calibrated |
| 2D | Country →2E | Empty data |
| 2F | Disconnect Bluetooth | Source definition; app uses CoreBluetooth disconnect |
| 30 | Short name →31 | P2 response physically queried |
| 39 | Extended definition only | Emulator has no handler; research only |
| 40 | Extended40→41 | Emulator's one-byte response implementation is questionable |
| 42 | Extended42→43 | Emulator hardcodes `BK3432`; not this P2's reading |
| 7F | Extended7F→80 | Emulator hardcodes `v3.38.19` with padding |
| 81 | Extended81→82 | Emulator hardcodes `HME230_P2` with padding |

Compression, factory-mode enums/exit/persistence, firmware updates, LED control, real-time charging, and firmware long-image limits remain unconfirmed. Extended39/40/42/7F/81 are research clues, not automatically probed device capabilities. See [source revisions](source-revisions.json) and [emulator analysis](ESP32-EMULATOR.md).

## Windows USB transport

The [USB supplement](USB-PROTOCOL.md) records 2026-11-10 device measurements for all17 established GETs, cover/paper calibration, default-seed CRC, usbprint.sys and a four-packet countdown print. The handshake/index sections above describe the established BLE path. USB does not use FF03 credits or register key zero in these tests. ACK, host sent and mechanical completion remain distinct.
