# P2 charging and LED research

[Documentation](README.md) · [简体中文](CHARGING-AND-LED.zh-CN.md)

Recorded 2026-11-04. These are historical samples, not current battery readings.

## Battery observations

| Sample | Response11 payload | Battery estimate | Status0D |
|---|---|---|---|
| Earlier low-battery test |0D |13% |08 00 |
| Cable-connected read-only session |3A |58% |00 00 |
| Subsequent recheck |41 |65% |00 00 |

CRC-valid single-byte readings agree with the community battery interpretation. The rise supports that the power source and cable replenished the battery during this interval. It is not a USB-current measurement, a charging-IC flag, or proof of cable performance at every moment. The last sample in this particular investigation was65%; later GUI samples are described in [acquisition](ACQUISITION.md).

The user observed continuous white blinking across cable-connected → unplugged → reconnected states, after waiting. That observation was not synchronized with protocol captures, so no protocol-level plug-state behavior was established.

## Charging field

No independent charging field has been confirmed:

- GET_BAT_STATUS10→11 returned only one percentage byte.
- Voltage0E and temperature12 returned ACK only on this unit.
- Enumerated GATT did not expose standard Battery Service180F or an identified power-state field; module Device Information contained manufacturer/model/hardware/firmware strings.
- Bit0x08 existed at 13% and disappeared at 58/65%. This is compatible with a low-battery hypothesis, but charging, time, and load changed together. It is not calibrated as either low-battery or charging.

The app/API therefore reports charging as unknown rather than converting cable presence, blinking, rising battery, or bit3 into `charging:true`. Keep battery samples and their timestamps separately.

## LED control

The examined traditional00–31, P2 USB, P1 legacy/A5, and C2 materials supplied no reliable P2 set-LED command.17 is a Bluetooth-status event candidate;14 is factory-mode setting. Neither was tested as LED control. This is absence of verified support, not proof that hidden firmware commands cannot exist.

## Source/model distinctions

- [Manufacturer FAQ](https://www.paperang.com/en/pc/custom-service): general green/white normal and red fault descriptions, without this P2's exact persistent white-blink rhythm.
- [C1 quick-start](https://fcc.report/FCC-ID/2apwoc1/4416695.pdf): slow-green charging/steady-green meanings belong to C1, not directly to this P2.
- F2S/A4N7 blink descriptions concern larger printers and do not establish a P2 update state.
- [P2 FCC entry](https://fccid.io/document/3910340): metadata was identified, but manual body text was not read in that investigation; a cookie-blocked device.report page was not treated as evidence.

Next useful work is one read-only, time-labeled plugged/unplugged/replugged capture of status, battery, and unsolicited events. If no field changes, retain unknown. Compare only model-appropriate LED documentation or official-app captures before proposing writes. No reset, factory-mode change, or firmware operation was performed to alter the light.

Raw charging logs are external historical files under `PaperangStudio-Development/Legacy/PaperangP2/evidence/`, including `charging-current-query.log` and `charging-recheck.log`. See [research](RESEARCH.md) for provenance.

## USB follow-up — 2026-11-10

[USB measurements](USB-PROTOCOL.md) returned battery100%, voltage/temperature ACK-only and normal media00. USB connection is not a charging Boolean; no calibrated charging/LED field was found. Printer-class port status failed with WinError23 and is not a substitute charging/printing flag. Historical values above remain historical.
