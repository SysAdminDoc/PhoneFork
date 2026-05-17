# PhoneFork Project Context

Verified: 2026-05-17

## Project Identity

PhoneFork is a Windows-first, local-only Android migration cockpit for Samsung Galaxy devices. It drives two ADB-connected phones at the same time and migrates the parts Android allows without root: apps and split APKs, media, selected settings, Wi-Fi QR workflows, default app roles, reversible debloat, trust posture checks, Smart Switch handoff detection, and backup-format interop.

The product stance is deliberately narrow and honest: no root requirement, no cloud dependency, no Samsung account requirement, no telemetry, and no promise to copy third-party private app data from `/data/data`. Samsung Smart Switch remains the complement for Knox-bound and OEM-private categories.

## Current Repository State

- Repo: `SysAdminDoc/PhoneFork`, public, MIT, default branch `main`.
- Local branch: `main`; check `git status --short --branch` live before acting.
- Current product state: `v0.9.0-pre` per `CHANGELOG.md`.
- Visible version strings synced on 2026-05-17: README badge `0.9.0-pre`, WPF title `v0.9.0-pre`, app manifest `0.9.0.0`.
- Unsigned GitHub prerelease `v0.9.0-pre` is published with WPF/CLI ZIPs and an `ARTIFACT-TRUST.txt` note. The future tag workflow now emits an SPDX SBOM, SHA-256 checksums, provenance/SBOM attestations, and signs Windows EXE/DLL payloads when Azure Artifact Signing secrets are present.
- Local `rtk` command required by global instructions is not installed in this shell; use plain `git`, `gh`, and `dotnet` unless `rtk` becomes available.

## Stack

- Host: C# / .NET 10 / WPF / MVVM.
- CLI: Spectre.Console.Cli.
- Core ADB library: AdvancedSharpAdbClient 3.6.16.
- ADB binary: bundled Google platform-tools `tools/adb.exe` and Windows DLLs.
- APK parsing: AlphaOmega.ApkReader 2.0.10.
- Logging: Serilog + CompactJsonFormatter NDJSON audit logs.
- QR: QRCoder.
- Helper APK: Kotlin 2.3.21 / Android Gradle Plugin 8.13.2 / targetSdk 36 / minSdk 30.
- Tests: xUnit Core tests under `tests/PhoneFork.Core.Tests`.

## Current Architecture

- `src/PhoneFork.Core`: ADB host, shell quoting, device inventory, app migration, media diff/sync, settings diff/apply, debloat dataset, wireless ADB policy, trusted-pair registry, Smart Switch detection, backup interop, pre-flight, integrity verification, and helper lifecycle services.
- `src/PhoneFork.App`: WPF shell, Catppuccin Mocha theme, device bar, migration tabs, and an Operations tab for helper/Shizuku/Smart Switch/pre-flight/backup/media-integrity/trust/burst-mode workflows.
- `src/PhoneFork.Cli`: scriptable command surface for devices, apps, media, settings, debloat, backup inspect/AppManager export/install, Wi-Fi, CSC, roles, permissions, wireless ADB pairing/connect/disconnect, mDNS, honesty, helper lifecycle, Shizuku, Smart Switch, trusted pairs, and burst mode.
- `helper-apk`: companion Android APK with provider authorities and v1 JSON export envelopes for SMS, call log, contacts, Wi-Fi capability metadata, wallpaper metadata, ringtone defaults, and dictionary.
- `assets/debloat`: embedded AppManagerNG/UAD-NG package datasets plus PhoneFork overrides.
- `.github/workflows`: Windows .NET CI, Linux helper APK assemble/metadata/staging CI, and release packaging with Artifact Signing, SBOM, checksum, release-note, and provenance/SBOM attestation steps.

## Shipped Capability Summary

- Apps: enumerates third-party packages, pulls all split APK paths, stages safely on Windows, logs pulled APK byte counts and SHA-256 hashes, installs via session-based multi-APK install with Play attribution, and can report per-app APK/private-data/OBB transfer posture through the CLI.
- Media: manifests `/sdcard` categories, diffs source/destination, syncs pull-then-push, preserves mtime, supports delete/update/conflict policy, writes resumable checkpoints and evidence reports, emits huge-file/Quick Share advisories, and has size+mtime/CRC32/SHA-256 integrity verification primitives.
- Settings: snapshots AOSP namespaces, diffs them, applies selected values behind a safety blocklist.
- Debloat: embeds a 5,481-entry dataset, disables packages only, writes rollback snapshots, applies source-backed OEM/One UI override metadata, and lets CLI scans/applies load checksummed out-of-band overlay feeds.
- Wi-Fi: enumerates SSIDs where shell permits, renders QR PNG/SVG, and surfaces CSC mismatch.
- Roles and permissions: snapshots and applies AOSP default roles, runtime permissions, and appops.
- Wireless ADB: supports Android 11+ pairing/connect/disconnect, mDNS discovery, per-install ADB home, patch-level gate for CVE-2026-0073, trusted-pair registry with hashed serials, session timeout, and kill switch.
- Honesty/pre-flight: probes Samsung Pass/Wallet/Secure Folder/Routines/Notes/Gallery/OneDrive/Samsung Account, Samsung Messages/Google Messages/default SMS posture, OneDrive camera-backup account/permission/storage posture, CSC, security patch level, OEM unlock, Knox, and destination posture.
- Backup interop: AppManager-compatible v5 writer/reader with SHA-256 checksums, CLI inspect/export/install commands, retention sweeper, Android `.ab` sniffer, Open Android Backup sniffer, and open archive metadata including Android 16 QPR2 cross-platform-transfer posture.

## Known Gaps

- Helper APK restore writes are still guarded and intentionally disabled until the host workflow can sequence default-app and destructive-action confirmation safely.
- Helper APK release signing is not wired; CI builds debug and unsigned release APKs, signs the release APK with the CI debug keystore for verification-only staging, and exercises the verified staging path.
- WPF UI now exposes a first Operations surface for several v0.7.0-v0.9.0-pre services, but deeper rollback/audit drilldowns and archive import/export actions remain CLI-first.
- No signed release exists yet; README now distinguishes the unsigned prerelease from source builds and explains that even signed new-publisher builds may still trigger SmartScreen until reputation builds.
- Signing secrets are intentionally not provisioned.
- Release readiness notes live in `docs/release-readiness.md`; the first prerelease path is clearly unsigned unless Azure Artifact Signing secrets are provisioned.
- Version consistency is guarded by `scripts/Test-VersionConsistency.ps1` and CI.
- Current dependency scan is clean for vulnerabilities and outdated package reports after R012; test `xunit` is still flagged legacy and the xUnit v3 migration remains deferred.
- Hardware validation was not available in this research session.

## High-Value Guardrails

- Keep `AdbShell.Arg`, `AdbShell.PackageArg`, and `AdbShell.IsPackageName` as the shared Android shell boundary. Do not hand-build unquoted device or package shell strings.
- Keep Windows-safe local staging in `LocalPathNames.SafeFileName` and `LocalPathNames.CombineSafeRelativePath`.
- Store raw device serials only where absolutely necessary; logs and trust registries should use `SerialHash`.
- Keep debloat default action reversible (`pm disable-user --user 0`), not uninstall.
- Treat wireless ADB as USB-first, opt-in, and patch-gated.
- Do not claim private app data migration without root. Use honesty reports and official app handoff workflows instead.
- Preserve the WPF host for v1; Avalonia is a v2 portability track.

## Verification Commands

Use these as the standard local gate after source changes:

```powershell
dotnet restore PhoneFork.slnx
pwsh scripts/Test-VersionConsistency.ps1
dotnet build PhoneFork.slnx -c Release --no-restore
dotnet test tests/PhoneFork.Core.Tests/PhoneFork.Core.Tests.csproj -c Release --no-build
dotnet list PhoneFork.slnx package --vulnerable --include-transitive
dotnet list PhoneFork.slnx package --outdated
```

Release SBOM smoke:

```powershell
Compress-Archive -Path artifacts\publish\wpf\* -DestinationPath artifacts\PhoneFork-local-wpf-win-x64.zip
Compress-Archive -Path artifacts\publish\cli\* -DestinationPath artifacts\PhoneFork-local-cli-win-x64.zip
& .\scripts\New-ReleaseSbom.ps1 -Version local -OutputPath artifacts\release-sbom.spdx.json -ArtifactPath @("artifacts\PhoneFork-local-wpf-win-x64.zip", "artifacts\PhoneFork-local-cli-win-x64.zip")
```

After signing a helper release APK, stage it for host packaging with:

```powershell
pwsh scripts/Stage-HelperApk.ps1 -ApkPath PhoneForkHelper.apk -OutputDirectory assets/helper
```

Useful smoke commands when a device or host UI is available:

```powershell
dotnet run --project src\PhoneFork.Cli -c Release --no-build -- devices
dotnet run --project src\PhoneFork.Cli -c Release --no-build -- apps report --device <serial> --json
dotnet run --project src\PhoneFork.Cli -c Release --no-build -- backup inspect scratch\cli-backup-smoke --json
dotnet run --project src\PhoneFork.Cli -c Release --no-build -- wifi qr --ssid AuditTest --psk abc123 -o scratch\audit-test.png
dotnet run --project src\PhoneFork.App -c Release --no-build
```

## Current Planning Artifacts

- `ROADMAP.md`: prioritized, sourced execution plan.
- `.ai/research/2026-05-17/STATE_OF_REPO.md`: current repo audit.
- `.ai/research/2026-05-17/MEMORY_CONSOLIDATION.md`: instruction and memory reconciliation.
- `.ai/research/2026-05-17/SOURCE_REGISTER.md`: source inventory.
- `.ai/research/2026-05-17/FEATURE_BACKLOG.md`: raw opportunity harvest.
- `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md`: scored roadmap candidates.
