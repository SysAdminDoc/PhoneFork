# PhoneForkHelper

Companion Android APK for PhoneFork (v0.7.0). Surfaces categories that `adb shell` alone cannot reach without a manifest-permission-holding process: SMS, call log, contacts, Wi-Fi networks (with PSK), wallpaper, ringtones, and user dictionary.

## Architecture

Two-tier model lifted from scrcpy and adbsms:

1. **`phonefork-agent.jar`** — push-and-run JAR launched as the `shell` UID via `app_process`. Stays in `/data/local/tmp/`, leaves no Settings/Apps entry. Read-only operations: settings dumps, role queries, dumpsys parsing. (F011)
2. **`PhoneForkHelper.apk`** — signed APK with ContentProvider authorities for each category. Installs over ADB, holds READ_SMS / WRITE_SMS / READ_CONTACTS / READ_CALL_LOG / ACCESS_WIFI_STATE / etc. The host (Windows) queries it via `adb shell content query --uri content://com.sysadmindoc.phonefork.helper/<authority>`. Uninstalls after migration via `pm uninstall` (F019).

## Wire protocol

JSON-shaped responses from each provider's `query()` method. Selection/projection arguments are forwarded as `Bundle` extras. Examples:

```
adb shell content query --uri content://com.sysadmindoc.phonefork.helper/sms --projection thread_id:address:date:body
adb shell content query --uri content://com.sysadmindoc.phonefork.helper/wifi
adb shell content insert --uri content://com.sysadmindoc.phonefork.helper/sms/restore --bind json:s:'<base64-json>'
```

## Build

```bash
cd helper-apk
./gradlew assembleRelease
# Output: app/build/outputs/apk/release/PhoneForkHelper-release.apk
```

Sign with `apksigner`:

```bash
apksigner sign --ks ~/.android/phonefork-helper/phonefork-helper.jks --out PhoneForkHelper.apk app/build/outputs/apk/release/PhoneForkHelper-release-unsigned.apk
apksigner verify --print-certs PhoneForkHelper.apk
```

CI smoke (F020) calls `apksigner verify --print-certs` before embedding the artifact in the .NET publish.

## Target SDK policy

- `compileSdk` 36 (Android 16 QPR2 surface).
- `targetSdk` 36. Move to 37 (Android 17) only after `ACCESS_LOCAL_NETWORK` permission + rationale UX are in place.
- `minSdk` 30 (Android 11). Below 11 the Wireless Debugging primitive doesn't exist, so the host can't drive the install path.

## Status

**v0.7.0 — work in progress.** Gradle scaffold + manifest + ContentProvider stubs exist; provider bodies and JAR push-and-run path follow.
