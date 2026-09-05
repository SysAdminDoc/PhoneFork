# Research — PhoneFork

Date: 2026-09-04. Replaces all prior research in this file. The dated snapshots in `docs/` (2026-05-14 through 2026-05-17) are superseded where this file contradicts them.

## Executive Summary

PhoneFork is a Windows WPF + CLI tool that drives two USB-connected Samsung phones over ADB at once and migrates apps, media, settings, Wi-Fi metadata, default app roles, and a debloat profile, with no root and no Samsung account. The engineering foundation is genuinely strong: 163 Core unit tests pass in 242 ms, shell-argument quoting is centralised and enforced (`src/PhoneFork.Core/Services/AdbShell.cs`), device serials are hashed before they reach disk or logs, every write path emits an NDJSON audit line plus a JSON receipt, and `dotnet list package --vulnerable` reports zero vulnerable dependencies across all four projects.

The weakness is the gap between what is built and what is reachable. Three of the four capabilities added in v0.7.0 through v0.9.3-pre are wired end to end in neither the GUI nor the CLI, and the safety dataset that gates the one destructive operation is four months stale in a way that actively misclassifies packages.

Top opportunities, in priority order:

1. Refresh the debloat dataset and adopt upstream's new removal taxonomy. 188 packages are classified less dangerously in PhoneFork than upstream now rates them, and 176 of those sit in the default Conservative profile.
2. Grant the helper APK its runtime permissions at install time. Without this, the SMS, call log and contacts providers can only ever return `permission-denied`.
3. Expose the helper export path. `HelperAppService.QueryAsync` has zero callers anywhere in the repo.
4. Add a Cancel control. 33 view-model commands already accept a `CancellationToken`; no XAML surfaces a way to trigger one.
5. Ship the `app_process` agent JAR and use it for Wi-Fi PSK export. The host plumbing exists, the payload does not.
6. Detect Android Advanced Protection Mode in pre-flight. It blocks sideloading and USB data, so it breaks PhoneFork outright, and nothing in the codebase looks for it.
7. Drop or earn the AppManager backup compatibility claim. The on-disk format does not match AppManager 4.1.1.
8. Warn about Android 17's SMS OTP query filtering before an SMS export silently loses messages.
9. Grow the settings safety corpus past its current 38 safe rules, which cover a small fraction of a real 271-key diff.
10. Add accessibility names. Eight of ten views carry zero `AutomationProperties`.

## Product Map

**Core workflows**

- Pair two devices, assign Source and Destination roles, run per-domain dry-run then Apply (`src/PhoneFork.App/Views/MainWindow.xaml`, seven tabs, all live).
- Apps: enumerate `-3` packages, pull every `pm path` split, install with `install-create/-write/-commit -i com.android.vending --install-reason 4` (`AppInstallerService.cs`).
- Media: manifest diff then pull/push with mtime preservation, checkpoint resume, evidence report (`MediaSyncService.cs`, 397 lines, the largest service).
- Settings: three-namespace snapshot, set diff, corpus-gated `settings put` (`SettingsApplyService.cs`, `SamsungSettingsCorpus.cs`).
- Debloat: `pm disable-user --user 0` only, with a pre-apply snapshot and `cmd package install-existing` + `pm enable` rollback (`DebloatService.cs:124,197-198`).

**Personas.** A single technical owner migrating between two phones they control, post-factory-reset. Secondary: a repair or IT tech doing the same for someone else. The CLI's `--allow-multi-user` gate and receipt output suit the second case; the GUI blocks multi-user writes entirely.

**Platforms and distribution.** Windows 10/11, .NET 10 Desktop Runtime, framework-dependent zips built locally and published to GitHub Releases (`artifacts/release/`, with `SHA256SUMS` and an SPDX SBOM). Windows binaries are unsigned; the helper APK is signed with an external key. No CI by repo policy.

**Key integrations and data flows.** Bundled `tools/adb.exe` 37.0.0 drives the ADB server on port 5037. The embedded AppManagerNG/UAD-NG dataset (five JSON files, 5,478 entries) gates debloat. `PhoneForkHelper.apk` (159 KB, signed) installs over ADB and exposes seven exported ContentProviders behind a shell/system UID allow-list. Audit NDJSON and JSON receipts land under `%LOCALAPPDATA%\PhoneFork\`.

## Competitive Landscape

**Samsung Smart Switch.** The baseline. As of One UI 9.0 it gives wireless transfers the same data coverage as wired and adds QR-based setup, closing the gap PhoneFork's `docs/competitor-research.md` recorded in 2026-05. Learn: users now expect QR pairing as the default onboarding gesture. Avoid: Smart Switch's sequential one-phone-at-a-time PC flow, which is exactly PhoneFork's differentiator.

**Universal Android Debloater Next Generation** (9,045 stars, dataset commits through 2026-08-29). The upstream source of PhoneFork's debloat list. It has since renamed its removal taxonomy from `delete/replace/caution/unsafe` to `Recommended/Advanced/Expert/Unsafe` and added `dependencies`, `neededBy` and `labels` fields per package. Learn: the dependency graph is directly usable as a pre-apply warning ("`com.samsung.oda.service` is needed by `com.samsung.android.app.telephonyui`"). Avoid: UAD-NG fetches its list at launch, which PhoneFork deliberately does not do; keep the offline embed and add a verified refresh instead.

**App Manager** (8,889 stars, v4.1.1 released 2026-09-04). PhoneFork's stated backup-format compatibility target. Its real v5 layout is `info_v5.am.json` plus an encrypted `meta_v5.am.json`, with fields `version`, `backup_name`, `package_name`, `data_dirs`, `is_split_apk`, `split_configs`, `apk_name`, `installer`. Learn: separating a plaintext info header from an encrypted metadata body is a good pattern for an archive that must be inspectable before decryption. Avoid: claiming compatibility without implementing it.

**open-android-backup** (1,376 stars, v1.2.3). The closest direct competitor to PhoneFork's helper model: ADB plus a companion app, no root, cross-platform. It backs up apps without data, internal storage, and contacts as vCard; SMS and call logs are export-only in CSV and text and explicitly cannot be restored; MMS attachments are dropped; its companion needs physical interaction because unattended mode is unimplemented. Learn: PhoneFork's shell-UID-gated provider design is unattended by construction, which is a real advantage nobody in this space has shipped. Avoid: its Flutter companion's manual-interaction requirement.

**scrcpy** (148,900 stars, v4.1). Source of the `app_process` push-and-run pattern PhoneFork scaffolded in `AppProcessAgentService.cs`. Learn: a single pushed JAR run as the shell user is the cheapest route to privileged reads with zero install footprint. Avoid nothing; the pattern is sound and PhoneFork has simply not produced the JAR.

**Shizuku** (29,747 stars, last release v13.6.0 2025-05-25). Dormant for over a year but still the canonical shell-UID elevation model. Learn: Android 11 and later grant the shell user the permission behind `WifiManager.getPrivilegedConfiguredNetworks()`, which is the exact mechanism `zacharee/WiFiList` uses to read saved PSKs without root. This is reachable from PhoneFork's own agent JAR without Shizuku being installed at all.

**Seedvault** (1,840 stars, active through 2026-08-12). ROM-integrated backup. Not a competitor on Samsung stock firmware, but the relevant reference for what an open, documented archive format should specify. PhoneFork's `OpenArchiveSpec.cs` defines a schema with no writer or reader; Seedvault shows the cost of getting that wrong.

**Neo Backup** (3,794 stars, 8.3.18, quiet since 2026-05) and **Android-DataBackup** (7,311 stars, 2.0.12). Both root-required, both with their own incompatible archive formats. Learn: the multi-format import matrix is a maintenance sink; pick one interchange format and document it.

**hoardy-adb** (96 stars) and assorted ADB GUI wrappers. Thin tooling around the dead `adb backup` path. Nothing to learn beyond confirming that PhoneFork's decision to avoid `adb backup` was correct.

## Reported Issues

PhoneFork's own tracker carries no signal. `gh issue list` returns zero open and zero closed issues, `gh pr list` zero PRs, discussions are enabled but empty, and the repo has 1 star and 1 fork as of 2026-09-04. There is nothing to triage, and no user-reported bug outranks the code-traced defects below.

Upstream trackers that do bear on PhoneFork:

- UAD-NG issue #1394, already handled: the `com.samsung.android.smartsuggestions` One UI 8.5 override in `assets/debloat/overrides.json`. Its `reviewAfter` date of 2026-07-01 has passed with no review.
- UAD-NG has 15+ open `pkg(scope)` issues filed since 2026-07-28 proposing Samsung package reclassifications, several already merged into the dataset PhoneFork does not track.
- AdvancedSharpAdbClient issue #123 (handle inheritance when `AdbServer.StartServer` launches adb.exe). Judged not actionable here; see Rejected Ideas.

Community signal outside GitHub, refreshed 2026-09: Samsung Community threads on the Galaxy S26 report Google Messages content failing to transfer through Smart Switch, and Android Central threads report apps arriving without notification state and needing reinstall. Both converge on the same conclusion the repo's own `docs/community-signal.md` reached in 2026-05, and both point at the SMS export path PhoneFork built but never exposed.

## Security, Privacy, and Reliability

**Verified safe.** Shell quoting is centralised in `AdbShell.Arg` and `AdbShell.PackageArg` with a `^[A-Za-z0-9_.]+$` package regex, and every interpolation site in `src/PhoneFork.Core` passes pre-quoted values. Serials are SHA-256 prefixed before disk (`SerialHash.cs`, `TrustedPairRegistry.cs`). The wireless-ADB gate refuses transports below the 2026-05-01 patch level (`SecurityPosture.cs:40`); the May 2026 Android Security Bulletin lists CVE-2026-0073 (System/adbd, RCE, Critical) under the 2026-05-01 patch level, so this constant is correct as written. No vulnerable or deprecated production packages; `xunit` 2.9.3 is flagged Legacy in the test project only.

**Debloat dataset drift (highest severity finding).** `assets/debloat/*.json` was captured 2026-05-14. Diffing it against the live upstream `resources/assets/uad_lists.json` on 2026-09-04:

- Upstream: 5,372 entries. Embedded: 5,478. 127 packages dropped upstream, 17 added.
- Upstream renamed every removal value. `delete` became `Recommended`, `replace` became `Advanced`, `caution` became `Expert`, `unsafe` stayed `Unsafe`.
- The old-to-new mapping is inferred, not published: it holds exactly across every sampled pair (for example `org.lineageos.jelly` delete to Recommended, `com.cyanogenmod.filemanager` replace to Advanced, `org.omnirom.omnistyle` caution to Expert) and the four bucket sizes line up. Treat the mapping as Likely rather than Verified and confirm it against an upstream commit before regenerating the embed.
- Applying that mapping, **188 packages are rated more dangerous upstream than in the embedded copy, and 176 of those are in PhoneFork's `delete` tier, which is the default Conservative profile** (`DebloatViewModel.cs:229`, `DebloatApplyCommand.cs:17`).
- Four are now `Unsafe` upstream while PhoneFork still lists them as `delete`: `com.android.networkstack.tethering.inprocess`, `jp.co.omronsoft.iwnnime.ml`, `com.lenovo.ue.device`, `com.google.android.overlay.gmsconfig.photos`.
- Concrete example: `com.samsung.oda.service` is `delete` in the embed and `Advanced` upstream, with the upstream description "Disabling causes SIM Manager to instantly crash on dual sim setup."
- `DebloatDataset.cs:31-37` only parses the four lowercase legacy values, so a current upstream dump fed through the overlay path yields a null tier for every entry. The refresh mechanism cannot ingest the data it is meant to refresh.

**Helper permissions are never granted.** `HelperAppService.InstallAsync` runs `pm install -r` (`HelperAppService.cs:54`) with no `-g` and no follow-up `pm grant`. `PhoneForkHelper` declares `READ_SMS`, `READ_CALL_LOG` and `READ_CONTACTS` as runtime-dangerous permissions and ships no launcher activity (`helper-apk/app/build.gradle.kts`, `AndroidManifest.xml`), so it can never prompt for them. Every `SmsProvider`, `CallLogProvider` and `ContactsProvider` query therefore takes the `SecurityException` path in `Providers.kt:exportRows` and returns a `permission-denied` envelope. Confidence: Verified by code reading; needs live validation on hardware to confirm the exact failure mode.

**Android 17 will silently truncate SMS exports.** Android 17's SMS OTP protection withholds `SMS_RECEIVED_ACTION` and filters SMS provider database queries for three hours for apps that are not the intended WebOTP recipient, and the change applies to all apps regardless of target API level. `SmsProvider` queries `Telephony.Sms.CONTENT_URI` and is not the default SMS handler, so a migration run within three hours of an OTP arriving will drop those rows with no error.

**Advanced Protection Mode is undetected.** Android 16 and later can block the sideloading permission, disable USB data signaling while locked, and (rolling out) disable Developer Options entirely. `PreflightService.cs` probes patch level, `oem_unlock_allowed`, `ro.boot.warranty_bit` and `ro.boot.flash.locked`, and Samsung honesty state. Nothing checks Advanced Protection, so a user hits an opaque ADB failure instead of a pre-flight explanation.

**Rollback coverage.** Debloat has a real snapshot-and-restore path. Apps, settings, roles and media do not; settings apply relies on the safety corpus rather than a restore point, and there is no "undo this migration" surface anywhere. The corpus is 38 `Safe` rules, 6 `Review` and 14 `Blocked` (`SamsungSettingsCorpus.cs`), against a hardware-measured 271-key applicable diff, so most real keys resolve to `Unknown` and are skipped by default.

**No way to stop a running operation.** 33 view-model commands take a `CancellationToken` and Core honours it, but no `[RelayCommand]` sets `IncludeCancelCommand`, and `grep Cancel src/PhoneFork.App/Views/*.xaml` returns nothing. A migration that installs hundreds of split APKs or disables thousands of packages runs to completion or to a crash.

## Architecture Assessment

**Unreachable features.** Three subsystems have host plumbing, tests on their constants, and no runtime caller:

- `HelperAppService.QueryAsync` (`HelperAppService.cs:91-111`). Callers of `HelperAppService` are limited to install, uninstall, probe and residue in `HelperCommands.cs` and `OperationsViewModel.cs:292,304,327`. Nothing reads SMS, call log, contacts, dictionary, ringtone or wallpaper data.
- `AppProcessAgentService` (`AppProcessAgentService.cs`). Referenced only by constant assertions in `tests/PhoneFork.Core.Tests/HelperAppServiceTests.cs:168-175`. No `phonefork-agent.jar` exists and `helper-apk/settings.gradle.kts` declares only `:app`, so no Gradle module produces one.
- `OpenArchiveSpec.cs`. Round-tripped in tests and cited by a URL string in `PlatformMigrationWatcherService.cs:61`, with no writer or reader.

**Dead code inside the helper.** `Providers.kt:rejectRestoreWithoutConfirmation` returns `null` on every path including the one where `confirmRestore` is true, so the confirmation branch is unreachable. `BaseHelperProvider.onQuery`'s `not-implemented` default is now unreachable too, since all seven concrete providers override it.

**Backup format mismatch.** `AppManagerBackupWriter.cs:93-95` writes `meta.am.v5` with an `am_meta_version` field plus `checksums.txt`. AppManager 4.1.1 writes `info_v5.am.json` and `meta_v5.am.json` and shares no field names. The reader in `AppManagerBackupReader.cs:42` looks only for PhoneFork's own file. This is a self-consistent private format wearing another project's name.

**Pagination bug in the helper.** `Providers.kt:exportRows` advances the cursor inside the `items.length() >= limit && c.moveToNext()` guard before breaking, which perturbs `totalSeen` and can produce a `nextOffset` that skips a row at each page boundary. Confidence: Likely from reading; needs a device or Robolectric test to confirm.

**Test gaps.** 163 tests, all pure unit tests over Core, all in-process, 242 ms total. There is no ADB fake or transport double, so no service that actually talks to a device is exercised. `src/PhoneFork.App` and `src/PhoneFork.Cli` have no test project at all, which is why the 33-command cancellation gap and the unreachable helper export were both invisible to the suite.

**Dead files.** `src/PhoneFork.App/Views/PlaceholderView.xaml` is referenced only from generated `obj/` output; all seven tabs bind real views. `assets/helper-apk-stub/` is an empty directory.

**Documentation drift.** Every file in `docs/` except `release-readiness.md` carries a 2026-05-14 to 2026-05-17 evidence date. Since then One UI 8.5 shipped with the Galaxy S26 (Feb/Mar 2026), One UI 9 on Android 17 entered beta, Smart Switch gained wireless/wired parity and QR setup on One UI 9.0, App Manager moved to 4.1.1, and scrcpy moved to v4.1. `docs/research-delta-2026-05-14.md` states scrcpy v4.0 as current.

**Dependency currency.** Behind latest: `Spectre.Console` 0.55.2 to 0.57.2, `Serilog` 4.3.1 to 4.4.0, `CliWrap` 3.10.1 to 3.10.5, `JsonSchema.Net` 9.2.1 to 9.4.0, `Microsoft.Xaml.Behaviors.Wpf` 1.1.142 to 1.1.158, `Microsoft.NET.Test.Sdk` 18.5.1 to 18.9.0, `xunit.runner.visualstudio` 3.1.5 to 4.0.0, `coverlet.collector` 10.0.0 to 10.0.1. `xunit` 2.9.3 is deprecated in favour of `xunit.v3`. Bundled `tools/adb.exe` is 37.0.0, which is current.

## Rejected Ideas

- **Bump the bundled adb.** Already at 37.0.0, the current platform-tools release. Source: developer.android.com platform-tools release notes.
- **Change the CVE-2026-0073 patch-level gate.** Two secondary sources disagreed on 2026-05-01 vs 2026-05-05, but the primary Android Security Bulletin lists the CVE under 2026-05-01, matching `SecurityPosture.cs:40`. No change needed.
- **Work Android developer verification into the roadmap.** ADB installs are explicitly exempt, the README already says so correctly, and the owner's standing rule bars Play, F-Droid, Wear OS and verification work on Android repos. Source: vault note `Research/Android Developer Verification 2026.md`.
- **Work around AdvancedSharpAdbClient issue #123** (adb.exe inherits parent handles). The consequence is a leaked listening socket surviving a host crash; PhoneFork opens no listening sockets, so there is nothing to leak.
- **Root-based third-party app-data migration.** Contradicts the project's stated no-root philosophy, and the README already routes users to Smart Switch for that subset.
- **Add GitHub Actions CI.** Repo policy is local builds only; workflows were deliberately removed in commit 4f9abe6.
- **Multi-format backup import (Neo Backup, DataBackup, Seedvault).** Three mutually incompatible formats, all requiring root to produce, for a tool that cannot read app data anyway. Source: Neo Backup 8.3.18 and Android-DataBackup 2.0.12 format docs.
- **Commercial device corpus and test lab.** Already captured and correctly gated in `Roadmap_Blocked.md`.
- **Authenticode signing for the Windows zips.** Real friction (SmartScreen warns on every download) but it needs a purchased certificate and an owner decision, and `docs/release-readiness.md` already records it as pending a local certificate. Nothing an implementing agent can act on.
- **A plugin or extension system.** The debloat overlay feed (`--overlay-feed` with SHA-256 verification) is the extension point this tool actually needs, and it already exists. A general plugin host would add attack surface to a program that installs packages on a phone.
- **Observability beyond what exists.** NDJSON audit logging with hashed serials plus per-operation JSON receipts already cover the diagnostic need at this scale; a metrics or telemetry layer would contradict the local-only, no-cloud positioning.
- **Localisation to non-English locales.** The tool is Windows-only, single-operator, and every string is currently inline in XAML with no `.resx`. Extracting resources is real work with no evidenced demand at 1 star. Revisit if the tracker ever shows a non-English request.

## Sources

Project trackers and code
- https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation
- https://raw.githubusercontent.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/main/resources/assets/uad_lists.json
- https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/issues/1394
- https://github.com/MuntashirAkon/AppManager/releases
- https://raw.githubusercontent.com/MuntashirAkon/AppManager/master/app/src/main/java/io/github/muntashirakon/AppManager/backup/MetadataManager.java
- https://raw.githubusercontent.com/MuntashirAkon/AppManager/master/app/src/main/java/io/github/muntashirakon/AppManager/backup/struct/BackupMetadataV5.java
- https://github.com/mrrfv/open-android-backup
- https://github.com/zacharee/WiFiList
- https://github.com/Genymobile/scrcpy
- https://github.com/RikkaApps/Shizuku
- https://github.com/seedvault-app/seedvault
- https://github.com/NeoApplications/Neo-Backup
- https://github.com/XayahSuSuSu/Android-DataBackup
- https://github.com/Own-Data-Privateer/hoardy-adb
- https://github.com/SharpAdb/AdvancedSharpAdbClient/issues/123

Platform and standards
- https://developer.android.com/about/versions/17/behavior-changes-all
- https://developer.android.com/about/versions/17/behavior-changes-17
- https://developer.android.com/tools/releases/platform-tools
- https://source.android.com/docs/security/bulletin/2026/2026-05-01
- https://developer.android.com/developer-verification/guides/faq

Security advisories
- https://www.runzero.com/blog/android-debug-bridge/
- https://www.smarttech247.com/threat-intel-reports/android-cve-2026-0073-wireless-adb-auth-flaw
- https://www.androidauthority.com/android-advanced-protection-3556885/
- https://www.androidauthority.com/android-advanced-protection-mode-developer-options-3679725/

Vendor and community signal
- https://www.sammobile.com/news/samsung-one-ui-8-5-everything-to-know/
- https://9to5google.com/2026/05/12/samsung-galaxy-one-ui-9-beta-android-17/
- https://us.community.samsung.com/t5/Galaxy-S26/Google-S26-Google-messages-not-transferring/td-p/3501331
- https://forums.androidcentral.com/threads/use-smart-switch-or-do-clean-setup-on-s26-ultra.1086767/
- https://www.samsung.com/us/support/answer/ANS10010422

Local commands whose output is cited above
- `dotnet test -c Release` (163 passed, 2026-09-04)
- `dotnet list package --outdated | --vulnerable | --deprecated` (2026-09-04)
- `tools/adb.exe version` (1.0.41 / 37.0.0-14910828)

## Open Questions

1. Should `backup export-appmanager` become genuinely AppManager-readable (encrypted `meta_v5.am.json` and its field names), or should the command and format be renamed to PhoneFork's own archive and the compatibility claim dropped from README and CLAUDE.md? Both are defensible; the choice changes roughly a day of work and the v1.0 positioning.
2. Is SMS and call-log **restore** in scope, or is export-only the intended ceiling? `Providers.kt` currently refuses all restores, and restoring SMS requires the helper to become the default SMS app, which is a large and intrusive change. Nothing in the repo states the intent.
3. Does the operator want One UI 9 / Android 17 treated as a supported target before v1.0, or as post-v1.0 work? The hardware validation matrix is Android 16 / One UI 8 only and the two phones on hand determine what can actually be tested.
