#!/usr/bin/env bash
set -euo pipefail
SOURCE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v python3 >/dev/null || { echo 'Python 3.10+ is required.' >&2;exit 1; }
exec python3 "$SOURCE/run.py" "$@"
