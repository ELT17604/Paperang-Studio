#!/usr/bin/env bash
# macOS foreground launcher. Keep this folder inside the complete Server/ tree.
set -euo pipefail
HERE="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
SERVER="$(cd -- "$HERE/.." && pwd)"
[[ $(uname -s) == Darwin ]] || { echo 'This launcher is for macOS. On Linux use Server/run-temporary.sh.' >&2;exit 1; }
[[ -f "$SERVER/run.py" && -f "$SERVER/relay.py" && -d "$SERVER/web" ]] || { echo 'Keep macOS-Temporary inside the complete Server folder (run.py, relay.py and web/ are required).' >&2;exit 1; }
supported(){ [[ -x "$1" ]] && "$1" -c 'import sys; sys.exit(0 if sys.version_info >= (3,10) else 1)' >/dev/null 2>&1; }
PYTHON=""
if [[ -n ${PAPERANG_PYTHON:-} ]];then
 candidate="$(command -v "$PAPERANG_PYTHON" 2>/dev/null || printf '%s' "$PAPERANG_PYTHON")"
 supported "$candidate" || { echo 'PAPERANG_PYTHON must select a working Python 3.10+ interpreter.' >&2;exit 1; }
 PYTHON="$candidate"
else
 for candidate in "$(command -v python3 2>/dev/null || true)" /opt/homebrew/bin/python3 /usr/local/bin/python3 /usr/bin/python3;do
  if [[ -n $candidate ]] && supported "$candidate";then PYTHON="$candidate";break;fi
 done
fi
[[ -n $PYTHON ]] || { echo 'Python 3.10+ is required. Install Python, or set PAPERANG_PYTHON to its executable path. Install Server/requirements.txt into a dedicated venv.' >&2;exit 1; }
"$PYTHON" -c 'import argon2, cryptography' 2>/dev/null || { echo 'Install Server/requirements.txt in a venv and set PAPERANG_PYTHON to its python.' >&2;exit 1; }
# exec keeps Python in the foreground: Ctrl-C/SIGHUP reaches the actual server.
exec "$PYTHON" "$SERVER/run.py" "$@"
