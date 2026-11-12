#!/usr/bin/env python3
"""Pack an exact-size PNG into a Paperang raster1 file and optional decoded preview."""

import argparse
import base64
import hashlib
import json
from pathlib import Path

from PIL import Image


def pack(source: Path, threshold: int) -> tuple[int, bytes]:
    with Image.open(source, formats=["PNG"]) as image:
        width, height = image.size
        if width != 576 or not 1 <= height <= 1400:
            raise ValueError("PNG must be 576 pixels wide and 1–1400 pixels high")
        rgba = image.convert("RGBA")
        white = Image.new("RGBA", rgba.size, "white")
        gray = Image.alpha_composite(white, rgba).convert("L")
        black = gray.point(lambda pixel: 255 if pixel < threshold else 0).convert("1", dither=Image.Dither.NONE)
        return height, black.tobytes()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--preview", type=Path)
    parser.add_argument("--threshold", type=int, default=128)
    args = parser.parse_args()
    if not 1 <= args.threshold <= 255:
        parser.error("--threshold must be 1–255")
    paths = [args.source, args.output] + ([args.preview] if args.preview else [])
    if len({path.resolve() for path in paths}) != len(paths):
        parser.error("source, output and preview must be separate files")
    if args.output.suffix.lower() != ".json":
        parser.error("output must end in .json")
    try:
        height, bitmap = pack(args.source, args.threshold)
        envelope = {
            "type": "raster1", "schemaVersion": 1, "width": 576, "height": height,
            "encoding": "base64", "bitOrder": "msb-first", "blackIsOne": True,
            "data": base64.b64encode(bitmap).decode("ascii"),
            "sha256": hashlib.sha256(bitmap).hexdigest(),
        }
        args.output.write_text(json.dumps(envelope, indent=2) + "\n", encoding="utf-8")
        if args.preview:
            # Pillow mode 1 uses white=1. Invert while unpacking for display.
            display = Image.frombytes("1", (576, height), bitmap).point(lambda pixel: 255 - pixel)
            display.save(args.preview, format="PNG")
    except (ValueError, OSError) as error:
        parser.error(str(error))


if __name__ == "__main__":
    main()
