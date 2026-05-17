# Research Log - 2026-05-17

## Objective

Build a durable, source-backed understanding of PhoneFork's current state,
external ecosystem, risks, dependencies, and next roadmap. This run focused on
reconciling local repo facts against stale memory and external 2026 platform
changes.

## Local Reconnaissance

Commands and files used:

- `git status --short --branch`
- `git log -10 --oneline --decorate`
- `git ls-files`
- `gh repo view SysAdminDoc/PhoneFork`
- `dotnet --info`
- `dotnet list PhoneFork.slnx package --vulnerable --include-transitive`
- `dotnet list PhoneFork.slnx package --deprecated`
- `dotnet list PhoneFork.slnx package --outdated`
- `rg --files`
- `rg -n "not-implemented|work in progress|stub|placeholder|TODO|FIXME|HACK|XXX"`

Important local files read:

- `README.md`, `CHANGELOG.md`, old `ROADMAP.md`, `CONTRIBUTING.md`
- `src/PhoneFork.Core/**/*.cs`
- `src/PhoneFork.Cli/Program.cs`
- `src/PhoneFork.App/**/*.xaml`
- `.github/workflows/ci.yml`
- `.github/workflows/release.yml`
- `helper-apk/**`
- `docs/*.md`

Findings:

- Current code is `v0.9.0-pre`, not the older v0.8.0 state in local memory.
- Helper provider bodies are real stubs and must be treated as current P0 work.
- CI does not yet assemble the helper APK.
- No vulnerable NuGet packages were detected.
- Package update and xUnit legacy signals are real but secondary to release
  readiness and helper implementation.

## Memory And Instruction Pass

Inspected global Codex/Claude instruction files, shared memory index,
PhoneFork memory, C# and Android stack convention memory, repo-local ignored
`CLAUDE.md`, and existing repo docs/roadmap.

Reconciled:

- Durable project facts moved into `PROJECT_CONTEXT.md`.
- Stale or contradictory claims recorded in `MEMORY_CONSOLIDATION.md`.
- Tool-specific instructions were not merged away.
- The user's direct requirement for `.ai/research/<date>/` artifacts was treated
  as higher priority than the general "no AI references in repos" note.

## External Search Passes

Source classes:

- Official Android policy and tooling docs.
- Official Android Security Bulletin and NVD CVE record.
- Samsung support/application pages.
- Microsoft OneDrive and Artifact Signing docs.
- Apple Android transfer support.
- GitHub repo stats, releases, and issue signals.
- Commercial transfer-tool landing pages and support pages.
- OSS backup, ADB, Shizuku, debloat, file transfer, and SMS-provider projects.

Representative queries:

- `Android developer verification ADB install FAQ`
- `Android platform-tools release notes 37.0.0 adb wireless debugging mDNS`
- `CVE-2026-0073 adbd Android Security Bulletin May 2026`
- `Samsung Messages discontinued July 2026`
- `Samsung Gallery OneDrive sync September 30 2026`
- `Android Quick Share AirDrop iPhone QR 24 hours`
- `iOS 26.3 transfer to Android 17 eSIM`
- `Azure Artifact Signing pricing Basic Premium`
- `AppManager Shizuku issue backup Android`
- `UAD-NG One UI 8.5 smartsuggestions`
- `open-android-backup restore issue adb no devices`
- `ADB Explorer retry thumbnails WPF`

Tools:

- Web search/open for official docs and support pages.
- `gh api repos/<owner>/<repo>` for stars, pushed date, default branch, and
  activity snapshots.
- `gh api repos/<owner>/<repo>/releases/latest` for latest release snapshots.
- `gh issue list --search` for current issue signals.

## Saturation Test

The research was considered saturated for this planning run after:

- Official Android, Samsung, Microsoft, Apple, GitHub, and CA/B Forum sources
  covered the major platform/security/release claims.
- Existing repo docs covered prior commercial and community research.
- GitHub lookup covered the main OSS comparators in ADB, Shizuku, backup,
  debloat, local transfer, and SMS/provider architecture.
- Follow-up searches did not change the top priorities: helper provider bodies,
  helper CI, WPF surfacing, release readiness, Samsung Messages, OneDrive
  cutoff, media resilience, debloat overlay, signing/provenance, and dependency
  maintenance.

## Failed Or Limited Searches

- `rtk` required by global instructions was not installed in this shell.
- No hardware devices were available, so ADB/device behavior was not validated.
- GitHub stats are point-in-time and may drift quickly.
- Commercial product claims are marketing claims unless backed by official
  documentation; roadmap items use them as feature-positioning evidence, not
  correctness evidence.
- Some sources from prior docs may be stale and should be refreshed before
  acting on them months later.

## Source Handling

All source IDs are registered in `SOURCE_REGISTER.md`. Roadmap and research
claims use those IDs instead of repeating every URL inline.
