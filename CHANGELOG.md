# Changelog

All notable changes to PhoneFork.

## Unreleased

### Added
- CLI AppManager backup workflow: `phonefork backup inspect`, `backup
  export-appmanager`, and `backup install-appmanager` now expose checksum
  verification, package listing, APK/split export, and local APK-set install
  from AppManager-compatible backup directories.
- Samsung Messages / Google Messages transition pre-flight checks detect the
  SMS default role, installed messaging apps, the US July 2026 retirement path,
  and whether helper-assisted SMS work is safe to proceed.

## v0.9.0-pre — 2026-05-17

Backup format detection layer (F031, F032) + Android `<cross-platform-transfer>`
metadata in the open archive manifest (F035). All sniffer paths are read-only;
real extraction/decryption follows in v0.9.1. The 2026-05-17 continuation adds
the helper provider contract, WPF Operations surface, release-readiness gate,
and version-consistency check for the first unsigned prerelease.

### Added
- `AndroidBackupReader.Sniff()` — parses the legacy `adb backup` archive header
  (magic, format version, compression flag, encryption tag, key-block presence).
- `OpenAndroidBackupReader.Sniff()` — detects an Open Android Backup archive
  directory (`*.7z` + optional sidecar JSON / txt).
- `CrossPlatformMetadata` on `OpenArchiveManifest` — carries the per-archive
  list of apps that opted into Android 16 QPR2+ iOS interop.
- PhoneForkHelper providers now emit `phonefork.helper.v1` JSON envelopes for
  SMS, call log, contacts, Wi-Fi capability metadata, wallpaper metadata,
  ringtone defaults, and user dictionary rows.
- WPF Operations tab for helper install/probe/uninstall, Shizuku checks, Smart
  Switch detection, backup interop inspection, pre-flight bundles, media
  size/mtime verification, trusted-pair visibility, and ADB Burst Mode.
- `scripts/Stage-HelperApk.ps1` verifies helper package metadata and signature
  before host packaging consumes `assets/helper/PhoneForkHelper.apk`.
- `scripts/Test-VersionConsistency.ps1` compares changelog, README badge, WPF
  title/header, app manifest, helper APK versionName, and release tag trigger.
- `docs/release-readiness.md` and `ARTIFACT-TRUST.txt` workflow output document
  unsigned prerelease behavior and future Artifact Signing expectations.

### Tests
- 113 → 129 (+16). New coverage: AB header sniffer (plain, compressed/encrypted,
  non-AB, missing file), OAB archive detection with both sidecar shapes,
  cross-platform metadata JSON serialization, optional manifest field, helper
  provider JSON extraction/parsing, malformed/empty helper responses,
  pagination, capabilities, and URI construction.

## v0.8.0 — 2026-05-16

Smart Switch interop + backup interop foundations + pre-flight bundle + media
verify + ADB Burst Mode + trusted-pair CLI. This release primarily ships the
plumbing that the WPF UI will surface in v0.8.1 and the helper APK provider
bodies will use in v0.7.1; the host-side surfaces are tested in isolation here.

### Added (Smart Switch — F024 / F025 / F027)
- `SmartSwitchDetection` — probes the legacy MSI install (registry +
  `Program Files (x86)`) and the Microsoft Store sandboxed package directory.
  CLI: `phonefork smartswitch detect`.
- `SandboxParser` — out-of-process parser launcher with stdout/stderr buffering,
  a 30 s timeout, and an empty environment. Wires up the slot for the AppContainer-
  hardened `.bk` parser binary that ships in v0.8.1.

### Added (Backup interop — F029 / F030 / F033 / F034)
- `AppManagerBackupSpec` / `Writer` / `Reader` — AppManager-compatible v5
  on-disk layout (`base.apk` + `split_*.apk` + `meta.am.v5` + `checksums.txt`).
  Writer SHA-256-hashes each APK and stores the device hash, never the raw
  serial. Reader verifies every referenced checksum before returning a handle.
- `BackupRetentionSweeper` + `RetentionPolicy` — count-based, time-based, and
  total-byte-cap retention rules. Plan/apply split so the UI can preview which
  backups would be removed before clicking Apply.

### Added (Pre-flight — F037 / F043 / F044)
- `PreflightService` aggregates the Samsung honesty probe, the security posture
  (transport + patch level), CSC/locale/country diff (`persist.sys.sales_code`,
  `ro.csc.country_code`, `persist.sys.locale`), the destination's OEM-Unlock
  toggle status (One UI 8.5 removed it on S25/S26), and Knox warranty/flash-lock
  state. Single record returned to the UI/CLI.

### Added (Media integrity — F046 / F098)
- `MediaIntegrityService` with three modes: Size+Mtime (fast, default during
  incremental sync), CRC32 (mid-tier; via `cksum`/`crc32` shell), SHA-256
  (trust-grade; via `sha256sum`). Host-side `Crc32()` covered by unit tests
  against the IEEE 802.3 polynomial.

### Added (ADB Burst Mode — F104)
- `AdbBurstModeService` toggles the `ADB_BURST_MODE` environment variable for
  newly-spawned ADB servers. Bundled platform-tools 37.0.0 supports the flag.
  CLI: `phonefork burst-mode on|off`.

### Added (Trusted-pair CLI)
- `phonefork trusted list` / `phonefork trusted forget <hash>`. Operates on
  hashes only — raw serials are never displayed or accepted.
- New `TrustedPairRegistry.ForgetByHash` method.

### Tests
- 93 → 113 (+20). New coverage: AppManager backup write/read round-trip + tamper
  detection, retention plan/apply under count/time/byte limits, CSC mismatch
  flags, CRC32 polynomial implementation against known vectors, integrity report
  cleanliness rules, burst-mode env-var toggle round-trip, trusted-pair removal
  by hash, Smart Switch detection shape on any host.

## v0.7.0 — 2026-05-16

Helper companion APK foundations (F010, F011, F012, F019, F020, F021, F022, F023, F072).
This release lays the v0.7.x groundwork: a Kotlin/Gradle helper-APK scaffold, the host-side
push-and-run JAR pattern, Shizuku detection, helper lifecycle/probe/residue APIs, an audit
scope for provider calls, a backup-capability probe, an open-archive spec, and CI/release
workflows. No new tabs in the WPF UI yet — that lands as v0.7.1 once the provider bodies
ship in the helper APK.

### Added
- `helper-apk/` — Gradle 8.7 / Kotlin 2.0 scaffold with `compileSdk=36`, `targetSdk=36`,
  `minSdk=30`, applicationId `com.sysadmindoc.phonefork.helper`. ContentProvider stubs
  for SMS, call log, contacts, Wi-Fi, wallpaper, ringtone, dictionary. Shell-UID gate
  baked into `BaseHelperProvider` so only ADB-driven queries reach the providers.
- `HelperAppService` (Core) — install / uninstall / health-probe / residue-check for
  the PhoneForkHelper.apk. CLI: `phonefork helper {install|uninstall|probe|residue}`.
- `AppProcessAgentService` (Core) — scrcpy-style push-and-run JAR runner against
  `/data/local/tmp/phonefork-agent.jar` via `CLASSPATH=... app_process /`.
- `ShizukuService` (Core) — detect Shizuku state (`NotInstalled` / `NotRunning` /
  `Running`) and emit a step-by-step runbook. CLI: `phonefork shizuku status`.
- `ProviderCallAudit` (Core) — Serilog `LogContext` scopes for each helper call,
  picking up the existing SerialHashingEnricher so device IDs are hashed on disk.
- `BackupCapabilityService` (Core) — per-app `dumpsys package` probe for
  `allowBackup` / `dataExtractionRules` / `cross-platform-transfer`. Mapped to
  `HonestyFinding` entries (F022).
- `OpenArchiveManifest` (Core) — JSON-shaped spec for the open-export archive
  layout (F023). Hashed serials only — never raw IDs.
- `CONTRIBUTING.md` — first contributor guide (F072).
- `.github/workflows/ci.yml` — restore/build/test/vuln-scan on every push (F067, F068).
- `.github/workflows/release.yml` — tag-triggered publish with Azure Artifact Signing
  hooks (F064/F065, gated by repo secrets) + SLSA build provenance (F066).

### Tests
- 85 → 93 Core tests. New coverage: HelperAppService constants, Shizuku runbook
  per state, AppProcessAgent constants, BackupCapability finding mapping,
  OpenArchiveManifest JSON round-trip + schema stability, ProviderCallAudit
  scope idempotency.

## v0.6.9 — 2026-05-16

Trust And Maintenance Gate. Wireless ADB is now governed by an explicit USB-first policy,
patch-level gate, session timeout, per-install ADB key directory, and a hashed-serial
trusted-pair registry. CVE-2026-0073 (zero-click RCE in wireless `adbd`, public PoC)
is refused by default below patch level 2026-05-01.

### Added
- `SecurityPostureService` + `SecurityPosture` model. Classifies transport (USB vs TCP)
  and parses `ro.build.version.security_patch`. Handles Samsung's `-N` Knox revision suffix.
- `WirelessPolicy`: opt-in wireless session with configurable timeout, kill switch,
  patch-level gate (refuses pre-2026-05-01 devices), explicit "Allow unpatched" override.
- Per-install ADB key directory (F002). `AdbHostService` sets `HOME` to
  `%LOCALAPPDATA%\PhoneFork\adb-home` before starting the server, so the user's global
  `.android/adbkey` is no longer reused across PhoneFork installs.
- `SerialHash` (12-hex-char SHA-256 prefix) + Serilog enricher that hashes
  `device` / `*Serial` properties on the NDJSON audit log path (F006).
- `TrustedPairRegistry` (F004): JSON store at `%LOCALAPPDATA%\PhoneFork\trusted-pairs.json`
  with hashed serials, transport class, first/last seen, and last endpoint. Stores no raw IDs.
- `adb mdns services` reconnect surface (F005) in the DeviceBar and as
  `phonefork mdns services` in the CLI. Cross-references trusted-pair registry.
- Samsung honesty pre-flight detector (`SamsungHonestyService`, F040, F108). Probes for
  Samsung Pass, Wallet, Secure Folder, Routines, Notes, Account, Gallery/OneDrive.
  Surfaced as `phonefork honesty --device <serial>`.
- Debloat dataset override overlay (F102). `assets/debloat/overrides.json` carries
  per-One-UI hot fixes (e.g. `com.samsung.android.smartsuggestions` flagged Unsafe on
  One UI 8.5 per UAD-NG #1394). `DebloatDataset.WithOverridesFor(oneUi, android)`
  applies the overlay; `DebloatViewModel` calls it before scanning.
- README: Android developer-verification posture note (F008).

### Improved
- DeviceBar pairing panel now shows the wireless session state, expiry, and the
  unpatched-override toggle. Pair/Connect refuse with structured reasons when the
  policy or patch gate would block the action.
- CLI `phonefork pair` and `phonefork connect` accept `--allow-unpatched` for parity
  with the WPF override toggle.

### Dependencies
- Serilog 4.2.0 → 4.3.1.
- Microsoft.Xaml.Behaviors.Wpf 1.1.135 → 1.1.142.
- Spectre.Console / Spectre.Console.Cli held at 0.55.0 (no stable 0.55.x; only -alpha).

### Tests
- Core test suite grew 16 → 76. New coverage: WirelessPolicy decisions (USB-first,
  patch-level gate, session timeout, kill switch, unpatched override), SerialHash
  determinism + redaction, TrustedPairRegistry persistence + raw-serial absence,
  mDNS services parser, DebloatDataset override matching, version predicate parsing.

## v0.6.8 — 2026-05-14

ADB shell, local path, and migration reliability hardening.

### Added
- Core unit tests for ADB shell quoting, Windows-safe local path generation, Wireless ADB QR parsing, and duplicate snapshot diff handling.
- Shared local-path sanitizer for serials, packages, APK filenames, media paths, and Wi-Fi QR exports.
- Shared ADB shell argument helper for shell-escaped values and package-name validation.

### Improved
- Wireless ADB serials, Android filenames, and media paths now stage safely on Windows without leaking path separators or reserved device names into local cache paths.
- App dry-runs now use the same APK pull path as real migrations, reducing duplicated behavior between WPF, CLI, and Core.
- Imported Settings and Media snapshots now tolerate duplicate namespaces, categories, and relative paths instead of failing during diff construction.
- Wi-Fi QR export creates the destination directory before writing PNG or SVG output.
- ADB host startup now honors the documented PATH fallback when a bundled `adb.exe` is not present.
- Debloat dry-run no longer writes rollback snapshots and now reports that no changes were written.
- CLI device listing now formats Samsung One UI versions the same way as the WPF device cards.

### Fixed
- Shell commands now consistently quote user/device-derived values before passing them through Android shell paths.
- Debloat apply and rollback reject malformed package IDs before command construction.
- Short device serials no longer crash `PhoneInfo.ShortLabel`.
- CLI permission grants now reject malformed package IDs and empty grant requests with clear messages instead of surfacing exceptions.

## v0.6.7 — 2026-05-14

Screenshot-driven WPF polish repair.

### Improved
- Main WPF shell now owns the dark background explicitly so the Catppuccin surface no longer leaks the default light Windows client color.
- Combo boxes and checkboxes now use custom Catppuccin templates instead of native light controls, including hover, focus, selected, and disabled states.
- Tab labels and disabled commands now read as intentional interface states instead of washed-out or broken controls.
- App migration option labels now use product-facing wording with tooltips instead of clipped CLI flag notation.
- Windows title bar opts into dark mode where the OS supports it.

## v0.6.6 — 2026-05-14

Premium WPF polish pass.

### Added
- WPF Wireless ADB panel in the device bar for pairing, connecting, disconnecting, and parsing Android `WIFI:T:ADB` pairing payloads.
- Empty states for Apps, Settings, Debloat, Wi-Fi, and Roles so first-run tables explain the next action and safety model.
- Selection counters across actionable tables so apply/migrate readiness is visible.

### Improved
- Shared Catppuccin theme now covers stronger keyboard focus, hover, pressed, disabled, textbox, combo box, DataGrid row/cell, tab, and progress states.
- Device cards now show a compact authorization status indicator and clearer no-device guidance.
- Row checkbox changes now refresh selected counts and command enabled states immediately across Apps, Settings, Debloat, and Roles.
- The main header now states the core trust posture: local-only, no root, no cloud.

## v0.6.5 — 2026-05-14

Wireless ADB pairing.

### Added
- `AdbPairingService` (Core) — CliWrap-driven `adb pair host:port code`, `adb connect`, `adb disconnect`. AdvancedSharpAdbClient doesn't expose the TLS pairing handshake; we shell out for this one path only. Includes a `ParsePairingQr` helper for `WIFI:T:ADB;S:<svc>;P:<code>;;` strings (Android Studio QR format).
- CLI: `phonefork pair <ip:port> <code>`, `phonefork connect <ip:port>`, `phonefork disconnect [ip:port]`. `adb disconnect` (no args) confirmed to work cleanly on the existing connected USB devices.

### Notes
- WPF wireless pairing surface shipped in v0.6.6.

## v0.6.0 — 2026-05-14

Roles tab live + roles/perms CLI branches. **All six core migration tabs now live.**

### Added
- `DefaultRoles` constants for the 8 AOSP role IDs PhoneFork enumerates (`android.app.role.{DIALER, SMS, BROWSER, HOME, ASSISTANT, CALL_REDIRECTION, CALL_SCREENING, EMERGENCY}`).
- `RoleHolder` + `RoleSnapshot` models.
- `RoleService` — wraps `cmd role get-role-holders --user 0 <role>` (handles both `package:<pkg>` and `[<pkg>]` Android-version output formats) and `cmd role add-role-holder --user 0 <role> <pkg>`. Also `GrantAsync` for `pm grant` and `SetAppOpAsync` for `appops set`.
- WPF Roles tab: side-by-side `DataGrid` (Role / Source holder / Dest holder / Match / Status) with default-select-different rows, Apply Selected / Select All Different / Select None / Dry-run.
- CLI: `phonefork roles get --device <s>`, `phonefork roles apply --from <s> --to <d> [--dry-run]`, `phonefork perms grant --device <d> --package <pkg> [--permission <p>] [--appop OP=mode]`.

### Notes
- Hardware-validated S25: all 8 roles snapshotted including non-default holders (Brave as browser, Google Messages as SMS, Should I Answer as call screener) — the exact preferences Smart Switch silently drops.
- Notification listener + Accessibility service enablers via `cmd notification allow_listener` and `enabled_accessibility_services` deferred to v0.6.1 (cosmetic; `cmd role` covers the high-value defaults).

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
