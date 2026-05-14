# Changelog

All notable changes to PhoneFork.

## v0.5.0 — 2026-05-14

Wi-Fi tab live + Wi-Fi/CSC CLI branches.

### Added
- `WifiNetwork` model + `WifiAuth` enum (`Wpa`/`Wep`/`Nopass`/`WpaEap`).
- `WifiQrService` — wraps `QRCoder.PayloadGenerator.WiFi` to emit the standard `WIFI:T:...;S:...;P:...;H:...;;` payload, plus PNG and SVG render helpers. QRCoder dependency moved into Core so the CLI can use it too.
- `WifiSnapshotService` — enumerates SSIDs via `cmd wifi list-networks` with a `dumpsys wifi` regex fallback. Explicitly documents that PSKs are not recoverable through this path; that's gated on the v0.7 helper APK / Shizuku binder.
- `CscSnapshot` model + `CscDiffService` — reads `persist.sys.sales_code`, `ro.csc.country_code`, `persist.sys.locale`, `persist.sys.timezone`, `gsm.sim.operator.iso-country` via `getprop`. Emits a `CscDiffSummary` with per-property mismatch flags.
- WPF Wi-Fi tab: CSC pre-flight banner, source-side SSID `DataGrid` with per-row "Enter PSK to build QR" input + "Use" button that fills the manual composer, manual composer (SSID / PSK / Auth / Hidden) with live PNG preview + Save PNG to `%LOCALAPPDATA%\PhoneFork\wifi-qrs\`.
- CLI: `phonefork wifi list --device <s>`, `phonefork wifi qr --ssid <name> --psk <key> [--auth Wpa|Wep|Nopass|WpaEap] [--hidden] [-o out.png|out.svg]`, `phonefork csc diff --from <s> --to <d>`.

### Notes
- Hardware-validated S25 vs S22: CSC diff surfaced country-code mismatch (UK & IRE vs USA) and carrier ISO mismatch — exactly the kind of pre-flight signal Smart Switch silently misses. SSID list on S25 returned 2 networks with auth types parsed correctly. QR PNG renders with `WIFI:T:WPA;S:TestNetwork;P:supersecret123;;` payload (479-byte PNG).
- PSK auto-export deferred to v0.7 (helper APK / Shizuku-bound `WifiManager.getPrivilegedConfiguredNetworks()`).

## v0.4.0 — 2026-05-14

Debloat tab live + debloat CLI branch. Ships the AppManagerNG / UAD-NG dataset embedded as 5 JSON resources (5,481 entries total).

### Added
- `assets/debloat/{oem,google,carrier,aosp,misc}.json` — embedded as `EmbeddedResource` with `LogicalName="PhoneFork.Core.Assets.Debloat.*"`.
- `DebloatEntry` + `DebloatTier` (Delete / Replace / Caution / Unsafe) + `DebloatList` (Oem / Google / Carrier / Aosp / Misc) models.
- `DebloatDataset.Load()` — reflects the assembly's embedded resources into 5,481 `DebloatEntry` rows indexed by package id.
- `DebloatScanner` — intersects dataset with destination's `pm list packages -s -e` + `-s -d`, returning a list of `DebloatCandidate(Entry, IsEnabled)`.
- `DebloatService` — `pm disable-user --user 0 <pkg>` only (never `pm uninstall`). Captures a JSON snapshot of the pre-debloat enabled set to `%LOCALAPPDATA%\PhoneFork\debloat-snapshots\<serial>-<ts>.json` before every apply. Rollback re-enables via `cmd package install-existing <pkg>` + `pm enable <pkg>`.
- WPF Debloat tab: filterable `DataGrid` of (Package / Label / Tier / List / State / Status / Warning), tier-chip toggles (Delete/Replace/Caution/Unsafe), enabled/disabled state toggles, profile dropdown (Conservative / Recommended / Aggressive), Apply Profile / Disable Selected / Select None / Dry-run.
- CLI: `phonefork debloat list --device <d> [--tier <CSV>] [--list <CSV>] [--include-disabled]`, `phonefork debloat apply --device <d> [--profile <name>] [--include-unsafe] [--package <pkg>] [--dry-run]`, `phonefork debloat rollback --device <d> --snapshot <path> [--dry-run]`.

### Notes
- Hardware-validated against S22 Ultra: 475 packages of the 5,481-entry dataset are installed; 207 Delete-tier, 70 Replace, 133 Caution, 65 Unsafe. Conservative profile would queue 204 enabled Delete-tier packages.
- "Disable carrier MSM bloat-installer first" deferred to v0.4.1 — current implementation queues all matches in a single batch.

## v0.3.0 — 2026-05-14

Settings tab live + settings CLI branch.

### Added
- `PhoneFork.Core.Models.SettingsSnapshot` + `SettingsNamespace` enum (Secure / System / Global).
- `SettingsSnapshotService` — captures all three AOSP namespaces via `settings list <ns>`. First-`=`-split parsing tolerates values that legitimately contain `=` (JSON blobs, URIs). Drops the AOSP "null" sentinel.
- `SettingsDiffer` — set-diff yielding `SettingsPlan` with per-namespace buckets: OnlyOnSource / OnlyOnDest / Same / Different.
- `SettingsApplyService` — `settings put` per row with safety blocklist (15 keys: `device_provisioned`, `adb_enabled`, `android_id`, `bluetooth_address`, carrier `preferred_network_mode`, etc.). Single-quote shell-escaping for values with whitespace. Exception-text detection on Android's stderr-on-stdout fallback.
- `SettingsApplyService.SetDefaultSoundUrisAsync` — sets `system.ringtone` / `notification_sound` / `alarm_alert` URI by remote path. Closes Smart Switch's missing-default-ringtone gap that Media-tab pushes alone don't.
- WPF Settings tab: filterable `DataGrid` of (Ns / Key / Outcome / Source value / Dest value), "Show only applicable" toggle, free-text filter, per-row checkbox, "Select All applicable" / "Select None" / Apply buttons, Dry-run toggle. Default-select rows with outcome `Different`; "only on source" rows are opt-in.
- CLI: `phonefork settings dump --device <s> [--namespaces <CSV>]`, `phonefork settings diff --src a.json --dst b.json [--show-different]`, `phonefork settings apply --from <s> --to <d> [--namespaces ...] [--keys ...] [--include-only-on-source] [--dry-run]`.

### Notes
- Hardware-validated against S25 Ultra (1,062 keys) and S22 Ultra (967 keys): 271 keys are applicable (sum of Different + OnlyOnSource), 791 already aligned, 48 OnlyOnDest. Dry-run pipeline works clean end-to-end.

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
