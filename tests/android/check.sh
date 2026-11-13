#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
deps=Android/build/deps/core-3.5.4.jar
if [[ ! -f "$deps" ]]; then ./Android/build.sh; fi
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
src=Android/src/com/elt/pprstudio/adc
javac --release 17 -encoding UTF-8 -cp "$deps" -d "$scratch" \
    "$src/Wire.java" "$src/P2Transfer.java" "$src/PrintOptions.java" "$src/PaperPlacement.java" "$src/PaperGesture.java" tests/android/WorkspaceCheck.java
java -cp "$scratch:$deps" com.elt.pprstudio.adc.WorkspaceCheck
