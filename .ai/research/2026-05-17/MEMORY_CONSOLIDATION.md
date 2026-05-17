# Memory Consolidation - 2026-05-17

## Files Inspected

Instruction and memory sources:

- `C:\Users\--\.claude\CLAUDE.md`
- `C:\Users\--\CLAUDE.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\MEMORY.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\phonefork.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\stack-csharp.md`
- `C:\Users\--\.claude\projects\c--Users----repos\memory\stack-android.md`
- `C:\Users\--\.codex\memories\MEMORY.md`

Repo-local files:

- `CLAUDE.md` (ignored, not tracked)
- `README.md`
- `CHANGELOG.md`
- `ROADMAP.md`
- `CONTRIBUTING.md`
- `docs/competitor-research.md`
- `docs/community-signal.md`
- `docs/migration-feasibility.md`
- `docs/oss-dependencies.md`
- `docs/oss-references.md`
- `docs/research-delta-2026-05-14.md`
- `helper-apk/README.md`

Search for repo-local `AGENTS.md`, `.claude/**`, `.cursor/rules/**`, `.cursorrules`, `.windsurfrules`, `GEMINI.md`, `.github/copilot-instructions.md`, `.ai/**`, `memory*.md`, `context*.md`, `project*.md`, `notes*.md`, `TODO*`, `ARCHITECTURE*`, and alternate roadmap/changelog files found only `CLAUDE.md`, `README.md`, `CHANGELOG.md`, `ROADMAP.md`, and `CONTRIBUTING.md` before this run.

## Durable Project Facts Consolidated

- PhoneFork is a local-only Windows WPF migration tool for Samsung Android devices, using ADB and no root.
- The canonical feature philosophy is honest migration: copy reachable state, explicitly warn about unreachable app-private and Knox-bound categories, and keep Smart Switch as a complement.
- The current implementation is significantly beyond the older `v0.6.x` memory: local HEAD is `v0.9.0-pre`.
- Important shared hardening decisions remain valid:
  - use `AdbShell` for shell quoting and package validation;
  - use `LocalPathNames` for Windows-safe staging;
  - hash serials in logs and trusted-pair storage;
  - keep debloat reversible by default;
  - treat wireless ADB as USB-first, opt-in, and patch-gated.
- The helper APK is scaffolded but not complete. This is a material current gap, not a shipped category implementation.

These facts were written to root `PROJECT_CONTEXT.md`.

## Stale Or Contradictory Claims

| Source | Claim | Current resolution |
|---|---|---|
| `README.md` | Version badge said `0.8.0`. | Stale after `CHANGELOG.md` `v0.9.0-pre`; synced to `0.9.0-pre`. |
| `src/PhoneFork.App/app.manifest` | Assembly identity version was `0.8.0.0`. | Stale after `v0.9.0-pre`; synced to `0.9.0.0`. |
| `src/PhoneFork.App/Views/MainWindow.xaml` | Title said `v0.6.8`. | Stale after `v0.9.0-pre`; synced to `v0.9.0-pre`. |
| Existing `ROADMAP.md` | "No stub functions in production code." | Stale. Helper APK provider bodies return `status:not-implemented`; roadmap and state docs now call this out. |
| Existing `ROADMAP.md` | Samsung Gallery OneDrive deadline was treated as April 2026 from non-official early signals. | Superseded by Microsoft Support, last updated May 14, 2026: Samsung Gallery direct OneDrive sync ends September 30, 2026. |
| Existing `CLAUDE.md` and memory | Current status line says `v0.8.0` and next work is v0.7.1/v0.8.1/v0.8.2. | Superseded by Git history and changelog: `v0.9.0-pre` added backup sniffer and cross-platform metadata. `CLAUDE.md` is ignored, so it was not changed in the commit. |
| `README.md` | Install section pointed users to a latest release even though GitHub has no release. | Corrected to build-from-source guidance until a release exists. |
| `README.md` | Wi-Fi row implied helper/Shizuku PSK export was already usable. | Corrected to current shell/QR capability and helper-assisted PSK export as planned. |
| Global instruction | "No AI references in repos" and AI working files should be local-only. | The user's direct prompt required `.ai/research/<date>/` artifacts and `PROJECT_CONTEXT.md`. The specific task requirement wins for this planning run. |
| Global instruction | Always prefix commands with `rtk`. | `rtk` is unavailable in this shell. Plain commands were used and the discrepancy is recorded. |

## Open Conflicts

1. The global "auto-commit-and-push" convention conflicts with the user's prompt line "Commit changes locally" and with the branch already being ahead of origin by four commits. This run commits locally and does not push.
2. The global "No tests unless explicitly requested" convention conflicts with repo CI expectations and the Definition of Done. Because this is a repository planning and state verification task, build/test/package scans are appropriate and were run.
3. The global "No AI references in repos" convention conflicts with the required `.ai/research` artifacts. This run preserves repo tool-specific files and creates only the requested planning artifacts.
4. Repo `CLAUDE.md` is ignored and not tracked, but it is the living working notes. It was read and reconciled; durable, non-tool-specific facts moved into `PROJECT_CONTEXT.md`.

## Canonical Memory Output

Root `PROJECT_CONTEXT.md` is now the canonical project context for future sessions. It intentionally does not replace `CLAUDE.md`; it consolidates project facts, architecture, current gaps, and verification commands in a tracked document.

## Future Memory Maintenance

- When implementation changes ship, update `PROJECT_CONTEXT.md` alongside `ROADMAP.md`.
- Keep `CLAUDE.md` as local working notes if desired, but do not rely on it as the only durable source because it is ignored by Git.
- Treat `.ai/research/<date>/` as point-in-time evidence. Verify package versions, GitHub stats, Android policy pages, and Samsung support pages again before acting months later.
