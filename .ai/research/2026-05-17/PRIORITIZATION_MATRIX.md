# Prioritization Matrix - 2026-05-17

Scoring: Impact, effort, and risk use 1 low to 5 high. Confidence is based on
source quality and local fit. Roadmap IDs map to root `ROADMAP.md`; backlog IDs
map to `FEATURE_BACKLOG.md`.

## P0 - Immediate Stabilization

| Roadmap | Backlog | Candidate | Impact | Effort | Risk | Confidence | Why now |
|---|---|---|---:|---:|---:|---|---|
| R001 | B001-B003 | Helper APK provider contract | 5 | 5 | 4 | High | Current helper stubs are the largest gap between claims and capability. |
| R002 | B005 | Helper APK CI build/verify | 4 | 3 | 3 | High | A scaffolded helper without CI build is release risk. |
| R003 | B004, B010 | WPF surfacing for shipped Core services | 4 | 4 | 3 | High | CLI has outpaced UI; users need a cockpit, not hidden commands. |
| R004 | B060-B063 | Release readiness correction | 4 | 2 | 2 | High | Repo has release workflow scaffolding but no published release yet. |
| R005 | B064 | Version/doc consistency gate | 3 | 2 | 1 | High | Stale visible versions already recurred and are cheap to prevent. |

## P1 - v1.0 Trustworthy Migration

| Roadmap | Backlog | Candidate | Impact | Effort | Risk | Confidence | Why next |
|---|---|---|---:|---:|---:|---|---|
| R006 | B040-B045 | Backup archive interop workflow | 5 | 5 | 4 | Medium-high | Current sniffers/writers are foundations; user value requires inspect/import UI. |
| R007 | B020-B021 | Samsung Messages transition assistant | 4 | 3 | 3 | High | Official July 2026 discontinuation creates current migration confusion. |
| R008 | B022 | Gallery/OneDrive cutoff assistant | 4 | 2 | 2 | High | Official September 2026 cutoff supersedes old research. |
| R009 | B030-B034 | Media sync resilience | 5 | 4 | 3 | High | Large file transfers are core to no-cloud migration and competitor file UX is strong. |
| R010 | B050-B053 | Debloat safety overlay | 4 | 3 | 3 | Medium-high | OEM package safety changes faster than app releases. |
| R011 | B061-B063 | Signing/provenance release hardening | 4 | 3 | 3 | High | Windows public releases need trust signals and clear unsigned fallback. |
| R012 | B070-B075 | Dependency maintenance window | 3 | 3 | 3 | High | Package updates are known and vulnerability scan is clean, so do after P0. |

## P2 - v1.x Expansion

| Roadmap | Backlog | Candidate | Impact | Effort | Risk | Confidence | Notes |
|---|---|---|---:|---:|---:|---|---|
| R013 | B012, B045 | Package integrity and app-data honesty reports | 4 | 4 | 3 | Medium | Valuable before broad public use, but depends on helper/archive clarity. |
| R014 | B051 | Multi-user/work-profile awareness | 4 | 4 | 4 | Medium | Important for safety; needs real-device validation. |
| R015 | B023, B024 | Samsung/One UI settings corpus | 3 | 4 | 4 | Medium | High trust value, but corpus collection is slow. |
| R016 | B013 | Local migration receipts | 4 | 3 | 2 | Medium-high | Strong differentiator once core flows are stable. |
| R017 | B043, B093 | Seedvault/cross-platform backup watcher | 3 | 3 | 4 | Medium | Keep inspect-first until formats are stable. |

## Deferred

| Backlog | Candidate | Reason |
|---|---|---|
| B090 | Avalonia host | Defer until WPF v1 reaches workflow completeness. |
| B091 | WebADB/WebUSB helper | Browser ADB has capability and policy limits; useful only as v2 sidecar. |
| B092 | iOS-source bridge | Official transfer flows are moving; wait for Android 17/iOS 26.3 reality. |
| B094 | Local HTTP helper API | ContentProvider surface must be secure before adding another IPC surface. |

## Priority Formula Used

Priority was assigned by judgment rather than a single numeric formula, but the
effective ordering favored current repo gap, official platform/security signal,
fit with the no-root/local-first thesis, risk reduction before feature
expansion, and ability to verify with local tests or CI.
