# AndroidClient

AndroidClient prints directly to the Paperang P2 over BLE on Android 13 or later. The package ID is `com.elt.pprstudio.adc`.

## Build and install

Use JDK 21, Android SDK Platform 36 and Build Tools 36.1.0. The build script uses `ANDROID_HOME` or `ANDROID_SDK_ROOT`, and falls back to `~/Library/Android/sdk` on macOS.

```sh
./Android/build.sh
adb install -r Android/build/PaperangStudio-Android-debug.apk
```

Keep `Android/build/debug.keystore` to update an existing installation signed with that key. In the app, open **Printer**, grant Nearby Devices access, and scan for the P2. **About & version** shows the installed version, internal build number, and source build fingerprint.

## Compose and print

- **Text** and **Image** provide layout settings and a paper-length ruler.
- **Layout** combines text, images, labels and QR codes. Drag with one finger to move an object. Pan or zoom the page with two fingers. Paper length defaults to the last nonblank raster row and can be set manually.
- **Bitmap** imports raster1 JSON or RAW. The preview can be exported as PNG, RAW or raster1 JSON.
- **Drawing** opens a full-screen canvas with width and length rulers. Draw with one finger or a stylus, pan or zoom with two fingers, then save the drawing as a reusable asset or export it as raster1 JSON.

Check the preview, density, copies and paper feed before printing. **History** keeps local print records and their raster output. Restore an editable job, insert its raster as an illustration, or delete the record. **Assets** lists drawings saved to the local library.

[Android release notes](/en/release-v0.3.3-androidclient/) · [raster1 format](/en/raster1/) · [中文完整手册](/zh-CN/android/)
