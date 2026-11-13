#!/usr/bin/env python3
"""Validate a Paperang MacClient raster1 JSON and reconstruct a print preview."""

import argparse
import base64
import binascii
import hashlib
import json
from pathlib import Path

from PIL import Image

EXPECTED = {
	"type",
	"schemaVersion",
	"width",
	"height",
	"encoding",
	"bitOrder",
	"blackIsOne",
	"data",
	"sha256",
}


def verify(path: Path) -> tuple[int, bytes]:
	if path.stat().st_size > 144_000:
		raise ValueError("JSON exceeds the MacClient 144000-byte file limit")
	document = json.loads(path.read_text(encoding="utf-8"))
	if not isinstance(document, dict) or set(document) != EXPECTED:
		raise ValueError("raster1 object must contain the nine specified fields only")
	for key, expected in (
		("type", "raster1"),
		("schemaVersion", 1),
		("width", 576),
		("encoding", "base64"),
		("bitOrder", "msb-first"),
		("blackIsOne", True),
	):
		if document[key] != expected or type(document[key]) is not type(expected):
			raise ValueError(f"invalid {key}")
	height = document["height"]
	if type(height) is not int or not 1 <= height <= 1400:
		raise ValueError("height must be an integer in 1–1400")
	encoded = document["data"]
	if not isinstance(encoded, str) or len(encoded.encode("utf-8")) > 134_400:
		raise ValueError("Base64 field is too large or not a string")
	try:
		payload = base64.b64decode(encoded, validate=True)
	except (binascii.Error, ValueError) as exc:
		raise ValueError("invalid Base64") from exc
	if len(payload) != 72 * height or base64.b64encode(payload).decode("ascii") != encoded:
		raise ValueError("bitmap size or canonical Base64 mismatch")
	digest = hashlib.sha256(payload).hexdigest()
	if document["sha256"] != digest:
		raise ValueError("SHA-256 mismatch")
	return height, payload


def main() -> None:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("source", type=Path)
	parser.add_argument("--preview", type=Path, help="decoded black-on-white PNG")
	parser.add_argument("--raw", type=Path, help="optional raw packed bitmap export")
	args = parser.parse_args()
	destinations = [args.source, *[p for p in (args.preview, args.raw) if p is not None]]
	if len(set(p.resolve() for p in destinations)) != len(destinations):
		parser.error("source, preview and raw destinations must be distinct")
	try:
		height, payload = verify(args.source)
		if args.preview:
			args.preview.parent.mkdir(parents=True, exist_ok=True)
			decoded = Image.frombytes("1", (576, height), payload)
			decoded.point(lambda pixel: 255 - pixel).save(args.preview, format="PNG")
		if args.raw:
			args.raw.parent.mkdir(parents=True, exist_ok=True)
			args.raw.write_bytes(payload)
		black = sum(byte.bit_count() for byte in payload)
		print(
			f"valid raster1: 576x{height}, {len(payload)} bytes, {black} black pixels "
			f"({100 * black / (576 * height):.2f}%)"
		)
	except (OSError, ValueError, json.JSONDecodeError) as exc:
		parser.error(str(exc))


if __name__ == "__main__":
	main()
