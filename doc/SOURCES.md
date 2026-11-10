# Sources and acknowledgments

[Documentation](README.md) · [简体中文](SOURCES.zh-CN.md)

Public projects informed interoperability research and model comparisons. Downloaded snapshots remain outside this source distribution; third-party licenses remain with their owners. See [pinned revisions](source-revisions.json) and [acquisition record](ACQUISITION.md).

| Source | Reference used |
|---|---|
| [tinyprinter/python-paperang](https://github.com/tinyprinter/python-paperang) |Legacy02/03, CRC registration, old-app notes |
| [createskyblue/paperang-miaomiaoji-tool-gen2](https://github.com/createskyblue/paperang-miaomiaoji-tool-gen2) |P2 serial handshake,1008-byte raster packets, feed |
| [wyrtensi/paperang-cli](https://github.com/wyrtensi/paperang-cli) |Initial CLI and A5 failure comparison |
| [hurui200320/java-paperang-p2-usb](https://github.com/hurui200320/java-paperang-p2-usb) |576-dot/72-byte P2 image format |
| [mdj2812/paperang-p2-lib](https://github.com/mdj2812/paperang-p2-lib) / [PyPI](https://pypi.org/project/paperang-p2-lib/) |Models, commands, host rendering, field decoding |
| [mdj2812/paperang-hacs](https://github.com/mdj2812/paperang-hacs) |Services, per-device locks, telemetry, discovery fallback |
| [Orangray-MDJX/paperang-p1-connect](https://github.com/Orangray-MDJX/paperang-p1-connect) |30→31, legacy/A5 separation, queues/API/IPP |
| [burndown/paperang-c2](https://github.com/burndown/paperang-c2) |Cross-model dynamic metadata and AirPrint design |
| [Yrr0r/paperang-web](https://github.com/Yrr0r/paperang-web) |Alternative P1 BLE channel and host layout |
| [lxydiy/ESP32-Paperang-Emulator](https://github.com/lxydiy/ESP32-Paperang-Emulator) |Device emulation,40/42/7F/81 clues, SPP/CoD |
| [Paperang FAQ](https://www.paperang.com/en/pc/custom-service) |Nominal300DPI, monochrome thermal printing, general LEDs |

The distributed implementation is independent Swift/C code with original icon assets. It does not bundle official applications/firmware or third-party repository snapshots. Reading a model's implementation does not establish hardware support on this P2. See [research](RESEARCH.md), [related projects](RELATED-PROJECTS.md), and [emulator analysis](ESP32-EMULATOR.md).

## Windows USB sources and device evidence

See [USB provenance](USB-PROTOCOL.md#10-evidence-and-source-provenance) for pinned Java/P2/P1 references, Microsoft USBPRINT/SetupAPI/IOCTL documentation and redacted physical measurements. No third-party source files or drivers were bundled.
