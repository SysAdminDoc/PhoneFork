# PhoneFork Roadmap

**Version 2026.05.16b** — same-day refresh of 2026.05.16. Repository reconcile preserved, external research re-run end-to-end, material deltas folded in, ten net-new feature IDs (F099–F108), risk register expanded for public PoC exposure, source appendix grown from 72 to 95 external citations (S10–S104 IDs) plus 15 local repo sources. Working document: dense, source-backed, implementation-oriented.

Every external claim and feature idea maps to the Source Appendix. Local repo claims map to current files, commit history, and GitHub metadata checked on 2026-05-16.

## Vision

PhoneFork is the Windows-host migration tool Samsung Smart Switch should have been for power users: two Samsung phones connected at once, local-only, no root, no cloud, no Samsung account, no direction lock, no silent category drops, and an auditable plan before anything changes. It complements Smart Switch for Knox/private-app-data cases that only Samsung can reach, but owns the visible, reversible, scriptable migration cockpit: apps, media, settings, Wi-Fi, default roles, debloat, reports, and backup-format interop. As One UI 8.5 removes bootloader unlock entirely on Galaxy S26 and forward (S26), root-based competitors lose reach; PhoneFork's shell-UID + Shizuku + helper-APK posture becomes the only credible no-cloud migration path for current Samsung hardware.

## State Of The Repo

### Live Inventory

| Area | Current state |
|---|---|
| Repository | `SysAdminDoc/PhoneFork`, public, MIT, default branch `main`. Pre-launch on 2026-05-16: 0 stars, 0 open issues, 0 PRs, no tags yet — v0.6.9 release-readiness is queued under F107. |
| Version | README, CHANGELOG, XAML title, and app manifest show **v0.6.9** after the Trust And Maintenance Gate ship. Roadmap header at 2026.05.16b. |
| Recent history | v0.6.9 added CVE-2026-0073 wireless gate, per-install ADB key, trusted-pair registry with hashed serials, mDNS reconnect surface, Samsung honesty pre-flight, debloat dataset overrides, dependency bumps. Prior wave shipped v0.6.5–v0.6.8 (wireless ADB pair/connect, premium polish, hardening pass). |
| Stack | C# 14 / .NET 10 (SDK 10.0.202), WPF, MVVM (CommunityToolkit.Mvvm 8.4.2), Spectre.Console.Cli 0.55.0, AdvancedSharpAdbClient 3.6.16, AlphaOmega.ApkReader 2.0.10, CliWrap 3.10.1, **Serilog 4.3.1** + Compact NDJSON, QRCoder 1.6.0, MaterialDesignThemes 5.3.2, HandyControl 3.5.1, JsonSchema.Net 7.3.0, **Microsoft.Xaml.Behaviors.Wpf 1.1.142**, xUnit Core tests. |
| Build system | `PhoneFork.slnx`; projects: `src/PhoneFork.Core`, `src/PhoneFork.App`, `src/PhoneFork.Cli`, `tests/PhoneFork.Core.Tests`. Bundled `tools/adb.exe` + DLLs from platform-tools 37.0.0. |
| Entry points | WPF: `src/PhoneFork.App/App.xaml.cs`; CLI: `src/PhoneFork.Cli/Program.cs`. |
| Runtime target | Windows 10/11 with .NET 10 Desktop Runtime; Android 11+ devices over USB ADB or Android 11+ Wireless Debugging. |
| Code size | ~100 first-party C#/XAML files under `src` + `tests` (excluding `bin`/`obj` and generated `.g.cs`); ~8,000 LOC including XAML. |
| Top code surfaces | `CatppuccinMocha.xaml`, `DebloatService`, `MediaSyncService`, `DeviceBarViewModel`, `DebloatViewModel`, `WifiViewModel`, `SettingsViewModel`, `AppsViewModel`, `RolesViewModel`, `AppInstallerService`, `AdbPairingService`, `WirelessPolicy`, `TrustedPairRegistry`, `SamsungHonestyService`. |
| Shipped features | Apps, Media, Settings, Debloat (+ One UI 8.5 dataset overrides), Wi-Fi QR/CSC, Roles, Wireless ADB pair/connect/disconnect with USB-first policy + CVE-2026-0073 gate, mDNS reconnect, per-install ADB key, trusted-pair registry, hashed-serial NDJSON, Samsung honesty pre-flight, DeviceBar pair UI, shell/path hardening, first-run empty states, dark title bar. |
| Source markers | Source/test/docs scan found no TODO/FIXME/HACK/XXX/NotImplemented markers outside benign `[Obsolete]`-style enum docs. No stub functions in production code. |
| Tracked issues | GitHub issues list empty. No PRs. No external community signal yet. |
| Dependency state | `dotnet list package --vulnerable`: clean. Pending bumps after v0.6.9: QRCoder 1.6.0 → 1.8.0 (deferred), JsonSchema.Net 7.3.0 → 9.2.0 (deferred behind tests), Serilog.Sinks.File 6.0.0 → 7.0.0 (deferred), Spectre.Console 0.55.0 → 0.55.x-alpha (held; no stable cut). Test suite: 76/76 passing. |

### What It Does Today

| Domain | Current implementation | Gap that remains |
|---|---|---|
| Apps | Enumerates `pm list packages -3 -f`, resolves split APK paths, pulls APKs, installs with Play attribution and install reason. | No AppManager-format export, APK signature enforcement, OBB/ext-data awareness, or app-data honesty report yet. |
| Media | Builds `/sdcard` manifests, diffs categories, pull-then-push sync with mtime preservation and conflict options. | Needs resumable checkpoints, integrity verification mode, retry/replay manifest, huge-file handling and ETA. |
| Settings | Diffs `secure/system/global`, applies selected keys with safety blocklist. | Needs Samsung safe-key catalog, pre-flight surface, and per-Android/One UI corpus. |
| Debloat | Embeds AppManagerNG/UAD-NG 5,481-entry dataset, disables only, writes rollback snapshots. | Needs upstream sync policy, package breakage feed (e.g. UAD-NG #1394 `smartsuggestions` on One UI 8.5), work-profile/multi-user handling, per-device overrides. |
| Wi-Fi | Lists SSIDs when possible, renders QR PNG/SVG, displays CSC mismatch. | Needs Shizuku/helper path for PSK export/import and Android 17 `ACCESS_LOCAL_NETWORK` permission handling. |
| Roles | Snapshots/applies AOSP default roles and grants permissions/appops. | Needs notification listener/accessibility helper coverage and sequencing with app installs. |
| Wireless ADB | Pairs/connects/disconnects through bundled `adb.exe`; parses pairing QR payloads. **v0.6.9:** per-install ADB key directory, USB-first opt-in, session timeout/kill switch, trusted-pair registry with hashed serials, patch-level gate for CVE-2026-0073 with explicit "Allow unpatched" override, mDNS reconnect surface (UI + CLI). | Future: signed migration receipts (F085), per-device session policies. |
| Observability | Serilog CompactJsonFormatter writes NDJSON audit logs. **v0.6.9:** `SerialHashingEnricher` rewrites `device`/`*Serial` properties to 12-hex SHA-256 prefixes before disk so exports are share-safe. | Future: migration IDs, source/destination correlation, provider-call details (F021 after helper APK lands). |

### Hard Constraints

- **License:** MIT. GPL/AGPL projects are allowed as references or data-format compatibility targets, but no GPL code is copied into MIT binaries.
- **Platform:** Windows host first; WPF is intentional. Avalonia is a v2 portability path.
- **Android privilege ceiling:** no root required. Shell UID, ADB, `app_process` helper, and Shizuku are acceptable. Knox/system-signature-only data remains explicitly user-assisted or Smart Switch assisted. One UI 8.5 removed bootloader-unlock entirely on Galaxy S26 (S91, S94), reinforcing the no-root design choice rather than restricting it.
- **Privacy posture:** zero telemetry, zero cloud dependency, local audit log only.
- **Trust posture:** USB default, wireless opt-in, no legacy `adb tcpip 5555`, no long-lived broad LAN exposure.
- **UI style:** Catppuccin Mocha by default; no fully rounded text-bearing backdrops; dense utility cockpit, not a marketing page.

## External Research Summary

### Direct OSS And Adjacent GitHub Landscape

Stars, pushed dates, and active committers are point-in-time values collected from GitHub on 2026-05-16. Active committers = unique commit authors in the last 90 days from the GitHub commits API sample.

| Project | Role for PhoneFork | Stars | Last push | Active 90d | Current signal |
|---|---:|---:|---|---:|---|
| Genymobile/scrcpy | `app_process` no-install server pattern, optional mirroring | 141,728 | 2026-05-12 | 9 | v4.0 shipped 2026-05-15 with SDL3, bundled platform-tools 37.0.0, mDNS TCP detection, camera torch/zoom, flex display, --background-color, F11 fullscreen. |
| LocalSend | local transfer UX, mDNS, discovery, receive-notify patterns | 81,353 | 2026-04-30 | 6 | v1.17.0 stable. Android OEM file-picker bugs and desktop startup issues show cross-platform edge costs. |
| barry-ran/QtScrcpy | GUI/device-control adjacent | 29,522 | 2026-04-03 | 2 | Confirms demand for desktop phone-control wrappers. |
| RikkaApps/Shizuku | shell-UID privileged API pattern | 25,125 | 2025-06-18 | 0 | v13.6.0 (auto-start-on-trusted-Wi-Fi on Android 13+). No v14 announced; library stable. |
| timschneeb/awesome-shizuku | awesome-list for Shizuku ecosystem | 8,838 | 2026-05-14 | 3 | Harvest source for Shizuku-powered utilities. |
| MuntashirAkon/AppManager | backup format, APK scanner, permissions/rules extras | 8,022 | 2026-04-16 | 3 | Active backlog of Shizuku integration requests (#1970–#1973), APK verification request (#1974), Android 16 root mode incompatibility (#1962). |
| UAD-NG | debloat dataset and safety taxonomy | 6,604 | 2026-05-11 | 22 | Active stream of additions and One UI 8.5 breakage reports including #1394 `com.samsung.android.smartsuggestions` breaking Mobile Networks on Galaxy A57. |
| samolego/Canta | on-device Shizuku debloat UX | 4,778 | 2026-05-12 | 1 | Presets, descriptions, work-profile requests, uncertain-result messaging. |
| capcom6/android-sms-gateway | Android SMS provider/API architecture | 4,384 | 2026-05-09 | 3 | Active SMS/MMS/API issues; useful for helper APK HTTP/API stretch. |
| NeoApplications/Neo-Backup | rooted backup UI, retention, scheduling, restore pain | 3,614 | 2026-05-03 | 20 | v8.3.18 (2026-05-04): launcher shortcuts for schedules, backup sharing button. Format unchanged. |
| KieronQuinn/Smartspacer | Shizuku-adjacent system feature model | 3,333 | 2026-05-08 | 0 | Reference for non-root system integration but not migration. |
| yume-chan/ya-webadb | WebUSB ADB and browser fallback | 3,058 | 2026-05-15 | 2 | Open requests for Wireless Debugging TLS and sync compression. WebUSB Chromium-only. |
| nelenkov/android-backup-extractor | legacy `.ab` format import | 2,549 | 2026-05-12 | 0 | Mature reference for Android <= 11 `.ab` archives. |
| NetrisTV/ws-scrcpy | web scrcpy prototype | 2,412 | 2026-02-13 | 0 | Useful only if v2 web control appears. |
| RikkaApps/Shizuku-API | helper APK API dependency | 2,171 | 2025-05-29 | 0 | Stable API, low recent churn. |
| seedvault-app/seedvault | system backup transport, v1 format caution | 1,738 | 2026-04-29 | 4 | `android16` branch with Restic-inspired v1 format alongside legacy v0; extractor for v1 still WIP. |
| mrrfv/open-android-backup | open archive, companion app, hooks, 7-Zip export | 1,281 | 2026-04-24 | 3 | Companion app updated 2026-02-04. Strong archive/hook/interoperability lesson; no app private data. |
| google/adb-sync | ADB sync precedent | 1,094 | 2024-03-23 | 0 | Archived; confirms PhoneFork must own media sync. |
| Alex4SSB/ADB-Explorer | Windows ADB file manager UX | 851 | 2026-05-16 | 17 | Beta 0.9.26040 (2026-04-21). WpfUi redesign in progress. Active. |
| BaltiApps/Migrate-OSS | root-required ROM migration | 251 | 2026-04-25 | 1 | Active but root-first; sequencing reference only. With S26 bootloader-locked, less relevant for current Samsung hardware. |
| gonodono/adbsms | minimal ContentProvider over ADB | 1 | 2026-01-24 | 0 | Tiny but directly relevant pattern for SMS provider bridge. |
| jb2170/better-adb-sync | adb-sync rewrite with `--exclude` | 60+ | recent | low | Living reference for incremental media sync after google/adb-sync archived. |

### Commercial And Closed Source Signals

| Vendor/tool | What matters for PhoneFork |
|---|---|
| Samsung Smart Switch | Mobile app 3.7.69.15 (2026-02-12); PC version migrating to Microsoft Store v5.0.x. Transfers major categories with PC/Mac/mobile/external-storage paths but still exposes transfer failures and category omissions (notifications, login state, banking, Knox). PoC for cross-device-cable AOAP path remains undocumented publicly. |
| Wondershare MobileTrans | Commercially paywalls app/in-app-data claims, WhatsApp modules, iCloud-to-Android, selective transfer, speed/ETA language. |
| iMobie PhoneTrans | Sells clone/merge/custom migration and old phone/backup/iCloud/Google/iTunes input choices. Merge mode remains a concrete v2 opportunity. |
| Syncios Data Transfer | Has a "clear selected destination data before copy" option and an explicit failure/troubleshooting corpus. |
| Apeaksoft Android Data Backup & Restore | Markets encrypted backup, preview, selective restore, 8,000+ device support, and one-click restore. |
| MOBILedit Forensic | Imports mobile-created Smart Switch `.bk` backups, proving selective Smart Switch backup inspection is commercially valuable. |
| Microsoft Phone Link | Not migration: relays calls, SMS, notifications, drag/drop files (≤512 MB each, ≤100 at once). No backup. Companion only; do not position as a PhoneFork competitor. |
| Samsung Gallery × OneDrive | OneDrive backup discontinued 2026-04-11; Samsung Cloud is the new default. Users mid-migration may have stale OneDrive captures and no working Samsung Cloud auth. PhoneFork should warn pre-flight. |
| Samsung Pass × Samsung Wallet | Pass is being absorbed into Wallet. New phones should run the Pass-to-Wallet migration before PhoneFork attempts to surface Pass entries. Pre-flight warning required. |

### Material Deltas Since 2026.05.16 Baseline

1. **CVE-2026-0073 PoC exploit is public.** A working zero-click adjacent-network exploit against `adbd` `adbd_tls_verify_cert` has been published with full PoC and analysis. Patch level `2026-05-01` or later is required. PhoneFork's wireless ADB gate (F001) is no longer theoretical; UI must explicitly warn "exploit code is public" on devices below the patch line and refuse wireless pairing entirely below an opt-in override.
2. **Quick Share ↔ AirDrop interoperability rollout in 2026.** Cross-platform Quick Share is shipping to Samsung, OPPO, OnePlus, Vivo, Xiaomi, Honor through the year. QR-code Quick Share to iOS is rolling out to all Android in the next 30 days. Cross-platform sharing is no longer "watch in awareness"; PhoneFork's category-level "what didn't transfer" report should learn the Quick Share categories so it can recommend the OS feature instead of competing.
3. **Android 17 iPhone-to-Android migration tool launches July 2026** with One UI 9.0 on Galaxy Z Fold8/Flip8 first, Pixel 11 Pro in fall. Transfers WhatsApp data, eSIM, home-screen layout, accessibility settings, alarms, apps, calendar, call history, email, files, messages, notes, passwords, wallpapers. This is the de-facto Galaxy data-transfer ceiling for July 2026 onward; PhoneFork's iOS-source path (F082) becomes a v2 follow-on, not an early bet.
4. **One UI 8.5 stable rollout began 2026-05-06.** Galaxy S26 ships with it. The OEM Unlock toggle has been fully removed for S25/S26, all Z Fold 7, Z Flip 7, and devices updated to One UI 8 in all regions. Root-based competitors lose the current Samsung flagship; PhoneFork's no-root posture stops being a stylistic preference and becomes the only working path.
5. **UAD-NG dataset has a current safety regression on One UI 8.5.** Issue #1394 reports `com.samsung.android.smartsuggestions` breaks Mobile Networks settings on Galaxy A57. PhoneFork's embedded dataset must support out-of-band patches without a rebuild and a "known-bad on this OneUI" override.
6. **AppManager community is pushing for Shizuku integration.** Open issues #1970, #1972, #1973 all request Shizuku elevation, and #1974 requests APK verification. Direct social proof for PhoneFork's F010/F012/F060 roadmap items.
7. **Samsung Pass → Samsung Wallet migration is in active reminder mode in 2026.** Users completing PhoneFork-driven migrations during this transition need a pre-flight that detects `com.samsung.android.samsungpass`, warns about post-migration Wallet onboarding, and links to Samsung's documented one-time migration flow.
8. **Samsung Gallery × OneDrive backup ended 2026-04-11.** OneDrive option no longer appears for new accounts; Samsung Cloud is the replacement. Any "your photos are in the cloud" assumption from older migrations is invalid. Honesty report must surface this.
9. **ADB protocol Burst Mode and Android 11+ client-side compression** are in platform-tools 36+ and especially relevant to PhoneFork's media sync. Bundled `tools/adb.exe` is already platform-tools 37.0.0 (CHANGELOG and `THIRD-PARTY-NOTICES.md`), so the feature is available; PhoneFork should expose a "burst on / off" toggle and `ADB_BURST_MODE=1` env wiring for users with marginal cables.
10. **Velopack still pre-1.0.** Latest stable on NuGet remains 0.0.1298; no v1 cut. Keep updater work behind a feature flag until v1 lands.
11. **JsonSchema.Net 9.2.0** is current (April 2026). Existing pin is 7.3.0. Upgrade window is wider than the previous delta listed but is a behavior-change major; gate behind tests.
12. **Microsoft.Xaml.Behaviors.Wpf 1.1.142** is current (March 2026). Existing pin is 1.1.135. Drop-in patch upgrade.
13. **MaterialDesignThemes 5.3.2** confirmed last-updated 2026-05-01. Existing pin already at 5.3.2. Hold.
14. **No official `catppuccin/wpf` port exists** as of 2026-05-16. The hand-rolled `CatppuccinMocha.xaml` is still the only WPF path; the org's mid-May activity has been on Fleet, VS Code, Userstyles, Nix, website, Pantone, Monkeytype — no WPF additions.
15. **PhoneFork is pre-launch.** `github.com/SysAdminDoc/PhoneFork` shows 0 stars, 0 issues, 0 PRs, no releases, no tags. There is no community pressure on the project yet; the v1 release-track plan is what creates discovery and feedback loops.

## Feature Harvest And Prioritization Matrix

Scale: Impact/Effort/Risk = 1 low to 5 high. Prevalence: T = table stakes, C = common, R = rare but interesting. Tier sentence is the placement justification.

### Now

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F001 | Wireless ADB patch-level gate | S10,S11,S26,S82,S86 | Android bulletin, Shizuku, NVD, security press | security | T | Strong | 5/2/2 | DeviceService patch parser | Parity | Now: wireless ADB is shipped and must refuse/high-warn devices below 2026-05-01 before more wireless features land. |
| F002 | Per-install ADB RSA key | S81,S26,S39 | Shizuku, ADB docs | security | C | Strong | 5/3/2 | AdbHostService env injection | Parity | Now: avoids reusing the user's global adb key and narrows compromise blast radius. |
| F003 | USB-first wireless opt-in policy | S10,S11,S26,S82 | ADB security, Shizuku | security | C | Strong | 5/2/1 | F001 | Parity | Now: preserves PhoneFork's trust posture after CVE-2026-0073. |
| F004 | Trusted-pair registry with hashed serials | L04,S10,S26 | AnyDesk/TeamViewer pattern, ADB | security, observability | C | Strong | 4/3/2 | Local app data store | Parity | Now: lets the app remember safe devices without logging raw hardware IDs. |
| F005 | `adb mdns services` reconnect surface | L08,S27,S31 | scrcpy, ya-webadb | reliability | C | Strong | 3/2/2 | F001-F004 | Parity | Now: bundled adb already exposes the data; UI surfacing is cheap. |
| F006 | NDJSON serial hashing | L01,L02,L07 | audit-log practice | observability, privacy | C | Strong | 4/2/1 | F004 | Parity | Now: audit logs are shareable only if device identifiers are redacted. |
| F007 | Wireless session timeout and kill switch | S10,S11,S26 | ADB security | security, UX | C | Strong | 4/2/1 | DeviceBar state | Parity | Now: closes the "left Wireless Debugging exposed" failure mode. |
| F008 | Device developer-verification posture note | S13 | Android developer verification | docs, distribution | R | Strong | 3/1/1 | README/roadmap note | Parity | Now: users should know ADB-installed helper APKs remain allowed. |
| F009 | Dependency update batch | S49,S50,S51,S52,S55,S73,S98 | NuGet, dotnet list | security, reliability | T | Strong | 3/2/2 | Build smoke | Parity | Now: Serilog 4.3.1, Spectre.Console 0.55.2, QRCoder 1.8.0, Microsoft.Xaml.Behaviors.Wpf 1.1.142 are safe drop-ins; JsonSchema.Net 7.3.0 → 9.2.0 stays behind tests. |
| F010 | Helper APK Gradle scaffold | L06,L07,S26,S33,S97 | Shizuku, adbsms, Open Android Backup, AppManager Shizuku asks | mobile, platform | C | Strong | 5/4/3 | Android SDK/JDK 21 | Leapfrog | Now: unlocks SMS/calllog/contacts/Wi-Fi/wallpaper categories ADB shell cannot fully own. |
| F011 | `app_process` push-and-run JAR | L06,S27 | scrcpy | mobile, reliability | C | Strong | 5/4/3 | F010 shared protocol | Leapfrog | Now: gives read-side coverage without leaving an installed app behind. |
| F012 | Shizuku detect/start/runbook | S26,S39,S40,S41,S97 | Shizuku ecosystem, AppManager #1970-1973 | mobile, UX | C | Strong | 4/3/3 | F010 | Parity | Now: Wi-Fi PSK and privileged reads depend on a predictable shell-UID path. |
| F013 | SMS export/import provider | S33,S34,S64 | adbsms, SMS Gateway, MOBILedit | data, mobile | C | Strong | 5/5/4 | F010, default-SMS role choreography | Leapfrog | Now: message failures dominate community complaints; provider bridge is the least unrealistic no-root path. |
| F014 | Call-log provider | S33,S34,S64 | adbsms, SMS Gateway | data, mobile | C | Strong | 4/4/3 | F010 | Parity | Now: same companion architecture as SMS with lower role friction. |
| F015 | Contacts provider with vCard export | S20,S21,S64 | Apeaksoft, Open Android Backup | data, migration | T | Strong | 4/4/3 | F010 | Parity | Now: contacts are a core category and should also produce open export files. |
| F016 | Wi-Fi PSK export/import via Shizuku | L05,L06,S12,S26,S97 | Shizuku, Canta, AppManager community | data, platform | C | Strong | 5/5/4 | F010-F012 | Leapfrog | Now: Wi-Fi password loss is a repeated pain point and current v0.5 only has QR fallback. |
| F017 | Wallpaper/ringtone/notification setter | L06,S15,S74 | WallpaperUnbricker, Smart Switch | UX, platform | C | Strong | 3/3/2 | F010 | Parity | Now: restores visible personalization Smart Switch users notice. |
| F018 | Keyboard dictionary/user-dictionary export | L06,S65 | Smart Switch category taxonomy | data, platform | R | Medium | 3/4/4 | F010 | Leapfrog | Now: include in helper protocol design even if implementation follows SMS/contacts. |
| F019 | Helper self-uninstall and residue check | L06,S21 | scrcpy, Open Android Backup | trust, mobile | C | Strong | 4/2/2 | F010 | Parity | Now: "leave the phone clean" is central to local-first trust. |
| F020 | Helper APK signing and `apksigner verify` | L07,S13,S74 | Android signing, developer verification | security, distribution | T | Strong | 4/3/2 | F010, keystore | Parity | Now: required before embedding helper artifacts in the Windows app. |
| F021 | Provider-call audit events | L01,L06,S33,S67 | forensic tools, adbsms | observability | C | Strong | 4/3/2 | F010 | Leapfrog | Now: every helper read/write should be explainable in one NDJSON stream. |
| F022 | Backup/D2D capability probe | S14 | Android backup testing docs | honesty, data | C | Strong | 4/2/1 | App inventory | Parity | Now: tells users what Android's own D2D would or would not handle. |
| F023 | Open archive export sketch | S21 | Open Android Backup | data, docs | C | Strong | 3/2/1 | F015 | Parity | Now: design the helper output so VCF/CSV/JSON/7z export is not retrofitted later. |
| F102 | UAD-NG dataset hot-fix and override surface | S28,S93 | UAD-NG #1394 | data, reliability | C | Strong | 4/3/2 | DebloatDataset | Parity | Now: One UI 8.5 `smartsuggestions` regression proves the dataset must support per-OS overrides without an app rebuild. |
| F105 | CVE-2026-0073 PoC-aware UX | S10,S11,S82,S83,S84,S85,S86 | NVD, security press, PoC publications | security, UX | T | Strong | 5/2/1 | F001 | Parity | Now: the exploit is public, so a "patch level too low" condition must explicitly warn that working exploit code exists and refuse wireless pairing by default. |
| F108 | Samsung Pass presence and Pass→Wallet warning | L05,S87,S88,S89 | Samsung Pass migration reminders | honesty, platform | C | Strong | 4/2/1 | App inventory probe | Parity | Now: every Galaxy migration in 2026 happens during the Pass→Wallet handoff; pre-flight must show the warning so users don't lose Pass entries. |

### Next

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F024 | Smart Switch legacy/MS Store detection | S15,S16,S91 | Samsung support/community, Tech Community | integrations | C | Strong | 4/3/2 | none | Parity | Next: required before any Smart Switch handoff is reliable on current Windows installs (legacy `Program Files (x86)` vs MS Store `Packages\SamsungElectronicsCo...`). |
| F025 | FlaUI Smart Switch guided handoff | S15,S63,S75 | Smart Switch, FlaUI | integrations, UX | R | Medium | 4/5/4 | F024 | Leapfrog | Next: covers categories PhoneFork cannot legally/technically reach, while preserving audit trail. |
| F026 | Smart Switch mobile `.bk` import | S64,S65 | MOBILedit, Hur 2021 | migration, data | R | Strong | 5/5/5 | F028 | Leapfrog | Next: commercial forensic tools prove value; parser risk demands sandboxing. |
| F027 | AppContainer parser process | S66 | Project Zero parser lessons | security | T | Strong | 5/4/3 | F026 | Parity | Next: untrusted backup parsing must not run in the main WPF process. |
| F028 | Smart Switch cache opportunistic reader | S63,S64,S65 | forensic references | data | R | Medium | 3/5/4 | F024 | Leapfrog | Next: useful if present but brittle, so follow the automation foundation. |
| F029 | AppManager-format backup writer | S22,S23,S97 | AppManager + community demand | interop, data | C | Strong | 5/4/3 | F010, F020 | Leapfrog | Next: two-way AppManager compatibility is the strongest OSS credibility play. |
| F030 | AppManager-format backup reader | S22,S23 | AppManager | migration | C | Strong | 5/4/3 | F029 samples | Leapfrog | Next: lets users migrate existing backup assets into PhoneFork plans. |
| F031 | Legacy `.ab` import | S32,S14 | android-backup-extractor, Android docs | migration | C | Medium | 3/3/3 | F027 | Parity | Next: useful for Android <= 11 archives but not central to modern Samsung devices. |
| F032 | Open Android Backup archive bridge | S21 | Open Android Backup | interop, data | C | Strong | 4/4/3 | archive spec | Leapfrog | Next: open 7-Zip archive compatibility fills the "readable backup" parity gap. |
| F033 | Snapshot retention count | S24,S25 | Neo Backup, Seedvault | data, reliability | T | Strong | 4/2/2 | backup metadata store | Parity | Next: retention bugs are recurring in backup projects and easy to design early. |
| F034 | Retention by days and size | S24,S25 | Neo Backup, Seedvault | data, reliability | T | Strong | 4/2/2 | F033 | Parity | Next: prevents runaway local storage after repeated migrations. |
| F035 | Android `<cross-platform-transfer>` metadata | S43,S99 | Android Auto Backup, Android 16 QPR2 release notes | migration | R | Medium | 3/2/2 | F029 | Parity | Next: cheap metadata that keeps backup manifests aligned with Android 16+ semantics. |
| F036 | Seedvault v0/v1 compatibility note and watcher | S25,S61 | Seedvault, restic | migration, docs | R | Medium | 2/2/3 | F029 | Parity | Next: prevents false claims while keeping a future parser path visible. Seedvault v1 extractor remains WIP in `android16` branch. |
| F037 | Single pre-flight honesty screen | L05,S15,S71 | Smart Switch complaints | UX, reliability | T | Strong | 5/3/2 | Source/dest scan aggregation | Leapfrog | Next: users need the "what will not transfer" report before wiping source. |
| F038 | 2FA/authenticator audit | L05,S13,S71 | community | security, UX | C | Strong | 5/3/2 | App inventory | Leapfrog | Next: one of the highest-impact trust warnings. |
| F039 | Banking/DRM re-auth warning | L05,S43 | community, Android backup docs | honesty | C | Strong | 5/2/1 | App inventory | Parity | Next: reduces false expectations without risky extraction attempts. |
| F040 | Secure Folder/Pass/Wallet/Routines detector | L05,S15,S70,S87,S88 | Samsung support, community, Pass→Wallet migration | honesty, platform | C | Strong | 5/3/2 | Samsung package/key probes | Parity | Next: these are known Samsung exception categories; updated to include Pass→Wallet timing context. |
| F041 | `allowBackup` and data-extraction-rules parser | S14,S43,S99 | Android docs, Android 16 QPR2 cross-platform-transfer | honesty, data | C | Strong | 4/3/2 | AlphaOmega manifest read | Parity | Next: app inventory can already parse manifests; expose the result, including `<cross-platform-transfer>` posture. |
| F042 | Play Integrity/signature-sensitive app report | S13,S43 | Android verification | security, honesty | R | Medium | 3/4/3 | APK signature read | Leapfrog | Next: valuable for banking/DRM categories after basic pre-flight lands. |
| F043 | Global CSC/locale/region pre-flight | L02,L05,S15 | shipped Wi-Fi tab, community | platform, UX | C | Strong | 4/2/1 | Existing CscDiffService | Parity | Next: move shipped signal from Wi-Fi tab to whole-migration gate. |
| F044 | Knox/bootloader/warranty-bit check | S15,S65,S94 | Knox/forensics, S26 bootloader-unlock removal | security, platform | C | Strong | 4/3/2 | getprop probes | Parity | Next: cheap warnings; also explains why some data will remain inaccessible (Knox Vault) and why root-based competitors will fail on S26/S25 One UI 8.5. |
| F045 | SMS large-thread checkpointing | L05,S24 | community, Neo Backup | reliability, data | C | Strong | 5/4/3 | F013 | Leapfrog | Next: 50k+ message threads are a known failure class. |
| F046 | Media integrity verification modes | S36,S37,S59 | ADB Explorer, adb-sync, Syncthing | reliability | T | Strong | 4/3/2 | MediaSyncService | Parity | Next: file copy without checksum/retry will be challenged by large libraries. |
| F047 | USB stay-awake and role hold | S36,S81 | ADB Explorer, Android ADB practice | reliability | C | Strong | 4/2/2 | Device session lifecycle | Parity | Next: prevents mid-migration disconnects. |
| F048 | Offline/retry reconciliation | S24,S36 | Neo Backup, ADB Explorer | reliability | T | Strong | 5/4/3 | migration IDs | Parity | Next: long transfers need replay rather than restart. |
| F049 | Throughput/ETA and per-pipe progress | S17,S19,S36 | MobileTrans, Syncios, ADB Explorer | UX, performance | T | Strong | 4/3/1 | F048 | Parity | Next: paid tools sell speed; PhoneFork should measure it honestly. |
| F050 | VCF/CSV/HTML/PDF category exports | S20,S21,S34 | Apeaksoft, Open Android Backup, SMS tools | data, docs | C | Strong | 4/3/2 | F013-F015 | Leapfrog | Next: open deliverables beat opaque commercial backups. |
| F051 | WhatsApp/Signal/Telegram handoff wizards | L05,S17,S18,S71,S95 | MobileTrans, community, WhatsApp Drive transfer docs | integrations | C | Medium | 5/5/4 | pre-flight package mapping | Parity | Next: do not break sandbox rules; guide official app exports/imports. |
| F052 | Clear selected destination category before copy | S19 | Syncios | migration, UX | C | Strong | 4/3/3 | rollback/audit | Parity | Next: useful for contacts/messages/media duplicates but must be auditable. |
| F053 | Verified migration then source-clean checklist | S17,S19 | Dr.Fone/Syncios patterns | UX, security | C | Medium | 3/3/4 | F046, F050 | Parity | Next: only a checklist/deep link first; destructive automation waits. |
| F054 | Saved migration profiles | S15,S22,S60 | Knox/AppManager/Borgmatic | reusability | C | Strong | 4/3/2 | plan schema | Leapfrog | Next: repeat phone provisioning is a sysadmin use case. |
| F055 | Before/after PowerShell hooks | S21,S60 | Open Android Backup, Borgmatic | dev-experience | C | Strong | 3/3/3 | F054 | Parity | Next: powerful for advanced users; disabled unless explicitly enabled. |
| F056 | Healthchecks/webhook on completion | S24,S60 | Neo Backup, Borgmatic | observability | C | Medium | 3/3/3 | F054 | Parity | Next: optional local-to-user endpoint, no default telemetry. |
| F057 | Windows toast on completion/failure | S35 | LocalSend issues/features | UX | T | Strong | 3/2/1 | job status service | Parity | Next: long-running migrations need OS-level completion feedback. |
| F058 | Running jobs panel | S24,S35 | Neo Backup, LocalSend | UX, observability | T | Strong | 4/3/2 | migration job model | Parity | Next: multiple tabs already imply queued work. |
| F059 | Tracker/native-library APK scanner | S22 | AppManager | security, data | C | Medium | 3/4/3 | APK parser/signature path | Leapfrog | Next: useful as a post-install report after backup interop. |
| F060 | Per-package APK signature verification | S22,S23,S74,S97 | AppManager issue #1974, Android signing | security | T | Strong | 5/3/2 | APK parser/apksigner | Parity | Next: prevents accidental downgrade/tamper during app migration. |
| F061 | OBB/ext-data awareness | S21,S22,S36 | Open Android Backup, AppManager, ADB Explorer | data | C | Strong | 4/3/2 | app backup layout | Parity | Next: game/media-heavy apps need this even without private data. |
| F062 | Android storage virtual-disk/mount view | S36 | ADB Explorer | UX, data | R | Medium | 2/5/4 | media sync stable | Leapfrog | Next: defer until core transfer reliability is mature. |
| F063 | README screenshots and visual setup docs | L01,S15 | Samsung support, repo docs | docs, UX | T | Strong | 3/2/1 | stable UI | Parity | Next: releases need user-trust visuals. |
| F064 | Azure Artifact Signing / managed signing | S46,S47,S48,S100 | Microsoft, CA/B, Azure Artifact Signing FAQ | distribution, security | T | Strong | 5/4/2 | release workflow | Parity | Next: Windows utility trust hinges on signed artifacts. |
| F065 | RFC 3161 timestamping policy | S46,S48 | Microsoft, CA/B | distribution, security | T | Strong | 5/2/1 | F064 | Parity | Next: required to keep signatures valid after cert expiry. |
| F066 | Reproducible build + provenance | S46,S76 | Sigstore/SLSA/GitHub | security, distribution | C | Strong | 4/3/2 | CI | Parity | Next: public repo can emit provenance cheaply. |
| F067 | GitHub Actions CI and release artifact flow | L01,L07,S80 | repo stack, NuGet | testing, distribution | T | Strong | 5/3/2 | restore/build/test stable | Parity | Next: no tagged release exists; CI is a v1 trust gate. |
| F068 | Vulnerability scan in CI | S10,S73 | Android bulletin, NuGet | security, testing | T | Strong | 5/2/1 | F067 | Parity | Next: local scan is clean; keep it automated. |
| F069 | Dependency update policy | S49-S56,S73 | NuGet, GitHub releases | maintenance | T | Strong | 3/2/2 | F067 | Parity | Next: .NET/Android dependencies are moving quickly. |
| F070 | Velopack or release-poll updater | L07,S77 | Velopack, commercial tools | distribution | C | Medium | 3/4/3 | signing/release | Parity | Next: wait until Velopack v1 lands; current 0.0.x stays pinned by commit SHA if adopted. |
| F071 | Inno Setup installer alongside ZIP | L01,S46,S79 | Windows app norms | distribution | C | Strong | 3/3/2 | F064 | Parity | Next: ZIP is fine for power users; installer improves trust. |
| F072 | CONTRIBUTING.md | L01,S22,S28 | OSS norms | docs, dev-experience | T | Strong | 3/1/1 | CI commands | Parity | Next: needed before community issue intake. |
| F073 | GitHub Discussions enablement | S22,S28,S40 | AppManager/UAD/Shizuku ecosystems | community | C | Strong | 3/1/1 | F072 | Parity | Next: debloat profile feedback should not land as random bug reports. |
| F074 | Catppuccin Latte/Frappe/Macchiato themes | L07,S78 | Catppuccin, repo UI | accessibility, UX | C | Strong | 3/3/2 | theme dictionary split | Parity | Next: light/high-contrast variants improve repeat-use ergonomics. |
| F075 | WCAG 2.2 audit | S57,S101 | WCAG, accessibility testing guides 2026 | accessibility | T | Strong | 5/3/2 | stable UI controls | Parity | Next: device cockpit must be keyboard/Narrator usable. |
| F076 | i18n scaffolding en-US/ko-KR/pt-BR | S15,S56 | Samsung markets, .NET | i18n | C | Strong | 3/4/2 | Resources.resx extraction | Parity | Next: string extraction gets more expensive later. |
| F077 | UIA/Narrator smoke tests | S57,S101 | WCAG/WPF | accessibility, testing | C | Strong | 4/4/2 | F075 | Parity | Next: validates the dense DataGrid UI for assistive tech. |
| F078 | Device profile/corpus badges | S17,S18,S20 | commercial device-count claims | testing, docs | C | Strong | 3/3/2 | test device metadata | Parity | Next: honest "tested on" beats inflated 8,000-device marketing. |
| F079 | Samsung safe-settings catalog | L06,S65 | Hur 2021, settings tools | data, reliability | R | Strong | 4/4/3 | SettingsSnapshotService | Leapfrog | Next: curated safe-list becomes a community moat. |
| F099 | Samsung Gallery × OneDrive end-of-life detector | S88,S96 | Samsung 2026 cloud-photo backup change | honesty, data | R | Strong | 3/2/1 | App inventory | Parity | Next: OneDrive backup ended 2026-04-11; users must not assume their gallery is still cloud-backed. |
| F100 | ADB Burst Mode and compression toggle | S81,S92 | Android Debug Bridge docs, platform-tools 37 | performance, UX | C | Strong | 3/2/2 | AdbHostService env passthrough | Parity | Next: bundled platform-tools 37.0.0 already supports burst + Android 11+ compression; surface as a per-job toggle for marginal cables. |
| F103 | Galaxy AI on-device/cloud posture report | S90,S87 | Samsung Galaxy AI privacy controls | honesty, platform | R | Medium | 3/3/2 | getprop and pref probes | Leapfrog | Next: One UI 8.5 exposes per-feature cloud toggles; pre-flight should mirror source-side AI privacy choices on destination. |
| F107 | Public release/launch readiness pass | L01,S80,S77 | repo state (0 stars, 0 releases), GH Actions | distribution, community | T | Strong | 4/3/2 | F067, F072, F063 | Parity | Next: tagged release + screenshots + CONTRIBUTING is the discoverability floor; without it, F072/F073 community channels go cold. |

### Later

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F080 | Avalonia 12 host port | S58 | Avalonia | platform/OS | R | Medium | 3/5/4 | v1 Windows stable | Parity | Later: host OS reach matters after Windows flow proves durable. |
| F081 | WebUSB/browser fallback | S31 | ya-webadb | platform/OS | R | Medium | 3/5/4 | v2 architecture | Leapfrog | Later: useful for non-Windows hosts, but WebUSB/ADB auth is a separate product surface. |
| F082 | iOS source bridge | S18,S72,S87,S95 | PhoneTrans, Google I/O signal, Android 17 iPhone migration tool | migration, mobile | C | Medium | 4/5/4 | backup schema | Parity | Later: Android 17's official iPhone-to-Android tool ships July 2026; PhoneFork's standalone iOS path becomes a v2 follow-on, not an early bet. |
| F083 | Multi-source consolidation | S18 | PhoneTrans merge mode | multi-user, migration | C | Medium | 4/5/4 | contacts/messages dedupe | Parity | Later: merge mode is paid-tool parity after single-source reliability. |
| F084 | OEM plugin model | S62 | KDE Connect plugins | plugin ecosystem | R | Strong | 4/5/4 | stable Core contracts | Leapfrog | Later: lets Pixel/OnePlus/Xiaomi modules grow without bloating Samsung core. |
| F085 | Signed migration manifest receipts | S65,S67 | forensics | observability, security | R | Strong | 4/4/3 | stable manifest schema | Leapfrog | Later: high-value for repair shops and legal workflows. |
| F086 | UFDR-lite JSON/CSV exports | S64,S67 | forensic tools | data, docs | R | Medium | 3/4/3 | F085 | Leapfrog | Later: useful once category extraction is broad enough. |
| F087 | Headless/fleet mode | S15,S60 | Knox, Borgmatic | dev-experience, multi-user | C | Strong | 4/4/4 | F054, F067 | Leapfrog | Later: aligns with sysadmin provisioning but needs robust failure handling. |
| F088 | Local Kestrel API | S34,S35 | SMS Gateway, LocalSend | integrations | R | Medium | 3/5/4 | F087 | Leapfrog | Later: API mode is powerful but expands attack surface. |
| F089 | Notification mirroring while migrating | S35 | AirDroid/LocalSend adjacent | UX | R | Medium | 2/4/3 | helper APK | Parity | Later: convenient but not core migration. |
| F090 | SMS-from-PC gateway | S34 | android-sms-gateway | integrations | R | Medium | 2/4/4 | F013 | Parity | Later: adjacent utility, not migration-critical. |
| F091 | Printable SMS thread PDF | S34,S64 | Droid Transfer, forensic tools | docs, data | R | Medium | 3/4/3 | F013, F050 | Parity | Later: strong for legal/archive users after SMS import/export works. |
| F092 | Quick Share/AirDrop watcher | S72,S95 | Android Show 2026 signal, Quick Share AirDrop interop rollout | integrations | R | Low | 2/5/4 | platform APIs | Parity | Later: file sharing is increasingly OS-vendor territory; PhoneFork should detect Quick Share to iOS readiness and recommend it for ad-hoc transfers. |
| F101 | Android 17 / Cross-Device Migration watcher | S95,S99 | Android 17 release notes, Pixel 11 Pro launch | migration, integrations | R | Strong | 3/3/3 | platform detection | Parity | Later: Android 17 ships July 2026 with iPhone migration on Galaxy Z Flip8/Fold8 first; PhoneFork should detect that and explicitly say "the system tool covers this — use PhoneFork for the post-OOBE residue." |
| F106 | Phone Link/Link-to-Windows companion mode | S102 | Microsoft Phone Link docs | UX, integrations | R | Low | 2/4/3 | Win10+ COM/protocol stable | Parity | Later: Phone Link is a relay, not a migrator; an optional "open in Phone Link" deep link is cheap polish, not core. |

### Under Consideration

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F093 | LAN discovery grid | S31,S35,S62 | ya-webadb, LocalSend, KDE Connect | UX, platform | C | Medium | 3/4/4 | F001-F007 | Parity | Under consideration: contradicts USB-first trust unless explicitly gated. |
| F094 | Live device dashboard cards | S36,S38 | ADB Explorer, QtScrcpy | UX | C | Medium | 2/3/3 | DeviceService expansion | Parity | Under consideration: useful but can distract from migration outcomes. |
| F095 | In-app debloat list editor | S28,S29,S42 | UAD-NG, Canta | data, plugin ecosystem | C | Medium | 3/4/3 | upstream sync policy | Parity | Under consideration: upstream PRs may be better than a parallel editor. |
| F096 | Ringtone preview over ADB/audio cast | S38 | scrcpy/QtScrcpy adjacent | UX | R | Low | 2/3/2 | media tone migration | Parity | Under consideration: cheap, but niche. |
| F097 | OpenCLI/help schema dump | S51 | Spectre.Console | dev-experience | R | Strong | 2/1/1 | Spectre update | Parity | Under consideration: low-cost after CLI stabilizes. |
| F098 | CRC32 fast verification option | S36,S59 | ADB Explorer, Syncthing | performance, reliability | C | Medium | 3/2/2 | F046 | Parity | Under consideration: SHA-256 should be default for trust-sensitive modes. |
| F104 | Bundled platform-tools refresh policy | S81,S92 | Android Debug Bridge docs | distribution, reliability | C | Strong | 2/2/2 | release pipeline | Parity | Under consideration: cadence for bumping tools/adb.exe alongside Google releases; current 37.0.0 is fresh but the policy doc is missing. |

### Rejected

| ID | Idea | Sources | Category | Rejection |
|---|---|---|---|---|
| X001 | Promise full third-party private app data without root | L05,S14,S15,S43 | data | Android's sandbox and app backup opt-in model make this false advertising; PhoneFork should report limits and guide official exports. |
| X002 | Use `adb backup` as the main app-data path | S14,S32,S43 | migration | Modern Android makes it unreliable and often empty; keep only legacy `.ab` import. |
| X003 | Use `bmgr restore` after setup as a D2D restore mechanism | S14,S43 | migration | Android D2D restore is setup/OOBE-oriented; PhoneFork operates after users can run a Windows tool. |
| X004 | Direct Samsung Cloud REST client | S15,S63,S65 | integrations | No documented public API and it violates the no-account differentiator. |
| X005 | Seedvault frontend for One UI | S25,S61 | platform | Seedvault is a system backup transport; Samsung devices cannot load it as a normal app. |
| X006 | Mandatory Samsung-account login flow | L05,S15 | UX | Contradicts PhoneFork's core differentiator. |
| X007 | Legacy `adb tcpip 5555` wireless mode | S10,S11,S26 | security | TLS-less and unsafe compared with Android 11+ Wireless Debugging pairing. |
| X008 | Samsung Themes/Samsung Wallet token transfer | L05,S15,S70,S87 | platform | Account/Knox/keystore-bound data belongs in an honesty checklist, not an extraction feature. |
| X009 | Knox SDK consumer integration | S15,S65 | platform | Knox controls require enterprise roles and do not fit consumer no-root operation. |
| X010 | Frida/runtime key dumping | S11,S67 | security | Root/hooking path is not aligned with a trustworthy no-root migration utility. |
| X011 | Rewrite host as WinUI 3 or MAUI before v1 | L07,S58 | platform | WPF already ships and is the right Windows-host choice; cross-platform belongs after v1. |
| X012 | EV certificate as the primary signing strategy | S46,S48,S100 | distribution | Managed Artifact Signing plus timestamping is a better operational fit than hardware-token EV cert renewal under the 460-day CA/B rule. |
| X013 | Seedvault v1 parser in v0.9 | S25,S61 | migration | The Restic-inspired v1 format is divergent and extractor is still WIP; track after AppManager/Open Android Backup bridges. |
| X014 | Reverse engineer Samsung AOAP | L06,S15,S65 | integrations | High maintenance and low differentiation because PhoneFork's precondition is user-authorized ADB. |
| X015 | Runtime download/install of core dependencies | L07,S21 | distribution | Bundled tools and documented prerequisites are more trustworthy than surprise downloads. |
| X016 | Telemetry/cloud analytics | L01,L05,L06 | observability | Violates privacy posture; use local NDJSON and optional user-configured webhooks only. |
| X017 | Position Microsoft Phone Link as a PhoneFork backend | S102 | integrations | Phone Link relays calls, SMS, notifications, and small file drops; it does not back up. Treat as a deep-link target, not infrastructure. |
| X018 | Samsung Gallery OneDrive backup re-implementation | S88,S96 | data | Samsung killed OneDrive backup on 2026-04-11. Replicating a deprecated path is wasted effort; warn users instead. |
| X019 | Bootloader-unlock guidance for S25/S26 | S94 | platform | One UI 8.5 fully removed OEM Unlock toggle; chasing that path encourages risky firmware-mode workarounds and contradicts no-root posture. |
| X020 | Cross-Device Migration parity (iPhone source) before v1 | S95,S99 | migration | Android 17's built-in tool launches in July 2026 with a category surface PhoneFork cannot match cheaply; revisit as Later (F082, F101). |

## Release Tracks

### Now: v0.6.9 - Trust And Maintenance Gate (✅ SHIPPED 2026-05-16)

1. [x] Wireless ADB patch-level gate for CVE-2026-0073 with PoC-aware messaging (F001, F105). — `WirelessPolicy` refuses below 2026-05-01 by default; "Allow unpatched" override is explicit; PoC warning surfaces in `WirelessSessionStatus`.
2. [x] Per-install ADB key directory and `HOME` wiring (F002). — `AdbHostService.ConfigureAdbKeyDirectory()` sets `HOME` to `%LOCALAPPDATA%\PhoneFork\adb-home` before `StartServer`.
3. [x] USB-first wireless opt-in and timeout/kill switch (F003, F007). — `WirelessPolicy.OptInWireless()` + 30 min default session window + `KillWireless()` exposed on DeviceBar.
4. [x] Trusted-pair registry with hashed serials and raw-serial-free NDJSON (F004, F006). — `TrustedPairRegistry` at `%LOCALAPPDATA%\PhoneFork\trusted-pairs.json`; `SerialHashingEnricher` rewrites `device`/`*Serial` properties to SHA-256 prefixes before disk.
5. [x] `adb mdns services` reconnect view for trusted devices (F005). — `AdbPairingService.ListMdnsServicesAsync()` + DeviceBar mDNS card + `phonefork mdns services` CLI.
6. [x] Samsung Pass→Wallet pre-flight detector for any 2026 Galaxy migration (F108). — `SamsungHonestyService` covers Pass, Wallet, Secure Folder, Routines, Notes, Gallery/OneDrive, Account; CLI: `phonefork honesty --device`.
7. [x] UAD-NG dataset override hook + One UI 8.5 hot-fixes (notably `com.samsung.android.smartsuggestions`) (F102). — `DebloatDataset.WithOverridesFor()` + `assets/debloat/overrides.json`. Live for One UI >= 8.5.
8. [~] Dependency patch batch (F009). — Serilog 4.3.1 ✓, Microsoft.Xaml.Behaviors.Wpf 1.1.142 ✓. Spectre.Console held at 0.55.0 (no stable 0.55.x cut; only -alpha). QRCoder 1.8.0 still pending verification. JsonSchema.Net 9.x deferred behind tests.
9. [x] README note that Android developer verification does not block ADB-installed helper builds (F008).

**Why now:** wireless support already shipped, CVE-2026-0073's exploit is public, One UI 8.5 stable is rolling out, and Samsung's Pass/Wallet/Gallery transitions are happening live. Maintenance drift is also cheapest before helper APK work starts.

### Now/Next: v0.7.0 - Helper Companion APK And JAR

1. `helper-apk/` Kotlin/Gradle scaffold, target SDK 36 initially (F010).
2. Signed `PhoneForkHelper.apk` with provider authorities for SMS, call log, contacts, Wi-Fi, wallpaper, tones, and user dictionary (F010, F013, F014, F015, F016, F017, F018).
3. `phonefork-agent.jar` push-and-run path using the scrcpy `CLASSPATH=... app_process / ...` pattern for read-side operations (F011).
4. Shizuku detect/start guidance and helper binding for privileged Wi-Fi PSK reads (F012, F016).
5. Helper install/uninstall/query lifecycle through `HelperAppService` (F019).
6. Provider-call audit events and self-uninstall residue check (F021).
7. Open archive export sketch (F023) and backup/D2D capability probe (F022).
8. CI smoke for `apksigner verify --print-certs` (F020).

**Why now:** this is the pivot from "ADB shell can do it" to "honest no-root coverage expansion." It unlocks the highest-value missing categories without lying about `/data/data`. AppManager's open issues #1970–#1974 confirm the same Shizuku + verification arc.

### Next: v0.8.0 - Smart Switch Interop

1. Detect legacy MSI and Microsoft Store Smart Switch footprints (F024).
2. Drive Smart Switch via FlaUI only for categories PhoneFork cannot reach (F025).
3. Inspect existing Smart Switch backup folders and mobile-created `.bk` files (F028).
4. Run `.bk` parsing in an AppContainer-restricted child process (F026, F027).
5. Import readable categories into PhoneFork's selective apply views (F028).

**Why next:** Smart Switch remains the only path for some Samsung/Knox categories. PhoneFork should orchestrate it honestly rather than pretend to replace it.

### Next: v0.9.0 - Backup Interop

1. Write and read AppManager-compatible backups (F029, F030).
2. Add `.ab` legacy import for Android <= 11 archives (F031).
3. Add Open Android Backup archive bridge for 7-Zip/open export compatibility (F032).
4. Implement snapshot retention by count, days, and size (F033, F034).
5. Emit Android cross-platform-transfer metadata where it maps cleanly (F035).
6. Document Seedvault v0/v1 boundaries without overpromising (F036).

**Why next:** interop turns PhoneFork from a one-shot migrator into a reusable backup asset manager.

### Next: v1.0.0 - Signed, Accessible, Documented Release

1. Azure Artifact Signing or equivalent managed signing, with RFC 3161 timestamping (F064, F065).
2. GitHub Actions CI: restore, build, test, vulnerable-package scan, JSON lint, publish, sign, attest (F067, F068, F069).
3. Reproducible build flags, SourceLink, SLSA provenance, artifact verification (F066).
4. Inno Setup installer alongside framework-dependent ZIP (F071).
5. CONTRIBUTING.md, README screenshots, release notes, support matrix (F063, F072, F078, F107).
6. Pre-flight honesty screen, 2FA audit, banking/DRM warnings, Secure-Folder/Pass/Wallet/Routines detector, Knox/bootloader check, CSC pre-flight, Gallery/OneDrive notice, Galaxy AI posture probe (F037, F038, F039, F040, F043, F044, F099, F103).
7. ADB Burst Mode/compression toggle (F100); media integrity verification modes, USB stay-awake, offline reconciliation, throughput/ETA, running jobs, Windows toasts, retry/reconcile (F046, F047, F048, F049, F057, F058).
8. Per-package APK signature verification, `allowBackup`/data-extraction-rules parser, OBB/ext-data awareness, tracker/library scanner (F041, F042, F059, F060, F061).
9. SMS large-thread checkpointing, WhatsApp/Signal/Telegram handoff wizards, clear-destination toggle (F045, F051, F052).
10. Saved profiles + before/after PowerShell hooks + optional webhooks (F054, F055, F056).
11. WCAG 2.2 audit, UIA/Narrator smoke, focus-visible/target-size fixes (F075, F077).
12. Resource extraction and baseline localization: en-US, ko-KR, pt-BR (F076).
13. Catppuccin theme variants with accessible contrast (F074).
14. GitHub Discussions for profiles/debloat feedback (F073).
15. Velopack adoption decision when stable v1 ships; until then ZIP + Inno Setup only (F070).

**Why next:** v1 should be installable, signed, understandable, accessible, and verifiable by someone who did not build it locally. The 0-star/0-release current state means the v1 launch is also the discovery event; everything in the launch-readiness pass (F107) flows from this gate.

## Risk Register

| Risk | Probability | Impact | Mitigation |
|---|---:|---:|---|
| Wireless ADB CVE-2026-0073 exposure now has public PoC code | High | High | Patch gate (F001), USB default (F003), session timeout (F007), explicit "exploit code is public" warning on patch < 2026-05-01 (F105). |
| Helper APK permission behavior changes on Android 17 target SDK 37 | Medium | Medium | Start target SDK 36, add `ACCESS_LOCAL_NETWORK` and test Android 17 path before bump (F010, F012, F016). |
| Samsung One UI settings keys drift | High | Medium | Safe-key catalog (F079), per-device corpus (F078), fail-loud per-row errors. |
| Debloat dataset causes OEM breakage (e.g. One UI 8.5 `smartsuggestions` regression) | Medium | High | Disable-only default, rollback snapshots, upstream sync, hot-fix override surface (F102), conservative defaults, package breakage feed review. |
| Smart Switch UI automation breaks under MS Store packaging | High | Medium | Dual install detection (F024), version-specific locator tests, manual handoff fallback. |
| `.bk` parser bug on untrusted input | Medium | High | AppContainer parser process (F027), size limits, fuzz samples, no parser in WPF process. |
| AppManager/Open Android Backup format drift | Medium | Medium | Sample fixtures, versioned importers, strict schema and readable error. |
| Long media/SMS migrations fail mid-run | High | Medium | Job IDs, checkpoints (F045), replay, retry/offline reconciliation (F048). |
| Signing/certificate churn under CA/B 460-day rule | Medium | Medium | Managed signing (F064), timestamping (F065), documented renewal runbook. |
| Privacy trust loss from logs | Low | High | Hash serials (F006), local-only logs, redaction export command. |
| Android 17 system-level iPhone migration tool encroaches on F082 timeline | Medium | Medium | Reposition F082 as a Later item (F082, F101); make PhoneFork the post-OOBE residue tool, not a setup-wizard replacement. |
| Quick Share ↔ AirDrop interop reduces "transfer file" demand | Low | Low | Welcome it; F092 watcher recommends Quick Share for ad-hoc, PhoneFork owns batch + selective + audited. |
| Samsung Pass/Wallet/Gallery cloud transitions occur mid-migration | Medium | Medium | Pre-flight detectors (F040, F099, F108) with deep-links to Samsung's own migration flows. |
| Velopack v1.0 still not cut when v1 release nears | Medium | Low | Defer updater (F070); ship signed ZIP + Inno only (F064, F071). |
| Repo discovery stalls (0 stars, 0 releases as of 2026-05-16) | Medium | Medium | Launch-readiness pass (F107): tagged release, screenshots, CONTRIBUTING, Discussions, signed installer. |

## Source Appendix

### Local Repo Sources

- L01: `README.md`
- L02: `CHANGELOG.md`
- L03: `THIRD-PARTY-NOTICES.md`
- L04: `docs/competitor-research.md`
- L05: `docs/community-signal.md`
- L06: `docs/oss-references.md`
- L07: `docs/oss-dependencies.md`
- L08: `docs/migration-feasibility.md`
- L09: `docs/research-delta-2026-05-14.md`
- L10: `src/PhoneFork.Cli/Program.cs`
- L11: `src/PhoneFork.App/App.xaml.cs`
- L12: `src/PhoneFork.Core/PhoneFork.Core.csproj`
- L13: `src/PhoneFork.App/PhoneFork.App.csproj`
- L14: `src/PhoneFork.Cli/PhoneFork.Cli.csproj`
- L15: `tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj`

### External Sources

- S10: Android Security Bulletin, May 2026 — https://source.android.com/docs/security/bulletin/2026/2026-05-01
- S11: NVD CVE-2026-0073 — https://nvd.nist.gov/vuln/detail/CVE-2026-0073
- S12: Android local network permission — https://developer.android.com/privacy-and-security/local-network-permission
- S13: Android developer verification FAQ — https://developer.android.com/developer-verification/guides/faq
- S14: Android backup and D2D testing docs — https://developer.android.com/identity/data/testingbackup
- S15: Samsung Smart Switch official support page — https://www.samsung.com/us/support/owners/app/smart-switch
- S16: Samsung Community Smart Switch Microsoft Store update thread — https://eu.community.samsung.com/t5/computers-it/new-smart-switch-pc-software-update-system/m-p/12136709
- S17: Wondershare MobileTrans phone transfer — https://mobiletrans.wondershare.com/phone-to-phone-transfer.html
- S18: iMobie PhoneTrans buy/features — https://www.imobie.com/phonetrans/buy.htm
- S19: Syncios Data Transfer FAQ — https://www.syncios.com/support/syncios-data-transfer-faq.html
- S20: Apeaksoft Android Data Backup and Restore — https://www.apeaksoft.com/android-data-backup-and-restore/
- S21: Open Android Backup — https://github.com/mrrfv/open-android-backup
- S22: AppManager — https://github.com/MuntashirAkon/AppManager
- S23: AppManager issue 1974 APK verify — https://github.com/MuntashirAkon/AppManager/issues/1974
- S24: Neo Backup — https://github.com/NeoApplications/Neo-Backup
- S25: Seedvault — https://github.com/seedvault-app/seedvault
- S26: Shizuku — https://github.com/RikkaApps/Shizuku
- S27: scrcpy — https://github.com/Genymobile/scrcpy
- S28: UAD-NG — https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation
- S29: Canta — https://github.com/samolego/Canta
- S30: Migrate-OSS — https://github.com/BaltiApps/Migrate-OSS
- S31: ya-webadb — https://github.com/yume-chan/ya-webadb
- S32: Android Backup Extractor — https://github.com/nelenkov/android-backup-extractor
- S33: adbsms — https://github.com/gonodono/adbsms
- S34: Android SMS Gateway — https://github.com/capcom6/android-sms-gateway
- S35: LocalSend — https://github.com/localsend/localsend
- S36: ADB Explorer — https://github.com/Alex4SSB/ADB-Explorer
- S37: google/adb-sync — https://github.com/google/adb-sync
- S38: QtScrcpy — https://github.com/barry-ran/QtScrcpy
- S39: Shizuku API — https://github.com/RikkaApps/Shizuku-API
- S40: awesome-shizuku — https://github.com/timschneeb/awesome-shizuku
- S41: Canta website — https://samolego.github.io/Canta/
- S42: Awesome Android Root debloating guide — https://awesome-android-root.org/guides/android-apps-debloating
- S43: Android Auto Backup docs — https://developer.android.com/identity/data/autobackup
- S44: MobileTrans pricing — https://mobiletrans.wondershare.com/buy/pricing-for-individuals-windows.html
- S45: Apeaksoft store/pricing — https://www.apeaksoft.com/store/android-data-recovery/
- S46: Microsoft Artifact Signing FAQ — https://learn.microsoft.com/en-us/azure/trusted-signing/faq
- S47: Azure Artifact Signing pricing — https://azure.microsoft.com/en-us/pricing/details/artifact-signing/
- S48: CA/B Forum CSC-31 discussion — https://groups.google.com/a/groups.cabforum.org/d/msgid/cscwg-public/DS0PR14MB62161918BD2B73422EB3EEF49217A%40DS0PR14MB6216.namprd14.prod.outlook.com
- S49: Serilog releases — https://github.com/serilog/serilog/releases
- S50: QRCoder NuGet — https://www.nuget.org/packages/QRCoder/
- S51: Spectre.Console releases — https://github.com/spectreconsole/spectre.console/releases
- S52: JsonSchema.Net NuGet — https://www.nuget.org/packages/JsonSchema.Net/
- S53: WPF-UI releases — https://github.com/lepoco/wpfui/releases
- S54: MaterialDesignThemes NuGet — https://www.nuget.org/packages/MaterialDesignThemes/
- S55: Microsoft.Xaml.Behaviors.Wpf NuGet — https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Wpf/
- S56: .NET 10 announcement — https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/
- S57: WCAG 2.2 — https://www.w3.org/TR/WCAG22/
- S58: Avalonia — https://avaloniaui.net/
- S59: Syncthing BEP v1 — https://docs.syncthing.net/specs/bep-v1.html
- S60: Borgmatic hooks — https://torsion.org/borgmatic/docs/how-to/add-preparation-and-cleanup-steps-to-backups/
- S61: restic design — https://restic.readthedocs.io/en/stable/100_references.html#design
- S62: KDE Connect — https://invent.kde.org/network/kdeconnect-kde
- S63: Cellebrite Smart Switch forensic post — https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/
- S64: MOBILedit Smart Switch backup import — https://forensic.manuals.mobiledit.com/MM/samsung-smart-switch-backup
- S65: Hur, Lee, Cha 2021, forensic analysis of Samsung Smart Switch backup files — https://doi.org/10.1016/j.fsidi.2021.301172
- S66: Project Zero FORCEDENTRY sandbox escape — https://googleprojectzero.blogspot.com/2022/03/forcedentry-sandbox-escape.html
- S67: SWGDE mobile device evidence collection — https://swgde.org/documents/published-by-committee/mobile-devices/
- S68: Hacker News, Android backup discussion — https://news.ycombinator.com/item?id=42648597
- S69: Samsung Members, app data/logins transfer — https://r1.community.samsung.com/t5/samsung-smart-switch/transferring-app-data-and-logins/td-p/14255832
- S70: XDA Secure Folder not restoring with Smart Switch — https://xdaforums.com/t/secure-folder-not-restoring-by-smart-switch.4665109/
- S71: Reddit/SamsungGalaxy Smart Switch data complaints, 2026 — https://www.reddit.com/r/samsunggalaxy/comments/1rnwv3f/smart_switch/
- S72: Android Central, Android Show 2026 Quick Share/migration updates — https://www.androidcentral.com/apps-software/the-ios-android-file-sharing-nightmare-is-officially-over-for-more-android-users
- S73: NuGet vulnerability/outdated scan via `dotnet list package`, run locally 2026-05-16 against https://api.nuget.org/v3/index.json
- S74: Android app signing docs — https://developer.android.com/studio/publish/app-signing
- S75: FlaUI — https://github.com/FlaUI/FlaUI
- S76: GitHub artifact attestations — https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations
- S77: Velopack — https://github.com/velopack/velopack
- S78: Catppuccin — https://catppuccin.com/
- S79: Inno Setup — https://jrsoftware.org/isinfo.php
- S80: GitHub Actions — https://docs.github.com/en/actions
- S81: Android Debug Bridge docs — https://developer.android.com/tools/adb
- S82: SecurityWeek, Critical Remote Code Execution Vulnerability Patched in Android — https://www.securityweek.com/critical-remote-code-execution-vulnerability-patched-in-android-2/
- S83: Cybersecurity News, PoC Exploit Released for Android 0-Click Vulnerability — https://cybersecuritynews.com/poc-exploit-android-zero-click-vulnerability/
- S84: Dark Web Informer, CVE-2026-0073 Zero-Click RCE Flaw in Android Wireless ADB — https://darkwebinformer.com/cve-2026-0073-zero-click-rce-flaw-in-androids-wireless-adb-bypasses-authentication/
- S85: Smarttech247 Threat Intel, CVE-2026-0073 — https://www.smarttech247.com/threat-intel-reports/android-cve-2026-0073-wireless-adb-auth-flaw
- S86: Penligent.ai, Android adbd Zero-Click Shell — https://www.penligent.ai/hackinglabs/cve-2026-0073-android-adbd-zero-click-shell-through-wireless-adb/
- S87: Samsung Newsroom, One UI 8.5 official rollout — https://news.samsung.com/global/samsungs-one-ui-8-5-official-rollout-starts-may-6
- S88: SamMobile, 2026 Galaxy photo backup OneDrive change — https://www.sammobile.com/news/2026-bring-change-galaxy-phone-photo-backup-system/
- S89: Android Authority, Samsung Pass to Wallet migration reminder — https://www.androidauthority.com/samsung-pass-to-wallet-migration-reminder-3535393/
- S90: Samsung Newsroom, Galaxy AI Privacy Controls — https://news.samsung.com/global/your-privacy-secured-how-galaxy-ai-empowers-you-to-take-control-of-your-data
- S91: Microsoft Tech Community, SmartSwitch PC Updates Microsoft Store only — https://techcommunity.microsoft.com/discussions/windowsinsiderprogram/smartswitch-pc-updates-will-only-be-supported-through-the-ms-store-going-forward/4466517
- S92: Android Developers, SDK Platform Tools release notes — https://developer.android.com/tools/releases/platform-tools
- S93: UAD-NG issues — https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues
- S94: XDA Forums, OEM Unlock on Galaxy S26 series One UI 8.5 — https://xdaforums.com/t/oem-unlock-on-galaxy-s26-series-one-ui-8-5-is-it-still-possible.4781825/
- S95: Notebookcheck, Android 17 + AirDrop iPhone-to-Galaxy switching — https://www.notebookcheck.net/Android-17-AirDrop-to-make-switching-from-iPhone-17-Pro-to-Galaxy-S27-Ultra-Pixel-11-Pro-a-breeze.1296130.0.html
- S96: Samsung US Smart Switch transfer issues troubleshooting — https://www.samsung.com/us/support/troubleshoot/TSG10001511/
- S97: AppManager open issues 1970–1974 — https://github.com/MuntashirAkon/AppManager/issues
- S98: JsonSchema.Net 9.2.0 NuGet — https://www.nuget.org/packages/JsonSchema.Net
- S99: Android Developers Blog, Android 16 QPR2 release — https://android-developers.googleblog.com/2025/12/android-16-qpr2-is-released.html
- S100: Microsoft Learn, Azure Artifact Signing FAQ — https://learn.microsoft.com/en-us/azure/artifact-signing/faq
- S101: TheWCAG Accessibility Testing Guide 2026 — https://www.thewcag.com/testing-guide
- S102: Microsoft Phone Link sync across devices — https://www.microsoft.com/en-us/windows/sync-across-your-devices
- S103: AlternativeTo scrcpy 4.0 — https://alternativeto.net/news/2026/5/scrcpy-4-0-brings-sdl3-support-dynamic-aspect-ratio-enhanced-camera-controls-and-more/
- S104: Android Authority Galaxy S26 bloatware list — https://www.androidauthority.com/samsung-galaxy-s26-bloatware-apps-uninstall-free-up-storage-3655808/

## Self-Audit Ledger

- Security covered: CVE-2026-0073 patch gate (F001) + PoC-aware UX (F105), per-install ADB keys (F002), USB-first policy (F003), trusted-pair registry (F004), session timeout/kill switch (F007), NDJSON serial hashing (F006), helper signing (F020), sandbox parsers (F027), vulnerability CI (F068), managed signing (F064), timestamping (F065).
- Accessibility covered: WCAG 2.2 (F075), UIA/Narrator smoke (F077), theme variants with accessible contrast (F074), focus/target-size work in v1 release-track item 11.
- i18n/l10n covered: RESX extraction and en-US/ko-KR/pt-BR baseline (F076).
- Observability/telemetry covered: local NDJSON (existing), hashed device IDs (F006), no telemetry (X016), optional user-configured webhooks only (F056), provider-call audit events (F021), signed migration receipts (F085), UFDR-lite exports (F086).
- Testing covered: Core tests already exist; CI restore/build/test (F067), provider fixtures (F010 chain), parser sandbox tests (F027), vulnerable-package scans (F068), device corpus badges (F078), UIA smoke (F077).
- Docs covered: README screenshots (F063), CONTRIBUTING (F072), support matrix and launch-readiness pass (F107), compatibility notes, source appendix (this section).
- Distribution/packaging covered: signed ZIP, Inno installer (F071), provenance (F066), timestamping (F065), optional updater (F070), platform-tools refresh policy (F104), release/launch readiness (F107).
- Plugin ecosystem covered: OEM plugin model in v2+ after stable Core contracts (F084); in-app debloat editor under consideration (F095).
- Mobile covered: helper APK (F010), `app_process` JAR (F011), Shizuku (F012), Android 17 permission handling (F010 target SDK roadmap), Cross-Device Migration watcher (F101).
- Offline/resilience covered: checkpoints (F045), retry/replay (F048), retention (F033, F034), no-cloud default, USB stay-awake (F047).
- Multi-user/collab covered: saved profiles (F054), PowerShell hooks (F055), webhooks (F056), fleet/headless mode later (F087), GitHub Discussions (F073), local Kestrel API later (F088).
- Migration paths covered: Smart Switch (F024, F025, F026, F028), AppManager (F029, F030), Open Android Backup (F032), `.ab` (F031), Seedvault notes (F036), cross-platform metadata (F035), Android 17 system tool watcher (F101), iOS source bridge later (F082).
- Upgrade strategy covered: dependency policy (F069), CI scan (F068), signed releases (F064), platform-tools refresh policy (F104), updater decision gate (F070).
- Cloud transitions covered: Samsung Pass→Wallet (F108, F040), Samsung Gallery OneDrive removal (F099), Galaxy AI privacy posture (F103).
- Traceability checked: every Now/Next/Later/Under Consideration/Rejected item maps to local L## or external S## source IDs listed in the appendix.
- Duplicate check: Now/Next/Later/Under Consideration/Rejected are mutually exclusive in this document; F102 (Now hot-fix surface) and F095 (Under consideration in-app editor) cover distinct scopes; F082 (Later, iOS bridge) and F101 (Later, system-tool watcher) are paired but distinct.
- Adversarial review absorbed: a hostile reviewer would have flagged "Quick Share now does what PhoneFork does for files," "Android 17 ships iPhone migration officially," "One UI 8.5 removes root entirely," and "your CVE gate had no PoC at write time." All four are answered above with explicit Tier placement, source citations, and rejection-or-defer rationale (X017–X020, F092, F101, F105). The 0-star/0-release reality is handled by F107.
