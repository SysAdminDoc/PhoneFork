# PhoneFork Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

Added 2026-09-04. Evidence and full reasoning in RESEARCH.md. IDs continue the existing F### scheme (highest prior ID: F108).

### P0

### P1

### P2

- [ ] P2 — F120 Fix the helper provider pagination boundary
  Why: `exportRows` calls `c.moveToNext()` inside the `items.length() >= limit` guard before breaking, which advances the cursor an extra row and perturbs `totalSeen`, so `nextOffset` can skip a record at every page boundary. On a several-thousand-message SMS export that silently drops data.
  Evidence: `helper-apk/app/src/main/java/.../providers/Providers.kt`, `exportRows` loop and `nextOffset` computation.
  Touches: `helper-apk/app/src/main/java/.../providers/Providers.kt`, new `helper-apk/app/src/test/`
  Acceptance: a test over a fake cursor of 1,001 rows paged at `limit=500` returns 1,001 distinct ids across three pages with no gaps and no repeats; the same test fails if the extra `moveToNext()` is restored.
  Complexity: S

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
