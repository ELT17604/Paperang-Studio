#!/usr/bin/env python3
"""Package already-rendered AppIcon.png using macOS sips and PNG-backed ICNS/ICO."""

from pathlib import Path
import os, struct, subprocess

root = Path(__file__).resolve().parent.parent
work = (
	Path(os.environ.get("PAPERANG_WORK_DIR", root.parent / "PaperangStudio-Development")) / "AppIcon.iconset"
)
work.mkdir(parents=True, exist_ok=True)
for size in (16, 32, 128, 256, 512):
	for scale in (1, 2):
		name = f"icon_{size}x{size}" + ("@2x" if scale == 2 else "") + ".png"
		subprocess.run(
			[
				"sips",
				"-z",
				str(size * scale),
				str(size * scale),
				str(root / "assets/AppIcon.png"),
				"--out",
				str(work / name),
			],
			check=True,
			stdout=subprocess.DEVNULL,
		)
parts = []
for code, name in [
	("icp4", "icon_16x16.png"),
	("icp5", "icon_32x32.png"),
	("icp6", "icon_32x32@2x.png"),
	("ic07", "icon_128x128.png"),
	("ic08", "icon_256x256.png"),
	("ic09", "icon_512x512.png"),
	("ic10", "icon_512x512@2x.png"),
]:
	data = (work / name).read_bytes()
	parts.append(code.encode() + struct.pack(">I", len(data) + 8) + data)
body = b"".join(parts)
(root / "assets/AppIcon.icns").write_bytes(b"icns" + struct.pack(">I", len(body) + 8) + body)
png = (work / "icon_256x256.png").read_bytes()
(root / "assets/AppIcon.ico").write_bytes(
	struct.pack("<HHH", 0, 1, 1) + struct.pack("<BBBBHHII", 0, 0, 0, 0, 1, 32, len(png), 22) + png
)
