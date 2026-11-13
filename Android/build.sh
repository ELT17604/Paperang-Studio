#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
sdk=${ANDROID_HOME:-${ANDROID_SDK_ROOT:-"$HOME/Library/Android/sdk"}}
tools="$sdk/build-tools/${PAPERANG_BUILD_TOOLS:-36.1.0}"
platform="$sdk/platforms/android-36/android.jar"
if [[ -z ${JAVA_HOME:-} && -x /usr/libexec/java_home ]]; then
	export JAVA_HOME="$(/usr/libexec/java_home -v 21)"
fi
java_bin=${JAVA_HOME:+$JAVA_HOME/bin/}
export PATH="${JAVA_HOME:+$JAVA_HOME/bin:}$PATH"
[[ -f "$platform" && -x "$tools/aapt2" ]] || {
	echo 'Android SDK Platform 36 and Build Tools 36.1.0 are required.' >&2
	exit 1
}
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
mkdir -p "$scratch/classes" "$scratch/dex" "$scratch/assets" build/deps
zxing=build/deps/core-3.5.4.jar
checksum=71de5d89341b5fcf5dd89da7f44e84d825d0e084cdf3ec77c9abe26b0f0ceb13
if [[ ! -f "$zxing" ]]; then
	curl --fail --silent --show-error --location --retry 2 \
		https://repo.maven.apache.org/maven2/com/google/zxing/core/3.5.4/core-3.5.4.jar -o "$zxing.tmp"
	echo "$checksum  $zxing.tmp" | shasum -a 256 -c -
	mv "$zxing.tmp" "$zxing"
fi
echo "$checksum  $zxing" | shasum -a 256 -c -
"${java_bin}javac" --release 17 -encoding UTF-8 -cp "$platform:$zxing" -d "$scratch/classes" src/com/elt/pprstudio/adc/*.java
"${java_bin}jar" --create --file "$scratch/classes.jar" -C "$scratch/classes" .
"$tools/d8" --release --min-api 33 --lib "$platform" --output "$scratch/dex" "$scratch/classes.jar" "$zxing"
# Identify the exact inputs behind a sideloaded build, including uncommitted edits.
{
	shasum -a 256 VERSION AndroidManifest.xml build.sh
	find src res -type f ! -name .DS_Store | LC_ALL=C sort | while IFS= read -r file; do shasum -a 256 "$file"; done
} | shasum -a 256 | cut -c1-16 > "$scratch/assets"/source-build.txt
"$tools/aapt2" compile --dir res -o "$scratch/resources.zip"
"$tools/aapt2" link -I "$platform" --manifest AndroidManifest.xml -A "$scratch/assets" --version-name "$(cat VERSION)" -o "$scratch/base.apk" "$scratch/resources.zip"
(cd "$scratch/dex" && zip -q "$scratch/base.apk" classes.dex)
"$tools/zipalign" -f 4 "$scratch/base.apk" "$scratch/aligned.apk"
if [[ ! -f build/debug.keystore ]]; then
	"${java_bin}keytool" -genkeypair -keystore build/debug.keystore -storepass android -keypass android \
		-alias androiddebugkey -keyalg RSA -keysize 2048 -validity 10000 -dname 'CN=Android Debug'
fi
"$tools/apksigner" sign --ks build/debug.keystore --ks-pass pass:android --v4-signing-enabled false \
	--out build/PaperangStudio-Android-debug.apk "$scratch/aligned.apk"
"$tools/apksigner" verify build/PaperangStudio-Android-debug.apk
(cd build && shasum -a 256 PaperangStudio-Android-debug.apk > SHA256SUMS)
echo "$PWD/build/PaperangStudio-Android-debug.apk"
echo "Source build: $(cat "$scratch/assets"/source-build.txt)"
