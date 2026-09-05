# PhoneFork Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

Added 2026-09-04. Evidence and full reasoning in RESEARCH.md. IDs continue the existing F### scheme (highest prior ID: F108).

### P0

### P1

- [ ] P1 — F117 Resolve the AppManager backup compatibility claim
  Why: `AppManagerBackupWriter` writes `meta.am.v5` with an `am_meta_version` field, while App Manager 4.1.1 writes `info_v5.am.json` plus an encrypted `meta_v5.am.json` and shares no field names. The current format is a private PhoneFork format wearing another project's name, so the documented round-trip does not exist.
  Evidence: `src/PhoneFork.Core/Services/AppManagerBackupWriter.cs:93-95` and `AppManagerBackupReader.cs:42`; https://raw.githubusercontent.com/MuntashirAkon/AppManager/master/app/src/main/java/io/github/muntashirakon/AppManager/backup/MetadataManager.java (`currentBackupMetaVersion = 5`, `info_v5.am.json`, `meta_v5.am.json`) and `.../struct/BackupMetadataV5.java` field list.
  Touches: `src/PhoneFork.Core/Services/AppManagerBackupSpec.cs`, `AppManagerBackupWriter.cs`, `AppManagerBackupReader.cs`, `src/PhoneFork.Cli/Commands/BackupCommands.cs`, `README.md`, `CLAUDE.md`, `tests/PhoneFork.Core.Tests/BackupInteropTests.cs`
  Acceptance: either the writer emits `info_v5.am.json` and `meta_v5.am.json` using App Manager's field names and a backup produced by PhoneFork is listed by App Manager 4.1.1 on a device, or the commands and format are renamed to PhoneFork's own archive and every "AppManager-compatible" and "round-trip" claim is removed from README.md and CLAUDE.md. See Open Question 1 in RESEARCH.md.
  Complexity: L

- [ ] P1 — F118 Warn when Android 17 SMS OTP filtering can truncate an SMS export
  Why: Android 17 withholds `SMS_RECEIVED_ACTION` and filters SMS provider queries for three hours for apps that are not the intended WebOTP recipient, for all apps regardless of target API level. The helper is not the default SMS handler, so an export run soon after an OTP arrives loses those rows with no error at all.
  Evidence: https://developer.android.com/about/versions/17/behavior-changes-all SMS OTP protection section; `helper-apk/app/src/main/java/.../providers/Providers.kt` `SmsProvider` queries `Telephony.Sms.CONTENT_URI`.
  Touches: `helper-apk/app/src/main/java/.../providers/Providers.kt`, `src/PhoneFork.Core/Services/HelperProviderContract.cs`, `src/PhoneFork.Core/Services/MessageTransitionService.cs`
  Acceptance: on API 37 and above the SMS provider adds a `warnings` entry naming the three-hour OTP delay, and the host surfaces it in the export output and receipt; the CLI export exits non-zero only on real failure, not on this warning.
  Complexity: S

- [ ] P1 — F119 Remove the unreachable restore branch in the helper or implement restore
  Why: `rejectRestoreWithoutConfirmation` returns `null` on every path, including the one guarded by `confirmRestore`, so the confirmation logic is dead code that reads as a working gate.
  Evidence: `helper-apk/app/src/main/java/.../providers/Providers.kt`, `rejectRestoreWithoutConfirmation`; `restoreDisabled()` already refuses restore queries separately.
  Touches: `helper-apk/app/src/main/java/.../providers/Providers.kt`
  Acceptance: either the function is reduced to an unconditional refusal that matches `restoreDisabled()`'s wording, or restore is implemented behind the confirmation flag with a test proving the confirmed path inserts and the unconfirmed path does not. See Open Question 2 in RESEARCH.md.
  Complexity: S

### P2

- [ ] P2 — F120 Fix the helper provider pagination boundary
  Why: `exportRows` calls `c.moveToNext()` inside the `items.length() >= limit` guard before breaking, which advances the cursor an extra row and perturbs `totalSeen`, so `nextOffset` can skip a record at every page boundary. On a several-thousand-message SMS export that silently drops data.
  Evidence: `helper-apk/app/src/main/java/.../providers/Providers.kt`, `exportRows` loop and `nextOffset` computation.
  Touches: `helper-apk/app/src/main/java/.../providers/Providers.kt`, new `helper-apk/app/src/test/`
  Acceptance: a test over a fake cursor of 1,001 rows paged at `limit=500` returns 1,001 distinct ids across three pages with no gaps and no repeats; the same test fails if the extra `moveToNext()` is restored.
  Complexity: S

- [ ] P2 — F121 Surface upstream package dependency warnings before a debloat apply
  Why: upstream now ships `dependencies` and `neededBy` per package, which turns "disabling this breaks that" from a description users skim into a checkable pre-apply warning. `com.samsung.oda.service` is `neededBy` `com.samsung.android.app.telephonyui`.
  Evidence: live `uad_lists.json` records carry `dependencies`, `neededBy` and `labels`; PhoneFork's `DebloatEntry` model has no equivalent fields.
  Touches: `src/PhoneFork.Core/Models/DebloatEntry.cs`, `src/PhoneFork.Core/Services/DebloatDataset.cs`, `src/PhoneFork.Core/Services/DebloatScanner.cs`, `src/PhoneFork.App/Views/DebloatView.xaml`, `src/PhoneFork.Cli/Commands/DebloatApplyCommand.cs`
  Acceptance: the parsed dataset carries `neededBy`; the queue preview lists, per selected package, any still-enabled package that needs it; the CLI prints the same list before applying and the GUI shows it in the plan area.
  Complexity: M

- [ ] P2 — F122 Add accessibility names to the eight views that have none
  Why: only `AppsView.xaml` (2) and `DeviceBar.xaml` (11) carry `AutomationProperties`. Debloat, Media, Operations, Roles, Settings, Wi-Fi, MainWindow and PlaceholderView have zero, so a screen-reader user cannot identify the buttons that disable packages or write settings.
  Evidence: per-file `AutomationProperties` counts across `src/PhoneFork.App/Views/*.xaml` on 2026-09-04.
  Touches: `src/PhoneFork.App/Views/DebloatView.xaml`, `MediaView.xaml`, `OperationsView.xaml`, `RolesView.xaml`, `SettingsView.xaml`, `WifiView.xaml`, `MainWindow.xaml`
  Acceptance: every interactive control that is not self-labelling by its `Content` has an `AutomationProperties.Name`; every `DataGrid` and tab has an `AutomationProperties.Name`; Accessibility Insights for Windows reports no missing-name errors on any tab.
  Complexity: M

- [ ] P2 — F123 Grow the settings safety corpus beyond the current 38 safe rules
  Why: the corpus holds 38 Safe, 6 Review and 14 Blocked rules against a hardware-measured 271-key applicable diff, so most real keys resolve to Unknown and are skipped unless the user passes `--include-uncatalogued-settings`, which gives up the safety gate wholesale.
  Evidence: rule counts in `src/PhoneFork.Core/Services/SamsungSettingsCorpus.cs`; the 1,062 vs 967 key snapshot and 271 applicable figure recorded for v0.3.0 in CLAUDE.md.
  Touches: `src/PhoneFork.Core/Services/SamsungSettingsCorpus.cs`, `tests/PhoneFork.Core.Tests/DifferTests.cs`
  Acceptance: the corpus classifies at least 60 percent of the keys in a captured S25-to-S22 diff fixture as Safe, Review or Blocked rather than Unknown; each new rule carries a rationale and a source id; a test asserts the Unknown share of that fixture stays under 40 percent.
  Complexity: L

- [ ] P2 — F124 Add an ADB transport fake and test the App and Cli projects
  Why: all 163 tests are pure Core unit tests with no transport double, and neither `src/PhoneFork.App` nor `src/PhoneFork.Cli` has a test project. That is why the missing cancel affordance and the unreachable helper export both went unnoticed.
  Evidence: `dotnet test -c Release` on 2026-09-04 reports 163 passed in 242 ms from a single assembly, `PhoneFork.Core.Tests.dll`; `tests/` contains one project.
  Touches: new `tests/PhoneFork.App.Tests/`, new `tests/PhoneFork.Cli.Tests/`, a shared fake `IAdbClient`, `PhoneFork.slnx`
  Acceptance: a fake `IAdbClient` replays recorded shell output; tests cover the apps migrate, settings apply and debloat apply view-model flows including a cancellation case, and cover CLI exit codes for at least the apps, settings, debloat and helper branches; the suite runs with no device attached.
  Complexity: L

- [ ] P2 — F125 Validate against One UI 8.5 and One UI 9 / Android 17
  Why: the hardware validation matrix is Android 16 / One UI 8 only. One UI 8.5 shipped with the Galaxy S26 in early 2026 and One UI 9 on Android 17 is in beta, and `assets/debloat/overrides.json` already carries `oneUi: ">=8.5"` rules that have never been exercised.
  Evidence: CLAUDE.md hardware-validated list; https://www.sammobile.com/news/samsung-one-ui-8-5-everything-to-know/; https://9to5google.com/2026/05/12/samsung-galaxy-one-ui-9-beta-android-17/
  Touches: `docs/release-readiness.md`, `src/PhoneFork.Core/Services/CscDiffService.cs`, `src/PhoneFork.Core/Services/SamsungSettingsCorpus.cs`
  Acceptance: a documented compatibility matrix records, per One UI version tested, whether `cmd wifi list-networks`, `cmd role get-role-holders`, `settings list`, `pm disable-user` and `pm install-create` behave as PhoneFork expects; any divergence is either handled in code or named as a known limitation in README.md. Note: this overlaps the physical-hardware gate already tracked in Roadmap_Blocked.md and inherits that constraint for anything needing a second device.
  Complexity: M

- [ ] P2 — F126 Bring dependencies current and move the test project off deprecated xunit
  Why: eight packages are behind latest and `xunit` 2.9.3 is flagged Legacy by NuGet in favour of `xunit.v3`.
  Evidence: `dotnet list package --outdated` and `--deprecated` on 2026-09-04: Spectre.Console 0.55.2 to 0.57.2, Serilog 4.3.1 to 4.4.0, CliWrap 3.10.1 to 3.10.5, JsonSchema.Net 9.2.1 to 9.4.0, Microsoft.Xaml.Behaviors.Wpf 1.1.142 to 1.1.158, Microsoft.NET.Test.Sdk 18.5.1 to 18.9.0, xunit.runner.visualstudio 3.1.5 to 4.0.0, coverlet.collector 10.0.0 to 10.0.1.
  Touches: `src/PhoneFork.Core/PhoneFork.Core.csproj`, `src/PhoneFork.App/PhoneFork.App.csproj`, `src/PhoneFork.Cli/PhoneFork.Cli.csproj`, `tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj`
  Acceptance: `dotnet list package --outdated` reports no updates for production projects, `--deprecated` is clean, `dotnet build -c Release` and the full test suite pass, and the Spectre.Console 0.55 to 0.57 upgrade is checked against every command class for breaking API changes.
  Complexity: M

- [ ] P2 — F127 Refresh the dated research documents in docs/
  Why: every file in `docs/` except `release-readiness.md` carries a 2026-05-14 to 2026-05-17 evidence date, and several statements are now wrong: `research-delta-2026-05-14.md` names scrcpy v4.0 as current, and `competitor-research.md` predates One UI 8.5, One UI 9 and Smart Switch's wireless parity.
  Evidence: file headers in `docs/community-signal.md`, `docs/competitor-research.md`, `docs/migration-feasibility.md`, `docs/oss-dependencies.md`, `docs/oss-references.md`, `docs/research-delta-2026-05-14.md`; scrcpy v4.1 published 2026-07-12; App Manager v4.1.1 published 2026-09-04.
  Touches: `docs/*.md`
  Acceptance: each doc either carries a refreshed evidence date with corrected version numbers, or a dated header stating it is a historical snapshot and pointing at RESEARCH.md; no doc states a version number contradicted by RESEARCH.md.
  Complexity: S

- [ ] P2 — F128 Delete dead files and unreachable helper defaults
  Why: `PlaceholderView.xaml` is referenced only by generated `obj/` output because all seven tabs bind real views, `assets/helper-apk-stub/` is an empty directory, and `BaseHelperProvider.onQuery`'s `not-implemented` default is unreachable now that all seven providers override it.
  Evidence: repo search for `PlaceholderView` outside `obj/` finds no usage; `ls assets/helper-apk-stub` is empty; `helper-apk/app/src/main/java/.../providers/Providers.kt` overrides `onQuery` in every concrete provider.
  Touches: `src/PhoneFork.App/Views/PlaceholderView.xaml`, `src/PhoneFork.App/Views/PlaceholderView.xaml.cs`, `assets/helper-apk-stub/`, `helper-apk/app/src/main/java/.../providers/BaseHelperProvider.kt`
  Acceptance: the files are removed, `dotnet build -c Release` and the helper release build both succeed, and the helper's `not-implemented` default is either removed or documented as the guard for a future provider with a test that reaches it.
  Complexity: S

### P3

- [ ] P3 — F129 Persist window size, position and last-selected tab
  Why: nothing in `src/PhoneFork.App` reads or writes window state, so a migration session that spans several launches restarts at the default size and the Apps tab every time.
  Evidence: repo search for `WindowState`, `RestoreBounds` and `Properties.Settings` under `src/PhoneFork.App` returns nothing.
  Touches: `src/PhoneFork.App/Views/MainWindow.xaml.cs`, `src/PhoneFork.App/App.xaml.cs`
  Acceptance: window bounds, maximised state and the selected tab persist to a JSON file under `%LOCALAPPDATA%\PhoneFork\`; a saved position entirely off the current monitor set falls back to the primary display rather than opening off-screen.
  Complexity: S

- [ ] P3 — F130 Add a light theme alongside Catppuccin Mocha
  Why: the theme is a single hand-rolled dictionary with 58 colour and brush keys and no light variant, and every view already resolves colours through `StaticResource` with zero hardcoded hex, so the swap is cheap.
  Evidence: `src/PhoneFork.App/Themes/CatppuccinMocha.xaml` is the only theme file; zero hex literals across `src/PhoneFork.App/Views/*.xaml`.
  Touches: new `src/PhoneFork.App/Themes/CatppuccinLatte.xaml`, `src/PhoneFork.App/App.xaml`, `src/PhoneFork.App/Views/MainWindow.xaml`
  Acceptance: a light dictionary defines every key the dark one defines; a toggle switches at runtime with no restart; the choice persists with F129's window state; no view renders unreadable text in either theme.
  Complexity: M
