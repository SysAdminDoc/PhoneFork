# PhoneFork Roadmap

**Version 2026.05.14** — research-driven rewrite. Supersedes the v0.1.0 bootstrap skeleton.

Working document. Dense, sourced, skimmable. Every claim or feature has a URL in the **Appendix**.

---

## Vision

The Windows-host migration tool Smart Switch should have been. Two USB-connected Samsung phones, parallel pipes, walk-away pipeline, brutally honest pre-flight, no Samsung account, no cloud, no platform-lock direction. Built around a small Core engine that the WPF GUI and Spectre CLI both drive, with an optional companion APK + push-and-run JAR helper for the operations ADB shell-UID can't reach. **Position: the OSS gap-filler for everything Smart Switch refuses to do or quietly drops, paired with a debloat pass nobody else ships in the same workflow.**

## Status quo (v0.1.0 — shipped 2026-05-14)

Three-project .NET 10 solution (`PhoneFork.Core` + `PhoneFork.App` (WPF) + `PhoneFork.Cli`). AdvancedSharpAdbClient 3.6.16 native binary protocol; bundled `tools/adb.exe` from platform-tools 37.0.0. Apps tab end-to-end: enumerate `pm list packages -3 -f`, pull splits via `SyncService.PullAsync`, install on destination via `pm install-create -i com.android.vending --install-reason 4 --user 0 -g`. Catppuccin Mocha hand-rolled theme. NDJSON audit log via Serilog `CompactJsonFormatter`. CLI sibling. Hardware-validated S25 Ultra → S22 Ultra (both Android 16 / One UI 8): 68 user apps enumerated, F-Droid 1.23.2 round-tripped with Play-Store installer attribution confirmed. Other five tabs (Media / Settings / Debloat / Wi-Fi / Roles) are placeholder shells.

## Hard constraints

- **License**: MIT (PhoneFork itself). Third-party Apache-2.0 / GPL data assets (UAD-NG debloat list, AppManager backup format) used as **data only**, never linked source. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- **Platform**: Windows 10+ / .NET 10 Desktop Runtime. Cross-platform port via Avalonia is a post-v1.0 stretch.
- **No-root**: every operation must work without root on the source or destination. Shell-UID + Shizuku is the elevation ceiling. Root features may exist behind an explicit toggle (v2+).
- **Android compatibility floor**: Android 11+ on both phones (Wireless ADB and split-APK install need it). Tested against Android 16 / One UI 8.
- **Privacy**: zero telemetry, zero cloud round-trip, no Samsung/Google account required. Audit log is local-only.
- **No pill backdrops** in the GUI (CLAUDE.md repo policy): backdrop radius ∈ {0, 4, 6, 8, 10, 12}; no `CornerRadius=999`, `Capsule()`, `rounded-full`.

## Themes (each Now/Next item maps to one or more)

1. **Coverage** — what gets migrated. Apps, media, settings, Wi-Fi, roles, SMS, contacts, call log, ringtones-per-contact, AOD, Edge Panels, wallpapers, keyboard data, default-launcher layout.
2. **Honesty** — pre-flight scans + "what won't transfer" reports so the user never wipes the source without informed consent.
3. **Velocity** — parallel pipes both phones, resumable per-data-type checkpoints, NDJSON manifest replay, exploit USB-3.x both sides.
4. **Trust** — Knox attestation gate, signed migration manifest, per-install ADB RSA key, sandboxed format parsers, USB-only enforcement.
5. **Reusability** — saved profiles ("MyHome", "Work-clean", "Family-handoff"), dry-run preview, lifecycle hooks, Healthchecks/webhook on done, OpenCLI dump.
6. **Interop** — AppManager backup format read/write, legacy `.ab` import, Smart Switch `.bk` read, Pixel-Restore co-existence, helper APK + DEX-via-`app_process`.
7. **Reach** — Wireless ADB pairing, mDNS auto-discovery, four Catppuccin flavors + light mode, WCAG 2.2 compliance, localization scaffolding, Avalonia/macOS+Linux port.

---

## Now (v0.2 → v0.5) — _the four-tab core_

### v0.2.0 — Media tab live ✅ _(shipped 2026-05-14)_

**Theme**: Coverage + Velocity. Sources: [scrcpy](https://github.com/Genymobile/scrcpy), [adb-sync](https://github.com/google/adb-sync), [Neo Backup](https://github.com/NeoApplications/Neo-Backup) #525/#709, community signal `§3`/`§7`.

- [x] **Manifest engine** — `find <root> -type f -printf '%P\t%s\t%T@\n'` per category, parsed into `Dictionary<string,(Size,Mtime)>`. Single shell call per category; missing roots return empty manifest cleanly.
- [x] **Per-category multi-select** — 11 categories: DCIM, Pictures, Movies, Music, Download, Documents, Ringtones, Notifications, Alarms, Recordings, WhatsApp media.
- [x] **Pull-then-push pipeline with per-file mtime preservation** — re-runs are idempotent because source mtime is propagated through `SyncService.PushAsync`.
- [x] **`--delete` / `--update` / `--preserve-conflicts` / `--dry-run`** flag vocabulary on CLI and WPF toggles.
- [x] **Total disk-size diff before run** — DataGrid columns: Src files/MiB, Dst files, New, Conflict, Same, Dst-only, MiB-to-xfer.
- [x] **Sync-conflict filename pattern** — `*.sync-conflict-<ts>-<sha8>.<ext>` per Syncthing pattern, opt-in via `--preserve-conflicts`.
- [x] **CLI**: `phonefork media manifest`, `phonefork media diff`, `phonefork media sync`.
- [ ] _Resumable chunked transfer with per-file CRC32 checkpoint_ — deferred to v0.2.1 (basic resumability already works because identical files are skipped on re-run; CRC32 verify is an opt-in stricter mode).
- [ ] _Parallel pull/push with bounded `SemaphoreSlim`_ — deferred to v0.2.1 (current pipeline is sequential per file; parallelism is a perf optimization, not a correctness gap).
- [ ] _Ringtone-default URI restore_ — deferred to v0.3 (lives in Settings tab; depends on the settings-apply engine).

### v0.3.0 — Settings tab live ✅ _(shipped 2026-05-14)_

**Theme**: Coverage + Honesty. Sources: [cmd-shell AOSP docs](https://source.android.com/docs/core/tests/vts/shell-commands), [Hur 2021](https://doi.org/10.1016/j.fsidi.2021.301172).

- [x] **Snapshot engine** — `settings list secure|system|global` per device into ordered `Dictionary<string,string>`. First-`=`-split parsing tolerates values that legitimately contain `=`. Drops AOSP "null" sentinel.
- [x] **Bucket coloring**: OnlyOnSource / OnlyOnDest / Same / Different. Set-diff via dictionary intersect.
- [x] **Cherry-pick `DataGrid`** — filterable by free-text + namespace + outcome. Default-select Different; OnlyOnSource is opt-in.
- [x] **Known-locked / dangerous allowlist** — 15-key blocklist enforced by `SettingsApplyService.KnownLockedOrDangerous` (`device_provisioned`, `android_id`, `bluetooth_address`, carrier `preferred_network_mode`, etc.).
- [x] **Single-quote shell-escaping** for values with whitespace / shell metas (the standard `'\''` dance).
- [x] **Ringtone-default URI restore** — `SettingsApplyService.SetDefaultSoundUrisAsync` sets `system.ringtone` / `notification_sound` / `alarm_alert` URI to a `file:///sdcard/Ringtones/...` remote path. Closes Smart Switch's missing-default-ringtone gap.
- [x] **CLI**: `phonefork settings dump|diff|apply`.
- [ ] _Samsung `com.sec.android.provider.settings` content-provider read_ — deferred to v0.3.1 (Phase-2 of Settings tab; v0.3 covers the three AOSP namespaces which already include most Samsung One UI keys exposed via `secure`/`system`).
- [ ] _Bixby Routines + Modes export_ — deferred to v0.7 helper-APK (Routines live in a Samsung-internal binder service, not the settings provider).
- [ ] _Saved presets ("AOD + Edge Panels", "Status bar tweaks")_ — deferred to v1.x Profiles + lifecycle hooks.

### v0.4.0 — Debloat tab live ✅ _(shipped 2026-05-14)_

**Theme**: Coverage + Reusability. Sources: [UAD-NG](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation), [Canta](https://github.com/samolego/Canta), [itxjobe/samsungdebloat](https://github.com/itxjobe/samsungdebloat).

- [x] **AppManagerNG debloat dataset embedded** at build via `<EmbeddedResource Include="..\..\assets\debloat\*.json">`. 5,481 entries (oem 4,289 + misc 481 + aosp 273 + carrier 249 + google 189).
- [x] **Intersection with destination's `pm list packages -s -e` + `-s -d`** via `DebloatScanner` returns `DebloatCandidate(Entry, IsEnabled)` rows.
- [x] **4-tier safety classification** — `DebloatTier { Delete, Replace, Caution, Unsafe }` mapped from dataset `removal` field. Default-hide Caution + Unsafe (tier-chip toggles in UI; Delete + Replace shown by default).
- [x] **Filter-tag column** — `DebloatList { Oem, Google, Carrier, Aosp, Misc }`.
- [x] **`pm disable-user --user 0 <pkg>` only** — never `pm uninstall`. Reversible via `cmd package install-existing <pkg>` + `pm enable <pkg>`.
- [x] **Snapshot-before-debloat + rollback** — captures full enabled-system-package set to `%LOCALAPPDATA%\PhoneFork\debloat-snapshots\<serial>-<ts>.json` before every apply.
- [x] **Conservative / Recommended / Aggressive presets** — `DebloatProfile` dropdown that intersects tiers (Conservative=Delete; Recommended=Delete+Replace; Aggressive=Delete+Replace+Caution; Unsafe always opt-in).
- [x] **CLI**: `phonefork debloat list|apply|rollback`.
- [ ] _Disable carrier MSM bloat-installer first_ — deferred to v0.4.1 (current implementation queues all matches in a single batch; the two-phase pattern is a perf optimization).
- [ ] _Per-package services inventory column_ — deferred to v0.4.1 (cosmetic — Tier+Warning already give the user enough signal to decide).
- [ ] _Community-list import (CSV/JSON additions on top of bundled dataset)_ — deferred to v1.x.

### v0.5.0 — Wi-Fi tab live ✅ _(shipped 2026-05-14)_

**Theme**: Coverage + Honesty + Trust. Sources: [Shizuku v13.6.0](https://github.com/RikkaApps/Shizuku), [QRCoder](https://github.com/codebude/QRCoder), community `§2` and `§9`.

- [x] **QR-bridge fallback path** — render any user-supplied SSID + PSK as a standard `WIFI:T:...;S:...;P:...;H:...;;` QR via `QRCoder.PayloadGenerator.WiFi`. Zero on-device install. PNG + SVG render helpers.
- [x] **Source SSID enumeration** — `cmd wifi list-networks` primary + `dumpsys wifi` regex fallback. Auth type parsed from the security column. Cross-OEM (works on any Android, not just Samsung).
- [x] **Per-row "Use → manual composer"** — pick an SSID from the list, type the PSK, render the QR. Selective by design (closes community §10 selective-restore signal).
- [x] **CSC / locale / region diff banner** — `CscDiffService` reads `persist.sys.sales_code`, `ro.csc.country_code`, `persist.sys.locale`, `persist.sys.timezone`, `gsm.sim.operator.iso-country`. UI banner surfaces mismatches with the "region-locked items may not restore" warning.
- [x] **CLI**: `phonefork wifi list`, `phonefork wifi qr`, `phonefork csc diff`.
- [ ] _Shizuku-bound `WifiManager.getPrivilegedConfiguredNetworks()` PSK export_ — deferred to v0.7 (depends on the companion APK).
- [ ] _Bulk import on destination via `WifiManager.addNetwork()`_ — deferred to v0.7 (same dependency).

---

## Next (v0.6 → v1.0) — _polish, trust, and the helper APK_

### v0.6.0 — Roles tab live ✅ _(shipped 2026-05-14)_

**Theme**: Coverage + Trust. Sources: [AOSP `cmd role`](https://source.android.com/docs/core/tests/vts/shell-commands), [Android 14 Restricted Settings](https://www.kaspersky.com/blog/android-restricted-settings/49991/).

- [x] **`cmd role get-role-holders` snapshot** for 8 roles: DIALER, SMS, BROWSER, HOME, ASSISTANT, CALL_REDIRECTION, CALL_SCREENING, EMERGENCY. Handles both `package:<pkg>` and `[<pkg>]` Android-version output formats.
- [x] **Side-by-side picker** — Source / Dest holder columns + per-row checkbox + default-select Different.
- [x] **`cmd role add-role-holder --user 0 <role> <pkg>`** apply.
- [x] **Per-app runtime permission grants** — `pm grant <pkg> <perm>` + `appops set <pkg> <op> <mode>` via `RoleService.GrantAsync` / `SetAppOpAsync`.
- [x] **CLI**: `phonefork roles get|apply`, `phonefork perms grant`.
- [ ] _Notification listener + Accessibility service auto-enablers_ — deferred to v0.6.1 (cosmetic; `cmd role` covers the high-value defaults).
- [ ] _Permission diff report column_ — deferred to v0.6.1 (`pm dump <pkg>` parsing is verbose; v0.6 surfaces the role-holder diff which is the load-bearing signal).

### v0.6.5 — Wireless ADB pairing ✅ _(shipped 2026-05-14)_

**Theme**: Reach + Reusability. Sources: [LineageOS adb_wifi.md](https://github.com/LineageOS/android_packages_modules_adb/blob/lineage-23.2/docs/dev/adb_wifi.md), [Shizuku QR pattern](https://github.com/RikkaApps/Shizuku).

- [x] **`adb pair <ip>:<port> <code>`** via `AdbPairingService` (CliWrap-driven). Returns structured success/output result.
- [x] **`adb connect` / `adb disconnect`** wrappers in the same service.
- [x] **`WIFI:T:ADB;S:<svc>;P:<code>;;` QR parser** for Android Studio-style pairing QRs.
- [x] **CLI**: `phonefork pair <ip:port> <code>`, `phonefork connect <ip:port>`, `phonefork disconnect [ip:port]`.
- [ ] _WPF pair dialog from DeviceBar_ — deferred to v0.6.5.1 polish; CLI surface is the load-bearing capability.
- [ ] _mDNS auto-reconnect via `_adb-tls-connect._tcp` discovery_ — deferred to v0.6.5.1 (the bundled `adb.exe` already handles auto-reconnect when paired; PhoneFork's wrapper just needs a UI shim).
- [ ] _Per-install ADB RSA key in `%LOCALAPPDATA%\PhoneFork\adb-keys\`_ — deferred to v1.0.0 trust-hardening (currently inherits the shared `%USERPROFILE%\.android\adbkey`).

### v0.7.0 — Helper companion APK + push-and-run JAR ([scrcpy](https://github.com/Genymobile/scrcpy) `app_process` pattern, [gonodono/adbsms](https://github.com/gonodono/adbsms))

**Theme**: Coverage + Interop.

- **`PhoneForkHelper.apk`** — signed companion containing N `ContentProvider` authorities behind one APK:
  - `content://phonefork/sms` — read/write SMS DB (replaces `bmgr backup` of `com.android.providers.telephony`).
  - `content://phonefork/calllog` — read/write call log.
  - `content://phonefork/contacts` — read full Contacts DB including per-contact ringtone URIs (closes community `§7`).
  - `content://phonefork/wifi` — Shizuku-bound `WifiManager.getPrivilegedConfiguredNetworks()` (v0.5 actually depends on this).
  - `content://phonefork/wallpaper` — `WallpaperManager.setStream()` via `am broadcast` ([WallpaperUnbricker](https://github.com/adryzz/WallpaperUnbricker) pattern).
  - `content://phonefork/keyboard` — Samsung Keyboard learned-words + Gboard user dictionary.
- **APK self-uninstalls** on migration completion. NDJSON audit logs every helper-mediated read/write.
- **Push-and-run JAR alternative** — `phonefork-agent.jar` (~250 KB) deployed via `adb push /data/local/tmp/`, executed `app_process / com.phonefork.Agent`. Read-only operations only; zero install footprint, scrcpy pattern verbatim.
- **`app_process` runs as shell UID** with `WRITE_SECURE_SETTINGS` and `READ_LOGS` already granted — useful for any v0.3 Settings keys the shell rejects via `settings put` but accepts via direct provider write.
- **Helper signing** — keystore at `~/.android/phonefork-helper/phonefork-helper.jks` (same pattern as SwiftFloris). GitHub Actions job builds the APK, `apksigner sign --v3-signing-enabled true`, copies output to `assets/helper.apk`, then .NET publish embeds.

### v0.8.0 — Smart Switch UI Automation handoff ([UI Automation / FlaUI](https://github.com/FlaUI/FlaUI), community `§1` app-data complaints, [Cellebrite goldmine post](https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/))

**Theme**: Coverage + Interop + Honesty.

- **Detect installed Smart Switch PC** at `C:\Program Files\Samsung\Smart Switch PC\`. If present, offer it as a complementary handoff for the app-data tier PhoneFork can't reach.
- **Drive `SmartSwitch.exe` via FlaUI** — UI Automation walk: open → backup-mode → select categories PhoneFork doesn't cover → confirm → wait-for-done. Treat Smart Switch as a subprocess in PhoneFork's audit log.
- **Read Smart Switch's intermediate cache** at `~/Documents/Samsung/SmartSwitch/backup/` BEFORE Samsung's final encryption pass ([Cellebrite "goldmine"](https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/)) — opportunistic ingest if a Smart Switch run is mid-flight.
- **`.bk` file read-only import** — implement AES-256-CBC + PBKDF2-HMAC-SHA1 decryption per [Hur 2021](https://doi.org/10.1016/j.fsidi.2021.301172). User-supplied passphrase (their Samsung-account PIN at backup time). Extract Messages, Contacts, Call Log SQLite DBs; render in the relevant tabs for selective re-apply.

### v0.9.0 — Backup-format compatibility ([AppManager](https://github.com/MuntashirAkon/AppManager) `meta.am.v5`, [abe](https://github.com/nelenkov/android-backup-extractor), [Android 16 QPR2 cross-platform-transfer](https://android-developers.googleblog.com/2025/12/android-16-qpr2-is-released.html))

**Theme**: Interop + Reusability.

- **Write AppManager-format backups** — `<pkg>/<ts>/{base.apk, split_*.apk, data.tar.gz.0, meta.am.v5, checksums.txt, rules.am.tsv, permissions.am.tsv}`. Two-way: PhoneFork backups restorable in AppManager; AppManager backups restorable in PhoneFork. GPL-3.0 schema reuse, no code linkage.
- **Legacy `.ab` import path** — for users on Android ≤11 with old `adb backup` archives. Reuse `abe` reference; reimplement decrypt in C#. Closes [Neo Backup #885](https://github.com/NeoApplications/Neo-Backup/issues/885).
- **Snapshot-based retention** — keep N (default 3) snapshots per device per source serial; tags + filters per AppManager Profiles model.
- **Configurable retention** — N snapshots, max-N-days, max-N-GB. Source: [Seedvault #977](https://github.com/seedvault-app/seedvault/issues/977).
- **Android 16 QPR2 `<cross-platform-transfer platform="ios">` rules** — emit them in our `meta.am.v5` so future iOS-target migrations get hinted at correctly.

### v1.0.0 — Polish + signed release + WCAG 2.2 + tests + CI

**Theme**: Trust + Reach.

- **xUnit test project for Core** — `tests/PhoneFork.Core.Tests/`. Cover: `pm path` regex parsing of base + split-config lines, NDJSON event serialization shape, manifest-diff bucketing math, debloat-list JSON-schema validation, settings-diff with mocked snapshots. CLAUDE.md repo policy is "no tests unless explicitly requested" — this is the explicit request. Target ~70% line coverage of Core. UI tests deferred to v1.1.
- **GitHub Actions CI** — `windows-latest` runner. Steps: `dotnet restore`, `dotnet build -c Release`, `dotnet test`, `dotnet list package --vulnerable --include-transitive` fail-on-found, lint `*.json` debloat-dataset assets, `dotnet publish` artifact upload.
- **Pre-merge `cosign verify-blob` smoke** on attached artefacts to ensure SLSA provenance round-trips.
- **CONTRIBUTING.md** at repo root — repo conventions, build instructions, where to file issues. Includes "no Co-Authored-By" rule per repo policy.
- **GitHub Discussions enabled** for community feature requests + show-and-tell with debloat profiles.

- **EV / Microsoft Trusted Signing for the EXE** — Trusted Signing is the right call ($10/mo, no USB token, full SmartScreen reputation post-March-2024). EV cert only if kernel-driver work ever happens. Sources: [Microsoft signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options) and the 2026-02-23 CA/B Forum 15-month-cert change.
- **Reproducible builds** — `<Deterministic>true</Deterministic>`, `<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>`, SourceLink, GitHub `actions/attest-build-provenance` emitting SLSA L2 + Sigstore bundle. Free on public repos.
- **Velopack auto-update** — delta packages, ~2s update+relaunch, bundles `.NET 10` prereq install. Wire to GitHub Releases. Sources: [velopack/velopack](https://github.com/velopack/velopack).
- **Catppuccin Latte/Frappé/Macchiato variants** behind a Settings dropdown — same 26-color schema, swap `ResourceDictionary`. Honors CLAUDE.md "include light theme when practical".
- **WCAG 2.2 audit pass** — focus-visible (2.4.11), 24×24px target (2.5.8), `AutomationProperties.Name`/`HelpText`/`LiveSetting.Polite` on device-list, full Narrator scan via AccScope + UIA Verify. Source: [W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/).
- **Localization scaffolding** — externalize ~120 user-facing strings to `Resources.resx`; `dotnet xliff` round-trip; en-US baseline + ko-KR (Samsung's home market) + pt-BR (Brazilian Samsung market). Source: [.NET 10 localization tooling docs](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/).
- **DPI-aware screenshots for README** — per repo-global `screenshots.md` 125% DPI rule.
- **Branding** — 5-prompt RGBA logo flow (Minimal/App/Wordmark/Emblem/Abstract), 512×512 PNG, banner. Per repo-global `branding.md`.
- **Inno Setup installer alongside ZIP** — for users who want a Start-Menu shortcut without manually unzipping.
- **Pairing UX onboarding** — first-run wizard (cf. Neo Backup `v8.3.18`) walks through USB-debug enablement on both phones, RSA prompt, role assignment.

---

## Later (post-v1.0) — _stretch and direction-setting_

### v1.x — Pre-flight scan ("Honesty pass")

Single-screen pre-flight modal before any migration starts. Replaces every "oh, I just realized X didn't transfer" thread in the community report:

- **2FA Authenticator audit** — scan installed packages for Google Authenticator, Authy, Microsoft Authenticator, Duo, FIDO2 hardware-bound apps; block migration with a wizard per-app (export QR codes / re-link guidance). Source: community `§6`.
- **Banking + DRM app warning** — known list of apps that re-auth on a new device-ID. Source: community `§6`.
- **Secure Folder / Samsung Pass / Wallet / Bixby Routines presence detector** — surface deep-links to in-app export tools BEFORE wiping source. Source: community `§7`.
- **CSC / locale / region mismatch banner** — `getprop persist.sys.sales_code` diff. Source: community `§9`.
- **`allowBackup=false` enumerator** — parse manifest of every user app, list those Google Restore would skip. Source: [Android Restricted Settings](https://www.kaspersky.com/blog/android-restricted-settings/49991/) + community report.
- **Knox warranty-bit / bootloader-state check** — `getprop ro.boot.warranty_bit`, `ro.boot.flash.locked`, `keystore2 attestKey`. Refuse-or-warn-only options. Source: [Knox SDK 3.12 docs](https://docs.samsungknox.com/dev/knox-sdk/release-notes/knox-sdk-3-12/), Pieterse 2023.

### v1.x — Profiles + lifecycle hooks ([AppManager Profiles](https://github.com/MuntashirAkon/AppManager), [Borgmatic hooks](https://torsion.org/borgmatic/docs/how-to/add-preparation-and-cleanup-steps-to-backups/))

- **Saved op-sets re-runnable from the GUI or as a CLI plan** — `phonefork apply --plan family-handoff.json`.
- **Before/after PowerShell hooks** per migration domain — `before_migration: kill WhatsApp on source`, `after_migration: push custom wallpaper`.
- **Healthchecks / webhook POST on done** — POST migration manifest + summary JSON to a user-configured URL. Source: [Neo Backup #717](https://github.com/NeoApplications/Neo-Backup/issues/717).
- **Windows toast notification on completion** — source: [LocalSend #2995](https://github.com/localsend/localsend/issues/2995).
- **Running-jobs panel** — list in-progress per-device operations live. Source: [Neo Backup #763](https://github.com/NeoApplications/Neo-Backup/issues/763).

### v1.x — App scanner column ([AppManager](https://github.com/MuntashirAkon/AppManager))

- **Tracker + native-library report per APK** — cross-reference against `android-libraries` and `android-debloat-list` submodule datasets. Surface as a column in the Apps tab and as a per-app drill-down panel. Same data model AppManager uses.

### v1.x — Per-package signature verification on apply

`apksigner verify --print-certs` (or AlphaOmega.ApkReader's `SigningBlock`) on every base APK. Refuse-if-mismatch toggle on the Apps tab. Forensic users get receipts.

### v2.0 — Cross-platform (Avalonia 12)

- **Avalonia XPF compatibility smoke test** ([Avalonia 12 blog](https://avaloniaui.net/)) — run the existing WPF binary unchanged on macOS/Linux.
- **Native Avalonia port** if XPF performance is unacceptable — ~9 h/view porting estimate per tab (per Avalonia "expert guide").
- **WebUSB browser fallback** ([ya-webadb](https://github.com/yume-chan/ya-webadb)) — browser-based mode for users who refuse to install a desktop tool. Same Core engine, JS interop.

### v2+ — Bridge mode (cross-OS migration)

- **iOS source → Samsung sink** — `mautrix-imessage`-style puppet-thread mapping (academic Part B). Bridge daemon on the desktop side, no per-device installs.
- **Stretch**: Beeper/Matrix bridge integration so iMessage threads round-trip into Samsung Messages.

### v2+ — Multi-source consolidation

Merge state from N source phones into one destination ([iMobie PhoneTrans](https://www.imobie.com/) pattern). E.g., a vet manages 4 work iPads + 1 Samsung; consolidate to a single new Galaxy.

### v2+ — Plugin model

OEM-specific tabs as separable plugins ([KDE Connect](https://invent.kde.org/network/kdeconnect-kde) pattern). Pixel, OnePlus/Oppo (`com.oplus.*`), Xiaomi (`com.miui.*`), Motorola each load a `phonefork-oem-<x>.dll` from `%LOCALAPPDATA%\PhoneFork\plugins\`. Different settings-provider URIs, different debloat tag filters.

### v2+ — Forensic-friendly receipts

- **Migration manifest with SHA-256 per file + Ed25519 signature** using a per-session keypair. Sources: Pieterse 2023, SWGDE 3.5.
- **UFDR-lite JSON + CSV exports** for cross-tool verification (Cellebrite, Magnet AXIOM). Source: SWGDE 2024.
- **Replay capability** — `phonefork apply --replay manifest.json` re-runs the exact sequence on a different destination, recording the second run against the same audit trail.

### v2+ — Headless / fleet / MSP mode

- **`phonefork --headless --plan plan.json --device <serial>`** for unattended workflows. Existing CLI already half-supports this.
- **Optional Kestrel-on-localhost web UI** ([Syncthing-style split](https://docs.syncthing.net/specs/bep-v1.html)) — same Core engine, REST API instead of WPF DataBinding. Useful for sysadmin laptops without monitor access.
- **HTTP API mode on the helper APK** ([android-sms-gateway pattern](https://github.com/capcom6/android-sms-gateway)) — long-lived migration service rather than a one-shot.
- **Trusted-pair memory** — AnyDesk/TeamViewer pattern. Once a phone has been paired with PhoneFork once, store serial + RSA fingerprint; future plugs skip the trust prompt.

---

## Under Consideration

- **Discovery grid** ([LocalSend](https://github.com/localsend/localsend) pattern) — mDNS-discovered phones on the LAN as visual tiles. Compelling but contradicts the USB-only-by-default trust posture; need a clear opt-in.
- **Live device dashboard cards** ([3uTools](https://3u.com/) pattern) — battery health, storage, IMEI, root-state, Knox warranty bit. Visually rich but doubles parsing surface area against `dumpsys` output formats that change between One UI minors.
- **In-app debloat-list editor / annotator** — UAD-NG already ships this; PhoneFork could either (a) PR upstream improvements to UAD-NG and re-sync, or (b) build a parallel tool. Lean toward (a).
- **Audio cast over ADB for ringtone preview** ([Adb-Device-Manager-2](https://github.com/Shrey113/Adb-Device-Manager-2)) — listen to migrated ringtone on PC speakers before applying. Niche but cheap.
- **OpenCLI `--help-dump-opencli`** from Spectre.Console 0.52+ — surface CLI as machine-readable for AI agent integrations. Zero-cost. Defer until v1.x with no urgency.
- **CRC32 vs SHA-256 verification mode** for media sync — CRC32 is fast but birthday-bound; SHA-256 is correct but CPU-bound on the phone side. Probably ship both with CRC32 default.

## Rejected (explicit, with reasoning)

- **Run as a `Microsoft.UI.Xaml` / WinUI 3 app instead of WPF.** Rejected: WinUI 3 packaging friction on LTSC IoT boxes (PhoneFork's sysadmin audience runs them). Stack-consistent with PatientImage, FileOrganizer.UI, OrganizeContacts, Snapture, Devicer. (See `docs/oss-dependencies.md` §16.)
- **Run as a MAUI app.** Rejected: still iOS-first via Mac Catalyst on macOS, no Linux. Wrong target.
- **Use `adb backup` for app data.** Rejected: dead since Android 12 — most apps target API 31+, which excludes them by default. Replaced by helper-APK content-provider reads + Smart Switch handoff for the hard tier. Source: [Mobile Pentesting 101 — Death of adb backup](https://securitycafe.ro/2026/02/02/mobile-pentesting-101-the-death-of-adb-backup-modern-data-extraction-in-2026/).
- **Use `bmgr restore` to apply D2D-transport backups.** Rejected: `bmgr backupnow` works but `bmgr restore` only delivers via OOBE — destination is already provisioned by the time PhoneFork opens. Source: [Lucid Co D2D testing recipe](https://lucid.co/techblog/2022/11/14/testing-android-device-to-device-transfer).
- **Drive Samsung Cloud REST API directly.** Rejected: no publicly documented REST surface. Smart Switch handoff is the only path. Verified against Samsung Developer portal.
- **Run on Pixel/Calyx/Graphene as a Seedvault frontend.** Rejected: Seedvault is a system-signed backup transport that won't load on One UI (Samsung uses Samsung Cloud as the transport). PhoneFork's Samsung-first thesis doesn't translate. Source: `docs/oss-references.md`.
- **Single Samsung-account login flow.** Rejected: zero-Samsung-account is the headline differentiator vs Smart Switch. Community `§4` consistently complains about the account dependency.
- **`adb tcpip 5555` legacy wireless mode.** Rejected: TLS-less, exposed port surface. Use only `adb pair` (Wireless Debugging) on Android 11+. Source: [arXiv 2401.08961](https://arxiv.org/abs/2401.08961).
- **Re-implement Samsung Themes purchases transfer.** Rejected: account-bound, encrypted under `/data/data/com.samsung.android.themecenter/` (root-only), and unredeemable on a different Samsung account anyway. Wallpapers (free ones) migrate via the helper APK.
- **Reuse Knox SDK at consumer scope.** Rejected: Android 15+ requires AE Device Owner / Profile Owner status for the Knox SDK. Consumer use case doesn't satisfy that. Source: [Knox SDK 3.12 notes](https://docs.samsungknox.com/dev/knox-sdk/release-notes/knox-sdk-3-12/).
- **Use Frida runtime hooking to dump app keys.** Rejected from v1: requires root + Frida server. Tracked as a v2 "advanced mode" stretch only.
- **Bundle MaterialDesign3 wholesale.** Rejected: M3 aesthetic clashes with Catppuccin Mocha. Use MaterialDesignThemes 5.3.2 selectively (DataGrid + dialogs only).
- **Default to Velopack at v0.2.** Rejected: PhoneFork is a "run twice in a phone-lifecycle" tool, not a daily driver — auto-update infra is premature until v1.0. Source: `docs/oss-dependencies.md` §11.

---

## Appendix — Sources

All claims above trace to one of these. Roughly grouped.

### Repo-local research artefacts
- [docs/oss-dependencies.md](docs/oss-dependencies.md) — Windows/.NET OSS library research snapshot (16 sections).
- [docs/oss-references.md](docs/oss-references.md) — Android-side OSS reference implementations (Shizuku, scrcpy, UAD-NG, AppManager backup format).
- [docs/competitor-research.md](docs/competitor-research.md) — Commercial competitor deep-dive (Smart Switch, Wondershare, iMobie, MobiKin, MOBILedit, Google B&R).
- [CLAUDE.md](CLAUDE.md) (local-only) — repo working notes including the sync-over-async deadlock gotcha and XAML unicode-entity gotcha.

### Direct OSS competitors
- **Universal Android Debloater Next Generation** — https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation (issues [#1306](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1306), [#1313](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1313), [#1314](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1314), [#1317](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1317), [#1377](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1377); release [v1.2.0](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/releases/tag/v1.2.0)).
- **AppManager** — https://github.com/MuntashirAkon/AppManager (issues #1944, #1953, #1958, #1970, #1972, #1973; release v4.0.5; backup format `meta.am.v5`).
- **Neo Backup** — https://github.com/NeoApplications/Neo-Backup (issues #525, #688, #709, #717, #719, #738-744, #752, #758, #763, #766, #773, #780, #788, #795, #820, #822, #844, #885, #906, #908, #909, #977).
- **Migrate (BaltiApps)** — https://github.com/BaltiApps/Migrate-OSS.
- **Seedvault** — https://github.com/seedvault-app/seedvault.
- **Shizuku** — https://github.com/RikkaApps/Shizuku (release [v13.6.0](https://github.com/RikkaApps/Shizuku/releases/tag/v13.6.0)) + https://github.com/RikkaApps/Shizuku-API.
- **scrcpy** — https://github.com/Genymobile/scrcpy ([develop.md](https://github.com/Genymobile/scrcpy/blob/master/doc/develop.md), [virtual_display.md](https://github.com/Genymobile/scrcpy/blob/master/doc/virtual_display.md), v4.0 release).
- **KDE Connect** — https://invent.kde.org/network/kdeconnect-kde, https://invent.kde.org/network/kdeconnect-android.
- **Gadgetbridge** — https://codeberg.org/Freeyourgadget/Gadgetbridge.
- **LocalSend** — https://github.com/localsend/localsend (issues #2995, #3007, #3014, #3024, #3036, #3041, #3071; [protocol-v2.md](https://github.com/localsend/localsend/blob/main/documentation/protocol-v2.md)).
- **ya-webadb (Tango)** — https://github.com/yume-chan/ya-webadb.
- **Android Backup Extractor** — https://github.com/nelenkov/android-backup-extractor.
- **google/adb-sync** — https://github.com/google/adb-sync (archived 2024-03).
- **Canta** — https://github.com/samolego/Canta (Shizuku debloater w/ APK restore).
- **itxjobe/samsungdebloat** — https://github.com/itxjobe/samsungdebloat/tree/main/ (S25 / One UI 8 toolkit).
- **Achno/debloat-samsung-ADB-shizuku** — https://github.com/Achno/debloat-samsung-ADB-shizuku.
- **timschneeb/debuggable-app-data-backup** — https://github.com/timschneeb/debuggable-app-data-backup.
- **android-sms-gateway / vernu/textbee** — https://github.com/capcom6/android-sms-gateway, https://github.com/vernu/textbee.
- **WallpaperUnbricker** — https://github.com/adryzz/WallpaperUnbricker.
- **gonodono/adbsms** — https://github.com/gonodono/adbsms.
- **Adb-Device-Manager-2** — https://github.com/Shrey113/Adb-Device-Manager-2.
- **Syncthing BEP v1** — https://docs.syncthing.net/specs/bep-v1.html.
- **Rclone Fs interface** — https://github.com/rclone/rclone/blob/master/fs/fs.go.
- **restic design** — https://restic.readthedocs.io/en/stable/100_references.html#design.
- **Borgmatic hooks** — https://torsion.org/borgmatic/docs/how-to/add-preparation-and-cleanup-steps-to-backups/.
- **Velero restore reference** — https://velero.io/docs/main/restore-reference/.
- **Mautrix bridges double-puppeting** — https://docs.mau.fi/bridges/general/double-puppeting.html.
- **iMazing transfer guide** — https://imazing.com/guides/how-to-transfer-data-from-one-iphone-to-another.

### Commercial competitors
- **Samsung Smart Switch** — https://www.samsung.com/us/support/owners/app/smart-switch and https://www.samsung.com/us/support/answer/ANS10002458/.
- Smart Switch backup file-format wiki — http://fileformats.archiveteam.org/wiki/Samsung_Smart_Switch_backup.
- **Wondershare MobileTrans** — https://mobiletrans.wondershare.com/.
- **iMobie DroidKit / AnyTrans / PhoneTrans** — https://www.imobie.com/.
- **MOBILedit Phone Manager / Forensic** — https://www.mobiledit.com/, https://forensic.manuals.mobiledit.com/MM/samsung-smart-switch-backup.
- **MyPhoneExplorer** — https://www.fjsoft.at/ (via FossHub).
- **MobiKin** — https://www.mobikin.com/.
- **Coolmuster** — https://www.coolmuster.com/.
- **Tenorshare iCareFone / iTransGo / 4uKey** — https://www.tenorshare.com/.
- **AnyMP4 / Apeaksoft** — https://www.anymp4.com/ + https://www.apeaksoft.com/.
- **Syncios / Anvsoft** — https://www.syncios.com/.
- **ApowerManager** — https://www.apowersoft.com/phone-manager.
- **Droid Transfer (Wide Angle)** — https://www.wideanglesoftware.com/droidtransfer/.
- **AirDroid** — https://www.airdroid.com/.
- **Samsung Knox Configure / Manage** — https://www.samsungknox.com/.
- **Cellebrite blog (Smart Switch goldmine)** — https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/.

### Community signal
- **HN — Setting up phones is a nightmare** — https://news.ycombinator.com/item?id=47170958 + https://joelchrono.xyz/blog/setting-up-phones-is-a-nightmare/ + https://www.osnews.com/story/144520/setting-up-phones-is-a-nightmare/.
- **HN — How do you backup Android (2024)** — https://news.ycombinator.com/item?id=42648597.
- **HN — fundamentally impossible to backup Android** — https://news.ycombinator.com/item?id=19879358.
- **Android Central — Smart Switch missing data** — https://forums.androidcentral.com/threads/samsung-smart-switch-does-not-transfer-apps.1020392/ + https://forums.androidcentral.com/threads/replacing-phone-using-smart-switch-what-wont-transfer.1056747/.
- **Samsung Community: app data missing** — https://eu.community.samsung.com/t5/galaxy-s24-series/completed-full-smart-switch-process-none-of-the-apps-know-who-i/td-p/9102766, https://us.community.samsung.com/t5/Galaxy-S25/SmartSwitch-quot-There-are-no-items-that-can-be-restored-quot/td-p/3165050, https://eu.community.samsung.com/t5/galaxy-s25-series/apps-section-not-showing-in-smart-switch/td-p/12029744.
- **Samsung Community: Wi-Fi passwords** — https://eu.community.samsung.com/t5/galaxy-s23-series/how-to-restore-wifi-passwords-from-previous-device/td-p/10785508, https://xdaforums.com/t/transferring-wifi-passwords.3606429/, https://eu.community.samsung.com/t5/galaxy-s24-series/transfer-saved-networks-from-huawei-or-xiaomi-to-samsung-s24/td-p/9300525.
- **Samsung Community: stuck at 99%** — https://us.community.samsung.com/t5/Galaxy-S24/Samsung-Switch-hangs-at-99-9-and-1-minute-remainin-when-trying/td-p/2877811, https://r2.community.samsung.com/t5/Galaxy-S/S24-ULTRA-SMARTSWITCH-STUCK/td-p/16147179.
- **Samsung Community: Secure Folder / Wallet / Pass** — https://xdaforums.com/t/secure-folder-not-restoring-by-smart-switch.4665109/, https://xdaforums.com/t/secure-folder-samsung-forgot-a-point-here-secure-folder-warning-on-samsung-switch-application.4569759/, https://r1.community.samsung.com/t5/galaxy-s/how-do-sync-wallet-to-new-phone/td-p/21432514.
- **Samsung Community: Bixby Routines** — https://us.community.samsung.com/t5/Suggestions/Backup-Bixby-Routines-Please/td-p/2407892.
- **Samsung Community: NEW→OLD direction** — https://eu.community.samsung.com/t5/questions/no-option-to-downgrade-on-smartswitch/td-p/4300646, https://r2.community.samsung.com/t5/Galaxy-S/No-option-In-smart-switch-PC/td-p/12180690.
- **Samsung Community: account-locked at OOBE** — https://eu.community.samsung.com/t5/mobile-apps-services/smart-switch-on-new-s23-ultra-not-allowing-receive-mode/td-p/8111453.
- **XDA: Samsung debloat threads** — https://xdaforums.com/t/s25-ultra-debloat-and-privacy-list.4716655/, https://xdaforums.com/t/s24-ultra-debloat-and-privacy-list.4654142/, https://xdaforums.com/t/galaxy-s25-ultra-debloat-guide.4747503/.
- **XDA: selective restore** — https://xdaforums.com/t/samsung-smart-switch-select-what-apps-to-restore.3896118/.
- **Technibble: Authenticator on Smart Switch** — https://www.technibble.com/forums/threads/microsoft-authenticator.90656/.
- **Android Police: Samsung apps you can delete** — https://www.androidpolice.com/samsung-galaxy-apps-can-be-deleted-smartphone/.

### Standards, APIs, platform
- **AOSP shell-cmd surface** — https://source.android.com/docs/core/tests/vts/shell-commands.
- **Android Auto Backup docs** — https://developer.android.com/identity/data/autobackup.
- **Android 16 QPR2 announce (cross-platform-transfer)** — https://android-developers.googleblog.com/2025/12/android-16-qpr2-is-released.html.
- **Android 15 Foreground Service types** — https://developer.android.com/about/versions/15/changes/foreground-service-types.
- **Android 14 Restricted Settings (Kaspersky writeup)** — https://www.kaspersky.com/blog/android-restricted-settings/49991/.
- **Android 15 ECM (emteria)** — https://emteria.com/blog/android-15-sideloading.
- **Lucid Co — D2D bmgr testing recipe** — https://lucid.co/techblog/2022/11/14/testing-android-device-to-device-transfer.
- **adb wireless docs (LineageOS adb_wifi.md)** — https://github.com/LineageOS/android_packages_modules_adb/blob/lineage-23.2/docs/dev/adb_wifi.md.
- **Android adb docs (developer.android.com)** — https://developer.android.com/tools/adb.
- **Samsung Knox SDK 3.12 release notes** — https://docs.samsungknox.com/dev/knox-sdk/release-notes/knox-sdk-3-12/.
- **Samsung Knox ISV docs** — https://docs.samsungknox.com/dev/knox-sdk/sample-app-tutorials/get-started-with-isv-apis/manage-devices-using-isv-apis/.
- **Samsung Now Bar (One UI 8)** — https://news.samsung.com/global/samsung-begins-official-rollout-of-one-ui-8-to-galaxy-devices, https://www.developer-tech.com/news/samsung-open-now-bar-developers-one-ui-8/.
- **Mobile-hacker — Shizuku capabilities (Jul 2025)** — https://www.mobile-hacker.com/2025/07/14/shizuku-unlocking-advanced-android-capabilities-without-root/.
- **HackTricks ADB 5555** — https://hacktricks.wiki/en/network-services-pentesting/5555-android-debug-bridge.html.
- **HackTricks Shizuku** — https://book.hacktricks.wiki/en/mobile-pentesting/android-app-pentesting/shizuku-privileged-api.html.

### Dependency releases
- **AdvancedSharpAdbClient releases** — https://github.com/SharpAdb/AdvancedSharpAdbClient/releases.
- **Serilog releases** — https://github.com/serilog/serilog/releases.
- **CommunityToolkit.Mvvm 8.4 (.NET Blog)** — https://devblogs.microsoft.com/dotnet/announcing-the-dotnet-community-toolkit-840/.
- **WPF-UI lepoco** — https://github.com/lepoco/wpfui/releases.
- **MaterialDesignThemes NuGet** — https://www.nuget.org/packages/MaterialDesignThemes/.
- **Spectre.Console 0.52 release notes** — https://spectreconsole.net/blog/2025-10-10-spectre-console-0-52-released.
- **Velopack** — https://velopack.io/ + https://github.com/velopack/velopack.
- **QRCoder NuGet** — https://www.nuget.org/packages/qrcoder/.
- **.NET 10 announce** — https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/.
- **WPF state in .NET 10 (CODE Mag)** — https://www.codemag.com/Article/2507051/The-New-Features-and-Enhancements-in-.NET-10.

### Security / signing / standards
- **GHSA-5crp-9r3c-p9vr (Newtonsoft.Json)** — https://github.com/advisories/GHSA-5crp-9r3c-p9vr.
- **Microsoft code-signing options** — https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options.
- **Sectigo CS** — https://www.sectigo.com/ssl-certificates-tls/code-signing.
- **Sigstore + cosign bundles** — https://blog.sigstore.dev/cosign-verify-bundles/.
- **W3C WCAG 2.2** — https://www.w3.org/TR/WCAG22/.
- **Microsoft accessibility testing** — https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-testing.
- **Catppuccin palette** — https://catppuccin.com/palette/.
- **Avalonia 12 blog** — https://avaloniaui.net/ + https://avaloniaui.net/blog/the-expert-guide-to-porting-wpf-applications-to-avalonia.
- **Uno Platform 6.0 announce** — https://platform.uno/blog/uno-platform-studio-6-0/.

### Academic + forensics
- **Hur, Lee, Cha (2021)** — "Forensic analysis of Samsung Smart Switch backup files", *FSI:DI* vol. 37, https://doi.org/10.1016/j.fsidi.2021.301172.
- **Park, Choi, Lee (2018)** — "Forensic analysis of KakaoTalk backup", *Digital Investigation*, https://doi.org/10.1016/j.diin.2017.10.002.
- **Han & Lee (2016)** — "Decryption of Samsung Smart Switch telephony backup", *Korean Journal of Forensic Science*.
- **Domingues & Frade (2022)** — "Android forensics techniques", *Electronics MDPI*, https://doi.org/10.3390/electronics11030337.
- **Pieterse, Olivier, van Heerden (2023)** — "Authenticity of smartphone evidence", *FSI:DI* vol. 44, https://doi.org/10.1016/j.fsidi.2023.301506.
- **Akinbi & Ojie (2024)** — "Systematic review of mobile forensic tools", *Wiley IDR*, https://doi.org/10.1002/wfs2.1556.
- **Mayrhofer, Stoep, Brubaker, Kralevich (2021)** — "The Android Platform Security Model", *ACM TOPS* 24(3), https://doi.org/10.1145/3448609.
- **Heid & Heider (2023)** — "Telegram forensics on Android", *FSI:DI* vol. 45, https://doi.org/10.1016/j.fsidi.2023.301543.
- **NIST SP 800-101 Rev.1 (2014)** — https://doi.org/10.6028/NIST.SP.800-101r1.
- **SWGDE Mobile Device Evidence Collection v3.0 (2023)** — https://swgde.org/documents/published-by-committee/mobile-devices/.
- **SWGDE Min Requirements for QA in Digital Forensics v3.5 (2024)** — https://swgde.org/documents.
- **Project Zero FORCEDENTRY (2022)** — https://googleprojectzero.blogspot.com/2022/03/forcedentry-sandbox-escape.html.
- **arXiv 2401.08961 — ADB security empirical study (2024)** — https://arxiv.org/abs/2401.08961.
- **Mobile Pentesting 101 — Death of adb backup (2026)** — https://securitycafe.ro/2026/02/02/mobile-pentesting-101-the-death-of-adb-backup-modern-data-extraction-in-2026/.
- **AOAP v2 spec** — https://source.android.com/docs/core/interaction/accessories/aoa2.
- **MasonFlint44 split-APK install gist** — https://gist.github.com/MasonFlint44/4b32d86da40f79d12355bb993fe23953.
- **AnatomicJC ADB app-backup gist** — https://gist.github.com/AnatomicJC/e773dd55ae60ab0b2d6dd2351eb977c1.
- **Raccoon — manually installing split APKs** — https://raccoon.onyxbits.de/blog/install-split-apk-adb/.
- **Incredigeek — using ADB to pull APKs off device** — https://www.incredigeek.com/home/using-adb-to-pull-apks-off-device/.

---

## What could derail this roadmap

Honest risk catalogue. Probability × impact, in rough descending order.

- **Smart Switch UI Automation breaks on a Samsung update** (high probability, medium impact). FlaUI-driven control of `SmartSwitch.exe` is fragile by design — Samsung rebrands a button, ships a new XAML layout, the locator misses. Mitigation: keep v0.8 as a soft handoff (PhoneFork's other tabs work without it), version-pin Smart Switch in the docs, and CI-test against the most common installed versions.
- **One UI minor bumps a Samsung settings key out of shell-UID write allowlist** (high prob, low impact). Settings tab `settings put` will fail-loud on the unwritable key but won't crash. Mitigation: per-One-UI version regression-test corpus, updated each Samsung release.
- **Wireless ADB pairing reliability over Wi-Fi 6E / Wi-Fi 7 enterprise networks** (medium prob, medium impact). PhoneFork's v0.6.5 wireless mode is a quality-of-life addition, not the core flow — if mDNS is unreliable on a network, fall back to USB transparently.
- **Helper APK signature mismatch breaking install-update flow** (medium prob, high impact). If the signing keystore is lost, future helper APKs can't update prior installs cleanly. Mitigation: keystore backed up to two independent locations + commit + document the recovery procedure in `CONTRIBUTING.md`.
- **AdvancedSharpAdbClient maintainer drops support** (low prob, high impact). Only mature .NET ADB client. Mitigation: vendor a local copy at the v1.0 mark; keep an eye on the upstream repo cadence; have a fallback plan to drive `adb.exe` via CliWrap shell.
- **AppManager backup-format breaks compat between v5 and v6** (low prob, medium impact). v0.9 ties PhoneFork to `meta.am.v5`. Mitigation: write integration tests against committed sample backups; pin the schema version we read/write; upgrade explicitly when v6 ships.
- **Samsung adds Knox-level USB lockdown** (low prob, very high impact). Knox MDM can blacklist `com.sec.android.easyMover` and ADB in enterprise tenants. PhoneFork is unaffected for consumer phones but would lock out from enterprise-managed devices. Mitigation: document the scope; users with MDM enrollment understand the boundary. No engineering remediation possible.
- **CA/B Forum tightens code-signing rules further** (medium prob, low impact). Trusted Signing should remain unaffected; switch providers if not.
- **Catppuccin org changes palette semantics** (very low prob). Mitigation: vendor the palette XAML in `Themes/`.

---

## Notes on this document

- Versioning convention: `vMAJOR.MINOR.PATCH` per CLAUDE.md repo policy. This roadmap iterates as a single document; date in the v-line at the top tracks the last rewrite.
- "Now / Next / Later / Under Consideration / Rejected" tiering per Phase 3 of the research protocol.
- Every Now/Next item maps to at least one source URL above. "Later" items may cite parent themes only; Under Consideration items may be source-light by design.
- This roadmap supersedes the v0.1.0 bootstrap skeleton. The v0.2 → v1.0 progression is unchanged in spirit; each tab is now fleshed with concrete sub-features and citations.
