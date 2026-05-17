# Feature Backlog - 2026-05-17

Raw harvested backlog before final prioritization. Scores and release placement
are in `PRIORITIZATION_MATRIX.md` and root `ROADMAP.md`.

## Helper APK And Privileged Categories

| ID | Idea | Source trail |
|---|---|---|
| B001 | Implement helper provider JSON contract for SMS, call log, contacts, Wi-Fi, wallpaper, ringtone, and dictionary. | L15-L17, G20 |
| B002 | Add host-side typed DTOs and contract tests for helper responses. | L13, L16 |
| B003 | Add helper pagination and resume tokens for large SMS/contact datasets. | L16, G20 |
| B004 | Add helper install/probe/uninstall WPF flow with visible permission and cleanup states. | L12, L13, L17 |
| B005 | Add CI helper APK assemble and signing verification. | L14, L18, L17 |
| B006 | Add app_process shell agent once the APK provider contract is stable. | L17, G01 |

## User-Facing Workflow

| ID | Idea | Source trail |
|---|---|---|
| B010 | Expose pre-flight, helper, Shizuku, Smart Switch, backup interop, media verify, trusted pairs, and burst mode in WPF. | L12, L13 |
| B011 | Build a first-run "two phones connected" readiness dashboard. | L04, L13 |
| B012 | Add category capability matrix before migration starts. | L13, S07, L22 |
| B013 | Add local migration receipt with checksums, skipped items, warnings, and rollback references. | L13, S18 |
| B014 | Add explicit "Smart Switch should handle this" handoff cards. | S07, L21 |
| B015 | Add "Quick Share is better for this ad hoc file share" handoff cards. | S11, S12 |

## Samsung Platform Changes

| ID | Idea | Source trail |
|---|---|---|
| B020 | Add Samsung Messages discontinuation and Google Messages default-app pre-flight. | S08 |
| B021 | Detect Samsung Messages, Google Messages, and default SMS role before helper SMS actions. | S08, L13, L16 |
| B022 | Update Gallery/OneDrive sync warning to September 30, 2026 and camera-backup guidance. | S10 |
| B023 | Add Samsung Wallet/Pass warning and handoff, not token migration. | S09, L21 |
| B024 | Keep CSC mismatch and Samsung account/Secure Folder warnings prominent. | L13, L21 |

## Media And Files

| ID | Idea | Source trail |
|---|---|---|
| B030 | Add media sync checkpoints and resume. | L13, G10, G11, G18 |
| B031 | Add retry/replay manifest and failure export. | L13, G10 |
| B032 | Add ETA, throughput, cable-quality hints, and burst-mode advice. | L13, S03 |
| B033 | Add huge-file policy and low-space preflight. | L13 |
| B034 | Add thumbnail/preview affordances after parity with ADB-Explorer expectations. | G10 |

## Backup And Archive Interop

| ID | Idea | Source trail |
|---|---|---|
| B040 | Finish AppManager-compatible archive export/import workflow. | L13, G04 |
| B041 | Add `.ab` metadata inspection and safe decode path. | L13, G16 |
| B042 | Add Open Android Backup archive inspection. | L13, G15, G23 |
| B043 | Add cross-platform-transfer metadata explanation for Android 16 QPR2+. | S04 |
| B044 | Add archive retention policy UI. | L13, G17 |
| B045 | Add backup preview before restore. | C06, G04 |

## Debloat And Safety

| ID | Idea | Source trail |
|---|---|---|
| B050 | Add out-of-band debloat overlay feed with source URLs and One UI ranges. | G06, G07, G22 |
| B051 | Add work-profile and multi-user package handling. | G08, L13 |
| B052 | Add package disable impact preview with rollback snapshot link. | L13, G08 |
| B053 | Add "known bad on this device/One UI" warning state. | G07 |

## Release, Supply Chain, And Docs

| ID | Idea | Source trail |
|---|---|---|
| B060 | Correct README install/release language until a first release exists. | L03, L04 |
| B061 | Publish first pre-release ZIPs for WPF and CLI. | L19 |
| B062 | Wire Artifact Signing with documented unsigned fallback. | S15, S16 |
| B063 | Verify GitHub artifact attestations and SBOM output. | L19, S18 |
| B064 | Add version consistency script/test. | L04, L05, L09 |
| B065 | Add screenshots and a short "what PhoneFork cannot migrate" section. | L04, L22 |

## Dependency And Test Maintenance

| ID | Idea | Source trail |
|---|---|---|
| B070 | Upgrade QRCoder 1.6.0 to 1.8.0. | L27 |
| B071 | Upgrade Spectre.Console 0.55.0 to 0.55.2. | L27 |
| B072 | Evaluate JsonSchema.Net 7.3.0 to 9.2.1 with schema tests. | L27 |
| B073 | Upgrade Serilog.Sinks.File 6.0.0 to 7.0.0. | L27 |
| B074 | Upgrade Microsoft.NET.Test.Sdk and coverlet collector. | L27 |
| B075 | Plan xUnit v3 migration. | L29 |

## v2 Watchlist

| ID | Idea | Source trail |
|---|---|---|
| B090 | Avalonia host after WPF v1 completes. | L09, L13 |
| B091 | WebADB/WebUSB helper for browser-based mode. | G12, G13 |
| B092 | iOS-source bridge after iOS 26.3/Android 17 behavior stabilizes. | S13, S14 |
| B093 | Seedvault v1 or inspect-only import watcher. | G14 |
| B094 | Local HTTP helper API after provider security matures. | G20 |

## Hypotheses To Validate

- H001: Users will value a local migration receipt enough to justify building it before broader category support.
- H002: Samsung Messages discontinuation will create enough SMS migration confusion to justify a dedicated assistant rather than a generic warning.
- H003: ADB media throughput and retries will drive more user trust than adding more low-reach categories.
- H004: Artifact Signing Basic is sufficient for early public releases.
