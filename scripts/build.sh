#!/bin/zsh
set -eu
cd "${0:A:h}/.."
ROOT="$PWD"
APP_VERSION="$(tr -d '\r\n' < VERSION)"
WORK="${PAPERANG_WORK_DIR:-${ROOT:h}/PaperangStudio-Development}"
mkdir -p "$WORK/build" "$WORK/Release"
WORK="${WORK:A}"
export CLANG_MODULE_CACHE_PATH="$WORK/build/ModuleCache"
export SWIFTPM_MODULECACHE_OVERRIDE="$CLANG_MODULE_CACHE_PATH"
swift build -c release --build-system native --disable-sandbox --scratch-path "$WORK/build" --cache-path "$WORK/build/cache" --config-path "$WORK/build/config" --security-path "$WORK/build/security"
APP="$WORK/Release/Paperang Studio.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$WORK/build/release/PaperangLab" "$APP/Contents/MacOS/"
cp "$WORK/build/release/PaperangPTYHost" "$APP/Contents/MacOS/"
codesign --force --sign - "$APP/Contents/MacOS/PaperangPTYHost"
cp doc/openapi.json "$APP/Contents/Resources/openapi.json"
cp assets/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
cp LICENSE NOTICE "$APP/Contents/Resources/"
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?><!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd"><plist version="1.0"><dict><key>CFBundleExecutable</key><string>PaperangLab</string><key>CFBundleIdentifier</key><string>local.paperang.lab</string><key>CFBundleName</key><string>Paperang Studio</string><key>CFBundleDisplayName</key><string>Paperang Studio</string><key>CFBundleIconFile</key><string>AppIcon</string><key>CFBundlePackageType</key><string>APPL</string><key>CFBundleVersion</key><string>$APP_VERSION</string><key>CFBundleShortVersionString</key><string>$APP_VERSION</string><key>LSMinimumSystemVersion</key><string>14.0</string><key>NSBluetoothAlwaysUsageDescription</key><string>连接用户选择的Paperang打印机，读取设备数据并执行用户指定的测试打印。</string><key>NSHighResolutionCapable</key><true/></dict></plist>
PLIST
codesign --force --sign - "$APP"
codesign --verify --deep --strict "$APP"
print "App: $APP"
