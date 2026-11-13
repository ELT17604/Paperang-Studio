#!/usr/bin/env python3
"""Render UTF-8 text as paginated 576-pixel Paperang raster1 sheets."""

import argparse
from functools import lru_cache
import math
import re
from pathlib import Path
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

WIDTH = 576
MAX_HEIGHT = 1400


def wrap_text(content: str, font: ImageFont.FreeTypeFont, usable_width: int) -> list[str]:
	"""Wrap ASCII words together and CJK by character; preserve explicit blank lines."""
	width = lru_cache(maxsize=4096)(font.getlength)
	lines: list[str] = []
	for paragraph in content.expandtabs(4).split("\n"):
		if not paragraph:
			lines.append("")
			continue
		current = ""
		used = 0.0
		tokens = re.findall(r"[A-Za-z0-9]+(?:['’._/-][A-Za-z0-9]+)*|[ ]+|.", paragraph)
		for token in tokens:
			token_width = width(token)
			if used + token_width <= usable_width:
				current += token
				used += token_width
				continue
			if current.strip():
				lines.append(current.rstrip())
				current = ""
				used = 0.0
			token = token.lstrip(" ")
			for char in token:
				advance = width(char)
				if used + advance > usable_width:
					if not current:
						raise ValueError("a single glyph exceeds the printable width; reduce --size")
					lines.append(current)
					current = char
					used = advance
				else:
					current += char
					used += advance
		lines.append(current.rstrip())
	return lines


def main() -> None:
	parser = argparse.ArgumentParser(description=__doc__)
	parser.add_argument("text_file", type=Path, help="UTF-8 plain text input")
	parser.add_argument("output_prefix", type=Path, help="destination prefix, e.g. output/note")
	parser.add_argument(
		"--font", type=Path, required=True, help="installed TTF/OTF/TTC font with required glyphs"
	)
	parser.add_argument("--size", type=int, default=28, help="font size in pixels (default 28)")
	parser.add_argument("--margin", type=int, default=24, help="all-side margin in pixels (default 24)")
	parser.add_argument("--line-gap", type=int, default=8, help="extra vertical pixels per line (default 8)")
	parser.add_argument("--max-height", type=int, default=1400, help="sheet limit, 64–1400 (default 1400)")
	parser.add_argument("--threshold", type=int, default=128, help="binarization threshold, 1–255")
	args = parser.parse_args()
	if not 8 <= args.size <= 160 or not 0 <= args.margin <= 120 or not 0 <= args.line_gap <= 100:
		parser.error("require --size 8–160, --margin 0–120, --line-gap 0–100")
	if not 64 <= args.max_height <= MAX_HEIGHT or not 1 <= args.threshold <= 255:
		parser.error("require --max-height 64–1400 and --threshold 1–255")
	try:
		content = args.text_file.read_text(encoding="utf-8-sig")
		if not content.strip():
			raise ValueError("input text must contain printable content")
		if len(content) > 100_000:
			raise ValueError("limit input to 100000 characters per run")
		font = ImageFont.truetype(str(args.font), args.size)
		ascent, descent = font.getmetrics()
		line_height = math.ceil(ascent + descent + args.line_gap)
		usable = WIDTH - 2 * args.margin - 4
		capacity = (args.max_height - 2 * args.margin) // line_height
		if usable <= 0 or capacity < 1:
			raise ValueError("font and margins leave no room for one printable line")
		lines = wrap_text(content, font, usable)
		pages = [lines[i : i + capacity] for i in range(0, len(lines), capacity)]
		args.output_prefix.parent.mkdir(parents=True, exist_ok=True)
		packer = Path(__file__).with_name("pack_raster.py")
		for page_num, page_lines in enumerate(pages, 1):
			height = max(1, min(args.max_height, 2 * args.margin + len(page_lines) * line_height))
			image = Image.new("RGB", (WIDTH, height), "white")
			draw = ImageDraw.Draw(image)
			for index, line in enumerate(page_lines):
				draw.text(
					(args.margin, args.margin + index * line_height),
					line,
					font=font,
					fill="black",
					anchor="lt",
				)
			stem = f"{args.output_prefix.name}-{page_num:03d}"
			source = args.output_prefix.parent / f"{stem}.png"
			output = args.output_prefix.parent / f"{stem}.raster1.json"
			preview = args.output_prefix.parent / f"{stem}-preview.png"
			image.save(source, format="PNG")
			subprocess.run(
				[
					sys.executable,
					str(packer),
					str(source),
					str(output),
					"--preview",
					str(preview),
					"--threshold",
					str(args.threshold),
				],
				check=True,
			)
			print(f"{page_num}/{len(pages)}: {output} | {preview}")
	except (OSError, ValueError, UnicodeError, subprocess.CalledProcessError) as exc:
		parser.error(str(exc))


if __name__ == "__main__":
	main()
