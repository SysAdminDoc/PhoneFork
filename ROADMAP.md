# PhoneFork Roadmap

Version: 2026.05.17

This roadmap is a point-in-time plan built from local repository reconnaissance,
project memory consolidation, dependency checks, GitHub competitor inspection,
and official Android/Samsung/Microsoft/Apple/GitHub/Microsoft Artifact Signing
sources. Source IDs refer to `.ai/research/2026-05-17/SOURCE_REGISTER.md`.

## Product Thesis

PhoneFork should be the local-first Windows migration cockpit for Samsung Galaxy
users who need an honest, auditable transfer plan instead of a black-box cloud
or account-bound handoff. It should copy what Android permits without root,
explain what it cannot copy, provide safe handoffs to official tools where they
are better, and preserve reversibility for every destructive or privileged
operation.

Guardrails:

- No root requirement and no promise to read third-party private app data.
- USB-first ADB with wireless ADB opt-in, session timeout, patch gating, and
  serial-hash logging.
- Smart Switch, Google Messages, OneDrive, Quick Share, Wallet, and Android/iOS
  first-party transfer flows are complements, not enemies.
- WPF remains the v1 host. Avalonia/WebUSB/iOS-source work is v2+ unless a
  smaller compatibility bridge becomes obvious.
- Every roadmap claim should map to a local source, official platform source,
  competitor signal, dependency scan, or marked hypothesis.

## Current State

| Area | Verified state |
|---|---|
| Version | `CHANGELOG.md` is at `v0.9.0-pre`; README badge, WPF title, and manifest were synced during this research pass. Sources: L04, L05. |
| Repo | `SysAdminDoc/PhoneFork`, public, MIT, default branch `main`; first unsigned prerelease `v0.9.0-pre` is published with WPF/CLI ZIPs and `ARTIFACT-TRUST.txt`. Sources: L03, L31. |
| Host stack | .NET 10, WPF, MVVM, Spectre CLI, AdvancedSharpAdbClient, bundled platform-tools. Sources: L08-L13, L26. |
| Helper APK | Gradle/Kotlin helper targets SDK 36 and now emits `phonefork.helper.v1` JSON envelopes for SMS, call log, contacts, Wi-Fi capability metadata, wallpaper metadata, ringtone defaults, and dictionary. Restore writes remain guarded/disabled. Sources: L14-L17. |
| CI/release | Windows build/test/vulnerable scan workflow exists; helper APK CI now assembles debug/release APKs, verifies metadata/signature, and uploads staged helper artifacts; release workflow has published the unsigned `v0.9.0-pre` prerelease and still has signing/attestation slots for signed releases. Sources: L18, L19, L31. |
| Dependency state | No vulnerable NuGet packages found; `xunit` 2.9.3 is flagged legacy; several package updates are available. Sources: L27-L29. |
| Research artifacts | Canonical project context and dated research set live in `PROJECT_CONTEXT.md` and `.ai/research/2026-05-17/`. |

## Research Deltas That Changed The Plan

1. Samsung Gallery direct OneDrive sync ends on September 30, 2026, not April
   2026. The pre-flight warning should point users to OneDrive camera backup and
   distinguish existing OneDrive files from Samsung Gallery sync. Source: S10.
2. Samsung Messages is being discontinued in the US market in July 2026, with
   Samsung directing users to Google Messages; Samsung says existing
   conversations transfer between those apps but may take up to about 24 hours.
   Source: S08.
3. Android developer verification explicitly exempts ADB installs, so
   PhoneFork's helper APK sideload path remains viable. Source: S01.
4. CVE-2026-0073 is a critical `adbd` remote/proximal code execution issue
   fixed by the 2026-05-01 patch level. PhoneFork's wireless ADB patch gate is a
   core safety feature. Sources: S05, S06.
5. Quick Share now covers Android, Chromebooks, select Windows PCs,
   AirDrop-capable Apple devices on supported Android phones, QR fallback,
   24-hour encrypted server-hosted transfers, and documented size/count limits.
   PhoneFork should recommend it for specific ad hoc categories instead of
   trying to own every file-transfer case. Sources: S11, S12.
6. Apple's Android transfer support now documents iOS 26.3/iPadOS 26.3 to
   Android 17 flows including eSIM, photos, contacts, calendars, call history,
   messages, accessibility settings, home screen layout, and wallpaper. This
   makes iOS-source support a watch item for v2, not a v1 dependency. Source:
   S14.
7. Artifact Signing is the practical Windows signing path for this repo; it does
   not issue EV certificates, but Basic is $9.99/month for 5,000 signatures and
   Premium is $99.99/month for 100,000 signatures. Sources: S15, S16.

## Now: v0.9.x Stabilization

### R001 - Implement the Helper APK provider contract

Priority: P0. Impact: 5. Effort: 5. Risk: 4.
Status: Completed 2026-05-17. Restore write bodies are deliberately disabled
behind guarded restore endpoints until the host has a full destructive-action
confirmation workflow.

Implement real provider bodies for SMS, call log, contacts, Wi-Fi metadata,
wallpaper, ringtones, and user dictionary behind the current shell/system UID
gate. Define versioned JSON envelopes, pagination, read/write modes, error
codes, source/destination capability probes, and host-side contract tests.

Acceptance:

- `content query .../health` and each category endpoint return versioned JSON.
- Provider bodies never expose data to non-shell callers.
- Host services parse helper responses through typed DTOs and audit each call
  with serial hashes.
- Restore endpoints require explicit per-category confirmation.
- Unit tests cover malformed JSON, denied caller, empty result, pagination, and
  category capability reports.

Sources: L15-L17, G20, G02, G03.

### R002 - Build and verify the helper APK in CI

Priority: P0. Impact: 4. Effort: 3. Risk: 3.
Status: Completed 2026-05-17 for CI build/metadata verification and verified
host staging. Release signing remains tracked under R004/R011.

Replace tree-only helper APK CI with a real `assembleRelease` or reproducible
equivalent. If Gradle wrapper jars remain intentionally untracked, document and
install the pinned Gradle runtime in CI. Add `apksigner verify --print-certs`
once release signing is configured.

Acceptance:

- CI compiles the helper APK on Linux or Windows.
- Build artifacts expose package name, versionCode, minSdk, targetSdk, and
  signing certificate summary.
- Host packaging consumes only a verified helper artifact.

Sources: L14, L18, L17.

### R003 - Surface v0.7-v0.9 Core services in WPF

Priority: P0. Impact: 4. Effort: 4. Risk: 3.
Status: Completed 2026-05-17. The Operations tab surfaces helper lifecycle,
Shizuku checks, Smart Switch detection, backup interop inspection, pre-flight
bundles, media size/mtime verification, trusted-pair visibility, and ADB Burst
Mode status/toggle. Deeper archive import/export actions remain tracked under
R006.

The CLI exposes more of the shipped Core surface than the WPF cockpit. Add
workflow panels for helper lifecycle, Shizuku status, Smart Switch detection,
backup interop, pre-flight bundles, media integrity verification, trusted pairs,
and burst mode.

Acceptance:

- A user can discover, install/probe/uninstall helper, check Shizuku, run
  pre-flight, inspect backup archives, and verify media without dropping to CLI.
- Busy, empty, warning, failure, and rollback states are visible.
- Dangerous actions require confirmation and leave audit logs.

Sources: L12, L13, L04, M01, M02.

### R004 - Release-readiness correction pass

Priority: P0. Impact: 4. Effort: 2. Risk: 2.
Status: Completed 2026-05-17 for the unsigned prerelease path. Local publish
docs/workflow trust notes are wired, screenshots are captured, publish outputs
verify locally, and `v0.9.0-pre` is published as an unsigned GitHub prerelease
because signing secrets are not provisioned.

Before the first public release, produce screenshots, verify publish outputs
locally, and tag a signed or clearly unsigned pre-release with matching
changelog.

Acceptance:

- README distinguishes the unsigned prerelease from source builds.
- `dotnet publish` produces WPF and CLI artifacts.
- Release notes include no unsupported migration claims.
- GitHub attestation and signing behavior is documented for unsigned builds and
  for future signed builds.

Sources: L03, L04, L19, S15, S16, S18.

### R005 - Add a version and documentation consistency gate

Priority: P0. Impact: 3. Effort: 2. Risk: 1.
Status: Completed 2026-05-17. `scripts/Test-VersionConsistency.ps1` checks the
changelog top version, README badge, WPF title/header, app manifest numeric
identity, helper APK versionName, and release workflow `v*` tag trigger.

The repo had three visible stale version strings. Add a small verification
script or test that compares changelog top version, README badge, app title,
manifest identity, and release workflow tag expectations.

Acceptance:

- The gate fails when visible version strings drift.
- The gate is cheap enough for CI and local pre-release runs.
- The rule allows `-pre` suffixes while using numeric manifests.

Sources: L04, L05, L09.

## Next: v1.0 Trustworthy Migration

### R006 - Finish backup archive interop beyond sniffing

Priority: P1. Impact: 5. Effort: 5. Risk: 4.
Status: Completed 2026-05-17 for a CLI-first AppManager APK/split
export/import workflow, offline metadata inspection, checksum verification, and
inspect-only `.ab`/Open Android Backup handling. Real-device export/install
validation remains under the hardware validation release gate.

Convert the current AppManager-compatible writer/reader and archive sniffers
into a user-visible import/export/inspect workflow. Treat Android `.ab`, Open
Android Backup, AppManager, and future cross-platform metadata as distinct
formats with honest capability matrices.

Acceptance:

- Inspect archive metadata without restoring.
- Verify checksums and list packages/categories before import.
- Support at least one full AppManager export/import path end to end.
- `.ab` and Open Android Backup formats may remain inspect-only until restore
  semantics are proven.

Sources: L13, G04, G15, G16, G23, S04.

### R007 - Samsung Messages and Google Messages transition assistant

Priority: P1. Impact: 4. Effort: 3. Risk: 3.

Add a pre-flight card that detects Samsung Messages/Google Messages/default SMS
role, warns US users about July 2026 discontinuation, and recommends the safest
sequence before PhoneFork touches SMS backup/restore.

Acceptance:

- Detect current default SMS role and installed message apps.
- Show Samsung's US-market discontinuation note and conversation-transfer delay
  caveat.
- Route SMS migration through helper APK only after default-app state is clear.

Sources: S08, L13, L16, G20.

### R008 - Gallery and OneDrive cutoff assistant

Priority: P1. Impact: 4. Effort: 2. Risk: 2.

Update the existing Samsung honesty/pre-flight logic to use Microsoft's
September 30, 2026 direct-sync cutoff and guide users toward OneDrive camera
backup validation when relevant.

Acceptance:

- Detect Samsung Gallery, OneDrive, and Samsung account/cloud indicators.
- Explain that existing OneDrive files remain accessible outside Samsung
  Gallery after cutoff.
- Include camera-backup permission/account/storage checks in the pre-flight
  output.

Sources: S10, L13, L21.

### R009 - Media sync resilience and evidence trail

Priority: P1. Impact: 5. Effort: 4. Risk: 3.

Harden large media migration with checkpoints, retry/replay manifests, ETA,
throughput sampling, huge-file warnings, partial failure recovery, and an
optional "recommend Quick Share instead" decision for ad hoc single-file cases.

Acceptance:

- Interrupted sync can resume from a checkpoint.
- Verification distinguishes size+mtime, CRC32, and SHA-256 levels.
- Reports include skipped, retried, failed, and user-deferred files.
- Quick Share handoff appears only where it is better than ADB sync.

Sources: L13, S11, S12, G10, G11, G18.

### R010 - Debloat safety feed and out-of-band overrides

Priority: P1. Impact: 4. Effort: 3. Risk: 3.

The embedded dataset is valuable but OEM regressions change quickly. Add a
signed or checksummed overlay feed so known-bad package actions can be patched
without a full app release.

Acceptance:

- Overlay entries include package, OEM/OneUI range, risk, source URL, action
  override, and expiry/review date.
- Local offline mode still works with embedded data.
- UAD-NG issue-derived overrides can be added with source trails.

Sources: L13, G06, G07, G08, G22.

### R011 - Windows release signing and provenance

Priority: P1. Impact: 4. Effort: 3. Risk: 3.

Wire the existing release workflow to a real signing profile and document the
unsigned-development path. Use GitHub artifact attestations for release ZIPs and
SBOMs.

Acceptance:

- Release workflow emits WPF ZIP, CLI ZIP, SBOM, and attestation.
- Signing step uses Artifact Signing when secrets are available.
- Release notes explain SmartScreen reputation honestly.
- Verification instructions cover `signtool verify` and attestation checks.

Sources: L19, S15, S16, S17, S18.

### R012 - Dependency maintenance window

Priority: P1. Impact: 3. Effort: 3. Risk: 3.

Upgrade low-risk patch dependencies first, then major behavior-change packages
under tests. Treat `JsonSchema.Net` and test infrastructure changes as separate
PRs/commits when possible.

Acceptance:

- QRCoder, Spectre.Console, and test infrastructure updates are applied with
  build/test green.
- JsonSchema.Net major upgrade is evaluated with schema compatibility tests.
- `xunit.v3` migration is tracked but not forced into a release crunch.

Sources: L27-L29.

## Later: v1.x Expansion

| ID | Initiative | Sources |
|---|---|---|
| R013 | Package integrity, installer/source provenance, OBB/external-data discovery, and per-app "what can/cannot transfer" reports. | L13, G04, G05 |
| R014 | Multi-user and work-profile awareness before destructive package or settings actions. | L13, G06, G08 |
| R015 | Samsung/One UI safe settings corpus with read-only comparison before apply. | L13, L21, G07 |
| R016 | Local migration receipts with devices, categories, versions, counts, failures, warnings, and rollback snapshot locations. | L13, L19, S18 |
| R017 | Seedvault, Android cross-platform transfer metadata, and official Android 17+ migration watcher. | S04, S13, S14, G14 |

## v2 Watchlist

- Avalonia or another cross-platform host only after the WPF v1 cockpit is
  complete.
- WebUSB/WebADB companion only if browser ADB can match PhoneFork's safety and
  logging requirements.
- iOS-source migration helper after Android 17/iOS 26.3 flows stabilize and
  there is a clear gap PhoneFork can solve locally.
- Optional local HTTP helper API only after ContentProvider contract security is
  proven.
- Commercial-grade device corpus and test lab once public releases create real
  user demand.

Sources: G12, G13, S13, S14, C01-C08.

## Rejected Or Deferred Ideas

| Idea | Decision | Reason |
|---|---|---|
| Promise private `/data/data` migration without root | Reject | Android privilege boundary and project honesty stance. Sources: L22, S07. |
| Use legacy `adb tcpip 5555` as a convenience path | Reject | Conflicts with current wireless ADB security posture and CVE-2026-0073 risk. Sources: S02, S05, S06. |
| Direct Samsung Wallet/Pass token migration | Reject | OEM/account-bound and not accessible to PhoneFork. Use pre-flight guidance. Sources: S09, L21. |
| Root-first migration mode | Defer indefinitely | It contradicts the product thesis and narrows current Samsung reach. Sources: L22, G17, G21. |
| Full Smart Switch clone | Reject | Smart Switch has OEM privileges and account access PhoneFork will not have. PhoneFork should interop and warn. Source: S07. |
| Web-first rewrite | Defer | WPF is already the v1 app and current Core services are Windows-oriented. Sources: L09, L13. |

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Helper APK grants sensitive permissions and is misused by another app | High | UID gate, shell-only queries, short install window, clear uninstall, contract tests. |
| Wireless ADB exposes a vulnerable device | High | USB-first pairing, patch-level gate, timeout, kill switch, clear CVE warning. |
| Roadmap overpromises app-private data migration | High | Keep honesty reports and category capability matrices in every user-facing flow. |
| Embedded debloat dataset becomes stale | Medium | Overlay feed, source-backed warnings, conservative defaults. |
| Release artifacts are unsigned or poorly trusted | Medium | Artifact Signing, attestations, clear pre-release docs. |
| External platform policy changes | Medium | Date-stamped research logs and source refresh before each release. |
| No real-device validation in this session | Medium | Mark hardware validation as required before public v1. |

## Completion Gates For First Public Release

- Build/test pass locally and in CI.
- Helper APK either ships as fully working and verified or is hidden behind an
  explicit experimental flag.
- README no longer points to non-existent releases.
- WPF exposes the main migration path without requiring CLI fallback.
- Release artifact contains clear signed/unsigned status and provenance.
- At least one real two-phone Samsung migration smoke test is documented.
- Roadmap, changelog, README, app title, manifest, and GitHub release all agree
  on the version.

## Planning Artifacts

Detailed evidence, raw ideas, scoring, and research notes are in:

- `PROJECT_CONTEXT.md`
- `.ai/research/2026-05-17/STATE_OF_REPO.md`
- `.ai/research/2026-05-17/MEMORY_CONSOLIDATION.md`
- `.ai/research/2026-05-17/SOURCE_REGISTER.md`
- `.ai/research/2026-05-17/RESEARCH_LOG.md`
- `.ai/research/2026-05-17/COMPETITOR_MATRIX.md`
- `.ai/research/2026-05-17/FEATURE_BACKLOG.md`
- `.ai/research/2026-05-17/PRIORITIZATION_MATRIX.md`
- `.ai/research/2026-05-17/SECURITY_AND_DEPENDENCY_REVIEW.md`
- `.ai/research/2026-05-17/DATASET_MODEL_INTEGRATION_REVIEW.md`
