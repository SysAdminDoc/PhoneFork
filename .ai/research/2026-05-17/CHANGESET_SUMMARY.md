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
