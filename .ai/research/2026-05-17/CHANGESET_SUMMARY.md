# Changeset Summary - 2026-05-17

## Files Created

- `PROJECT_CONTEXT.md`: canonical project context for future sessions.
- `.ai/research/2026-05-17/STATE_OF_REPO.md`: local repository reconnaissance and verification snapshot.
- `.ai/research/2026-05-17/MEMORY_CONSOLIDATION.md`: instruction/memory inventory, stale claims, conflicts, and consolidation decisions.
- `.ai/research/2026-05-17/SOURCE_REGISTER.md`: local, memory, official, OSS, commercial, and platform source IDs.
- `.ai/research/2026-05-17/RESEARCH_LOG.md`: search strategy, tools, failed searches, and saturation notes.
- `.ai/research/2026-05-17/COMPETITOR_MATRIX.md`: OSS, commercial, and platform comparator analysis.
- `.ai/research/2026-05-17/FEATURE_BACKLOG.md`: raw harvested ideas before prioritization.
- `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md`: scored and tiered roadmap candidates.
- `.ai/research/2026-05-17/SECURITY_AND_DEPENDENCY_REVIEW.md`: security, dependency, helper APK, wireless ADB, privacy, and signing review.
- `.ai/research/2026-05-17/DATASET_MODEL_INTEGRATION_REVIEW.md`: datasets, integrations, backup formats, and model/AI non-recommendation.

## Files Modified

- `ROADMAP.md`: replaced stale 2026.05.16b roadmap with a reconciled 2026.05.17 roadmap tied to source IDs and current `v0.9.0-pre` state.
- `README.md`: synchronized version badge from `0.8.0` to `0.9.0-pre`, corrected install guidance for the no-release state, and narrowed Wi-Fi/helper wording to current capability.
- `src/PhoneFork.App/Views/MainWindow.xaml`: synchronized WPF title to `v0.9.0-pre`.
- `src/PhoneFork.App/app.manifest`: synchronized assembly identity to `0.9.0.0`.
- `src/PhoneFork.Cli/Commands/SmartSwitchCommand.cs`: updated command override signature to the current Spectre.Console cancellation-token API.
- `src/PhoneFork.Cli/Commands/TrustedCommands.cs`: updated trusted-list, trusted-forget, and burst-mode command override signatures to the current Spectre.Console cancellation-token API.

## Key Corrections

- Treated helper APK provider bodies as current stubs instead of repeating the old "no production stubs" roadmap claim.
- Updated Samsung Gallery/OneDrive planning around the official September 30, 2026 direct-sync cutoff.
- Added Samsung Messages July 2026 transition as a concrete SMS pre-flight roadmap item.
- Preserved the distinction between local ADB/helper work and official platform handoffs.
- Repaired a Release build break in recent CLI commands caused by stale Spectre.Console `Execute` override signatures.

## Verification

- `git diff --check`: passed; Git reported CRLF normalization warnings only.
- `dotnet list PhoneFork.slnx package --vulnerable --include-transitive`: passed; no vulnerable packages reported.
- `dotnet list PhoneFork.slnx package --deprecated`: passed with one known test dependency warning, `xunit` 2.9.3 flagged legacy with `xunit.v3` as the alternative.
- `dotnet list PhoneFork.slnx package --outdated`: completed; upgrade candidates recorded in `SECURITY_AND_DEPENDENCY_REVIEW.md`.
- `dotnet build PhoneFork.slnx -c Release`: passed after the CLI command signature fix.
- `dotnet test tests\PhoneFork.Core.Tests\PhoneFork.Core.Tests.csproj -c Release --no-build`: passed, 122 tests.

## Continuation Implementation - R001/R002

Additional files created or modified after the research commit:

- `helper-apk/app/src/main/java/com/sysadmindoc/phonefork/helper/providers/BaseHelperProvider.kt`: changed health/default responses to the `phonefork.helper.v1` envelope.
- `helper-apk/app/src/main/java/com/sysadmindoc/phonefork/helper/providers/Providers.kt`: implemented SMS, call log, contacts, Wi-Fi capability metadata, wallpaper metadata, ringtone defaults, and dictionary export bodies with pagination and guarded restore endpoints.
- `src/PhoneFork.Core/Services/HelperProviderContract.cs`: added typed host parser, URI builder, and content-query JSON extraction for helper envelopes.
- `src/PhoneFork.Core/Services/HelperAppService.cs`: added typed provider querying and audit-scoped helper calls.
- `tests/PhoneFork.Core.Tests/HelperAppServiceTests.cs`: added malformed JSON, empty response, pagination, capability, and URI contract coverage.
- `helper-apk/build.gradle.kts` and `helper-apk/app/build.gradle.kts`: updated the helper stack to AGP 8.13.2 and Kotlin 2.3.21 with Java 17 target settings.
- `.github/workflows/ci.yml`: replaced tree-only helper validation with debug/release assembly, release metadata verification, CI debug-keystore signing for staging verification, signature verification, and artifact upload.
- `scripts/Stage-HelperApk.ps1`: added the verified staging gate used before the host consumes `assets/helper/PhoneForkHelper.apk`.
- `src/PhoneFork.Cli/PhoneFork.Cli.csproj` and `src/PhoneFork.App/PhoneFork.App.csproj`: copy a staged helper artifact into host outputs when present.
- `helper-apk/README.md`, `PROJECT_CONTEXT.md`, `ROADMAP.md`, `SOURCE_REGISTER.md`, and this file: reconciled roadmap and memory state with the implemented helper provider/CI path.

R001 is complete for export/provider contract behavior. R002 is complete for CI build, metadata verification, and verified host staging; release signing remains explicitly tracked under R004/R011.

Continuation verification:

- `dotnet build PhoneFork.slnx -c Release`: passed.
- `dotnet test tests\PhoneFork.Core.Tests\PhoneFork.Core.Tests.csproj -c Release --no-build`: passed, 129 tests.
- `gradle --no-daemon :app:assembleDebug :app:assembleRelease` in `helper-apk`: passed with Gradle 8.13 and Android SDK 36.
- `scripts/Stage-HelperApk.ps1` against a CI debug-keystore-signed release APK: passed and produced `artifacts/helper/PhoneForkHelper.apk` plus metadata and SHA-256 files.
- `aapt dump badging helper-apk/app/build/outputs/apk/release/app-release-unsigned.apk`: confirmed package `com.sysadmindoc.phonefork.helper`, versionCode `2`, versionName `0.9.0-pre`, minSdk `30`, and targetSdk `36`.
- `apksigner verify --print-certs artifacts/helper/PhoneForkHelper.apk`: passed with the local Android debug certificate for verification-only staging.
- `dotnet list PhoneFork.slnx package --vulnerable --include-transitive`: passed; no vulnerable packages reported.
- `git diff --check`: passed; Git reported CRLF normalization warnings only.

## Continuation Implementation - R003 Slice

Additional WPF parity work after R001/R002:

- `src/PhoneFork.App/ViewModels/OperationsViewModel.cs`: added a WPF view-model for helper install/probe/uninstall, Shizuku checks, Smart Switch detection, trusted-pair visibility, ADB Burst Mode toggle/status, pre-flight bundles, backup interop inspection, and media size/mtime verification.
- `src/PhoneFork.App/Views/OperationsView.xaml` and `.xaml.cs`: added the Operations tab UI with status rows, pre-flight findings, trusted-pair rows, helper controls, backup inspection path input, and action buttons.
- `src/PhoneFork.App/ViewModels/MainViewModel.cs` and `src/PhoneFork.App/Views/MainWindow.xaml`: wired the Operations tab into the WPF shell and synchronized the visible header version string to `v0.9.0-pre`.
- `ROADMAP.md` and `PROJECT_CONTEXT.md`: marked R003 as in progress with the completed WPF parity slice and remaining UI depth.

Verification:

- `dotnet build PhoneFork.slnx -c Release`: passed after adding the Operations tab.
- `dotnet test tests\PhoneFork.Core.Tests\PhoneFork.Core.Tests.csproj -c Release --no-build`: passed, 129 tests.
- `dotnet list PhoneFork.slnx package --vulnerable --include-transitive`: passed; no vulnerable packages reported.
- `git diff --check`: passed; Git reported CRLF normalization warnings only.
