# Legacy P2 USB protocol and telemetry

[简体中文](USB-PROTOCOL.zh-CN.md) · [Protocol](PROTOCOL.md) · [Documentation](README.md)

Verified on 2026-11-10, Asia/Shanghai, on the user's legacy `Paperang_P2`, firmware bytes `09 00 01`, Windows x64. Based on `v0.3.2` (`46d55b7d6782d02ba0ff296e0af3c79a7f19b674`), branch `dev/p2-usb-protocol-research`. **0.3.3 follow-up:** native USB is integrated into the WinLite candidate with USB-first/BLE fallback and manual switching; see [release/upgrade](RELEASE-v0.3.3-winlite.md). The diagnostic acquisition below remains dated evidence. BLE protocol and Relay HTTP v2 remain unchanged. Hidden firmware commands cannot be described as exhaustively discovered.

## 1. Transport and discovery

| Item | Evidence |
| --- | --- |
| USB identity | VID `4348`, PID `5584`; PnP description `MPTII Printer` |
| Windows driver | Microsoft `usbprint.sys`, `usbprint.inf`; no replacement driver installed |
| USBPRINT interface | `{28D78FAD-5A12-11D1-AE5B-0000F803A8C2}` |
| Application discovery | SetupDiGetClassDevsW with PRESENT + DEVICEINTERFACE, then EnumDeviceInterfaces/GetDeviceInterfaceDetailW; filter P2 VID/PID; require exactly one match |
| Open/I/O | CreateFileW, GENERIC_READ + GENERIC_WRITE, OPEN_EXISTING, FILE_FLAG_OVERLAPPED; ReadFile/WriteFile |
| Reference Bulk endpoints | OUT `02`, IN `81`, interface 0 in the pinned Java source; Windows USBPRINT abstracts these, no direct endpoint-descriptor capture claimed |

No Windows print queue, GDI printer driver, spool job, PyUSB, libusb, Zadig, administrator access or Bluetooth was used for these USB tests. A charging-only cable is insufficient. The printer must be powered on and enumerate successfully. Multiple matching units are refused rather than choosing one silently. Disconnect competing apps before probing; the Python session serializes requests with a lock, but cannot coordinate independent BLE/software clients.

Registry interface names can remain after unplugging: the initial proof used those names; the final tool enumerates *present* interfaces through SetupAPI instead. Failed open/read/write is a transport error, not a paper sensor value.

All I/O has a bounded wait and CancelIoEx/GetOverlappedResult cleanup. Buffers and OVERLAPPED remain alive until cancellation finishes. A short or timed-out write has an unknown delivery outcome and is never automatically replayed. Driver cancellation completion itself depends on the Windows driver; do not free active I/O memory to force a deadline.

## 2. Wire format and session

```text
02 | command:u8 | remaining:u8 | payloadLength:u16LE | payload | crc32:u32LE | 03
```

The envelope is 10 bytes, payload at most 1023. CRC covers the payload only: `zlib.crc32(payload, 0x35769521)`, reflected polynomial `EDB88320`, conventional initial/final XOR. Retain this USB seed; the USB tests did not send CRC registration `18`. A BLE session's key-zero registration and FF03 credits must not be injected into this path. The later key-zero sharing result and read-only detection are documented in the follow-up below.

Captured host vectors:

```text
GET_MODEL /01 : 02 06 00 01 00 01 D0 B9 59 E9 03
GET_STATUS/01 : 02 0C 00 01 00 01 D0 B9 59 E9 03
FEED 150     : 02 1A 00 02 00 96 00 3D 78 38 6A 03
```

Receive is a byte stream: frames may be fragmented, combined, or contain an earlier ACK plus current data. Validate size, terminator and CRC. A captured transition contained a truncated header that looked like a length of 514 bytes; the decoder now resynchronizes to a later *complete CRC-valid* frame instead of waiting forever for that false length. This heuristic is for diagnostics: a contrived incomplete payload containing an entire valid nested frame could be ambiguous, and no perfect recovery from arbitrary corruption is claimed.

## 3. Transactions, stale data, and result states

GET payload is `01`. Common success ACK is the request command with single-byte `00`. ACK may precede or follow data and may remain buffered after closing/reopening the host handle. A query can have valid data even when its ACK was not seen in that request window. Raw raster `00` is sent without waiting for an ACK, matching the Java reference and the observed initial timeout.

Before each request the tool drains old input until a 60 ms quiet read (maximum drain window 500 ms), logs parsed events, then resets incomplete stale framing. Each query window writes its request once; read-only model seed detection is the explicit follow-up exception. Further reads wait up to 1.5 seconds for query data; no query-write retry is performed. MAC `08` accepts both source-defined `09` and observed `0B`; serial `0A` also uses `0B`, so requests must remain serialized. A frame's command alone cannot identify which of these two fields it represents. Unsolicited `0D` events share the status response command: timestamps and drain windows reduce stale samples, but the wire has no transaction ID proving causal association.

| Result | Meaning |
| --- | --- |
| `data` | Expected CRC-valid data frame was observed; ACK can be absent |
| `empty_data` | Expected data command with zero-length payload; distinct from a zero value |
| `ack_only` | Success ACK observed; no expected data within the query deadline |
| `no_response` | No accepted ACK/data; corruption or unsupported behavior is possible |
| `negative_ack` | Same-command one-byte nonzero result; data is not treated as successful |
| Raised I/O error | Windows/transport failure; not converted to one of the measured values |

ACK-only does not prove permanent absence of a feature. No result proves the printer's mechanical completion. Keep raw bytes, query time and interpretation separately. MAC/serial and their events are redacted from terminal/evidence by default.

## 4. All established GETs — USB measurements

All request/response numbers are hexadecimal. All 17 established GETs were queried. Fourteen returned nonempty data, one returned empty data, and two returned ACK only.

| Field | Request → data | USB value | Interpretation / limit |
| --- | --- | --- | --- |
| Firmware | 04 → 05 | `09 00 01` | Byte ordering/version string unconfirmed |
| Model | 06 → 07 | `Paperang_P2` | Printer identity, not USB module description |
| Bluetooth MAC | 08 → **0B** | 12 ASCII bytes, redacted | Source expected09; real response exception confirmed |
| Serial | 0A → 0B | 14 ASCII bytes, redacted | Serialize with MAC query |
| Cover/paper | 0C → 0D | u16LE; see below | Differential physical calibration |
| Voltage | 0E → 0F expected | ACK only | Voltage unknown; not 0 V |
| Battery | 10 → 11 | `64` | Estimated100%; no charging Boolean |
| Temperature | 12 → 13 expected | ACK only | Temperature unknown; not 0°C |
| Factory status | 15 → 16 | `FF` | Meaning unknown; factory mode was not changed |
| Density | 1C → 1D | `64` |100; prior USB sample60 |
| Auto power-down | 1F → 20 | `08 07` |1800 seconds; restart persistence not tested |
| Board | 23 → 24 | `V1.00.01 ` | Trailing ASCII space |
| Hardware | 25 → 26 | `00 00 00` | Unknown subfields |
| Maximum gap | 28 → 29 | `70` |112 numerically; unit unknown |
| Paper type | 2A → 2B | `00` | Setting, not paper-presence sensor; enum not calibrated |
| Country | 2D → 2E | empty payload | Empty data, not ACK-only |
| Short name | 30 → 31 | `P2` | Model short name |

The machine-readable [USB profile](p2-usb-profile.json) includes these redacted measurements and the full known opcode catalog. It is research metadata; the shipping clients do not load it automatically.

## 5. Cover and paper differential

The user performed the physical changes; no printing occurred while open/empty.

| User-confirmed condition | Stable USB data | Samples |
| --- | --- | --- |
| Open, roll retained | `11 00` |3 consecutive |
| Closed, roll removed | `01 00` |3 consecutive |
| Paper restored, closed | `00 00` |Fresh drained query plus later telemetry/print samples |

Opening/closing also produced `10 00` transitional events. Old events `01/11/10` were read after the roll was restored, before input draining was added. Those early samples were not relabeled as normal.

```text
raw = u16LE(payload), requiring exactly 2 bytes
coverOpen = (raw & 0x10) != 0
paperPresent = null if coverOpen else (raw & 0x01) == 0
unknownBits = raw & ~0x11
```

An open cover does not establish whether the roll is present, even if bit0 is set. Other bits remain uncalibrated. BLE historical low-battery `08/19/09` samples are separate from the USB `00/11/01` samples; bit3 must not be named charging, busy or low-battery solely from correlation.

## 6. Printing, feed and completion

Raster is576 dots/72 bytes per row, MSB-first,1=black, top-to-bottom. Use whole rows, at most1008 bytes/14 rows per frame. Send once per frame, then feed150 lines once after the batch. Host rendering remains responsible for fonts, scale, grayscale and dithering.

`19` sets density as **one byte**0–100, and `1C→1D` reads it. USB density60 gave a faint initial pattern; density100 was written and read back. The Java README's short-width description conflicts with its single-byte Commands.kt implementation; physical readback settles this unit's payload width. `1A` uses a u16LE line count.

The new four-band test sent576×56 dots in four1008-byte payloads, remaining counts **3→2→1→0**, with status checks between frames. The user confirmed: **all four bands complete, correct order, improved darkness**. The pattern had left/right borders and an inner line shifting right every14 rows. This verifies a short four-frame countdown stream on this unit, including interleaved GETs; it does not establish a maximum job, unlimited throughput or255-packet behavior. BLE's increasing index remains a separate tested convention.

Status before, between and after these packets and during a four-second post-send watch stayed `00 00`. No additional busy/completion event was identified. These short samples might miss a transient; they do not prove the firmware never exposes one.

| State | Supported interpretation |
| --- | --- |
| Host preparing/sending | Local execution phase only |
| `sent` | All intended host writes, including final feed, completed and feed ACK received |
| Interrupted/short/timed-out write | `unknown`; no automatic replay |
| Device `printing` | `null` until a protocol signal is established |
| Mechanically complete | `null`; normal media state or ACK is insufficient |
| Paper confirmed | Separate explicit human observation |

The tool reserves a new local evidence/intent file before a paper attempt, persists `unknown` before the first raster packet, then records `sent` only after feed ACK. It does not convert this chat's paper observation into a Relay C confirmation. The physical observation is recorded in these research documents.

## 7. USB printer-class diagnostics

This is a second channel, distinct from legacy Bulk command `0C`:

| IOCTL | Control code | Actual result |
| --- | --- | --- |
| GET_LPT_STATUS | `00220030` | WinError23, data error/CRC; no usable status byte |
| GET_1284_ID | `00220034` |96 bytes, model `MiaoMiaoJi`; manufacturer empty |

The IEEE1284 prefix was raw `60 60`, so its size encoding is not assumed correct. The tool retains prefix/returned byte count, parses only standard allowed text fields and redacts others. A generic model string is not evidence of IPP, ESC/POS, a spooler queue or native font support. The WinError23 here is a failed class-control request, separate from the validated legacy payload CRCs. Class status is not a fallback busy/paper sensor on this unit. No soft reset, vendor-control, port-cycle or protocol-switch IOCTL was sent.

## 8. Remaining command coverage

The profile catalogs every traditional opcode `00–31` including response/event entries; those are not50 host operations. The remaining host-side definitions are:

| Opcode | Function | USB status |
| --- | --- | --- |
| 00 | Raw raster |Single and four-frame physical output confirmed |
| 01 | Compressed raster |Source only; codec/flow unconfirmed |
| 02/03 | Firmware data/update |Not executed |
| 14 | Set factory mode |Mode/exit/persistence unknown; not executed |
| 17 | Bluetooth-status event |No verified host SET meaning |
| 18 | CRC registration |Known BLE handshake; not needed/executed in this USB session |
| 19 | Set density |Single-byte100 and readback verified |
| 1A | Feed |u16LE150, ACK and paper confirmed |
| 1B | Built-in self-test |Source only; not executed |
| 1E | Set auto power-down |Source u16LE seconds; not executed |
| 21 | Feed to head |Source u16LE; distinction from1A unconfirmed |
| 22 | Print default parameters |Source only; not executed |
| 27 | Set maximum gap |Units/persistence unknown; not executed |
| 2C | Set paper type |Enum/persistence unknown; not executed |
| 2F | Disconnect Bluetooth |Not needed for USB; not executed |

Emulator-only `39/40/42/7F/81` clues are cataloged separately. They do not establish real-P2 GET semantics; `40` is not established as charging/printing. No unknown opcode sweep was performed. LED control, independent charging, dynamic head width, compression, thermal-fault flags, buffer depth, reconnect after power loss, maximum length and reliable mechanical completion remain open research items.

## 9. Reproduce

From the repository root, Windows Python3, standard library only:

```powershell
python Windows/scripts/usb_probe.py
python Windows/scripts/usb_probe.py --all --class-info --output work/usb/new-query.json
python Windows/scripts/usb_probe.py --query status --watch --duration 10
python Windows/scripts/usb_probe.py --print-test --rows 56 --watch --duration 4
python -m unittest discover -s tests/windows -p test_usb_probe.py -v
```

`--all`, default reads and watch never print. `--print-test` sets density100, checks fresh model/media, verifies density, prints14–140 rows in14-row multiples, then feeds150; it consumes paper and leaves density100. It automatically creates a unique ignored `work/usb/attempt-<uuid>.json` when `--output` is omitted. Existing output paths are refused. Watch duration is bounded to120 seconds; interval is a delay between query cycles, not a guaranteed sampling rate. `--trace` emits parsed redacted frames rather than raw identifiers. There is no general raw-command CLI.

The dedicated20-test suite covers captured golden vectors, split/coalesced input, corrupt-header resync, media tri-state, ACK/data ordering, negative ACK, ACK-only/empty/no-response, MAC exception/redaction and write/query guards. The Python diagnostic remains separate from the packaged .NET backend. Native integration/build and hardware evidence are recorded in the [0.3.3 validation](VALIDATION-v0.3.3-windows.md).

## 10. Evidence and source provenance

Private ignored evidence: `work/usb/telemetry-all-01.json`, `print-status-01.json`, `class-status-01.json`; physical media captures were recorded in the chat/tool transcript. Public sanitized measurements are in the profile and this document. [Documentation review](USB-DOCUMENTATION-AUDIT.md) lists the baseline documents and update decisions.

- [Java P2 USB](https://github.com/hurui200320/java-paperang-p2-usb/tree/c50594c62d6fd6387d41e86b14f1fdf489d2b560): constants, framing, density width, countdown and no raster ACK. AGPL-3.0 source used as protocol reference; no source files bundled.
- [P2 library](https://github.com/mdj2812/paperang-p2-lib/tree/656fc8f37ff6f7b0744ef10f8a0ae38d4d594730): GET catalog, USB/SPP model metadata; its generic cmd+1, u16 density and CRC-unchecked parser were not adopted blindly.
- [P1 Connect](https://github.com/Orangray-MDJX/paperang-p1-connect/tree/9ec2cb535ec8ac09a7dc1ce2464bc2a114d569bf): short-name clue and protocol-generation separation.
- [ESP32 analysis](ESP32-EMULATOR.md): hardcoded replies are not sensor evidence.
- Microsoft: [USBPRINT](https://learn.microsoft.com/en-us/windows-hardware/drivers/print/usb-printing), [SetupAPI discovery](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdienumdeviceinterfaces), [CancelIoEx](https://learn.microsoft.com/en-us/windows/win32/api/ioapiset/nf-ioapiset-cancelioex), [port status](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbprint/ni-usbprint-ioctl_usbprint_get_lpt_status), [1284 ID](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/usbprint/ni-usbprint-ioctl_usbprint_get_1284_id), [header constants](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/shared/usbprint.h).

## Cross-transport CRC follow-up — WinLite0.3.3

A later real USB→BLE→USB cycle established that BLE key-zero registration also changes the CRC seed accepted over USB on this P2. A default-seed model query then received no data; a seed0 model query returned a CRC-valid Paperang_P2 reply. Native UsbP2Session and the Python diagnostic now try the default seed, and only after no response repeat the **read-only model query** with seed0; all later packets use the identified seed. No CRC registration/reset/write is needed for USB. The original default-seed captures above remain valid historical measurements. Switching back to USB was re-tested successfully. No physical print is retried for seed detection.
