# PhoneFork Roadmap

**Version 2026.05.16** - full repository reconciliation plus current external research. Supersedes 2026.05.14b. Working document: dense, source-backed, and implementation-oriented.

Every external claim and feature idea maps to the Source Appendix. Local repo claims map to current files, commit history, and GitHub metadata checked on 2026-05-16.

## Vision

PhoneFork is the Windows-host migration tool Samsung Smart Switch should have been for power users: two Samsung phones connected at once, local-only, no root, no cloud, no Samsung account, no direction lock, no silent category drops, and an auditable plan before anything changes. It complements Smart Switch for Knox/private-app-data cases that only Samsung can reach, but owns the visible, reversible, scriptable migration cockpit: apps, media, settings, Wi-Fi, default roles, debloat, reports, and backup-format interop.

## State Of The Repo

### Live Inventory

| Area | Current state |
|---|---|
| Repository | `SysAdminDoc/PhoneFork`, public, MIT, default branch `main`, no open GitHub issues, no tags, `main` aligned with `origin/main` at reconnaissance. |
| Version | README, CHANGELOG, XAML title, and app manifest show **v0.6.8**. Roadmap previously still said v0.6.5/v2026.05.14b. |
| Recent history | 15 commits total. Last commits: branding assets, 2026.05.14b roadmap refresh, ADB/path hardening, WPF polish, wireless ADB, Roles/Wi-Fi/Debloat/Settings/Media feature waves. |
| Stack | C# / .NET 10, WPF, MVVM, Spectre.Console.Cli, AdvancedSharpAdbClient, AlphaOmega.ApkReader, Serilog NDJSON, QRCoder, MaterialDesignThemes, HandyControl, xUnit Core tests. |
| Build system | `PhoneFork.slnx`; projects: `src/PhoneFork.Core`, `src/PhoneFork.App`, `src/PhoneFork.Cli`, `tests/PhoneFork.Core.Tests`. |
| Entry points | WPF: `src/PhoneFork.App/App.xaml.cs`; CLI: `src/PhoneFork.Cli/Program.cs`. |
| Runtime target | Windows 10/11 with .NET 10 Desktop Runtime; Android 11+ devices over USB ADB or Android 11+ Wireless Debugging. |
| Code size | 96 C#/XAML files under `src` + `tests`, excluding `bin/obj`; about 6,519 C#/XAML LOC. |
| Top code surfaces | `CatppuccinMocha.xaml`, `DebloatService`, `MediaSyncService`, `DeviceBarViewModel`, `DebloatViewModel`, `WifiViewModel`, `SettingsViewModel`, `AppsViewModel`, `RolesViewModel`, `AppInstallerService`. |
| Shipped features | Apps, Media, Settings, Debloat, Wi-Fi QR/CSC, Roles, Wireless ADB pair/connect/disconnect, DeviceBar pair UI, shell/path hardening, first-run empty states, dark title bar. |
| Source markers | Source/test/docs scan found no TODO/FIXME/HACK markers outside roadmap prose. Asset dataset text contains generic "stub" package descriptions only. |
| Tracked issues | GitHub issues list is empty for PhoneFork. |
| Dependency state | `dotnet list package --vulnerable --include-transitive`: no vulnerable packages. `dotnet list package --outdated --include-transitive`: QRCoder 1.8.0, Serilog 4.3.1, Serilog.Sinks.File 7.0.0, JsonSchema.Net 9.2.1, Spectre.Console 0.55.2, Microsoft.Xaml.Behaviors.Wpf 1.1.142, test-platform/coverlet/xUnit analyzer updates available. |

### What It Does Today

| Domain | Current implementation | Gap that remains |
|---|---|---|
| Apps | Enumerates `pm list packages -3 -f`, resolves split APK paths, pulls APKs, installs with Play attribution and install reason. | No AppManager-format export, APK signature enforcement, OBB/ext-data awareness, or app-data honesty report yet. |
| Media | Builds `/sdcard` manifests, diffs categories, pull-then-push sync with mtime preservation and conflict options. | Needs resumable checkpoints, integrity verification mode, retry/replay manifest, huge-file handling and ETA. |
| Settings | Diffs `secure/system/global`, applies selected keys with safety blocklist. | Needs Samsung safe-key catalog, pre-flight surface, and per-Android/One UI corpus. |
| Debloat | Embeds AppManagerNG/UAD-NG 5,481-entry dataset, disables only, writes rollback snapshots. | Needs upstream sync policy, package breakage telemetry-free reports, work-profile/multi-user handling, and per-device safety overrides. |
| Wi-Fi | Lists SSIDs when possible, renders QR PNG/SVG, displays CSC mismatch. | Needs Shizuku/helper path for PSK export/import and Android 17 local-network permission handling. |
| Roles | Snapshots/applies AOSP default roles and grants permissions/appops. | Needs notification listener/accessibility helper coverage and sequencing with app installs. |
| Wireless ADB | Pairs/connects/disconnects through bundled `adb.exe`; parses pairing QR payloads. | Needs per-install ADB key, USB-first policy, trusted-pair registry, patch-level gate for CVE-2026-0073, and mDNS reconnect UX. |
| Observability | Serilog CompactJsonFormatter writes NDJSON audit logs. | Raw serials should become hashed IDs; audit events need migration IDs, source/destination correlation, provider-call details, and share-safe export. |

### Hard Constraints

- **License:** MIT. GPL/AGPL projects are allowed as references or data-format compatibility targets, but no GPL code is copied into MIT binaries.
- **Platform:** Windows host first; WPF is intentional. Avalonia is a v2 portability path.
- **Android privilege ceiling:** no root required. Shell UID, ADB, app-process helper, and Shizuku are acceptable. Knox/system-signature-only data remains explicitly user-assisted or Smart Switch assisted.
- **Privacy posture:** zero telemetry, zero cloud dependency, local audit log only.
- **Trust posture:** USB default, wireless opt-in, no legacy `adb tcpip 5555`, no long-lived broad LAN exposure.
- **UI style:** Catppuccin Mocha by default; no fully rounded text-bearing backdrops; dense utility cockpit, not a marketing page.

## External Research Summary

### Direct OSS And Adjacent GitHub Landscape

Stars, pushed dates, and active committers are point-in-time values collected from GitHub on 2026-05-16. Active committers = unique commit authors in the last 90 days from the GitHub commits API sample.

| Project | Role for PhoneFork | Stars | Last push | Active 90d | Current signal |
|---|---:|---:|---|---:|---|
| Genymobile/scrcpy | app_process no-install server pattern, optional mirroring | 141,728 | 2026-05-12 | 9 | v4.0 shipped SDL3, adb 37.0.0, mDNS TCP detection. |
| LocalSend | local transfer UX, mDNS, discovery, receive-notify patterns | 81,353 | 2026-04-30 | 6 | Android OEM file-picker bugs and desktop startup issues show cross-platform edge costs. |
| barry-ran/QtScrcpy | GUI/device-control adjacent | 29,522 | 2026-04-03 | 2 | Confirms demand for desktop phone-control wrappers. |
| RikkaApps/Shizuku | shell-UID privileged API pattern | 25,125 | 2025-06-18 | 0 | Stable, high-trust dependency pattern; current release v13.6.0. |
| timschneeb/awesome-shizuku | awesome-list for Shizuku ecosystem | 8,838 | 2026-05-14 | 3 | Harvest source for Shizuku-powered utilities. |
| MuntashirAkon/AppManager | backup format, APK scanner, permissions/rules extras | 8,022 | 2026-04-16 | 3 | Open issues now push Shizuku/assistant privileged services and APK verification. |
| UAD-NG | debloat dataset and safety taxonomy | 6,604 | 2026-05-11 | 22 | Active package breakage/addition stream, including One UI 8.5 breakage reports. |
| samolego/Canta | on-device Shizuku debloat UX | 4,778 | 2026-05-12 | 1 | Presets, descriptions, work-profile requests, uncertain-result messaging. |
| capcom6/android-sms-gateway | Android SMS provider/API architecture | 4,384 | 2026-05-09 | 3 | Active SMS/MMS/API issues; useful for helper APK HTTP/API stretch. |
| NeoApplications/Neo-Backup | rooted backup UI, retention, scheduling, restore pain | 3,614 | 2026-05-03 | 20 | Recent issues cluster around scheduling, free-space checks, retention limits, progress status. |
| KieronQuinn/Smartspacer | Shizuku-adjacent system feature model | 3,333 | 2026-05-08 | 0 | Reference for non-root system integration but not migration. |
| yume-chan/ya-webadb | WebUSB ADB and browser fallback | 3,058 | 2026-05-15 | 2 | Open requests for Wireless Debugging TLS and sync compression. |
| nelenkov/android-backup-extractor | legacy `.ab` format import | 2,549 | 2026-05-12 | 0 | Mature reference for Android <= 11 `.ab` archives. |
| NetrisTV/ws-scrcpy | web scrcpy prototype | 2,412 | 2026-02-13 | 0 | Useful only if v2 web control appears. |
| RikkaApps/Shizuku-API | helper APK API dependency | 2,171 | 2025-05-29 | 0 | Stable API, low recent churn. |
| seedvault-app/seedvault | system backup transport, v1 format caution | 1,738 | 2026-04-29 | 4 | WebDAV, timeout, backend allow-list, v1 format divergence. |
| mrrfv/open-android-backup | open archive, companion app, hooks, 7-Zip export | 1,281 | 2026-04-24 | 3 | Strong archive/hook/interoperability lesson; no app private data. |
| google/adb-sync | ADB sync precedent | 1,094 | 2024-03-23 | 0 | Archived; confirms PhoneFork must own media sync. |
| Alex4SSB/ADB-Explorer | Windows ADB file manager UX | 850 | 2026-05-16 | 17 | Active Windows/C# adjacent; open issues on retry, >4GB, mtime retention, thumbnails. |
| BaltiApps/Migrate-OSS | root-required ROM migration | 251 | 2026-04-25 | 1 | Active but root-first; useful for sequencing, not no-root parity. |
| gonodono/adbsms | minimal ContentProvider over ADB | 1 | 2026-01-24 | 0 | Tiny but directly relevant pattern for SMS provider bridge. |

### Commercial And Closed Source Signals

| Vendor/tool | What matters for PhoneFork |
|---|---|
| Samsung Smart Switch | Official baseline; transfers major categories, offers PC/Mac/mobile/external-storage paths, now links Microsoft Store on Samsung support page, still exposes transfer failures and category omissions. |
| Wondershare MobileTrans | Commercially paywalls app/in-app-data claims, WhatsApp modules, iCloud-to-Android, selective transfer, speed/ETA language. |
| iMobie PhoneTrans | Sells clone/merge/custom migration and old phone/backup/iCloud/Google/iTunes input choices. Merge mode is a concrete v2 opportunity. |
| Syncios Data Transfer | Has a "clear selected destination data before copy" option and an explicit failure/troubleshooting corpus. |
| Apeaksoft Android Data Backup & Restore | Markets encrypted backup, preview, selective restore, 8,000+ device support, and one-click restore. |
| MOBILedit Forensic | Imports mobile-created Smart Switch `.bk` backups, proving selective Smart Switch backup inspection is commercially valuable. |

### Material Deltas Since 2026.05.14b

1. **Wireless ADB now has a fresh critical security constraint.** Android's May 2026 bulletin lists CVE-2026-0073 in `adbd`, a critical adjacent-network RCE as shell user affecting Android 14/15/16/16-qpr2. PhoneFork's wireless mode must check patch level and default to USB when a device is older than `2026-05-01`.
2. **Android 17 local-network restrictions are clearer.** Apps targeting SDK 37 need `ACCESS_LOCAL_NETWORK` for broad local network access; Android 16 has an opt-in compatibility gate. Helper APK planning should use target SDK 36 initially, then add permission/rationale before target SDK 37.
3. **Android developer verification is not a blocker for ADB-installed helper APKs.** The official FAQ says ADB installs do not require verification and are not subject to advanced-flow waiting periods.
4. **Open Android Backup is a stronger direct competitor than the previous roadmap credited.** It ships open 7-Zip archives, encryption, hooks, companion app, wireless mode, and Windows PowerShell entry, making "readable backup archive" a parity item.
5. **Dependency drift is larger than the previous delta listed.** `dotnet list package --outdated` shows QRCoder 1.8.0, JsonSchema.Net 9.2.1, Serilog.Sinks.File 7.0.0, Microsoft.Xaml.Behaviors.Wpf 1.1.142, and test-platform updates in addition to Serilog/Spectre.

## Feature Harvest And Prioritization Matrix

Scale: Impact/Effort/Risk = 1 low to 5 high. Prevalence: T = table stakes, C = common, R = rare but interesting. Tier sentence is the placement justification.

### Now

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F001 | Wireless ADB patch-level gate | S10,S11,S26 | Android bulletin, Shizuku | security | T | Strong | 5/2/2 | DeviceService patch parser | Parity | Now: wireless ADB is shipped and must refuse/high-warn devices below 2026-05-01 before more wireless features land. |
| F002 | Per-install ADB RSA key | S81,S26,S39 | Shizuku, ADB docs | security | C | Strong | 5/3/2 | AdbHostService env injection | Parity | Now: avoids reusing the user's global adb key and narrows compromise blast radius. |
| F003 | USB-first wireless opt-in policy | S10,S11,S26 | ADB security, Shizuku | security | C | Strong | 5/2/1 | F001 | Parity | Now: preserves PhoneFork's trust posture after CVE-2026-0073. |
| F004 | Trusted-pair registry with hashed serials | L04,S10,S26 | AnyDesk/TeamViewer pattern, ADB | security, observability | C | Strong | 4/3/2 | Local app data store | Parity | Now: lets the app remember safe devices without logging raw hardware IDs. |
| F005 | `adb mdns services` reconnect surface | L08,S27,S31 | scrcpy, ya-webadb | reliability | C | Strong | 3/2/2 | F001-F004 | Parity | Now: bundled adb already exposes the data; UI surfacing is cheap. |
| F006 | NDJSON serial hashing | L01,L02,L07 | audit-log practice | observability, privacy | C | Strong | 4/2/1 | F004 | Parity | Now: audit logs are shareable only if device identifiers are redacted. |
| F007 | Wireless session timeout and kill switch | S10,S11,S26 | ADB security | security, UX | C | Strong | 4/2/1 | DeviceBar state | Parity | Now: closes the "left Wireless Debugging exposed" failure mode. |
| F008 | Device developer-verification posture note | S13 | Android developer verification | docs, distribution | R | Strong | 3/1/1 | README/roadmap note | Parity | Now: users should know ADB-installed helper APKs remain allowed. |
| F009 | Dependency update batch | S49,S50,S51,S52,S55 | NuGet, dotnet list | security, reliability | T | Strong | 3/2/2 | Build smoke | Parity | Now: safe patch/minor upgrades reduce drift before helper work adds more moving parts. |
| F010 | Helper APK Gradle scaffold | L06,L07,S26,S33 | Shizuku, adbsms, Open Android Backup | mobile, platform | C | Strong | 5/4/3 | Android SDK/JDK 21 | Leapfrog | Now: unlocks SMS/calllog/contacts/Wi-Fi/wallpaper categories ADB shell cannot fully own. |
| F011 | `app_process` push-and-run JAR | L06,S27 | scrcpy | mobile, reliability | C | Strong | 5/4/3 | F010 shared protocol | Leapfrog | Now: gives read-side coverage without leaving an installed app behind. |
| F012 | Shizuku detect/start/runbook | S26,S39,S40,S41 | Shizuku ecosystem | mobile, UX | C | Strong | 4/3/3 | F010 | Parity | Now: Wi-Fi PSK and privileged reads depend on a predictable shell-UID path. |
| F013 | SMS export/import provider | S33,S34,S64 | adbsms, SMS Gateway, MOBILedit | data, mobile | C | Strong | 5/5/4 | F010, default-SMS role choreography | Leapfrog | Now: message failures dominate community complaints; provider bridge is the least unrealistic no-root path. |
| F014 | Call-log provider | S33,S34,S64 | adbsms, SMS Gateway | data, mobile | C | Strong | 4/4/3 | F010 | Parity | Now: same companion architecture as SMS with lower role friction. |
| F015 | Contacts provider with vCard export | S20,S21,S64 | Apeaksoft, Open Android Backup | data, migration | T | Strong | 4/4/3 | F010 | Parity | Now: contacts are a core category and should also produce open export files. |
| F016 | Wi-Fi PSK export/import via Shizuku | L05,L06,S12,S26 | Shizuku, Canta, community | data, platform | C | Strong | 5/5/4 | F010-F012 | Leapfrog | Now: Wi-Fi password loss is a repeated pain point and current v0.5 only has QR fallback. |
| F017 | Wallpaper/ringtone/notification setter | L06,S15,S74 | WallpaperUnbricker, Smart Switch | UX, platform | C | Strong | 3/3/2 | F010 | Parity | Now: restores visible personalization Smart Switch users notice. |
| F018 | Keyboard dictionary/user-dictionary export | L06,S65 | Smart Switch category taxonomy | data, platform | R | Medium | 3/4/4 | F010 | Leapfrog | Now: include in helper protocol design even if implementation follows SMS/contacts. |
| F019 | Helper self-uninstall and residue check | L06,S21 | scrcpy, Open Android Backup | trust, mobile | C | Strong | 4/2/2 | F010 | Parity | Now: "leave the phone clean" is central to local-first trust. |
| F020 | Helper APK signing and `apksigner verify` | L07,S13,S74 | Android signing, developer verification | security, distribution | T | Strong | 4/3/2 | F010, keystore | Parity | Now: required before embedding helper artifacts in the Windows app. |
| F021 | Provider-call audit events | L01,L06,S33,S67 | forensic tools, adbsms | observability | C | Strong | 4/3/2 | F010 | Leapfrog | Now: every helper read/write should be explainable in one NDJSON stream. |
| F022 | Backup/D2D capability probe | S14 | Android backup testing docs | honesty, data | C | Strong | 4/2/1 | App inventory | Parity | Now: tells users what Android's own D2D would or would not handle. |
| F023 | Open archive export sketch | S21 | Open Android Backup | data, docs | C | Strong | 3/2/1 | F015 | Parity | Now: design the helper output so VCF/CSV/JSON/7z export is not retrofitted later. |

### Next

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F024 | Smart Switch legacy/MS Store detection | S15,S16 | Samsung support/community | integrations | C | Strong | 4/3/2 | none | Parity | Next: required before any Smart Switch handoff is reliable on current Windows installs. |
| F025 | FlaUI Smart Switch guided handoff | S15,S63,S75 | Smart Switch, FlaUI | integrations, UX | R | Medium | 4/5/4 | F024 | Leapfrog | Next: covers categories PhoneFork cannot legally/technically reach, while preserving audit trail. |
| F026 | Smart Switch mobile `.bk` import | S64,S65 | MOBILedit, Hur 2021 | migration, data | R | Strong | 5/5/5 | F028 | Leapfrog | Next: commercial forensic tools prove value; parser risk demands sandboxing. |
| F027 | AppContainer parser process | S66 | Project Zero parser lessons | security | T | Strong | 5/4/3 | F026 | Parity | Next: untrusted backup parsing must not run in the main WPF process. |
| F028 | Smart Switch cache opportunistic reader | S63,S64,S65 | forensic references | data | R | Medium | 3/5/4 | F024 | Leapfrog | Next: useful if present but brittle, so follow the automation foundation. |
| F029 | AppManager-format backup writer | S22,S23 | AppManager | interop, data | C | Strong | 5/4/3 | F010, F020 | Leapfrog | Next: two-way AppManager compatibility is the strongest OSS credibility play. |
| F030 | AppManager-format backup reader | S22,S23 | AppManager | migration | C | Strong | 5/4/3 | F029 samples | Leapfrog | Next: lets users migrate existing backup assets into PhoneFork plans. |
| F031 | Legacy `.ab` import | S32,S14 | android-backup-extractor, Android docs | migration | C | Medium | 3/3/3 | F027 | Parity | Next: useful for Android <= 11 archives but not central to modern Samsung devices. |
| F032 | Open Android Backup archive bridge | S21 | Open Android Backup | interop, data | C | Strong | 4/4/3 | archive spec | Leapfrog | Next: open 7-Zip archive compatibility fills the "readable backup" parity gap. |
| F033 | Snapshot retention count | S24,S25 | Neo Backup, Seedvault | data, reliability | T | Strong | 4/2/2 | backup metadata store | Parity | Next: retention bugs are recurring in backup projects and easy to design early. |
| F034 | Retention by days and size | S24,S25 | Neo Backup, Seedvault | data, reliability | T | Strong | 4/2/2 | F033 | Parity | Next: prevents runaway local storage after repeated migrations. |
| F035 | Android `<cross-platform-transfer>` metadata | S43 | Android Auto Backup | migration | R | Medium | 3/2/2 | F029 | Parity | Next: cheap metadata that keeps backup manifests aligned with Android 16+ semantics. |
| F036 | Seedvault v0/v1 compatibility note and watcher | S25,S61 | Seedvault, restic | migration, docs | R | Medium | 2/2/3 | F029 | Parity | Next: prevents false claims while keeping a future parser path visible. |
| F037 | Single pre-flight honesty screen | L05,S15,S71 | Smart Switch complaints | UX, reliability | T | Strong | 5/3/2 | Source/dest scan aggregation | Leapfrog | Next: users need the "what will not transfer" report before wiping source. |
| F038 | 2FA/authenticator audit | L05,S13,S71 | community | security, UX | C | Strong | 5/3/2 | App inventory | Leapfrog | Next: one of the highest-impact trust warnings. |
| F039 | Banking/DRM re-auth warning | L05,S43 | community, Android backup docs | honesty | C | Strong | 5/2/1 | App inventory | Parity | Next: reduces false expectations without risky extraction attempts. |
| F040 | Secure Folder/Pass/Wallet/Routines detector | L05,S15,S70 | Samsung support, community | honesty, platform | C | Strong | 5/3/2 | Samsung package/key probes | Parity | Next: these are known Samsung exception categories. |
| F041 | `allowBackup` and data-extraction-rules parser | S14,S43 | Android docs | honesty, data | C | Strong | 4/3/2 | AlphaOmega manifest read | Parity | Next: app inventory can already parse manifests; expose the result. |
| F042 | Play Integrity/signature-sensitive app report | S13,S43 | Android verification | security, honesty | R | Medium | 3/4/3 | APK signature read | Leapfrog | Next: valuable for banking/DRM categories after basic pre-flight lands. |
| F043 | Global CSC/locale/region pre-flight | L02,L05,S15 | shipped Wi-Fi tab, community | platform, UX | C | Strong | 4/2/1 | Existing CscDiffService | Parity | Next: move shipped signal from Wi-Fi tab to whole-migration gate. |
| F044 | Knox/bootloader/warranty-bit check | S15,S65 | Knox/forensics | security, platform | C | Strong | 4/3/2 | getprop probes | Parity | Next: cheap warnings that explain why some data will remain inaccessible. |
| F045 | SMS large-thread checkpointing | L05,S24 | community, Neo Backup | reliability, data | C | Strong | 5/4/3 | F013 | Leapfrog | Next: 50k+ message threads are a known failure class. |
| F046 | Media integrity verification modes | S36,S37,S59 | ADB Explorer, adb-sync, Syncthing | reliability | T | Strong | 4/3/2 | MediaSyncService | Parity | Next: file copy without checksum/retry will be challenged by large libraries. |
| F047 | USB stay-awake and role hold | S36,S81 | ADB Explorer, Android ADB practice | reliability | C | Strong | 4/2/2 | Device session lifecycle | Parity | Next: prevents mid-migration disconnects. |
| F048 | Offline/retry reconciliation | S24,S36 | Neo Backup, ADB Explorer | reliability | T | Strong | 5/4/3 | migration IDs | Parity | Next: long transfers need replay rather than restart. |
| F049 | Throughput/ETA and per-pipe progress | S17,S19,S36 | MobileTrans, Syncios, ADB Explorer | UX, performance | T | Strong | 4/3/1 | F048 | Parity | Next: paid tools sell speed; PhoneFork should measure it honestly. |
| F050 | VCF/CSV/HTML/PDF category exports | S20,S21,S34 | Apeaksoft, Open Android Backup, SMS tools | data, docs | C | Strong | 4/3/2 | F013-F015 | Leapfrog | Next: open deliverables beat opaque commercial backups. |
| F051 | WhatsApp/Signal/Telegram handoff wizards | L05,S17,S18,S71 | MobileTrans, community | integrations | C | Medium | 5/5/4 | pre-flight package mapping | Parity | Next: do not break sandbox rules; guide official app exports/imports. |
| F052 | Clear selected destination category before copy | S19 | Syncios | migration, UX | C | Strong | 4/3/3 | rollback/audit | Parity | Next: useful for contacts/messages/media duplicates but must be auditable. |
| F053 | Verified migration then source-clean checklist | S17,S19 | Dr.Fone/Syncios patterns | UX, security | C | Medium | 3/3/4 | F046, F050 | Parity | Next: only a checklist/deep link first; destructive automation waits. |
| F054 | Saved migration profiles | S15,S22,S60 | Knox/AppManager/Borgmatic | reusability | C | Strong | 4/3/2 | plan schema | Leapfrog | Next: repeat phone provisioning is a sysadmin use case. |
| F055 | Before/after PowerShell hooks | S21,S60 | Open Android Backup, Borgmatic | dev-experience | C | Strong | 3/3/3 | F054 | Parity | Next: powerful for advanced users; disabled unless explicitly enabled. |
| F056 | Healthchecks/webhook on completion | S24,S60 | Neo Backup, Borgmatic | observability | C | Medium | 3/3/3 | F054 | Parity | Next: optional local-to-user endpoint, no default telemetry. |
| F057 | Windows toast on completion/failure | S35 | LocalSend issues/features | UX | T | Strong | 3/2/1 | job status service | Parity | Next: long-running migrations need OS-level completion feedback. |
| F058 | Running jobs panel | S24,S35 | Neo Backup, LocalSend | UX, observability | T | Strong | 4/3/2 | migration job model | Parity | Next: multiple tabs already imply queued work. |
| F059 | Tracker/native-library APK scanner | S22 | AppManager | security, data | C | Medium | 3/4/3 | APK parser/signature path | Leapfrog | Next: useful as a post-install report after backup interop. |
| F060 | Per-package APK signature verification | S22,S23,S74 | AppManager issue, Android signing | security | T | Strong | 5/3/2 | APK parser/apksigner | Parity | Next: prevents accidental downgrade/tamper during app migration. |
| F061 | OBB/ext-data awareness | S21,S22,S36 | Open Android Backup, AppManager, ADB Explorer | data | C | Strong | 4/3/2 | app backup layout | Parity | Next: game/media-heavy apps need this even without private data. |
| F062 | Android storage virtual-disk/mount view | S36 | ADB Explorer | UX, data | R | Medium | 2/5/4 | media sync stable | Leapfrog | Next: defer until core transfer reliability is mature. |
| F063 | README screenshots and visual setup docs | L01,S15 | Samsung support, repo docs | docs, UX | T | Strong | 3/2/1 | stable UI | Parity | Next: releases need user-trust visuals. |
| F064 | Azure Artifact Signing / managed signing | S46,S47,S48 | Microsoft, CA/B | distribution, security | T | Strong | 5/4/2 | release workflow | Parity | Next: Windows utility trust hinges on signed artifacts. |
| F065 | RFC 3161 timestamping policy | S46,S48 | Microsoft, CA/B | distribution, security | T | Strong | 5/2/1 | F064 | Parity | Next: required to keep signatures valid after cert expiry. |
| F066 | Reproducible build + provenance | S46,S76 | Sigstore/SLSA/GitHub | security, distribution | C | Strong | 4/3/2 | CI | Parity | Next: public repo can emit provenance cheaply. |
| F067 | GitHub Actions CI and release artifact flow | L01,L07,S80 | repo stack, NuGet | testing, distribution | T | Strong | 5/3/2 | restore/build/test stable | Parity | Next: no tagged release exists; CI is a v1 trust gate. |
| F068 | Vulnerability scan in CI | S10,S73 | Android bulletin, NuGet | security, testing | T | Strong | 5/2/1 | F067 | Parity | Next: local scan is clean; keep it automated. |
| F069 | Dependency update policy | S49-S56,S73 | NuGet, GitHub releases | maintenance | T | Strong | 3/2/2 | F067 | Parity | Next: .NET/Android dependencies are moving quickly. |
| F070 | Velopack or release-poll updater | L07,S77 | Velopack, commercial tools | distribution | C | Medium | 3/4/3 | signing/release | Parity | Next: wait until signed releases exist; pin if adopted. |
| F071 | Inno Setup installer alongside ZIP | L01,S46,S79 | Windows app norms | distribution | C | Strong | 3/3/2 | F064 | Parity | Next: ZIP is fine for power users; installer improves trust. |
| F072 | CONTRIBUTING.md | L01,S22,S28 | OSS norms | docs, dev-experience | T | Strong | 3/1/1 | CI commands | Parity | Next: needed before community issue intake. |
| F073 | GitHub Discussions enablement | S22,S28,S40 | AppManager/UAD/Shizuku ecosystems | community | C | Strong | 3/1/1 | F072 | Parity | Next: debloat profile feedback should not land as random bug reports. |
| F074 | Catppuccin Latte/Frappe/Macchiato themes | L07,S78 | Catppuccin, repo UI | accessibility, UX | C | Strong | 3/3/2 | theme dictionary split | Parity | Next: light/high-contrast variants improve repeat-use ergonomics. |
| F075 | WCAG 2.2 audit | S57 | WCAG | accessibility | T | Strong | 5/3/2 | stable UI controls | Parity | Next: device cockpit must be keyboard/Narrator usable. |
| F076 | i18n scaffolding en-US/ko-KR/pt-BR | S15,S56 | Samsung markets, .NET | i18n | C | Strong | 3/4/2 | Resources.resx extraction | Parity | Next: string extraction gets more expensive later. |
| F077 | UIA/Narrator smoke tests | S57 | WCAG/WPF | accessibility, testing | C | Strong | 4/4/2 | F075 | Parity | Next: validates the dense DataGrid UI for assistive tech. |
| F078 | Device profile/corpus badges | S17,S18,S20 | commercial device-count claims | testing, docs | C | Strong | 3/3/2 | test device metadata | Parity | Next: honest "tested on" beats inflated 8,000-device marketing. |
| F079 | Samsung safe-settings catalog | L06,S65 | Hur 2021, settings tools | data, reliability | R | Strong | 4/4/3 | SettingsSnapshotService | Leapfrog | Next: curated safe-list becomes a community moat. |

### Later

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F080 | Avalonia 12 host port | S58 | Avalonia | platform/OS | R | Medium | 3/5/4 | v1 Windows stable | Parity | Later: host OS reach matters after Windows flow proves durable. |
| F081 | WebUSB/browser fallback | S31 | ya-webadb | platform/OS | R | Medium | 3/5/4 | v2 architecture | Leapfrog | Later: useful for non-Windows hosts, but WebUSB/ADB auth is a separate product surface. |
| F082 | iOS source bridge | S18,S72 | PhoneTrans, Google I/O signal | migration, mobile | C | Medium | 4/5/4 | backup schema | Parity | Later: strategic but outside the Samsung-to-Samsung core. |
| F083 | Multi-source consolidation | S18 | PhoneTrans merge mode | multi-user, migration | C | Medium | 4/5/4 | contacts/messages dedupe | Parity | Later: merge mode is paid-tool parity after single-source reliability. |
| F084 | OEM plugin model | S62 | KDE Connect plugins | plugin ecosystem | R | Strong | 4/5/4 | stable Core contracts | Leapfrog | Later: lets Pixel/OnePlus/Xiaomi modules grow without bloating Samsung core. |
| F085 | Signed migration manifest receipts | S65,S67 | forensics | observability, security | R | Strong | 4/4/3 | stable manifest schema | Leapfrog | Later: high-value for repair shops and legal workflows. |
| F086 | UFDR-lite JSON/CSV exports | S64,S67 | forensic tools | data, docs | R | Medium | 3/4/3 | F085 | Leapfrog | Later: useful once category extraction is broad enough. |
| F087 | Headless/fleet mode | S15,S60 | Knox, Borgmatic | dev-experience, multi-user | C | Strong | 4/4/4 | F054, F067 | Leapfrog | Later: aligns with sysadmin provisioning but needs robust failure handling. |
| F088 | Local Kestrel API | S34,S35 | SMS Gateway, LocalSend | integrations | R | Medium | 3/5/4 | F087 | Leapfrog | Later: API mode is powerful but expands attack surface. |
| F089 | Notification mirroring while migrating | S35 | AirDroid/LocalSend adjacent | UX | R | Medium | 2/4/3 | helper APK | Parity | Later: convenient but not core migration. |
| F090 | SMS-from-PC gateway | S34 | android-sms-gateway | integrations | R | Medium | 2/4/4 | F013 | Parity | Later: adjacent utility, not migration-critical. |
| F091 | Printable SMS thread PDF | S34,S64 | Droid Transfer, forensic tools | docs, data | R | Medium | 3/4/3 | F013, F050 | Parity | Later: strong for legal/archive users after SMS import/export works. |
| F092 | Quick Share/AirDrop watcher | S72 | Android Show 2026 signal | integrations | R | Low | 2/5/4 | platform APIs | Parity | Later: file sharing is adjacent and increasingly covered by OS vendors. |

### Under Consideration

| ID | Feature | Sources | Seen in | Category | Prev | Fit | I/E/R | Dependencies | Novelty | Tier and justification |
|---|---|---|---|---|---:|---|---|---|---|---|
| F093 | LAN discovery grid | S31,S35,S62 | ya-webadb, LocalSend, KDE Connect | UX, platform | C | Medium | 3/4/4 | F001-F007 | Parity | Under consideration: contradicts USB-first trust unless explicitly gated. |
| F094 | Live device dashboard cards | S36,S38 | ADB Explorer, QtScrcpy | UX | C | Medium | 2/3/3 | DeviceService expansion | Parity | Under consideration: useful but can distract from migration outcomes. |
| F095 | In-app debloat list editor | S28,S29,S42 | UAD-NG, Canta | data, plugin ecosystem | C | Medium | 3/4/3 | upstream sync policy | Parity | Under consideration: upstream PRs may be better than a parallel editor. |
| F096 | Ringtone preview over ADB/audio cast | S38 | scrcpy/QtScrcpy adjacent | UX | R | Low | 2/3/2 | media tone migration | Parity | Under consideration: cheap, but niche. |
| F097 | OpenCLI/help schema dump | S51 | Spectre.Console | dev-experience | R | Strong | 2/1/1 | Spectre update | Parity | Under consideration: low-cost after CLI stabilizes. |
| F098 | CRC32 fast verification option | S36,S59 | ADB Explorer, Syncthing | performance, reliability | C | Medium | 3/2/2 | F046 | Parity | Under consideration: SHA-256 should be default for trust-sensitive modes. |

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
| X008 | Samsung Themes/Samsung Wallet token transfer | L05,S15,S70 | platform | Account/Knox/keystore-bound data belongs in an honesty checklist, not an extraction feature. |
| X009 | Knox SDK consumer integration | S15,S65 | platform | Knox controls require enterprise roles and do not fit consumer no-root operation. |
| X010 | Frida/runtime key dumping | S11,S67 | security | Root/hooking path is not aligned with a trustworthy no-root migration utility. |
| X011 | Rewrite host as WinUI 3 or MAUI before v1 | L07,S58 | platform | WPF already ships and is the right Windows-host choice; cross-platform belongs after v1. |
| X012 | EV certificate as the primary signing strategy | S46,S48 | distribution | Managed Artifact Signing plus timestamping is a better operational fit than hardware-token EV cert renewal. |
| X013 | Seedvault v1 parser in v0.9 | S25,S61 | migration | The Restic-inspired v1 format is divergent; track after AppManager/Open Android Backup bridges. |
| X014 | Reverse engineer Samsung AOAP | L06,S15,S65 | integrations | High maintenance and low differentiation because PhoneFork's precondition is user-authorized ADB. |
| X015 | Runtime download/install of core dependencies | L07,S21 | distribution | Bundled tools and documented prerequisites are more trustworthy than surprise downloads. |
| X016 | Telemetry/cloud analytics | L01,L05,L06 | observability | Violates privacy posture; use local NDJSON and optional user-configured webhooks only. |

## Release Tracks

### Now: v0.6.9 - Trust And Maintenance Gate

1. Wireless ADB patch-level gate for CVE-2026-0073.
2. Per-install ADB key directory and `ADB_VENDOR_KEYS` wiring.
3. USB-first wireless opt-in and timeout/kill switch.
4. Trusted-pair registry with hashed serials and raw-serial-free NDJSON.
5. `adb mdns services` reconnect view for trusted devices.
6. Dependency patch batch: Serilog 4.3.1, Spectre.Console 0.55.2, QRCoder 1.8.0, Microsoft.Xaml.Behaviors.Wpf 1.1.142; evaluate JsonSchema.Net 9.x and Serilog.Sinks.File 7.x behind tests.
7. README/ROADMAP note that Android developer verification does not block ADB-installed helper builds.

**Why now:** wireless support already shipped, and the May 2026 Android bulletin changes the risk model. Maintenance drift is also cheapest before helper APK work starts.

### Now/Next: v0.7.0 - Helper Companion APK And JAR

1. `helper-apk/` Kotlin/Gradle scaffold, target SDK 36 initially.
2. Signed `PhoneForkHelper.apk` with provider authorities for SMS, call log, contacts, Wi-Fi, wallpaper, tones, and user dictionary.
3. `phonefork-agent.jar` push-and-run path using the scrcpy `CLASSPATH=... app_process / ...` pattern for read-side operations.
4. Shizuku detect/start guidance and helper binding for privileged Wi-Fi PSK reads.
5. Helper install/uninstall/query lifecycle through `HelperAppService`.
6. Provider-call audit events and self-uninstall residue check.
7. CI smoke for `apksigner verify --print-certs`.

**Why now:** this is the pivot from "ADB shell can do it" to "honest no-root coverage expansion." It unlocks the highest-value missing categories without lying about `/data/data`.

### Next: v0.8.0 - Smart Switch Interop

1. Detect legacy MSI and Microsoft Store Smart Switch footprints.
2. Drive Smart Switch via FlaUI only for categories PhoneFork cannot reach.
3. Inspect existing Smart Switch backup folders and mobile-created `.bk` files.
4. Run `.bk` parsing in an AppContainer-restricted child process.
5. Import readable categories into PhoneFork's selective apply views.

**Why next:** Smart Switch remains the only path for some Samsung/Knox categories. PhoneFork should orchestrate it honestly rather than pretend to replace it.

### Next: v0.9.0 - Backup Interop

1. Write and read AppManager-compatible backups.
2. Add `.ab` legacy import for Android <= 11 archives.
3. Add Open Android Backup archive bridge for 7-Zip/open export compatibility.
4. Implement snapshot retention by count, days, and size.
5. Emit Android cross-platform-transfer metadata where it maps cleanly.
6. Document Seedvault v0/v1 boundaries without overpromising.

**Why next:** interop turns PhoneFork from a one-shot migrator into a reusable backup asset manager.

### Next: v1.0.0 - Signed, Accessible, Documented Release

1. Azure Artifact Signing or equivalent managed signing, with RFC 3161 timestamping.
2. GitHub Actions CI: restore, build, test, vulnerable-package scan, JSON lint, publish, sign, attest.
3. Reproducible build flags, SourceLink, SLSA provenance, artifact verification.
4. Inno Setup installer alongside framework-dependent ZIP.
5. CONTRIBUTING.md, README screenshots, release notes, support matrix.
6. WCAG 2.2 audit, UIA/Narrator smoke, focus-visible/target-size fixes.
7. Resource extraction and baseline localization: en-US, ko-KR, pt-BR.
8. Catppuccin theme variants with accessible contrast.
9. GitHub Discussions for profiles/debloat feedback.

**Why next:** v1 should be installable, signed, understandable, accessible, and verifiable by someone who did not build it locally.

## Risk Register

| Risk | Probability | Impact | Mitigation |
|---|---:|---:|---|
| Wireless ADB CVE-2026-0073 exposed on unpatched devices | Medium | High | Patch gate, USB default, session timeout, clear warning on patch < 2026-05-01. |
| Helper APK permission behavior changes on Android 17 target SDK 37 | Medium | Medium | Start target SDK 36, add `ACCESS_LOCAL_NETWORK` and test Android 17 path before bump. |
| Samsung One UI settings keys drift | High | Medium | Safe-key catalog, per-device corpus, fail-loud per-row errors. |
| Debloat dataset causes OEM breakage | Medium | High | Disable-only default, rollback snapshots, upstream sync, conservative defaults, package breakage feed review. |
| Smart Switch UI automation breaks | High | Medium | Dual install detection, version-specific locator tests, manual handoff fallback. |
| `.bk` parser bug on untrusted input | Medium | High | AppContainer parser process, size limits, fuzz samples, no parser in WPF process. |
| AppManager/Open Android Backup format drift | Medium | Medium | Sample fixtures, versioned importers, strict schema and readable error. |
| Long media/SMS migrations fail mid-run | High | Medium | Job IDs, checkpoints, replay, retry/offline reconciliation. |
| Signing/certificate churn | Medium | Medium | Managed signing, timestamping, documented renewal runbook. |
| Privacy trust loss from logs | Low | High | Hash serials, local-only logs, redaction export command. |

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

- S10: Android Security Bulletin, May 2026 - https://source.android.com/docs/security/bulletin/2026/2026-05-01
- S11: NVD CVE-2026-0073 - https://nvd.nist.gov/vuln/detail/CVE-2026-0073
- S12: Android local network permission - https://developer.android.com/privacy-and-security/local-network-permission
- S13: Android developer verification FAQ - https://developer.android.com/developer-verification/guides/faq
- S14: Android backup and D2D testing docs - https://developer.android.com/identity/data/testingbackup
- S15: Samsung Smart Switch official support page - https://www.samsung.com/us/support/owners/app/smart-switch
- S16: Samsung Community Smart Switch Microsoft Store update thread - https://eu.community.samsung.com/t5/computers-it/new-smart-switch-pc-software-update-system/m-p/12136709
- S17: Wondershare MobileTrans phone transfer - https://mobiletrans.wondershare.com/phone-to-phone-transfer.html
- S18: iMobie PhoneTrans buy/features - https://www.imobie.com/phonetrans/buy.htm
- S19: Syncios Data Transfer FAQ - https://www.syncios.com/support/syncios-data-transfer-faq.html
- S20: Apeaksoft Android Data Backup and Restore - https://www.apeaksoft.com/android-data-backup-and-restore/
- S21: Open Android Backup - https://github.com/mrrfv/open-android-backup
- S22: AppManager - https://github.com/MuntashirAkon/AppManager
- S23: AppManager issue 1974 APK verify - https://github.com/MuntashirAkon/AppManager/issues/1974
- S24: Neo Backup - https://github.com/NeoApplications/Neo-Backup
- S25: Seedvault - https://github.com/seedvault-app/seedvault
- S26: Shizuku - https://github.com/RikkaApps/Shizuku
- S27: scrcpy - https://github.com/Genymobile/scrcpy
- S28: UAD-NG - https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation
- S29: Canta - https://github.com/samolego/Canta
- S30: Migrate-OSS - https://github.com/BaltiApps/Migrate-OSS
- S31: ya-webadb - https://github.com/yume-chan/ya-webadb
- S32: Android Backup Extractor - https://github.com/nelenkov/android-backup-extractor
- S33: adbsms - https://github.com/gonodono/adbsms
- S34: Android SMS Gateway - https://github.com/capcom6/android-sms-gateway
- S35: LocalSend - https://github.com/localsend/localsend
- S36: ADB Explorer - https://github.com/Alex4SSB/ADB-Explorer
- S37: google/adb-sync - https://github.com/google/adb-sync
- S38: QtScrcpy - https://github.com/barry-ran/QtScrcpy
- S39: Shizuku API - https://github.com/RikkaApps/Shizuku-API
- S40: awesome-shizuku - https://github.com/timschneeb/awesome-shizuku
- S41: Canta website - https://samolego.github.io/Canta/
- S42: Awesome Android Root debloating guide - https://awesome-android-root.org/guides/android-apps-debloating
- S43: Android Auto Backup docs - https://developer.android.com/identity/data/autobackup
- S44: MobileTrans pricing - https://mobiletrans.wondershare.com/buy/pricing-for-individuals-windows.html
- S45: Apeaksoft store/pricing - https://www.apeaksoft.com/store/android-data-recovery/
- S46: Microsoft Artifact Signing FAQ - https://learn.microsoft.com/en-us/azure/trusted-signing/faq
- S47: Azure Artifact Signing pricing - https://azure.microsoft.com/en-us/pricing/details/artifact-signing/
- S48: CA/B Forum CSC-31 discussion - https://groups.google.com/a/groups.cabforum.org/d/msgid/cscwg-public/DS0PR14MB62161918BD2B73422EB3EEF49217A%40DS0PR14MB6216.namprd14.prod.outlook.com
- S49: Serilog releases - https://github.com/serilog/serilog/releases
- S50: QRCoder NuGet - https://www.nuget.org/packages/QRCoder/
- S51: Spectre.Console releases - https://github.com/spectreconsole/spectre.console/releases
- S52: JsonSchema.Net NuGet - https://www.nuget.org/packages/JsonSchema.Net/
- S53: WPF-UI releases - https://github.com/lepoco/wpfui/releases
- S54: MaterialDesignThemes NuGet - https://www.nuget.org/packages/MaterialDesignThemes/
- S55: Microsoft.Xaml.Behaviors.Wpf NuGet - https://www.nuget.org/packages/Microsoft.Xaml.Behaviors.Wpf/
- S56: .NET 10 announcement - https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/
- S57: WCAG 2.2 - https://www.w3.org/TR/WCAG22/
- S58: Avalonia - https://avaloniaui.net/
- S59: Syncthing BEP v1 - https://docs.syncthing.net/specs/bep-v1.html
- S60: Borgmatic hooks - https://torsion.org/borgmatic/docs/how-to/add-preparation-and-cleanup-steps-to-backups/
- S61: restic design - https://restic.readthedocs.io/en/stable/100_references.html#design
- S62: KDE Connect - https://invent.kde.org/network/kdeconnect-kde
- S63: Cellebrite Smart Switch forensic post - https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/
- S64: MOBILedit Smart Switch backup import - https://forensic.manuals.mobiledit.com/MM/samsung-smart-switch-backup
- S65: Hur, Lee, Cha 2021, forensic analysis of Samsung Smart Switch backup files - https://doi.org/10.1016/j.fsidi.2021.301172
- S66: Project Zero FORCEDENTRY sandbox escape - https://googleprojectzero.blogspot.com/2022/03/forcedentry-sandbox-escape.html
- S67: SWGDE mobile device evidence collection - https://swgde.org/documents/published-by-committee/mobile-devices/
- S68: Hacker News, Android backup discussion - https://news.ycombinator.com/item?id=42648597
- S69: Samsung Members, app data/logins transfer - https://r1.community.samsung.com/t5/samsung-smart-switch/transferring-app-data-and-logins/td-p/14255832
- S70: XDA Secure Folder not restoring with Smart Switch - https://xdaforums.com/t/secure-folder-not-restoring-by-smart-switch.4665109/
- S71: Reddit/SamsungGalaxy Smart Switch data complaints, 2026 - https://www.reddit.com/r/samsunggalaxy/comments/1rnwv3f/smart_switch/
- S72: Android Central, Android Show 2026 Quick Share/migration updates - https://www.androidcentral.com/apps-software/the-ios-android-file-sharing-nightmare-is-officially-over-for-more-android-users
- S73: NuGet vulnerability/outdated scan via `dotnet list package`, run locally 2026-05-16 against https://api.nuget.org/v3/index.json
- S74: Android app signing docs - https://developer.android.com/studio/publish/app-signing
- S75: FlaUI - https://github.com/FlaUI/FlaUI
- S76: GitHub artifact attestations - https://docs.github.com/en/actions/how-tos/secure-your-work/use-artifact-attestations/use-artifact-attestations
- S77: Velopack - https://github.com/velopack/velopack
- S78: Catppuccin - https://catppuccin.com/
- S79: Inno Setup - https://jrsoftware.org/isinfo.php
- S80: GitHub Actions - https://docs.github.com/en/actions
- S81: Android Debug Bridge docs - https://developer.android.com/tools/adb

## Self-Audit Ledger

- Security covered: CVE-2026-0073 gate, per-install ADB keys, USB-first policy, helper signing, sandbox parsers, vulnerability CI, managed signing.
- Accessibility covered: WCAG 2.2, UIA/Narrator smoke, focus/target-size work, theme variants.
- i18n/l10n covered: RESX extraction and en-US/ko-KR/pt-BR baseline.
- Observability/telemetry covered: local NDJSON, hashed device IDs, no telemetry, optional user-configured webhooks only.
- Testing covered: Core tests already exist; CI, provider fixtures, parser sandbox tests, vulnerable-package scans, and device corpus are planned.
- Docs covered: README screenshots, CONTRIBUTING, support matrix, compatibility notes, source appendix.
- Distribution/packaging covered: signed ZIP, Inno installer, provenance, timestamping, optional updater.
- Plugin ecosystem covered: OEM plugin model in v2+ after stable Core contracts.
- Mobile covered: helper APK, app_process JAR, Shizuku, Android 17 permission handling.
- Offline/resilience covered: checkpoints, retry/replay, retention, no-cloud default.
- Multi-user/collab covered: saved profiles, fleet/headless mode later, GitHub Discussions.
- Migration paths covered: Smart Switch, AppManager, Open Android Backup, `.ab`, Seedvault notes, cross-platform metadata.
- Upgrade strategy covered: dependency policy, CI scan, signed releases, updater decision gate.
- Traceability checked: every item above includes local source IDs or external source IDs listed in the appendix.
- Duplicate check: Now/Next/Later/Under Consideration/Rejected are mutually exclusive in this document.
