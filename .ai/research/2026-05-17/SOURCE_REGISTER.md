# Source Register - 2026-05-17

## Local Sources

| ID | Source | Use |
|---|---|---|
| L01 | `git log -10 --oneline --decorate` | Recent development state and version progression. |
| L02 | `git status --short --branch` | Branch ahead/dirty state. |
| L03 | `gh repo view SysAdminDoc/PhoneFork` | GitHub public/private, stars, issues, PRs, release state. |
| L04 | `README.md` | User-facing feature claims and version badge. |
| L05 | `CHANGELOG.md` | Current `v0.9.0-pre` state and shipped feature history. |
| L06 | `ROADMAP.md` before rewrite | Existing roadmap, stale claims, source appendix baseline. |
| L07 | `CONTRIBUTING.md` | Build/test conventions and contributor surface. |
| L08 | `src/PhoneFork.Core/PhoneFork.Core.csproj` | Core dependencies and embedded debloat assets. |
| L09 | `src/PhoneFork.App/PhoneFork.App.csproj` | WPF target, dependencies, bundled tools copy path. |
| L10 | `src/PhoneFork.Cli/PhoneFork.Cli.csproj` | CLI target and Spectre pins. |
| L11 | `tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj` | Test dependencies and xUnit legacy finding. |
| L12 | `src/PhoneFork.Cli/Program.cs` | Current CLI command surface. |
| L13 | `src/PhoneFork.Core/Services/*` | Current service architecture. |
| L14 | `helper-apk/app/build.gradle.kts` | Helper targetSdk/minSdk and build state. |
| L15 | `helper-apk/app/src/main/AndroidManifest.xml` | Helper permissions and provider authorities. |
| L16 | `helper-apk/app/src/main/java/.../BaseHelperProvider.kt` and `Providers.kt` | Shell/system UID gate, v1 JSON envelopes, category export bodies, and guarded restore endpoints. |
| L17 | `helper-apk/README.md` | Helper APK status, wire protocol, build, signing, and verified staging plan. |
| L18 | `.github/workflows/ci.yml` | CI scope, helper APK assemble, metadata verification, signing check, and artifact upload. |
| L19 | `.github/workflows/release.yml` | Release artifacts, signing slots, attestation slot. |
| L20 | `docs/competitor-research.md` | Commercial competitor baseline. |
| L21 | `docs/community-signal.md` | Prior community pain-point harvest. |
| L22 | `docs/migration-feasibility.md` | Android migration feasibility baseline. |
| L23 | `docs/oss-dependencies.md` | Dependency selection baseline. |
| L24 | `docs/oss-references.md` | OSS architecture reference baseline. |
| L25 | `docs/research-delta-2026-05-14.md` | Previous delta research. |
| L26 | `dotnet --info` | Local SDK/runtime state. |
| L27 | `dotnet list PhoneFork.slnx package --outdated` | Current package upgrade candidates. |
| L28 | `dotnet list PhoneFork.slnx package --vulnerable --include-transitive` | Vulnerability scan. |
| L29 | `dotnet list PhoneFork.slnx package --deprecated` | Deprecated package scan. |
| L30 | `scripts/Stage-HelperApk.ps1` | Verified helper APK staging gate for host packaging. |
| L31 | `gh release view v0.9.0-pre --repo SysAdminDoc/PhoneFork` | Published unsigned prerelease URL, prerelease flag, and asset list. |
| L32 | `dotnet run --project src\PhoneFork.Cli\PhoneFork.Cli.csproj -c Release --no-build -- backup inspect scratch\cli-backup-smoke --json` | Offline AppManager backup inspection smoke test. |
| L33 | `src/PhoneFork.Core/Services/MessageTransitionService.cs` and `tests/PhoneFork.Core.Tests/PreflightAndIntegrityTests.cs` | Samsung Messages / Google Messages pre-flight implementation and tests. |
| L34 | `src/PhoneFork.Core/Services/GalleryOneDriveService.cs` and `tests/PhoneFork.Core.Tests/PreflightAndIntegrityTests.cs` | Gallery / OneDrive cutoff assistant implementation and tests. |
| L35 | `src/PhoneFork.Core/Services/MediaSyncEvidence.cs`, `MediaSyncService.cs`, and `tests/PhoneFork.Core.Tests/MediaSyncEvidenceTests.cs` | Media sync checkpoint, evidence report, retry, throughput, and Quick Share advisory implementation and tests. |
| L36 | `src/PhoneFork.Core/Services/DebloatDataset.cs`, `assets/debloat/overrides.json`, `src/PhoneFork.Cli/Commands/Debloat*Command.cs`, and `tests/PhoneFork.Core.Tests/DebloatOverrideTests.cs` | Checksummed debloat overlay feed, OEM/action/risk/source/review/expiry metadata, CLI feed options, and override tests. |
| L37 | `.github/workflows/release.yml`, `scripts/New-ReleaseSbom.ps1`, and `docs/release-readiness.md` | Release signing, SBOM, checksum, release-note, and GitHub attestation implementation. |
| L38 | `src/*/*.csproj`, `tests/PhoneFork.Core.Tests/SchemaCompatibilityTests.cs`, `tests/PhoneFork.Core.Tests/AuditLoggerTests.cs`, and `dotnet list PhoneFork.slnx package --outdated` | R012 dependency upgrades, schema compatibility coverage, audit-log sink coverage, and post-update package scan. |
| L39 | `src/PhoneFork.Core/Services/AppTransferReportService.cs`, `src/PhoneFork.Cli/Commands/AppsReportCommand.cs`, `src/PhoneFork.Core/Services/AppInstallerService.cs`, and `tests/PhoneFork.Core.Tests/AppTransferReportTests.cs` | R013 per-app transfer posture reports, OBB/external-data probes, and local APK SHA-256 provenance logging. |

## Instruction And Memory Sources

| ID | Source | Use |
|---|---|---|
| M01 | `C:\Users\--\.claude\CLAUDE.md` | Global rules, roadmap auto-continue, `rtk`, UI corner-radius rules. |
| M02 | `C:\Users\--\CLAUDE.md` | Session ritual, Definition of Done, auto-commit-and-push, stack conventions. |
| M03 | `C:\Users\--\.claude\projects\c--Users----repos\memory\MEMORY.md` | Active project index and PhoneFork pointer. |
| M04 | `C:\Users\--\.claude\projects\c--Users----repos\memory\phonefork.md` | Historical PhoneFork facts through v0.8.0-era roadmap. |
| M05 | `C:\Users\--\.claude\projects\c--Users----repos\memory\stack-csharp.md` | WPF/.NET conventions. |
| M06 | `C:\Users\--\.claude\projects\c--Users----repos\memory\stack-android.md` | Android helper APK conventions. |
| M07 | `C:\Users\--\.codex\memories\MEMORY.md` | Prior Codex PhoneFork hardening memory and verification bundle. |

## External Primary Sources

| ID | Source | URL | Use |
|---|---|---|---|
| S01 | Android developer verification FAQ | https://developer.android.com/developer-verification/guides/faq | ADB install exemption; verification timeline and advanced flow. |
| S02 | Android Debug Bridge docs | https://developer.android.com/tools/adb | Wireless debugging pairing, mDNS behavior, command-line pairing flow. |
| S03 | Android SDK Platform-Tools release notes | https://developer.android.com/tools/releases/platform-tools | Platform-tools release tracking. |
| S04 | Android Auto Backup docs | https://developer.android.com/identity/data/autobackup | Android 16 QPR2 `<cross-platform-transfer>` rules and APIs. |
| S05 | Android Security Bulletin May 2026 | https://source.android.com/docs/security/bulletin/2026/2026-05-01 | CVE-2026-0073 critical adbd RCE patch-level evidence. |
| S06 | NVD CVE-2026-0073 | https://nvd.nist.gov/vuln/detail/CVE-2026-0073 | Vulnerability description and vendor advisory reference. |
| S07 | Samsung Smart Switch support | https://www.samsung.com/us/support/owners/app/smart-switch | Smart Switch transfer scope and PC/Mac backup flow. |
| S08 | Samsung Messages end-of-service page | https://www.samsung.com/us/apps/samsung-messages/ | July 2026 US Samsung Messages retirement and Google Messages switch guidance. |
| S09 | Samsung Wallet support | https://www.samsung.com/us/support/answer/ANS10001347/ | Samsung Pass/Samsung Pay merge into Wallet and activation caveats. |
| S10 | Microsoft Support: Samsung Gallery and OneDrive | https://support.microsoft.com/en-gb/office/changes-to-samsung-gallery-sync-and-onedrive-475ecc9c-c2fe-4d3c-ab9e-38e995123767 | Official September 30, 2026 Samsung Gallery direct OneDrive sync cutoff. |
| S11 | Android Quick Share official page | https://www.android.com/quick-share/ | Quick Share supports Android, iPhone, Chromebook, Windows PC. |
| S12 | Android Help: Quick Share | https://support.google.com/android/answer/9286773 | AirDrop-capable device classes, QR fallback, limits, encryption and 24-hour availability. |
| S13 | Android Help: iPhone to Android transfer | https://support.google.com/android/answer/13626960 | Current iPhone-to-Android transfer categories and gaps. |
| S14 | Apple Support: Transfer eSIM and data to Android | https://support.apple.com/en-au/126058 | iOS 26.3/Android 17 transfer, eSIM, accessibility/home-screen/wallpaper transfer. |
| S15 | Microsoft Artifact Signing FAQ | https://learn.microsoft.com/en-us/azure/artifact-signing/faq | Availability, identity validation, timestamp endpoint, EV non-support. |
| S16 | Microsoft Artifact Signing SKU pricing | https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku | Basic and Premium monthly price and signature quotas. |
| S17 | CA/Browser Forum Code Signing Baseline Requirements | https://cabforum.org/working-groups/code-signing/requirements/ | Code signing certificate and timestamp guidance. |
| S18 | GitHub Artifact Attestations docs | https://docs.github.com/en/actions/concepts/security/artifact-attestations | Provenance, SBOM, and Sigstore behavior for public repos. |
| S19 | GitHub Actions setup-dotnet | https://github.com/actions/setup-dotnet | CI SDK setup action used by repo. |
| S20 | GitHub actions/attest | https://github.com/actions/attest | Current provenance/SBOM attestation action and inputs. |
| S21 | Azure Artifact Signing Action | https://github.com/Azure/artifact-signing-action | Current Windows runner, auth, file list, timestamp, and signing action inputs. |
| S22 | Microsoft MSIX signing guide | https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide | Artifact Signing rename, action reference, and SmartScreen reputation warning for new publishers. |

## External OSS And Competitor Sources

| ID | Source | URL | Use |
|---|---|---|---|
| G01 | Genymobile/scrcpy repo and release v4.0 | https://github.com/Genymobile/scrcpy/releases/tag/v4.0 | `app_process` model and latest release signal. |
| G02 | RikkaApps/Shizuku repo and release v13.6.0 | https://github.com/RikkaApps/Shizuku/releases/tag/v13.6.0 | Shell-UID elevation pattern. |
| G03 | RikkaApps/Shizuku-API | https://github.com/RikkaApps/Shizuku-API | Helper APK API ecosystem. |
| G04 | MuntashirAkon/AppManager | https://github.com/MuntashirAkon/AppManager | Backup format and package management reference. |
| G05 | AppManager Shizuku issues | https://github.com/MuntashirAkon/AppManager/issues/1970 | Current community demand for Shizuku operation. |
| G06 | UAD-NG | https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation | Debloat dataset and safety taxonomy. |
| G07 | UAD-NG issue 1394 | https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1394 | One UI 8.5 `smartsuggestions` safety regression. |
| G08 | samolego/Canta | https://github.com/samolego/Canta | On-device Shizuku debloat UX. |
| G09 | LocalSend | https://github.com/localsend/localsend | Local transfer UX and discovery reference. |
| G10 | Alex4SSB/ADB-Explorer | https://github.com/Alex4SSB/ADB-Explorer | Windows ADB file manager and retry/thumbnail/UI issue signals. |
| G11 | T0biasCZe/AdbFileManager | https://github.com/T0biasCZe/AdbFileManager | ADB protocol Windows file manager throughput reference. |
| G12 | yume-chan/ya-webadb | https://github.com/yume-chan/ya-webadb | Browser ADB and wireless-debugging gap. |
| G13 | ya-webadb issue 784 | https://github.com/yume-chan/ya-webadb/issues/784 | Android 11+ wireless debugging TLS/pairing feature request. |
| G14 | seedvault-app/seedvault | https://github.com/seedvault-app/seedvault | AOSP backup transport and v1/v0 compatibility caution. |
| G15 | mrrfv/open-android-backup | https://github.com/mrrfv/open-android-backup | Companion-app local backup and hook model. |
| G16 | nelenkov/android-backup-extractor | https://github.com/nelenkov/android-backup-extractor | Legacy `.ab` archive reference. |
| G17 | NeoApplications/Neo-Backup | https://github.com/NeoApplications/Neo-Backup | Root backup UX, retention, and restore issues. |
| G18 | google/adb-sync | https://github.com/google/adb-sync | Archived ADB media-sync precedent. |
| G19 | barry-ran/QtScrcpy | https://github.com/barry-ran/QtScrcpy | Desktop Android control adjacent. |
| G20 | capcom6/android-sms-gateway | https://github.com/capcom6/android-sms-gateway | SMS provider/API architecture reference. |
| G21 | BaltiApps/Migrate-OSS | https://github.com/BaltiApps/Migrate-OSS | Root-required ROM migration comparator. |
| G22 | SysAdminDoc/AppManagerNG | https://github.com/SysAdminDoc/AppManagerNG | Debloat dataset source used by PhoneFork. |
| G23 | Open Android Backup site | https://mrrfv.github.io/open-android-backup/ | User-facing open backup feature claims. |
| G24 | Neo Backup F-Droid | https://f-droid.org/packages/com.machiav3lli.backup/ | Current release packaging and app metadata. |

## Commercial And Platform Sources

| ID | Source | URL | Use |
|---|---|---|---|
| C01 | Wondershare MobileTrans | https://mobiletrans.wondershare.com/phone-to-phone-transfer.html | Paid phone-transfer feature framing. |
| C02 | MobileTrans Play listing | https://play.google.com/store/apps/details?id=com.wondershare.mobiletrans | App-store feature/data-safety signal. |
| C03 | iMobie PhoneTrans | https://www.imobie.com/phonetrans/ | Clone/merge/custom migration positioning. |
| C04 | Syncios Data Transfer | https://www.syncios.com/data-transfer/ | Selective backup/restore and "clear before copy" pattern. |
| C05 | Syncios support | https://www.syncios.com/support/data-transfer.html | Clear-data option and troubleshooting corpus. |
| C06 | Apeaksoft Android Data Backup & Restore | https://www.apeaksoft.com/android-data-backup-and-restore/ | Encrypted backup/selective restore category reference. |
| C07 | MOBILedit Forensic | https://www.mobiledit.com/forensic-express-details | Forensic backup inspection and export positioning. |
| C08 | Microsoft Phone Link | https://support.microsoft.com/windows/phone-link | Companion-not-migration boundary. |

## Search And API Notes

- GitHub repo stats were collected with `gh api repos/<owner>/<repo>` and `gh repo view`.
- Release metadata was collected with `gh api repos/<owner>/<repo>/releases/latest`.
- Issue signals were collected with `gh issue list --search`.
- Web searches prioritized official Android, Samsung, Microsoft, Apple, CA/B Forum, GitHub, NuGet, and project-owned pages.
