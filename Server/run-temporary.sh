#!/usr/bin/env bash
set -euo pipefail
SOURCE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Python 3.10+ is required.' >&2;exit 1; }
RELAY_PYTHON="${PAPERANG_PYTHON:-python3}"
"$RELAY_PYTHON" -c 'import argon2, cryptography' 2>/dev/null || { echo 'Install requirements.txt in a venv and set PAPERANG_PYTHON.' >&2;exit 1; }
exec "$RELAY_PYTHON" "$SOURCE/run.py" "$@"
