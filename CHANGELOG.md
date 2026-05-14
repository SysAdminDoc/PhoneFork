# Changelog

All notable changes to PhoneFork.

## v0.2.0 — 2026-05-14

Media tab live + media CLI branch.

### Added
- `PhoneFork.Core.Models.MediaCategory` enum with 11 user-storage subtrees (DCIM, Pictures, Movies, Music, Download, Documents, Ringtones, Notifications, Alarms, Recordings, WhatsAppMedia).
- `MediaManifestService` — single-shell-call enumeration via `find <root> -type f -printf '%P\t%s\t%T@\n'`. Skips missing roots cleanly (e.g. WhatsApp on a phone without it).
- `MediaDiffer` — set-diff producing `MediaPlan` with per-category buckets (NewOnSource / Conflict / Identical / NewOnDest) and byte counters.
- `MediaSyncService` — pull-then-push pipeline. Honors `--delete` (mirror destination), `--update` (skip conflicts when source mtime ≤ dest), `--preserve-conflicts` (rename dest to `*.sync-conflict-<ts>-<sha8>.<ext>` per Syncthing pattern), `--dry-run`.
- **Mtime preservation** — source mtime is propagated through `SyncService.PushAsync` so re-running a sync correctly classifies files as Identical, not Conflict.
- WPF Media tab: per-category checkbox `DataGrid` with Src/Dst counts + plan buckets + MiB-to-transfer columns, action toolbar (Scan / Apply / Select All / Select None), four toggle checkboxes, live progress bar + current-file display.
- CLI: `phonefork media manifest --device <s> --categories <CSV> --out <json>`, `phonefork media diff --src a.json --dst b.json`, `phonefork media sync --from <s> --to <d> [--dry-run] [--delete] [--update] [--preserve-conflicts]`.
- Stable JSON manifest format with `JsonStringEnumConverter` — categories serialize as `"Dcim"` not `0`.

### Notes
- Stage directory: `%LOCALAPPDATA%\PhoneFork\stage\<sourceSerial>\<Category>\`.
- Hardware-validated against S25 Ultra → S22 Ultra: 789 files / 131.6 MiB across 3 categories enumerated, diff correctly identified 6 files / 4.0 MiB to transfer with 0 conflicts.

## v0.1.0 — 2026-05-14

Initial release.

### Added
- C#/.NET 10/WPF solution scaffold: `PhoneFork.Core` (services), `PhoneFork.App` (WPF GUI), `PhoneFork.Cli` (Spectre console).
- ADB host service (AdvancedSharpAdbClient 3.6.16) — native binary protocol, no `adb.exe` shellout. Bundled platform-tools 37.0.0 in `tools/`.
- Device-pairing service: live ADB device discovery via `DeviceMonitor`, Source/Destination role assignment.
- Apps tab end-to-end:
  - Enumerate user apps (`pm list packages -3 -f`) with split-APK awareness.
  - APK metadata via AlphaOmega.ApkReader (`application-label`, icon, version, requested perms) — no `aapt2.exe` shellout.
  - Pull base + all `split_config.*.apk` to local cache.
  - Install on destination via `PackageManager.InstallMultiple` with `-i com.android.vending --install-reason 4 --user 0` attribution.
  - Auto-grant runtime permissions (`pm install -g`).
- NDJSON audit log via Serilog + CompactJsonFormatter at `%LOCALAPPDATA%\PhoneFork\logs\audit-YYYY-MM-DD.log`. One JSON event per operation, `device` / `op` / `pkg` / `outcome` enriched.
- Catppuccin Mocha theme (hand-rolled — no `catppuccin/wpf` NuGet exists yet).
- CLI: `phonefork devices`, `phonefork apps list`, `phonefork apps migrate`.

### Placeholder (planned-feature copy in v0.1.0; live in v0.2.0+)
- Media tab — `/sdcard/...` incremental sync (v0.2.0).
- Settings tab — AOSP + Samsung One UI key diff/apply (v0.3.0).
- Debloat tab — AppManagerNG dataset application (v0.4.0).
- Wi-Fi tab — Shizuku primary + QR-bridge fallback (v0.5.0).
- Roles tab — default app role assignment (v0.6.0).

### Notes
- Pin `CommunityToolkit.Mvvm` ≥ 8.4.2 (8.4.0 source-gen breaks on .NET 10).
- Third-party private app data (banking, messengers, game saves) cannot be migrated without root — by Android security design. Run Samsung Smart Switch alongside PhoneFork for that subset.
