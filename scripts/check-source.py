#!/usr/bin/env python3
"""Keep source checkouts free of build output and embedded private keys."""
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
names = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=root).decode().split("\0")
errors = []
for name in names:
    path = root / name
    if not path.is_file():
        continue
    if set(path.relative_to(root).parts) & {"node_modules", "bin", "obj", "__pycache__", ".venv", ".nuget"}:
        errors.append(name + ": generated directory")
    if path.suffix.lower() in {".exe", ".dll", ".pdb", ".apk", ".jar", ".sqlite3", ".pfx", ".p12"}:
        errors.append(name + ": binary/private artifact")
    if path.suffix.lower() in {".cs", ".py", ".swift", ".java", ".md", ".json", ".pem"}:
        text = path.read_text(encoding="utf-8", errors="replace")
        if re.search(r"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----\s+[A-Za-z0-9+/=]{40,}", text):
            errors.append(name + ": embedded private key")
for path in (root / "Windows/src/Paperang.WinClient").glob("*Checks.cs"):
    errors.append(path.relative_to(root).as_posix() + ": fixture in production source")
if errors:
    raise SystemExit("Source policy violations:\n" + "\n".join(errors))
print("PASS source/artifact separation, private-key scan and desktop fixture location")
