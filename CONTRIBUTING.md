# Contributing to PhoneFork

Thanks for your interest. PhoneFork is a small, focused tool, so the bar is "would this change make a Samsung→Samsung migration meaningfully more honest, safer, or faster?" Anything else is welcome as a Discussion before code lands.

## Filing issues

GitHub Issues is open. Please include:

- **Phone hardware**: source / destination models, Android version, One UI version.
- **PhoneFork version**: from the top of the WPF window or `phonefork --version`.
- **Steps to reproduce** as a numbered list.
- **Audit log excerpt** from `%LOCALAPPDATA%\PhoneFork\logs\audit-YYYY-MM-DD.log`. Device serials are SHA-256-hashed on disk (F006), so logs are safe to attach as-is.

If you're reporting a wireless-ADB or pairing problem, also include the output of `phonefork mdns services` and `phonefork shizuku status -d <serial>`.

## Building

```bash
git clone https://github.com/SysAdminDoc/PhoneFork.git
cd PhoneFork

# Host (.NET 10):
dotnet restore PhoneFork.slnx
dotnet build PhoneFork.slnx -c Release
dotnet test tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj -c Release

# Helper APK (Android, Kotlin/Gradle):
cd helper-apk
./gradlew assembleRelease     # JDK 21, compileSdk 36, targetSdk 36
# Output: app/build/outputs/apk/release/PhoneForkHelper-release-unsigned.apk
```

## Project layout

| Path | What it is |
|---|---|
| `src/PhoneFork.Core/` | ADB host, services, models. **No WPF/WinUI refs.** Reference from both the GUI and the CLI. |
| `src/PhoneFork.App/` | WPF GUI, ViewModels, Views, Catppuccin Mocha theme. |
| `src/PhoneFork.Cli/` | Spectre.Console.Cli console, subcommand classes. |
| `tests/PhoneFork.Core.Tests/` | xUnit. Live runs against device hardware are out of scope; unit tests only. |
| `helper-apk/` | Kotlin/Gradle companion APK (v0.7.0+, F010). |
| `assets/debloat/` | UAD-NG / AppManagerNG dataset JSON (5 buckets) + per-OS overrides overlay (F102). |
| `tools/` | Bundled `adb.exe` + Windows ADB DLLs (Apache-2.0). |

## Code style

- C# 14 / .NET 10. `<LangVersion>latest</LangVersion>` everywhere.
- `Nullable` enabled. Don't add `!` operator unless the assertion is genuinely safe.
- Prefer `partial` MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) for new VMs. CommunityToolkit.Mvvm 8.4.2+.
- Shell commands constructed in C# must go through `AdbShell.Arg()` / `AdbShell.PackageArg()`. Never inline an untrusted string into a shell command.
- Local file paths derived from serials / package IDs / APK names go through `LocalPathNames` (the sanitizer).
- One-line comments only. Doc comments are fine; multi-paragraph prose belongs in `docs/` or this file.

## Tests

Every new service or pure function gets a test. The current suite is 85+ tests at ~150 ms total — keep it fast.

```bash
dotnet test tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj -c Release
```

`InternalsVisibleTo="PhoneFork.Core.Tests"` is set on the Core project so tests can reach `internal` helpers without making them public.

## Commit style

Conventional commits, e.g.:

- `feat(wireless): per-install ADB key directory (F002)`
- `fix(media): mtime preservation for empty directories`
- `docs: refresh ROADMAP after v0.7.0 ship`
- `chore(deps): Serilog 4.2 → 4.3.1`

Tag the roadmap feature ID (`F###`) in the commit body where applicable so the roadmap line can be checked off mechanically.

## CI

`.github/workflows/ci.yml` runs on every push / PR: restore, build, test, vulnerable-package scan. `.github/workflows/release.yml` triggers on a `v*` tag and publishes signed ZIPs with SLSA build provenance.

If your PR touches:

- `src/PhoneFork.Core/Services/` — expect new unit tests.
- `src/PhoneFork.App/Views/` — include a screenshot for any visible change.
- `assets/debloat/overrides.json` — link the UAD-NG issue or breakage report driving the override.
- `helper-apk/` — bump `versionCode` in `app/build.gradle.kts`.

## Out-of-scope

Per the [ROADMAP](ROADMAP.md) rejected list (X001–X020), the following won't be merged:

- Claims of full third-party private app-data extraction without root.
- Re-implementations of Smart Switch's AOAP wire protocol.
- Telemetry or cloud-bound features.
- Bootloader-unlock guidance (One UI 8.5 removed the toggle entirely on S25/S26).
- iPhone source migration before v1 — Android 17's official tool covers that surface from July 2026.

Thanks for reading.
