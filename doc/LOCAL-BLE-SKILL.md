# LocalBLESkill · v0.3.2-localbleskill

[简体中文](LOCAL-BLE-SKILL.zh-CN.md) · [Skill and commands](../Skills/paperang-local-ble/SKILL.md) · [P2 protocol](PROTOCOL.md)

The macOS CLI builds with Xcode Command Line Tools and connects to a legacy Paperang P2 through CoreBluetooth. The skill uses repository ProtocolCore and PrintCore sources. Keep it in the checkout and symlink the directory into the local agent's skills directory. Check the installed version with `scripts/paperang --version`.

- **Transport.** FF00 service, FF01 frame notifications, FF02 writes without response, FF03 credits and packet size. Each operation connects by scanned UUID, registers the CRC session, verifies the P2 model and disconnects on exit.
- **Readings.** Model, cover/paper, battery estimate and density. Only catalogued read commands are accepted. ACK-only replies have no field value.
- **Raster.** Fixed 576 dots, 72 bytes per row, MSB first, black bit 1. Default content width 552 dots and 12-dot side margins. Maximum 1400 rows. PNG and packed bytes come from the same raster. At nominal 300 DPI, raster length is rows × 25.4 ÷ 300 mm, plus feed.
- **Print.** The CLI checks fresh media status and density readback before sending. It sends whole rows in indexed frames and obeys BLE credits. `sent` indicates transport completion. Check paper before retrying after an uncertain result.

The version follows the 0.3.2 compatibility baseline. The standalone build and offline text rendering were verified. Live BLE handshake, hardware readings and paper output remain to be verified.
