# State Of Repo - 2026-05-17

## Snapshot

- Working directory: `C:\Users\--\repos\PhoneFork`.
- Branch: `main`.
- Upstream: `origin/main`.
- Pre-run branch state: `main` was ahead of `origin/main` by 4 commits.
- Recent HEAD before this research change: `9a11178 feat(v0.9.0-pre): backup format detection layer + cross-platform metadata`.
- Remote: `https://github.com/SysAdminDoc/PhoneFork.git`.
- GitHub metadata checked with `gh repo view`: public repo, MIT project, default branch `main`, 0 stars, 0 issues, 0 pull requests, no latest release.
- Local `rtk` command required by global notes is not installed; `git log -10 --oneline --decorate` was used instead.

## Recent Commits

```text
9a11178 feat(v0.9.0-pre): backup format detection layer + cross-platform metadata
5fe4d6a feat(v0.8.0): Smart Switch interop + backup interop + pre-flight + verify + burst
09a84c4 feat(v0.7.0): helper companion APK foundations
0bfc12c feat(v0.6.9): Trust And Maintenance Gate
a59d71d docs: refresh research roadmap
0d074b0 branding: add 512/1024 RGBA app icons + banner
dc4af68 docs(roadmap): refresh 2026.05.14b after v0.6.5 ship + delta research
0127d4d Harden ADB shell and local path handling
d7ba677 Add PhoneFork branding prompts
6edf6ed Fix WPF shell theme polish
```

## File Inventory

`git ls-files` reported 183 tracked files before the new research artifacts.

Tracked source/doc line counts:

| Extension | Files | Lines |
|---|---:|---:|
| `.cs` | 125 | 9,182 |
| `.md` | 13 | 1,631 |
| `.xaml` | 11 | 1,450 |
| `.json` | 6 | 31,993 |
| `.kt` | 3 | 155 |
| `.kts` | 3 | 63 |
| `.xml` | 2 | 73 |
| `.yml` | 2 | 134 |
| `.manifest` | 1 | 27 |
| `.pro` | 1 | 3 |
| `.properties` | 1 | 6 |

## Solution And Projects

- `PhoneFork.slnx`
- `src/PhoneFork.Core/PhoneFork.Core.csproj`: `net10.0`, Core library.
- `src/PhoneFork.App/PhoneFork.App.csproj`: `net10.0-windows`, WPF app.
- `src/PhoneFork.Cli/PhoneFork.Cli.csproj`: `net10.0`, CLI assembly `phonefork`.
- `tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj`: `net10.0`, xUnit tests.
- `helper-apk`: Android Gradle/Kotlin helper app scaffold.

## Runtime And Package State

`dotnet --info` reported SDK `10.0.202`, MSBuild `18.3.3`, host runtime `10.0.7`, Windows `10.0.26100`, and Android/iOS/MacCatalyst/MAUI Windows workloads.

`dotnet list PhoneFork.slnx package --vulnerable --include-transitive` found no vulnerable packages.

`dotnet list PhoneFork.slnx package --deprecated` found no deprecated packages in App, CLI, or Core. Tests use `xunit` 2.9.3, which NuGet flags as legacy with `xunit.v3` as the alternative.

Current package upgrade candidates:

| Project | Package | Current | Latest |
|---|---|---:|---:|
| App/Core | QRCoder | 1.6.0 | 1.8.0 |
| CLI | Spectre.Console | 0.55.0 | 0.55.2 |
| Core | JsonSchema.Net | 7.3.0 | 9.2.1 |
| Core | Serilog.Sinks.File | 6.0.0 | 7.0.0 |
| Tests | coverlet.collector | 6.0.4 | 10.0.0 |
| Tests | Microsoft.NET.Test.Sdk | 18.0.1 | 18.5.1 |

## Current Feature Surfaces

Core services cover ADB host/pairing/burst, shell quoting, Android backup sniffing, AppManager backup writer/reader/spec, AppProcess agent, helper APK lifecycle, Shizuku status, Smart Switch detection, sandbox parser, media manifest/diff/sync/integrity, settings snapshot/diff/apply, debloat dataset/scanner/service, roles, Wi-Fi QR/list, CSC diff, pre-flight, Samsung honesty, security posture, trusted pairs, serial hashing, and wireless policy.

CLI commands cover devices, apps, media, settings, debloat, Wi-Fi, CSC, roles, permissions, pair/connect/disconnect, mDNS, honesty, helper install/uninstall/probe/residue, Shizuku status, Smart Switch detect, trusted list/forget, and burst mode.

## Important Findings

1. `CHANGELOG.md` has advanced to `v0.9.0-pre`, but README badge, WPF title, and app manifest still carried older versions. This research pass synchronized them.
2. `ROADMAP.md` claimed there are no production stubs. That is stale: helper APK providers intentionally return `{"status":"not-implemented"}` until provider bodies land.
3. `helper-apk/README.md` confirms provider bodies and the JAR push-and-run path remain future work.
4. CI helper job is currently lint-only/tree-only; it does not assemble or sign the helper APK.
5. Release workflow is present and has Artifact Signing/provenance slots, but there are no tags or releases yet. README install copy was corrected to direct users to build from source until a release exists.
6. `dotnet list package --outdated` and `--vulnerable` should not be run concurrently against the solution in this environment. A concurrent first attempt failed with `Cannot create a file when that file already exists`; sequential reruns succeeded.
7. Release build initially failed because new CLI commands used the old Spectre.Console `Execute` override signatures. `SmartSwitchDetectCommand`, `TrustedListCommand`, `TrustedForgetCommand`, and `BurstModeCommand` were updated to the cancellation-token signatures used elsewhere in the CLI.

## Source Marker Scan

`rg -n "not-implemented|work in progress|stub|placeholder|TODO|FIXME|HACK|XXX"` found:

- `helper-apk/README.md`: helper is work in progress and providers are stubs.
- `helper-apk/app/src/main/java/.../BaseHelperProvider.kt`: default provider body returns `status:not-implemented`.
- `CHANGELOG.md`: documents helper provider stubs.
- Old `ROADMAP.md`: stale "no stub functions" claim.

## Verification Status

Build/test verification was run after artifact creation and the CLI signature fix; see `CHANGESET_SUMMARY.md` for final command outcomes.
