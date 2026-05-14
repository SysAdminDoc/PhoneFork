# Android-to-Android Migration Tooling — Feasibility Research

**Use case:** Samsung Galaxy S25 Ultra (new, Android 16 / One UI 8) → Samsung Galaxy S22 Ultra (freshly factory-reset, Android 16 / One UI 8). **NEW → OLD direction**, both Samsung, both same OS. Target deliverable: a Windows desktop app.

---

## 1. Proprietary Windows Tools

### Samsung Smart Switch PC (Windows / Mac)
- **Stack:** Proprietary (Samsung). Free.
- **Root:** No.
- **Latest:** Available via Microsoft Store + samsung.com. Requires Win 10+, Android 4.3+ (encrypted transfer = Android 7+).
- **Workflow:** Sequential, **one phone at a time** — backup S25 → disconnect → connect S22 → restore. No simultaneous dual-USB path on the PC version (that's mobile-app-only).
- **NEW → OLD verdict:** Officially designed for old → new, but the PC version's backup/restore split *does* allow reverse direction. **Major caveat:** restoring a backup from a newer Android/One UI version to an older device often fails or partially restores. Since both phones are on Android 16 / One UI 8, this risk is minimized — but version-string equality is not version-internal equality (S25 backups may reference Galaxy AI / S25-exclusive features the S22 can't accept).
- **What transfers:** contacts, messages, call logs, photos, music, calendars, notes, alarms, Wi-Fi, home-screen layout, **most settings**, some apps + limited app data via Samsung Cloud handoff. Skips: DRM content, mobile-banking logins, most third-party app private data, keystore-backed credentials.
- **Backup file format:** Per-category folders + encrypted ZIPs (AES-256-CBC, fixed IV/key historically; PIN-based since 4.1.16). Reverse-engineered by Han/Lee (2016), Park (2018), Hur (2021) — published in ScienceDirect.
- https://www.samsung.com/us/support/owners/app/smart-switch
- https://www.samsung.com/us/support/answer/ANS10002458/
- http://fileformats.archiveteam.org/wiki/Samsung_Smart_Switch_backup

### Wondershare MobileTrans (formerly Dr.Fone Phone Transfer)
- **Stack:** Proprietary. $39.99/yr or $49.99 perpetual (Windows).
- **Root:** No. USB to PC, both phones plugged in.
- **Transfers:** 18+ data types incl. apps + in-app data via batch reinstall, WhatsApp chats, photos/videos/contacts/messages. Bridges via PC, also has QR-code phone-to-phone mode.
- **NEW → OLD:** Direction-agnostic (PC picks source/target manually).
- **Reality check:** "in-app data" claims are oversold — full app sandbox transfer without root is impossible per Android security model. Likely uses Auto Backup APIs + WhatsApp's local backup format for that subset.
- https://mobiletrans.wondershare.com/phone-to-phone-transfer.html

### iMobie AnyTrans for Android / DroidKit
- **Stack:** Proprietary. Subscription.
- **Root:** No. USB, both phones connected to PC.
- **Transfers:** Photos, videos, messages, contacts, music, APKs (no private data without root). iMobie explicitly recommends **DroidKit** over AnyTrans for Android↔Android.
- **NEW → OLD:** Direction-agnostic.
- https://www.imobie.com/android-manager/phone-clone-app.htm

### MOBILedit Phone Manager / Forensic
- **Stack:** Proprietary. Forensic edition reads Smart Switch backups directly.
- **Root:** No for general; root unlocks more.
- **Transfers:** Contacts, SMS, calendar, photos, music, docs via three paths: PC-USB bridge, Bluetooth/Wi-Fi mobile app, or cloud relay. No real app-data migration without root.
- **NEW → OLD:** Direction-agnostic.
- https://www.mobiledit.com/mobiledit
- https://forensic.manuals.mobiledit.com/MM/samsung-smart-switch-backup

### MyPhoneExplorer (FJ Software)
- **Stack:** Proprietary freeware (closed source, free). Windows only. Latest PC 2.3 (Oct 2025); client APK 4.10 (Jan 2026).
- **Root:** No.
- **Transfers:** Contacts, calendar, notes, SMS, call logs, files, app *list* (not app data). 100% local (Wi-Fi/USB/Bluetooth) — no cloud.
- **NEW → OLD:** Direction-agnostic via backup/restore.
- **Why it matters:** Closest existing model for a Windows-resident two-phone manager with no cloud dependency.
- https://www.fjsoft.at/ (via FossHub: https://www.fosshub.com/MyPhoneExplorer.html)

---

## 2. OSS / Community Tools

### Migrate (BaltiApps) — `balti.migrate`
- **Stack:** OSS, GPL. Android app only.
- **Root:** **Required** (helper installs APKs + unpacks `/data/data` + restores runtime permissions).
- **Transfers:** APKs (incl. splits), full app data, permissions. Designed for ROM hops.
- https://github.com/BaltiApps/Migrate-OSS

### Neo Backup (NeoApplications, formerly OAndBackupX)
- **Stack:** OSS, on F-Droid. v8.3.18 (May 2026).
- **Root:** **Required** for app data. Without root, modern Android (10+) blocks `/data/data` access entirely.
- **Transfers:** APKs + data + SMS/call-log special backups (JSON). Keystore-backed apps can't fully restore (re-auth required).
- https://github.com/NeoApplications/Neo-Backup
- https://f-droid.org/packages/com.machiav3lli.backup/

### Swift Backup + Shizuku
- **Stack:** Proprietary app (freemium) over OSS Shizuku ADB-relay.
- **Root:** No (Shizuku via Wireless ADB pairing on Android 11+).
- **Transfers without root:** APKs, SMS, call logs, wallpapers, permissions, batch install/uninstall/enable. **Cannot** transfer private `/data/data` — Shizuku is shell-level, not root.
- **Why it matters:** Architecturally the most realistic blueprint for a no-root Windows tool — same Wireless-ADB primitive.
- https://www.swiftapps.org/faq
- https://shizuku.rikka.app/

### Seedvault
- **Stack:** OSS (Calyx/GrapheneOS), AOSP-integrated.
- **Root:** No, but **must be the system backup transport** — only works on Pixel/Calyx/Graphene/e/OS. **Won't run on Samsung One UI** (Samsung uses Samsung Cloud as the transport instead). Disqualified for this use case.
- **PC drive flow:** Backups land in `.SeedVaultAndroidBackup/` (Storage Access Framework). Decryptable on PC via official `seedvault-extractor`. Restore via `adb shell am start com.stevesoltys.seedvault/.restore.RestoreActivity`.
- https://github.com/seedvault-app/seedvault

### scrcpy + ecosystem
- **Stack:** OSS (Genymobile). Display/control only — **not a migration tool**, but the underlying ADB tunnel + server-on-device pattern is the right primitive to crib.
- https://github.com/Genymobile/scrcpy

### Pure-ADB scripts (the actual no-root recipe)
The canonical no-root flow on Android 16, used everywhere from AnatomicJC's gist to Incredigeek's PowerShell script:
1. `adb shell pm list packages -3` — enumerate user apps
2. `adb shell pm path <pkg>` — get all split APK paths
3. `adb pull <path>` for each (handles `base.apk`, `split_config.*.apk`)
4. `adb install-multiple base.apk split1.apk split2.apk` on target
- https://gist.github.com/AnatomicJC/e773dd55ae60ab0b2d6dd2351eb977c1
- https://gist.github.com/jwhb/12767a6d25620748d69d106fd1293787
- https://www.incredigeek.com/home/using-adb-to-pull-apks-off-device/

### Not viable / not worth deeper look
- **adbGUI / ADB-Tool / Better Android Tools** — thin AutoHotkey/WinForms wrappers around adb commands; no migration-specific logic, no Samsung knowledge.
- **KnoxPatch** (LSPosed module to restore Samsung apps on rooted devices) — adjacent but not migration.
- https://github.com/salvogiangri/KnoxPatch

---

## 3. Technical Reality Check (Android 16, no root)

| Capability | Status on Android 16 | Notes |
|---|---|---|
| `adb pull` APKs via `pm path` | **Works** | Standard. Handles split APKs. |
| `adb install-multiple` to target | **Works** | Required for split APKs (most Play Store apps). |
| `adb backup` for app data | **Effectively dead** | Deprecated. Since Android 12, apps targeting API 31+ are *excluded by default*. Only debuggable apps backup. |
| `adb shell run-as <pkg>` | Debuggable apps only | 99% of production apps strip `android:debuggable`. |
| `settings get/put global\|secure\|system` | **Works** | Standard 3 AOSP namespaces. Some `secure` writes blocked. |
| Samsung-specific settings | **Partial** | Live in `com.sec.android.provider.settings` content provider (often called `sec_settings`). Read via `content query --uri content://com.sec.android.provider.settings/...`. Writes typically blocked without system/signature perms. |
| Shizuku Wireless ADB relay | **Works** | Best path for no-root automation. Pair via Android 11+ wireless debugging. |
| Auto Backup (`data-extraction-rules`) | **Works** | Per-app, 25 MB cap, Google Drive only. Android 16 QPR2 added `<cross-platform-transfer platform="ios">`. No PC sideload path. |
| Knox / Samsung Cloud restore protocol | **Closed** | No public OSS reimplementation. Smart Switch uses Android Open Accessory Protocol (AOAP), `com.sec.android.easyMover`. Backup format reverse-engineered academically (Hur 2021), but Samsung Cloud server protocol is not. |

---

## 4. Recommendation for a Windows App

**Realistic feature ceiling, no-root, both Samsung Android 16:**

1. **Smart Switch PC backup chain** — automate the backup-then-restore handoff with a Windows wrapper. Drives `SmartSwitch.exe` via UI automation (UIAutomation/FlaUI) since there's no public CLI. This is the only path that captures Samsung-internal settings + system data.
2. **APK + split-APK migration** — pure `adb` flow, no root needed. Enumerate via `pm list packages -3`, pull, `install-multiple` on target.
3. **Shizuku-style app data where supported** — Wireless ADB to both phones, run `bmgr` / `pm` / `cmd appops` for what's accessible.
4. **Media + user files** — direct `adb pull` from `/sdcard/`, push to target.
5. **Settings diff/apply** — AOSP `settings list <ns>` snapshot + diff + `settings put` on target. Samsung-specific via `content query`/`content insert` for known-safe keys.

**Differentiator from Smart Switch:** drive *both phones at once* (Smart Switch can't), reverse direction officially supported, no Samsung-account/Samsung-Cloud dependency, full per-item visibility, NDJSON-style audit log.

**Acknowledged gaps without root:**
- Most third-party app private data (banking, messengers besides WhatsApp's local-backup format, game saves) — cannot transfer. Surface this clearly in UX.
- Knox-protected enterprise data — cannot transfer.
- Keystore-backed credentials — re-auth required on target.

---

## Sources

- [Samsung Smart Switch](https://www.samsung.com/us/support/owners/app/smart-switch)
- [Smart Switch PC backup/restore guide](https://www.samsung.com/us/support/answer/ANS10002458/)
- [Smart Switch backup format wiki](http://fileformats.archiveteam.org/wiki/Samsung_Smart_Switch_backup)
- [Hur et al. — Smart Switch decryption (ScienceDirect)](https://www.sciencedirect.com/science/article/abs/pii/S2666281721002353)
- [Wondershare MobileTrans](https://mobiletrans.wondershare.com/phone-to-phone-transfer.html)
- [iMobie Phone Clone landscape](https://www.imobie.com/android-manager/phone-clone-app.htm)
- [MOBILedit Phone Manager](https://www.mobiledit.com/mobiledit)
- [MyPhoneExplorer (FossHub)](https://www.fosshub.com/MyPhoneExplorer.html)
- [BaltiApps Migrate-OSS](https://github.com/BaltiApps/Migrate-OSS)
- [Neo Backup (GitHub)](https://github.com/NeoApplications/Neo-Backup)
- [Neo Backup FAQ — root limitations](https://github.com/NeoApplications/Neo-Backup/blob/main/FAQ.md)
- [Swift Backup FAQ](https://www.swiftapps.org/faq)
- [Shizuku](https://shizuku.rikka.app/)
- [Seedvault](https://github.com/seedvault-app/seedvault)
- [scrcpy](https://github.com/Genymobile/scrcpy)
- [AnatomicJC ADB app-backup gist](https://gist.github.com/AnatomicJC/e773dd55ae60ab0b2d6dd2351eb977c1)
- [Incredigeek adbapkbackup](https://www.incredigeek.com/home/using-adb-to-pull-apks-off-device/)
- [Mobile Pentesting 101 — Death of adb backup (2026)](https://securitycafe.ro/2026/02/02/mobile-pentesting-101-the-death-of-adb-backup-modern-data-extraction-in-2026/)
- [Android Auto Backup + cross-platform rules](https://developer.android.com/identity/data/autobackup)
- [KnoxPatch (LSPosed)](https://github.com/salvogiangri/KnoxPatch)
- [Samsung Knox Smart Switch enforcement docs](https://docs.samsungknox.com/admin/knox-manage/kbas/kba-294-how-to-enable-smart-switch-knox-manage/)
