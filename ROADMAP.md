# PhoneFork Roadmap

Living list. Each version is a working, shippable increment.

## v0.1.0 — Bootstrap + Apps tab _(2026-05-14, current)_
- [x] Solution scaffold (Core + App + Cli)
- [x] ADB host (AdvancedSharpAdbClient), platform-tools bundle
- [x] Device pairing UI: live ADB device list, Source/Destination roles
- [x] Apps tab: enumerate `-3` user apps, render with label/icon/version, multi-select, pull base + splits, install-multiple on dest with Play-Store attribution
- [x] NDJSON audit log
- [x] Catppuccin Mocha theme
- [x] CLI: `devices`, `apps list`, `apps migrate`

## v0.2.0 — Media tab
- [ ] `/sdcard/` recursive manifest (path/size/mtime via `find -printf`)
- [ ] Set-diff manifest UI (per-category: DCIM, Pictures, Movies, Download, Documents, Music, Ringtones, Notifications, Alarms, Recordings)
- [ ] Resumable pull/push with checkpoint file
- [ ] Per-file SHA-256 verify (opt-in, slow)
- [ ] CLI: `media manifest`, `media sync`

## v0.3.0 — Settings tab
- [ ] Snapshot `settings list secure|system|global` per device
- [ ] Reference profile: dump of a fresh One UI 8 factory-reset for diff baseline
- [ ] Samsung `com.sec.android.provider.settings` content-provider read
- [ ] Per-key checkbox cherry-pick with bucket coloring (S25-only / S22-only / both-but-different)
- [ ] Known-safe / known-locked allowlist (status bar tweaks, AOD, refresh rate, font scale, animation scales, accessibility shortcuts)
- [ ] Apply via `settings put <ns> <key> <val>`
- [ ] CLI: `settings dump`, `settings diff`, `settings apply`

## v0.4.0 — Debloat tab
- [ ] Pull AppManagerNG debloat dataset (`oem.json`/`google.json`/`carrier.json`/`aosp.json`/`misc.json`) at build time, ship as embedded resource
- [ ] Cross-reference against destination's `pm list packages -s -e` to filter list to what's actually installed
- [ ] Category tree UI with `removal` filter (`delete` / `replace` / `caution` / `unsafe`)
- [ ] `pm disable-user --user 0` (reversible) — never `pm uninstall`
- [ ] Reversal: `cmd package install-existing <pkg>` per row
- [ ] CLI: `debloat list`, `debloat apply --profile {conservative|recommended|aggressive}`

## v0.5.0 — Wi-Fi tab
- [ ] Helper APK or Shizuku binding: `WifiManager.getPrivilegedConfiguredNetworks()` JSON export
- [ ] QR-bridge fallback: per-network `WIFI:T=WPA;S=…;P=…;;` QR via QRCoder
- [ ] Selective sync UI (skip work / corporate networks)
- [ ] CLI: `wifi export`, `wifi import`

## v0.6.0 — Roles & permissions tab
- [ ] `cmd role get-role-holders` snapshot per device
- [ ] Side-by-side default-app picker (dialer / SMS / browser / launcher / assistant / home)
- [ ] `cmd role add-role-holder` apply
- [ ] Per-installed-app runtime permission grants via `pm grant` / `appops set`
- [ ] Notification listener + accessibility service enablers
- [ ] CLI: `roles get`, `roles set`, `perms grant`

## v0.7.0 — Helper companion APK
- [ ] `PhoneForkHelper.apk` — small content-provider helper for SMS / contacts / call log / ringtone-default URI
- [ ] `app_process` JAR for read-side ops (no install footprint, scrcpy pattern)
- [ ] Apksigner signing in CI (`~/.android/phonefork-helper/phonefork-helper.jks`)
- [ ] Auto-uninstall on migration complete

## v0.8.0 — Smart Switch integration
- [ ] Drive `SmartSwitch.exe` via UI Automation (FlaUI) for app-data domain
- [ ] Detect installed Smart Switch, offer as "complete the data side" step after PhoneFork's apps/media/settings/etc finish
- [ ] Pre-fly check + handoff (warn user not to run both in parallel on the same phone)

## v0.9.0 — AppManager backup compatibility
- [ ] Write backups in AppManager's format: `<pkg>/<ts>/{base.apk, split_*.apk, meta.am.v5, checksums.txt, permissions.am.tsv, rules.am.tsv}`
- [ ] Restore from AppManager backups
- [ ] Two-way cross-tool compatibility

## v1.0.0 — Polish + signed release
- [ ] EV-signed `PhoneFork.exe`
- [ ] DPI-aware screenshots for README (per `screenshots.md` global rule)
- [ ] Auto-update via Velopack
- [ ] Inno Setup installer alternative to ZIP
- [ ] Branch protection (`enforce_admins: true`)
- [ ] Logo + banner per branding workflow (5-prompt RGBA, 512×512)

## Beyond v1.0
- Avalonia port for macOS/Linux ADB hosts.
- Wireless ADB pairing flow (no USB cable required) — same primitive as Shizuku.
- Multi-source migration (3+ source phones consolidating to one new one).
- Plugin model for non-Samsung OEM-specific settings keys (Pixel, OnePlus, Xiaomi).
