# Competitor Matrix - 2026-05-17

Stats are point-in-time where collected from GitHub or official product pages.
Use `SOURCE_REGISTER.md` for URLs and source IDs.

## Direct And Adjacent OSS Projects

| Project | Category | Current signal | PhoneFork lesson | Sources |
|---|---|---|---|---|
| scrcpy | ADB `app_process` server and desktop control | Very active, large user base, v4.0 current in research pass | Its push-and-run server model supports PhoneFork's planned shell-UID agent pattern, but PhoneFork should stay migration-focused. | G01 |
| Shizuku | Shell/system API bridge | Stable, widely used, latest release signal from 2025 | Shell-UID elevation is accepted by power users; PhoneFork should treat Shizuku as optional assist, not a hard dependency. | G02, G03 |
| AppManager | Android package/backup/policy manager | Active, large backlog, Shizuku requests and APK verification requests visible | AppManager format interop and package verification are high-value; do not copy GPL code into MIT binaries. | G04, G05 |
| UAD-NG | Debloat dataset and safety taxonomy | Active, current Samsung/One UI issue signals | Embedded debloat data needs an out-of-band safety overlay and source-backed risk notes. | G06, G07 |
| Canta | On-device Shizuku debloat UX | Active enough, user-friendly presets | Users need descriptions, risk categories, and uncertain-result language before disabling packages. | G08 |
| LocalSend | Local file transfer | Large user base, mature discovery UX | Great reference for discovery and transfer-status UX; not a migration planner. | G09 |
| ADB-Explorer | Windows ADB file manager | Active WPF file-manager work and retry/thumbnail issue signals | Windows ADB users value retry, progress, thumbnails, and filesystem ergonomics. | G10 |
| AdbFileManager | Windows ADB file manager | Active adjacent project | Throughput and file-manager UX are competitive pressure for PhoneFork media sync. | G11 |
| ya-webadb | Browser ADB library/app | Active, WebUSB and wireless-debugging requests | Useful v2 watchlist; browser ADB is not enough for current WPF v1 safety model. | G12, G13 |
| Seedvault | System backup transport | Active AOSP backup project | Treat as future format watcher; system privileges limit direct PhoneFork reuse. | G14 |
| Open Android Backup | Local open backup tool | Active enough, companion app and hooks | Good archive/hook UX reference; restore and device-detection issues show risk. | G15, G23 |
| android-backup-extractor | Legacy `.ab` archive tooling | Mature reference | Useful for inspect/import of older `.ab` backups, not a primary migration path. | G16 |
| Neo Backup | Root backup app | Active root-first project | Retention and restore UX are useful; root requirement conflicts with PhoneFork thesis. | G17, G24 |
| google/adb-sync | ADB sync precedent | Dormant/archived signal | Confirms PhoneFork must own modern media sync, resilience, and verification. | G18 |
| QtScrcpy | GUI device-control adjacent | Large user base | Confirms demand for desktop Android utilities but not migration-specific. | G19 |
| android-sms-gateway | SMS provider/API architecture | Active SMS API project | Useful for helper-provider SMS design and future local API ideas. | G20 |
| Migrate-OSS | Root migration app | Small root-first comparator | Sequencing ideas only; root-first approach is not PhoneFork's path. | G21 |

## Commercial And Platform Comparators

| Product/source | Positioning | Lessons for PhoneFork | Sources |
|---|---|---|---|
| Samsung Smart Switch | Official Samsung transfer app and PC/Mac backup tool | Complement it for Knox/OEM-private categories. Detect and explain its gaps instead of claiming a full clone. | S07 |
| Samsung Messages to Google Messages | Official US market messaging transition | Build SMS pre-flight around default app status, Google Messages readiness, and the documented data-transfer delay. | S08 |
| Samsung Wallet / Pass | Account-bound wallet/pass transition | Warn and hand off; never promise token/password migration. | S09 |
| Samsung Gallery + OneDrive | Official sync cutoff | Update pre-flight to September 30, 2026 and camera-backup guidance. | S10 |
| Quick Share | OS-level file transfer across Android, Windows, Chromebook, and some Apple device flows | Use as category-specific handoff for ad hoc files, not as PhoneFork replacement. | S11, S12 |
| Apple iOS/iPadOS to Android transfer | Official Android 17/iOS 26.3 setup-time transfer | Track for v2; do not invest early unless PhoneFork can solve a clear local gap. | S14 |
| Wondershare MobileTrans | Paid phone-to-phone transfer | Marketing emphasizes app data, WhatsApp, speed, and simplicity; PhoneFork should counter with evidence, local control, and honesty. | C01, C02 |
| iMobie PhoneTrans | Clone/merge/custom migration | Merge/custom vocabulary is useful for future planning UX. | C03 |
| Syncios Data Transfer | Selective backup/restore and clear-before-copy | "Clear destination before copy" should be a carefully gated destructive option if ever added. | C04, C05 |
| Apeaksoft Android Data Backup & Restore | Encrypted backup, preview, selective restore | Preview-before-restore and encryption language are table stakes for backup UX. | C06 |
| MOBILedit Forensic | Commercial forensic backup inspection | Smart Switch backup inspection is valuable, but forensic positioning is not PhoneFork's user promise. | C07 |
| Microsoft Phone Link | Companion relay for SMS/files/calls | Not a migration competitor; use as boundary language. | C08 |

## Positioning Summary

PhoneFork's strongest defensible lane is not "more magical transfer." It is
"local, explicit, reversible, source-backed migration planning and execution for
what ADB/helper/Shizuku can actually reach." Competitors either have OEM/system
privileges, root requirements, cloud/account dependencies, or single-purpose
file-control UX. PhoneFork should win by combining category planning, safe ADB
execution, honest gaps, and repeatable audit receipts.
