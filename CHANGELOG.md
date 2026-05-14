# Changelog

All notable changes to PhoneFork.

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
