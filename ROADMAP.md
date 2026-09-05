# PhoneFork Roadmap

Actionable work only. Historical and completed roadmap material is archived in CHANGELOG.md; blocked work is kept in Roadmap_Blocked.md.

## Actionable Items

## Research-Driven Additions

Added 2026-09-04. Evidence and full reasoning in RESEARCH.md. IDs continue the existing F### scheme (highest prior ID: F108).

### P0

### P1

### P2

- [ ] P2 — F124 Add an ADB transport fake and test the App and Cli projects
  Why: all 163 tests are pure Core unit tests with no transport double, and neither `src/PhoneFork.App` nor `src/PhoneFork.Cli` has a test project. That is why the missing cancel affordance and the unreachable helper export both went unnoticed.
  Evidence: `dotnet test -c Release` on 2026-09-04 reports 163 passed in 242 ms from a single assembly, `PhoneFork.Core.Tests.dll`; `tests/` contains one project.
  Touches: new `tests/PhoneFork.App.Tests/`, new `tests/PhoneFork.Cli.Tests/`, a shared fake `IAdbClient`, `PhoneFork.slnx`
  Acceptance: a fake `IAdbClient` replays recorded shell output; tests cover the apps migrate, settings apply and debloat apply view-model flows including a cancellation case, and cover CLI exit codes for at least the apps, settings, debloat and helper branches; the suite runs with no device attached.
  Complexity: L

### P3

- [ ] P3 — F130 Add a light theme alongside Catppuccin Mocha
  Why: the theme is a single hand-rolled dictionary with 58 colour and brush keys and no light variant, and every view already resolves colours through `StaticResource` with zero hardcoded hex, so the swap is cheap.
  Evidence: `src/PhoneFork.App/Themes/CatppuccinMocha.xaml` is the only theme file; zero hex literals across `src/PhoneFork.App/Views/*.xaml`.
  Touches: new `src/PhoneFork.App/Themes/CatppuccinLatte.xaml`, `src/PhoneFork.App/App.xaml`, `src/PhoneFork.App/Views/MainWindow.xaml`
  Acceptance: a light dictionary defines every key the dark one defines; a toggle switches at runtime with no restart; the choice persists with F129's window state; no view renders unreadable text in either theme.
  Complexity: M
