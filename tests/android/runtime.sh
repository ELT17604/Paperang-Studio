#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
serial=${1:?Usage: tests/android/runtime.sh EMULATOR_SERIAL}
sdk=${ANDROID_HOME:-${ANDROID_SDK_ROOT:-"$HOME/Library/Android/sdk"}}
tools="$sdk/build-tools/${PAPERANG_BUILD_TOOLS:-36.1.0}"
platform="$sdk/platforms/android-36/android.jar"
adb="$sdk/platform-tools/adb"
if [[ -z ${JAVA_HOME:-} && -x /usr/libexec/java_home ]]; then
	export JAVA_HOME="$(/usr/libexec/java_home -v 21)"
fi
export PATH="${JAVA_HOME:+$JAVA_HOME/bin:}$PATH"
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
apk=Android/build/PaperangStudio-Android-debug.apk
[[ -f "$apk" && -f Android/build/debug.keystore ]]
mkdir "$scratch/classes" "$scratch/target" "$scratch/dex"
cat > "$scratch/AndroidManifest.xml" <<'XML'
<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.elt.pprstudio.adc.check">
    <uses-sdk android:minSdkVersion="33" android:targetSdkVersion="36" />
    <application android:label="Paperang runtime check" />
    <instrumentation android:name="com.elt.pprstudio.adc.RuntimeCheck" android:targetPackage="com.elt.pprstudio.adc" />
</manifest>
XML
deps=Android/build/deps/core-3.5.4.jar
javac --release 17 -encoding UTF-8 -cp "$platform:$deps" -d "$scratch/target" Android/src/com/elt/pprstudio/adc/*.java
jar --create --file "$scratch/target.jar" -C "$scratch/target" .
javac --release 17 -encoding UTF-8 -cp "$platform:$scratch/target.jar:$deps" \
	-d "$scratch/classes" tests/android/RuntimeCheck.java
jar --create --file "$scratch/check.jar" -C "$scratch/classes" .
"$tools/d8" --min-api 33 --lib "$platform" --classpath "$scratch/target.jar" --classpath "$deps" \
	--output "$scratch/dex" "$scratch/check.jar"
"$tools/aapt2" link -I "$platform" --manifest "$scratch/AndroidManifest.xml" -o "$scratch/unsigned.apk"
(cd "$scratch/dex" && zip -q ../unsigned.apk classes.dex)
"$tools/zipalign" -f 4 "$scratch/unsigned.apk" "$scratch/aligned.apk"
"$tools/apksigner" sign --ks Android/build/debug.keystore --ks-pass pass:android \
	--out "$scratch/check.apk" "$scratch/aligned.apk"
"$adb" -s "$serial" install -r "$apk"
"$adb" -s "$serial" install -r "$scratch/check.apk"
"$adb" -s "$serial" shell am instrument -w -r com.elt.pprstudio.adc.check/com.elt.pprstudio.adc.RuntimeCheck | tee "$scratch/result"
grep -F 'PASS Android runtime:' "$scratch/result"
grep -F 'INSTRUMENTATION_CODE: -1' "$scratch/result"
