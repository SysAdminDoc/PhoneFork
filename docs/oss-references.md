# PhoneFork — OSS Reference Implementations

Research notes for the dual-Samsung Galaxy ADB migration tool. Lead is the **insight** that changes how PhoneFork should be built; links and licenses are supporting evidence.

---

## 1. Shizuku — `RikkaApps/Shizuku`

- **URL:** https://github.com/RikkaApps/Shizuku
- **License:** Apache-2.0 · **Last release:** v13.6.0 (2025-05-25) · 25 k+ stars
- **Relevance:** The canonical "elevate to `shell` UID over ADB without root" pattern, plus the Android 11+ wireless-debugging pairing UX everyone now copies.

**INSIGHT — the starter is shipped as a JNI lib, not a DEX/APK.** [`manager/.../starter/Starter.kt`](https://github.com/RikkaApps/Shizuku/blob/master/manager/src/main/java/moe/shizuku/manager/starter/Starter.kt) reveals the trick: `libshizuku.so` lives under `applicationInfo.nativeLibraryDir`, and the user runs `adb shell <that-path> --apk=<sourceDir>`. The "starter" `.so` is actually an `app_process`-launched Java process. This means PhoneFork's companion APK can ship a single-file native-extracted binary that, once installed, has a stable shell-invokable entry point. The C# host doesn't need to wrangle dex paths — it just does `adb shell pm path <pkg>` then `adb shell <path-to-libphonefork.so>`.

**INSIGHT — the `USER_SERVICE_CMD_FORMAT` is a literal copy target.** [`starter/.../ServiceStarter.java`](https://github.com/RikkaApps/Shizuku/blob/master/starter/src/main/java/moe/shizuku/starter/ServiceStarter.java) builds: `(CLASSPATH='%s' app_process /system/bin --nice-name='%s' moe.shizuku.starter.ServiceStarter --token='%s' --package='%s' ...)&`. PhoneFork's C# `AdbProcessRunner` should expose `RunAsShellUser(string classpath, string mainClass, string[] args)` that emits this exact shape.

**INSIGHT — drive Shizuku from Windows, don't ask the user to install it.** Shizuku's starter is just an ADB shell command (`adb shell sh /sdcard/Android/data/.../start.sh`). PhoneFork can detect-or-install the Shizuku APK silently, run the same start command, and then bind to the AIDL `IShizukuService` via PhoneFork's companion APK. For the donor S25 Ultra in particular this is gold — Shizuku unlocks `getPrivilegedConfiguredNetworks()`, default-role queries, and `WallpaperManager` reads.

---

## 2. Swift Backup (proprietary, docs only)

- **Site:** https://swiftapps.com/swiftbackup
- **Relevance:** Sets the consumer-facing baseline of what's possible without root.

**INSIGHT — Swift Backup admits it cannot transfer: Wi-Fi PSKs, default-app roles, SMS/MMS *body*, call-log *body*, Samsung-Knox-locked apps, payment tokens, or biometric-gated app data.** This is your competitive moat list. PhoneFork's companion APK (the "unblocker") should explicitly target these gaps — that is the entire value prop over Swift Backup. Their Shizuku integration is **one-time pairing only** (the binder reference survives until reboot); follow that UX — pair once at PhoneFork launch, never per-operation.

---

## 3. scrcpy — `Genymobile/scrcpy`

- **URL:** https://github.com/Genymobile/scrcpy
- **License:** Apache-2.0 · **Last release:** v4.0 (2026-05-12) · 141 k stars
- **Relevance:** The reference implementation of "push a JAR, run it via `app_process`, no install."

**INSIGHT — this is the "no APK left behind" pattern PhoneFork should default to for read-only operations.** [`app/src/server.c`](https://github.com/Genymobile/scrcpy/blob/master/app/src/server.c) hard-codes `SC_DEVICE_SERVER_PATH "/data/local/tmp/scrcpy-server.jar"` and the launch line: `CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server <args>`. The JAR is pushed via `adb push`, run from shell UID, and on disconnect `/data/local/tmp` retains the JAR but no Settings/Apps entry exists. For PhoneFork, this means: build a `phonefork-helper.jar` for read-side ops (Wi-Fi scan, ringtone URI lookup, role-holder query) and only fall back to the installed APK for write-side ops that need a manifest permission (SMS provider, wallpaper write).

**Architecture note** — [`Server.java`](https://github.com/Genymobile/scrcpy/blob/master/server/src/main/java/com/genymobile/scrcpy/Server.java) derives `SERVER_PATH` from `java.class.path[0]`, so the JAR self-locates without a CLI flag. Crib this; it makes the C# side hardcode-free.

---

## 4. Samsung Smart Switch backup format

- **Hur et al. 2021** (Forensic Sci. Int.: Digital Investigation, S2666281721002353) — full AES-CBC schema, PBKDF2 with 1000 iterations, PIN-derived KEK, per-category SQLite stores wrapped in `.bk` containers.
- **Han Lee 2016** + **Park 2018** — earlier schema (pre-4.1.16, no PIN).
- **Public PoC repos:** `seanpm2001/UltraSwitch` (clone attempt, 19 stars, mostly aspirational; not a working decryptor). No production-grade `samsung-smart-switch-decryptor` exists on GitHub as of May 2026 — the academic papers are the only complete reference.

**INSIGHT — don't write a Smart Switch reader; *steal its category taxonomy*.** Hur et al. enumerate Samsung's internal migration categories: `CONTACT`, `MESSAGE`, `CALLLOG`, `CALENDAR`, `MEMO`, `APKFILE`, `APKDATA`, `WIFICONFIG`, `WALLPAPER`, `LOCKSCREEN`, `RINGTONE`, `ALARM`, `BLUETOOTH`, `HOMESCREEN_LAYOUT`, `SETTING`, `SBROWSER`, `S_HEALTH`, `S_NOTE`, `EMAIL`. That's your PhoneFork manifest table. Anything not in this list, Samsung itself has decided is not migratable — so don't promise it.

**INSIGHT — the PIN-derived-key step proves the on-device half (`com.sec.android.easyMover`) has a Knox-blessed sealed-key API that no third party gets.** Don't try to match Smart Switch on the categories Knox-locks (Secure Folder, Samsung Pay tokens, biometric vault) — they are unreachable without OEM signing. List them in PhoneFork's UI as "Smart Switch only" with a one-line explanation.

---

## 5. `com.sec.android.easyMover` AOAP

- **Status:** No public mitmproxy/USB-AOAP dump exists. Academic mentions (Hur 2021) describe the AOAP wire format as a custom binary protocol multiplexing category streams, but no GitHub PoC.
- **Recommendation:** Do not attempt to MITM AOAP. It changes per One UI version and Samsung has obfuscated it since 2023.

**INSIGHT — skip AOAP entirely; PhoneFork uses ADB-over-USB exclusively.** Smart Switch's AOAP path exists because it has to work in Samsung's setup wizard *before* ADB authorization. PhoneFork's setup story is "developer options enabled on both phones, allow USB debugging" — that's already the precondition. Spending engineering on AOAP RE is pure cost with no differentiation.

---

## 6. UAD-NG — `Universal-Debloater-Alliance/universal-android-debloater-next-generation`

- **URL:** https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation
- **License:** GPL-3.0 · **Last release:** v1.2.0 (2026-01-12) · 6.6 k stars · Rust/iced

**INSIGHT — `Removal` is a 4-tier enum, not a star-rating; clone it verbatim.** [`src/core/uad_lists.rs`](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/blob/main/src/core/uad_lists.rs) defines `enum Removal { Recommended, Advanced, Expert, Unsafe, Unlisted }`. PhoneFork's debloat profile should adopt the same names because (a) users already understand them from UAD, (b) the source JSON already labels every package this way (PhoneFork already synced the 562-entry dataset), and (c) it makes a future "UAD profile import" trivial — bytes-identical category names.

**INSIGHT — friendly-error translation is the secret-sauce file.** [`src/core/sync.rs`](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/blob/main/src/core/sync.rs) wraps every ADB call with `make_friendly_error_message()` that converts `DELETE_FAILED_USER_RESTRICTED` → human-readable "blocked by Samsung Knox, try disable instead." Crib the whole function. Specifically for PhoneFork on S22/S25 Ultras, expect: `DELETE_FAILED_USER_RESTRICTED` (Knox), `DELETE_FAILED_DEVICE_POLICY_MANAGER`, `Failure [INSTALL_FAILED_USER_RESTRICTED]`, `cmd: Can't find service: package` (when shell context dies mid-batch).

**INSIGHT — `pm disable-user --user 0 <pkg>` is the reversible action; PhoneFork should never use `pm uninstall --user 0` in its default debloat profile.** UAD-NG's rollback path is `pm enable <pkg>`. Pure-uninstall is unrecoverable without re-flashing the system partition. Default to disable; expose uninstall as an "Expert" toggle gated behind a confirmation.

**INSIGHT — reuse UAD-NG's restore safety net.** Yes, share the JSON dataset bi-directionally. PhoneFork should write a `phonefork-debloat-state.json` sidecar with `{pkg, action: disable|uninstall, prev_state, timestamp}` so the same Restore button works whether UAD-NG or PhoneFork did the disabling.

---

## 7. AppManager — `MuntashirAkon/AppManager`

- **URL:** https://github.com/MuntashirAkon/AppManager
- **License:** GPL-3.0 · **Last release:** v4.0.5 (2025-07-28) · 8 k stars
- **Backup format docs:** `docs/raw/en/guide/backup-restore.tex`

**INSIGHT — AppManager's backup is split across N files with a metadata sidecar and `checksums.txt`. PhoneFork should *adopt this layout wholesale*.** Structure per [the docs](https://github.com/MuntashirAkon/AppManager/blob/master/docs/raw/en/guide/backup-restore.tex): `<pkg>/<timestamp>/{base.apk, split_*.apk, data.tar.gz.0, ext_data.tar.gz.0, obb.tar.gz.0, meta.am.v5, checksums.txt, rules.am.tsv, permissions.am.tsv}`. Why this matters: a PhoneFork backup is restorable in AppManager and vice-versa. Two ecosystems, one on-disk format = massive credibility win in r/androidroot, XDA, etc. License is GPL-3.0 — schema reuse is fine, code copy would viralize.

**INSIGHT — `Extras` (permissions, net policy, battery optimization, SSAID) is the category most other backup tools skip and is the silent killer of "my apps got restored but they keep asking for permissions again."** Include `permissions.am.tsv` in PhoneFork's backup spec from day 1; restore them with `pm grant <pkg> <perm>` after install.

**INSIGHT — split-APK handling: dump via `pm path <pkg>` returns 1-to-N paths.** Use `pm install-create` → `pm install-write` for each → `pm install-commit`. The naive `adb install <single.apk>` path breaks on every modern Samsung app (every Galaxy Store app is split by ABI + density + locale).

---

## 8. APK pull-and-install batch scripts

- **AnatomicJC gist** (`e773dd55ae60ab0b2d6dd2351eb977c1`) — single-shot bash; teaches the `pm path` → multi-`adb pull` → `pm install-multiple` flow.
- **`google/adb-sync`** — Apache-2.0, **archived 2024-03-23**. Rsync-style file diffing over `adb shell ls`/`pull`. Don't depend on it as upstream.
- **`jb55/adb-sync`** — predecessor of google's, also dormant.
- **incredigeek's PowerShell variants** — community scripts; no canonical repo, scattered gists.

**INSIGHT — production-grade split-APK install is `pm install-create -S <total-bytes>` then per-split `pm install-write <session> <idx> <name> <path>` then `pm install-commit <session>`.** The total-bytes flag is non-obvious and missing it makes Samsung's One UI 8 reject the install with a useless `INSTALL_PARSE_FAILED_NOT_APK`. Locale-split filtering: skip every `split_config.<lang>.apk` except `en` and the user's primary locale, or you'll bloat the target by 60 MB+ per app.

**INSIGHT — `adb-sync` is archived; PhoneFork ships its own media-sync.** Write it as a single C# class: `MediaSyncEngine.Sync(string srcSerial, string dstSerial, string remotePath, IProgress<long> bytes)`. Use `adb shell find <path> -printf "%T@ %s %p\n"` to get mtime+size+path per file in one round-trip, diff in C#, then batch-`pull`/`push`. Don't shell out to `adb sync` — that's host-side and doesn't do phone-to-phone.

---

## 9. WallpaperUnbricker — `adryzz/WallpaperUnbricker`

- **URL:** https://github.com/adryzz/WallpaperUnbricker
- **License:** none specified (treat as all-rights-reserved; **don't copy code**, copy pattern)
- **Last commit:** 2020-06-03

**INSIGHT — the entire "helper APK invoked by `am broadcast`" pattern in 11 lines.** Install the APK once, then trigger work via `adb shell am broadcast -a com.adryzz.wallpaperunbricker.SET_WALLPAPER -n com.adryzz.wallpaperunbricker/.SetWallpaperReceiver`. The receiver calls `WallpaperManager.setStream()` with the package's *own* permission, no Shizuku needed. PhoneFork's companion APK should expose one BroadcastReceiver per write-side category (`.SetWallpaper`, `.SetRingtone`, `.SetAlarmTone`, `.GrantPermission`), all behind an `<intent-filter>` with a custom permission only the host PC's `adb shell` can fire.

---

## 10. adbsms — `gonodono/adbsms`

- **URL:** https://github.com/gonodono/adbsms
- **License:** MIT · **Last release:** 0.0.10 (2026-01-24)
- **Pattern:** ContentProvider exposed at `content://adbsms` that the shell can `content query --uri content://adbsms`.

**INSIGHT — this is the exact pattern PhoneFork's companion APK should use for *every* provider-backed category.** One APK, multiple authorities: `content://phonefork/wifi`, `content://phonefork/calllog`, `content://phonefork/contacts`, `content://phonefork/ringtones`, `content://phonefork/alarms`. Each ContentProvider proxies to the system provider but holds the matching `READ_SMS` / `READ_CONTACTS` / `READ_CALL_LOG` permission in the APK manifest. The C# host then runs `adb shell content query --uri content://phonefork/<x>` on the donor and `adb shell content insert --uri content://phonefork/<x> ...` on the recipient. **adbsms's "temporarily assume default-SMS-app role" trick** is the only way to get write access to the SMS provider on Android 6+; PhoneFork inherits that constraint for SMS (read-only is easy; full migration requires the default-app role swap).

---

## 11. KDE Connect — `KDE/kdeconnect-android` + `kdeconnect-kde`

- **License:** GPL-2.0+ — **architecturally referenceable, code-uncopyable** into closed-source C#/.NET.
- **Plugin list:** `src/org/kde/kdeconnect/Plugins/` has ~25 plugins (battery, clipboard, contacts, file transfer, mpris, ping, presenter, sftp, share, sms, telephony, find-my-phone, run-command, mouse-pad, notifications, mediacontrol, etc.).

**INSIGHT — KDE Connect does *not* do batch APK transfer or Wi-Fi share.** It's a steady-state companion, not a one-shot migrator. So PhoneFork has a real gap to fill. The architectural lesson: **packet-based plugins with a versioned JSON schema** (`{"type": "kdeconnect.share.request", "body": {...}}`). PhoneFork's internal IPC between C# host and Android helper should use the same shape so future plugins (calendar, S Health, OneUI homescreen-layout) can be added without re-versioning the whole protocol.

**INSIGHT — KDE Connect uses TLS-pinned mDNS pairing over Wi-Fi.** Not relevant to PhoneFork's USB-tethered model, but if you ever want a "wireless mode" toggle, the pairing UX is already proven and OSS.

---

## 12. Settings dump/diff tools

- No canonical `RyanRodri/android-settings-backup` exists (verified — name was speculative). Closest:
  - **`mrRobot62/android-settings-backup`** — small bash, MIT, dormant since 2019.
  - Various XDA threads on `settings list global/system/secure` ranges.
- **Per-OEM key catalogs:** No maintained "safe-to-clone Samsung keys" list exists on GitHub. The Hur 2021 paper enumerates ~340 `sec_settings` keys Smart Switch migrates; that's the closest authoritative list.

**INSIGHT — there is no good OSS settings-diff tool. PhoneFork has a clear differentiation opportunity here.** Implementation plan: ship `data/samsung-safe-settings-keys.json` (curated from Hur 2021 + manual S22/S25 audit), iterate `adb shell settings list {global,system,secure}` on the donor, intersect with the safe-list, write each to the recipient via `adb shell settings put <ns> <key> <value>`. The safe-list itself becomes the moat — community-maintainable like UAD's `uad_lists.json`.

**INSIGHT — `settings put secure` requires `WRITE_SECURE_SETTINGS`, which `adb shell` has out of the box.** No helper APK needed for the settings category — this is pure ADB. Big architectural simplification.

---

## 13. Wi-Fi PSK export without root

- No standout OSS Windows-host tool exists. On-device closest match: `EmptyTechWifiKeyExporter` (defunct), various forks of `WifiList` requiring root.
- **The pattern that works on Android 11+:** Shizuku → call `WifiManager.getPrivilegedConfiguredNetworks()` via reflection on the privileged binder. AppManager does this internally for its "network policy" backup category.

**INSIGHT — Wi-Fi PSK export is the single highest-value category PhoneFork can offer that Swift Backup and Smart Switch both cap at "QR code one-at-a-time."** Companion APK + Shizuku binder → `getPrivilegedConfiguredNetworks()` → emit each as both raw `WifiConfiguration` (for recipient APK to `addNetwork()`) *and* standard `WIFI:T=WPA;S=<ssid>;P=<psk>;;` strings (for fallback manual QR display). Two output formats, one read.

**INSIGHT — on Android 16 the privileged API moved.** It's now `WifiManager.getPrivilegedConfiguredNetworks()` returning `List<WifiConfiguration>` but only when calling UID == 1000 (system) or 2000 (shell). Companion-APK-via-Shizuku gets you UID 2000. Don't try to call it from a normal app context — `SecurityException` since API 29.

---

## 14. Default-app role helpers

- **AOSP CTS** has `cts/tests/tests/role/.../RoleManagerTest.java` exercising `cmd role add-role-holder <role> <pkg>` from shell.
- No public wrapper library exposes `RoleManager` via Shizuku as of May 2026. Gap.

**INSIGHT — default-app roles are pure `cmd role` from `adb shell`; no companion APK needed.** Enumerate via `adb shell cmd role get-role-holders android.app.role.SMS` (and `.DIALER`, `.HOME`, `.BROWSER`, `.CALL_REDIRECTION`, `.CALL_SCREENING`, `.ASSISTANT`, `.EMERGENCY`). Restore via `adb shell cmd role add-role-holder <role> <pkg> --user 0`. Donor → recipient role mapping is straightforward; the only gotcha is the recipient must have the corresponding APK installed first (sequence: install APK, then assign role).

---

## 15. Multi-device USB-over-ADB on the Windows host

- **`adb -s <serial>`** is the canonical approach; nothing fancier exists at the protocol level.
- **No mature `libadb` for Windows hosts.** `cstamas/libadb-android` is on-device only. The .NET ecosystem has **`AdvancedSharpAdbClient`** (MIT, actively maintained, replaces madbutil/SharpAdbClient).
- **Robust serial discovery on Windows USB-C hub flips:** poll `adb devices -l` every 2 s; key by USB serial (stable across MTP↔PTP↔USB-debug mode changes); watch for `unauthorized` and `offline` states explicitly.

**INSIGHT — use `AdvancedSharpAdbClient` from the C# host instead of shelling out to `adb.exe`.** It speaks the ADB wire protocol directly, so it: (a) handles serial flips without re-shelling, (b) gives you `SyncService` for batched push/pull without re-spawn overhead per file, (c) exposes the framebuffer service if you ever want a thumbnail preview of each phone, (d) doesn't require bundling adb.exe (still recommended though, since the user's ADB version drives `pm install-create` behavior). Repo: https://github.com/SharpAdb/AdvancedSharpAdbClient — MIT, .NET Standard 2.0 + net8.

**INSIGHT — Samsung phones flip between MTP and PTP modes when the lockscreen times out.** PhoneFork must (a) hold a `wakelock` via `adb shell svc power stayon usb` for the duration of the migration, (b) keep both phones on the "File transfer" USB role, and (c) treat any `device offline` mid-stream as a retryable error with serial reconciliation, not a hard failure.

---

## 16. Helper-APK-less invocation via `app_process`

Already covered by §1 (Shizuku) and §3 (scrcpy). The unified takeaway:

**INSIGHT — PhoneFork uses a two-tier helper model.**
- **Tier 1 — JAR via `app_process` (no install).** Read-only operations that only need shell-UID permissions: Wi-Fi config dump (via Shizuku), settings list, package enumeration, content-resolver reads via `content query`. Push `phonefork-helper.jar` to `/data/local/tmp/`, run via `CLASSPATH=... app_process / com.maven.phonefork.Helper <args>`, never installs.
- **Tier 2 — APK install (write-side).** Categories needing manifest permissions: SMS write (default-app role), wallpaper set, ringtone-default URI write, contacts insert. Install `phonefork-companion.apk`, fire `am broadcast` per category, **uninstall on migration completion** (`pm uninstall com.maven.phonefork.companion`) — leaving a clean recipient phone is part of the UX promise.

---

## 17. Cross-platform alternatives — for awareness only

- **Avalonia** (MIT, https://github.com/AvaloniaUI/Avalonia) — closest WPF-feel on macOS/Linux. If PhoneFork's first wave gets traction, a v2 Avalonia port is one-week work.
- **Uno Platform** (Apache-2.0) — XAML on iOS/Android/macOS/Linux/Web. Heavier toolchain, larger ship size.
- **MAUI** — fine for mobile-first, awkward for the two-phones-on-USB-cables scenario PhoneFork actually is.

**INSIGHT — stay on WPF.** PhoneFork is a Windows-host tool that talks to two USB-tethered Android phones. The user has been clear about WPF + .NET 10 + Catppuccin Mocha. Cross-platform would only matter if the host moved to Mac/Linux, and at that point the bigger blocker is the ADB+USB driver story, not the GUI framework. Avalonia is the "if we ever port" answer; cross that bridge if/when a non-Windows user asks.

---

## Summary — the architecture this research implies

1. **C#/WPF host** uses `AdvancedSharpAdbClient` (MIT) for all ADB traffic — no `adb.exe` shellout.
2. **Two-tier helper model**: `phonefork-helper.jar` (no-install, scrcpy-pattern) for reads + `phonefork-companion.apk` (adbsms-pattern, broadcast-receiver-per-category) for writes.
3. **Optional Shizuku dependency** for Wi-Fi PSK and privileged-settings reads — auto-install/start it, don't ask the user.
4. **Backup format = AppManager-compatible** (`.am.tar.gz.0` + `meta.am.v5` + `checksums.txt`) — interop moat.
5. **Debloat profile = UAD-NG dataset + `Removal` enum verbatim** — disable-not-uninstall default, `phonefork-debloat-state.json` rollback sidecar.
6. **Category manifest = Hur 2021 / Smart Switch list** — anything outside it gets a "not supported" UI badge with the Knox reason.
7. **Settings sync = curated `samsung-safe-settings-keys.json`** — community-maintainable like UAD lists, no helper APK needed.
8. **Companion APK uninstalls itself at the end of the migration.**

**One sentence pitch:** "PhoneFork is `scrcpy` + `Shizuku` + `AppManager backup` + `UAD-NG debloat` welded into a two-phone WPF migration cockpit, with Samsung-specific knowledge baked in from the Hur 2021 paper."
