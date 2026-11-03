# ESP32 Paperang emulator analysis

[Documentation](README.md) · [简体中文](ESP32-EMULATOR.zh-CN.md)

Reviewed 2026-11-04: [lxydiy/ESP32-Paperang-Emulator](https://github.com/lxydiy/ESP32-Paperang-Emulator), commit `c38fbebb72bd9ef46a9d5b12b5afa9934ad88356`. Files inspected included `Program/Printer/Paperang.ino`, the CRC wrapper, Printer.ino, and Python backups. The third-party program was not built/run. It emulates a device; it is not real P2 firmware, and its README describes an incomplete version.

## Extended command clues

All command numbers are hexadecimal. The [pinned source](https://github.com/lxydiy/ESP32-Paperang-Emulator/blob/c38fbebb72bd9ef46a9d5b12b5afa9934ad88356/Program/Printer/Paperang.ino) is the provenance, not a hardware result. App catalog entries are marked research and excluded from automatic GETs.

| Request→response | Emulator behavior | Interpretation |
|---|---|---|
|30→31 |Hardcoded two-byte P1 |Independent source clue; this real P2 already returned P2 |
|39 |Defined only, no switch handler |Function/read-only nature unknown |
|40→41 |Attempts to return one-byte00 |Questionable pointer definition; not a proven charging field |
|42→43 |Hardcoded ASCII BK3432 |Module/chip identity candidate, not a reading from this P2 |
|7F→80 |12 bytes: v3.38.19 with zero padding |Version candidate, component unknown |
|81→82 |16 bytes: HME230_P2 with zero padding |Hardware identity candidate, real support untested |

`CMD_42` means hex42=decimal66, not decimal42=hex2A paper type. Battery90, serial, modelP1, and countryCN are also hardcoded. Fixed replies are not sensor readings or evidence of field units.

## Communication/search keywords

- `BluetoothSerial`, `esp_spp_api.h`, `SPP`, `RFCOMM`, `SDP`: classic serial transport, distinct from CoreBluetooth GATT. Shared02/03 frames do not imply FF03 credit control.
- `esp_bt_gap_set_cod`, `esp_bt_cod_t`: emulator Class of Device settings: major6, minor`0b100000`, service`0b00000100000`. These may affect discovery/classification; they are not protocol writes to reconfigure a real P2.
- `SET_CRC_KEY`, `0x35769521`, `paperang_send_ack`: legacy registration/same-command00 ACK clues; internal CRC seed representation differs.
- `CMD_40 CMD_41`; `CMD_42 CMD_43 BK3432`; `CMD_7F CMD_80 v3.38.19`; `CMD_81 CMD_82 HME230_P2`: useful search/capture labels.
- `STB`, `SPI`, `goFront`, `MOTOR_TIME`: DIY head/motor controls, not P2 BLE LED or factory commands.

## Implementation caveats

1. Incoming CRC fields are skipped, not validated; the Swift app continues to validate CRC.
2. CRC-key shifts lack clear grouping and the CRC library uses a different internal seed representation; do not substitute this for the verified key-zero vector.
3. GET_SN falls through into the power-down response because of a missing break.
4. CMD_40_DATA is declared as a null-address pointer but sent as a one-byte buffer;00 is not a reliable real measurement.
5. The head uses384 dots/48 bytes per row. FEED_LINE triggers printing and a fixed200-step movement, not confirmed P2u16LE feed semantics.
6. Emulator density reads one byte while the other P2 library sends two. Keep this unit's verified BLE one-byte density100 path.
7. GET_STATUS/voltage/temperature/factory sensing is not implemented, and default branches still ACK. Emulator acknowledgment does not prove real support.

## Next acquisition work

Capture complete official-app SPP/BLE requests, responses, timing, and physical actions around these numbers. Establish a GET's meaning before adding experimental queries. Compare42/7F/81 with module GATT strings if captures become available. Continue distinguishing empty data, ACK-only, CRC failure, and no response. Do not infer40's meaning from a blinking light or sweep unknown writes.
