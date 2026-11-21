#!/usr/bin/env python3
"""Check local Markdown/image links in source documents, excluding ignored output."""
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
paths = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT).decode().split("\0")
failures = []
documents = [ROOT / path for path in paths if path.endswith(".md") and (ROOT / path).is_file()]
for document in documents:
    text = document.read_text(encoding="utf-8")
    targets = re.findall(r'!?\[[^\]\n]*\]\(([^)\n]+)\)', text)
    targets += re.findall(r'<(?:img|a)\b[^>]*\b(?:src|href)=["\x27]([^"\x27]+)', text)
    for target in targets:
        target = target.strip().split(' "', 1)[0].strip("<>")
        if not target or target.startswith(("#", "$", "{")) or urlsplit(target).scheme:
            continue
        if document.relative_to(ROOT).parts[0] == "wiki" and target.startswith(("/en/", "/zh-CN/")):
            continue  # The wiki build/check validates generated site routes.
        path = unquote(target.split("#", 1)[0].split("?", 1)[0])
        if not path:
            continue
        resolved = (ROOT / path.lstrip("/") if path.startswith("/") else document.parent / path).resolve()
        if not resolved.exists():
            failures.append(f"{document.relative_to(ROOT).as_posix()}: {target}")
if failures:
    raise SystemExit("Broken documentation links:\n" + "\n".join(sorted(set(failures))))
print(f"PASS local links in {len(documents)} Markdown documents")
