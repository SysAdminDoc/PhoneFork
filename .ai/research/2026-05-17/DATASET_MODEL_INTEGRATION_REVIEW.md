# Dataset, Model, And Integration Review - 2026-05-17

PhoneFork is not an AI/ML project. There is no evidence that it needs a hosted
model, training pipeline, vector index, or benchmark suite to reach v1. The
relevant data surfaces are device/package datasets, backup/archive formats, and
external platform integrations.

## Existing Data Assets

| Asset | Current role | Opportunity | Sources |
|---|---|---|---|
| AppManagerNG and UAD-NG debloat datasets | Embedded package classifications and PhoneFork overrides | Add out-of-band overlay feed for OEM/One UI regressions and expiry/review dates. | L13, G06, G07, G22 |
| Smart Switch detection indicators | Pre-flight evidence for official handoff | Expand report wording and category-specific handoffs. | L13, S07 |
| Samsung honesty probes | Detect Pass/Wallet/Secure Folder/Gallery/OneDrive/Samsung Account risks | Update OneDrive cutoff and Messages/Wallet transition cards. | L13, S08-S10 |
| Backup archive metadata | AppManager/Open Android Backup/Android `.ab` detection | Move from sniffers to inspect/import workflows with capability matrices. | L13, G04, G15, G16, S04 |
| Audit logs | NDJSON evidence trail | Add safe export redaction and migration receipt summary. | L13, S18 |

## External Integrations

| Integration | Use | Status recommendation | Sources |
|---|---|---|---|
| ADB/platform-tools | Core transport and install/sync mechanism | Continue bundling and tracking release notes; keep wireless flows patch-gated. | S02, S03, S05 |
| Shizuku | Optional shell-UID assist | Keep optional; detect and explain readiness. | G02, G03, G05 |
| Helper APK ContentProviders | Sensitive category bridge | P0 implementation and security contract. | L15-L17, G20 |
| Smart Switch | Official OEM migration complement | Detect, report, and hand off where OEM privileges are required. | S07 |
| Google Messages | SMS transition path | Add default-app and transition assistant before SMS restore. | S08 |
| OneDrive camera backup | Photo/video cloud backup complement | Add cutoff-aware guidance, not direct cloud integration. | S10 |
| Quick Share | Ad hoc file handoff | Recommend for narrow ad hoc flows; do not depend on it for full migration. | S11, S12 |
| GitHub artifact attestations | Release provenance | Keep release workflow attestation. | S18 |
| Microsoft Artifact Signing | Windows signing | Use for public release trust when identity validation and secrets are ready. | S15, S16 |

## Backup And Format Watchlist

| Format/source | Potential | Risk |
|---|---|---|
| AppManager backups | Strong interop target because repo already has writer/reader foundations. | Format compatibility and GPL code boundary. |
| Android `.ab` | Useful for legacy archive inspection. | Deprecated/limited and not a future primary path. |
| Open Android Backup | Good local archive UX reference. | Restore semantics and companion app behavior need testing. |
| Seedvault v1 | Important system-backup watcher. | System privilege and evolving format limit direct use. |
| Android cross-platform-transfer metadata | Relevant to Android 16 QPR2+ backup semantics. | Official behavior may change; inspect-first until stable. |

## Model/AI Assessment

No ML integration is recommended for v1. Possible future AI-assisted ideas such
as package risk summarization, log triage, or migration advice should not be
implemented until:

- The underlying source-backed rules exist.
- Reports can cite exact local or official evidence.
- A no-network/offline fallback remains first-class.

For now, deterministic rules and source-backed datasets are more appropriate
than model-generated recommendations.

## Evaluation Opportunities

Build deterministic evaluation fixtures instead of ML benchmarks:

- Package classification fixture across Samsung/One UI versions.
- Helper provider JSON contract fixture.
- Backup archive inspect/import fixture.
- Media sync resume/retry fixture.
- Redacted log/migration receipt fixture.
- Version consistency fixture.
