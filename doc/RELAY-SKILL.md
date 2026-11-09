# RelaySkill · v0.3.0-relayskill


> opt1: Regression sources are archived outside the shipping repository. See [validation and reproduction](OPTIMIZATION-opt1.md). Historical test paths below refer to Git revision `4393ae0`.
> **v0.3.0:** [API v2 authentication](AUTH-v2.md) · [Upgrade](UPGRADE-v0.3.0.zh-CN.md) · [Handoff](HANDOFF-v0.3.0.zh-CN.md). WinLite includes a C management panel; web receipts no longer grant content access.

[简体中文](RELAY-SKILL.zh-CN.md) · [Skill entrypoint](../Skills/paperang-relay-print/SKILL.md) · [Version policy](VERSIONING.md) · [Feature matrix](FEATURE-MATRIX.md)

An independent sender-side skill distribution alongside MacClient, LinuxRelayServer and WinLite. It sends text or raster1 jobs to the same relay; an online receiving client still owns the Bluetooth printer. It is not another relay daemon or local printer driver.

## Installation and runtime

Copy the entire `Skills/paperang-relay-print/` folder into your agent's skills directory, for example `~/.codex/skills/paperang-relay-print/`. Keep SKILL.md, agents, references, scripts and VERSION together. The skill instructions are self-contained and the helper works independently of the source checkout.

Use Python 3.10+. Status, receipt tracking and direct-text submission use the standard library. Rendered text/images need Pillow; install it into an external virtual environment. On Windows the instructions suggest `%LOCALAPPDATA%/PaperangRelaySkill/python/`. Do not bundle runtimes, tokens or job/receipt artifacts into the skill.

Invoke `$paperang-relay-print` with the content to print and desired layout, for example: “Print this photo rotated 90 degrees, with photo dithering and a maximum height of 800 rows.” Explicit print requests authorize the requested content/copies; generating a preview or installing the skill does not print anything.

## Capabilities and boundaries

Direct text uses the receiver's current template. Pre-rendered text supports CJK font selection, size, alignment, margins and spacing. Image rendering supports EXIF orientation, clockwise rotation, contain/width/cover fitting, threshold/Floyd–Steinberg/Bayer conversion, brightness, contrast, gamma, inversion and an optional caption. Rendering produces bounded 576-dot raster1 and an optional PNG preview.

The helper preflights public policy and capabilities, creates an exclusive attempt record before each POST, submits only explicitly intended copies, and tracks receipt states without automatically resending. `sent` means transport completion, not physical paper confirmation.

Thermal density, feed and the active client template are **receiver-local settings**, not relay job parameters. Client templates may add metadata and resize an uploaded bitmap. Exact final layout requires an appropriate local Body-only template; do not pretend unsupported server settings override the receiver. Full composite height remains 1,400 rows. Detailed switches are in the skill's [parameter reference](../Skills/paperang-relay-print/references/parameters.md).

## Credentials and compatibility

Use `--server`, `PAPERANG_RELAY_URL` or configured endpoint. Windows can reuse only the endpoint from current-user encrypted WinLite settings. It never uses the saved device key as a submit key. Use an A/B random key via environment or private key-file path. Browser anonymous sessions are not reused. Configuration stores paths, not key material. Attempt files hold IDs and permission flags; keep private print artifacts outside the repository.

Baseline 0.3.0 uses relay API v2; raster1 schema 1 remains. A is acceptance-only; B can wait using its matching identity key. [VERSIONS.json](../VERSIONS.json) records the independent release, and `--version` agrees with its VERSION file. All implemented distributions have coordinated 0.3.0 source versions.

## Verification and packaging

```sh
python -m pip install Pillow
python scripts/check-versions.py
python Skills/paperang-relay-print/scripts/relay_print.py --version
```

Twelve checks cover CJK rendering, exact bit/row order, transparency, rotation/dithering, corruption/unsupported settings, input protection, quota/offline checks, receipts, intended copies, role separation and no duplicate replay. Original relay HTTP/SQLite integration validated text, image/caption, claims and final receipt tracking using a simulated printer. Read-only live status was also verified; the test suite never contacts a real printer.

Package the skill folder as `PaperangStudio-v0.3.0-relayskill.zip`, excluding caches, runtimes, credentials and generated jobs. Retain the repository's CC BY-NC-SA 4.0 license and ELT Group attribution when distributing it.
