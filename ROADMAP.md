# PhoneFork Roadmap

**Version 2026.05.14b** — incremental rewrite after the v0.6.5 ship. Supersedes the 2026.05.14 rewrite. Working document. Dense, sourced, skimmable. Every claim or feature has a URL in the **Appendix**.

The 2026.05.14 baseline's six themes and ~200 harvested features still ground this document; v0.2 → v0.6.5 shipped against that plan, plus an unscheduled hardening pass. This refresh reconciles shipped status, pulls in the [delta research](docs/research-delta-2026-05-14.md) findings, and re-projects v0.7 → v2+.

---

## Vision

The Windows-host migration tool Smart Switch should have been. Two USB-connected Samsung phones (or one over Wireless ADB), parallel pipes, walk-away pipeline, brutally honest pre-flight, no Samsung account, no cloud, no platform-lock direction. Built around a Core engine that the WPF GUI and Spectre CLI both drive, with an optional companion APK + push-and-run JAR helper for the operations ADB shell-UID can't reach. **Position: the OSS gap-filler for everything Smart Switch refuses to do or quietly drops, paired with a debloat pass nobody else ships in the same workflow.**

## Status quo (v0.6.5 — shipped 2026-05-14)

12 commits on `main`. ~9,187 LOC across 178 C# + 11 XAML files. Six functional migration tabs + Wireless ADB CLI + DeviceBar pair UI. Hardening pass landed: POSIX shell-quoting (`AdbShell.Arg/PackageArg/IsPackageName` with `^[A-Za-z0-9_.]+$` validation), Windows-safe path sanitization (`LocalPathNames.SafeFileName/CombineSafeRelativePath` with reserved-name protection + path-escape guard). WPF polish: SubtleCard style, empty-state cards on Settings/Debloat/Wi-Fi/Roles views, dark immersive title bar via `DwmSetWindowAttribute`, `InverseBoolToVis` converter. AppManagerNG dataset embedded (5,481 entries). Hardware-validated S25 Ultra ↔ S22 Ultra across every release. Zero `TODO`/`FIXME`/`HACK` markers in source.

### What v0.6.5 actually contains (per CHANGELOG.md + commits)

| Subsystem | Functional surface |
|---|---|
| **Apps** (v0.1.0) | enumerate `-3` user apps, pull base + splits via `SyncService.PullAsync`, install via `pm install-create -i com.android.vending --install-reason 4 --user 0 -g`. F-Droid 1.23.2 round-tripped end-to-end. |
| **Media** (v0.2.0) | manifest engine (`find -printf '%P\t%s\t%T@\n'`), set-diff with bucket coloring (NewOnSrc / Conflict / Identical / NewOnDst), pull-then-push with mtime preservation, `--delete` / `--update` / `--preserve-conflicts` / `--dry-run`. 11 categories. 789 files / 131.6 MiB on S25 enumerated. |
| **Settings** (v0.3.0) | snapshot 3 AOSP namespaces via `settings list`, set-diff, `settings put` with 15-key safety blocklist + POSIX `'\''` escaping, ringtone-URI restore via `SetDefaultSoundUrisAsync`. 271 keys applicable on S25 vs S22. |
| **Debloat** (v0.4.0) | 5,481-entry AppManagerNG/UAD-NG dataset embedded, `pm disable-user --user 0` only with pre-apply JSON snapshot, rollback via `cmd package install-existing` + `pm enable`, Conservative/Recommended/Aggressive profiles. 475 dataset matches on S22 (207 Delete / 70 Replace / 133 Caution / 65 Unsafe). |
| **Wi-Fi** (v0.5.0) | QRCoder-backed WIFI: payload + PNG/SVG render, SSID enumeration via `cmd wifi list-networks` + `dumpsys wifi`, CSC/locale/region pre-flight diff via 5 getprop keys. Country mismatch surfaced on S25 (UK & IRE) vs S22 (USA). |
| **Roles** (v0.6.0) | 8 AOSP `cmd role` snapshot/apply (DIALER, SMS, BROWSER, HOME, ASSISTANT, CALL_REDIRECTION, CALL_SCREENING, EMERGENCY), `pm grant`, `appops set`. Non-default holders preserved on S25 (Brave, Google Messages, Should I Answer). |
| **Wireless ADB** (v0.6.5) | `AdbPairingService` CliWrap-driven `adb pair/connect/disconnect`; `ParsePairingQr` for the `WIFI:T:ADB;S:<svc>;P:<code>;;` schema. Live pair UI in `DeviceBar` (`ToggleWirelessPairing` + `PairCommand`/`ConnectCommand`/`DisconnectCommand`). |
| **Hardening pass** (post v0.6.5) | `AdbShell` + `LocalPathNames`; `SubtleCard` / empty-state cards across Roles/Settings/Debloat/Wi-Fi; `DwmSetWindowAttribute` dark title bar; `MediaDiffer`/`SettingsDiffer`/dictionary `GroupBy().Last()` dedup; `EnsureOutputDirectory` for QR render. |
| **CLI** | `phonefork {devices, apps {list, migrate}, media {manifest, diff, sync}, settings {dump, diff, apply}, debloat {list, apply, rollback}, wifi {list, qr}, csc {diff}, roles {get, apply}, perms {grant}, pair, connect, disconnect}` |

## Hard constraints (unchanged from 2026.05.14)

- **License**: MIT. Third-party Apache-2.0 / GPL data assets (UAD-NG, AppManagerNG) used as data only.
- **Platform**: Windows 10+ / .NET 10 Desktop Runtime. Avalonia port post-v1.0.
- **No-root**: every operation must work without root on source and destination. Shell-UID + Shizuku is the elevation ceiling.
- **Android floor**: Android 11+ on both phones; tested against Android 16 / One UI 8.
- **Privacy**: zero telemetry, zero cloud, no Samsung/Google account required.
- **GUI policy** (CLAUDE.md): backdrop radius ∈ {0, 4, 6, 8, 10, 12}. No pill / capsule / `CornerRadius=999`. Catppuccin Mocha default.

## Themes (each Now/Next item maps to one or more)

1. **Coverage** — what gets migrated.
2. **Honesty** — pre-flight scans + "what won't transfer" reports.
3. **Velocity** — parallel pipes, resumable transfers, NDJSON manifest replay.
4. **Trust** — Knox attestation gate, signed manifest, per-install ADB RSA key, sandbox format parsers, USB-only enforcement.
5. **Reusability** — saved profiles, dry-run, lifecycle hooks, Healthchecks webhook, OpenCLI dump.
6. **Interop** — AppManager backup format, `.ab` import, Smart Switch `.bk` read, Pixel-Restore co-existence, helper APK + `app_process` JAR.
7. **Reach** — Wireless ADB ✅, mDNS auto-discovery, Catppuccin variants + light mode, WCAG 2.2, localization, Avalonia/macOS+Linux port.

---

## Shipped (v0.1 → v0.6.5) — _no action needed_

All six core tabs + Wireless ADB CLI live, hardware-validated S25 → S22. Detail above; per-version detail in [CHANGELOG.md](CHANGELOG.md).

---

## Now (v0.6.5.1 → v0.7) — _final-polish + helper APK pivot_

### v0.6.5.1 — Wireless-ADB trust hardening _(target: this week)_

**Theme**: Trust. Sources: [arXiv 2401.08961](https://arxiv.org/abs/2401.08961) ADB-security empirical study, [LineageOS adb_wifi.md](https://github.com/LineageOS/android_packages_modules_adb/blob/lineage-23.2/docs/dev/adb_wifi.md).

- **Per-install ADB RSA key** in `%LOCALAPPDATA%\PhoneFork\adb-keys\` (not the shared `%USERPROFILE%\.android\adbkey`). Set `ADB_VENDOR_KEYS` env before `AdbServer.StartServer()` per [Stack Overflow](https://stackoverflow.com/questions/22177451). Closes cross-tool key reuse.
- **USB-only enforcement default** — UI gates wireless-pair behind an opt-in toggle; CLI `--allow-wireless` flag.
- **Trusted-pair memory** — store per-device serial + RSA fingerprint to `%LOCALAPPDATA%\PhoneFork\paired-devices.json`; future plug-ins skip the trust prompt (AnyDesk/TeamViewer pattern from `docs/competitor-research.md`).
- **mDNS auto-reconnect surface** — `adb mdns services` probe on app start; if a previously-paired device appears, auto-connect. Falls out of the bundled `adb.exe` for free, just needs UI surfacing.
- **NDJSON audit log per-event device hash** — replace raw serial in audit log with `sha256(serial)[:12]` to avoid leaking the hardware ID if the user shares logs.

### v0.7.0 — Helper companion APK + push-and-run JAR _(target: 1-2 weeks)_

**Theme**: Coverage + Interop. Sources: [scrcpy app_process pattern](https://github.com/Genymobile/scrcpy/blob/master/doc/develop.md), [gonodono/adbsms](https://github.com/gonodono/adbsms), [Shizuku v13.6.0](https://github.com/RikkaApps/Shizuku), [WallpaperUnbricker](https://github.com/adryzz/WallpaperUnbricker), [Android 17 Beta 3](https://developer.android.com/about/versions/17) per [delta research](docs/research-delta-2026-05-14.md).

- **Android Studio Gradle project at `helper-apk/`** — Kotlin, AGP 8.7+, JDK 21 (`C:\Program Files\Android\openjdk\jdk-21.0.8` confirmed present locally).
- **`PhoneForkHelper.apk`** — single APK exposing N `ContentProvider` authorities:
  - `content://phonefork/sms` — Telephony provider read/write (replaces dead `bmgr backup`).
  - `content://phonefork/calllog` — read/write call log.
  - `content://phonefork/contacts` — full Contacts DB including per-contact ringtone URIs (closes community §7 Bixby/contact-ringtone signal).
  - `content://phonefork/wifi` — Shizuku-bound `WifiManager.getPrivilegedConfiguredNetworks()` for PSK export. Unlocks the v0.5.0 deferred path.
  - `content://phonefork/wallpaper` — `WallpaperManager.setStream()` via `am broadcast` per WallpaperUnbricker pattern.
  - `content://phonefork/keyboard` — Samsung Keyboard learned-words + Gboard user dictionary.
- **`targetSdk=36` + manifest `ACCESS_LOCAL_NETWORK`** — Android 17 Beta 3 (Platform Stability May 2026) gates local-network reach via this new permission; declare and request on first run. Source: delta research §Android 17.
- **Push-and-run JAR alternative** — `phonefork-agent.jar` (~250 KB, classes.dex shipped in JAR) deployed via `adb push /data/local/tmp/`, executed `app_process / com.phonefork.Agent`. Read-only ops; zero install footprint. scrcpy v4.0 reference implementation lift.
- **Self-uninstall on migration completion** + NDJSON audit log per provider call.
- **Signing** — keystore at `~/.android/phonefork-helper/phonefork-helper.jks` (gitignored, SwiftFloris pattern); GitHub Actions matrix builds + signs + embeds the APK as `EmbeddedResource` on `PhoneFork.Core`.
- **Build verification** — `apksigner verify --print-certs` smoke in CI.
- **Core surface** — `HelperAppService` (install via `adb install -t`, uninstall, query provider authority, marshal results).
- **CLI**: `phonefork helper {install, uninstall}`, plus the existing `wifi`, `sms` (new), `contacts` (new) sub-trees gain `--via-helper` flags.

---

## Next (v0.8 → v1.0) — _interop, polish, signed ship_

### v0.8.0 — Smart Switch UI Automation handoff _(target: post-v0.7)_

**Theme**: Coverage + Interop + Honesty. Sources: [Cellebrite "goldmine" post](https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/), [Hur et al. 2021](https://doi.org/10.1016/j.fsidi.2021.301172), [FlaUI](https://github.com/FlaUI/FlaUI), [delta research §Smart Switch](docs/research-delta-2026-05-14.md).

- **Dual installer probe** _(delta research finding)_ — Smart Switch is migrating to Microsoft Store. PhoneFork must detect both:
  - Legacy MSI at `C:\Program Files\Samsung\Smart Switch PC\`
  - MS Store sandbox at `%LOCALAPPDATA%\Packages\SAMSUNGELECTRONICSCO.LTD.SmartSwitch_*\`
  Mark unsupported if neither present; offer install link.
- **`SmartSwitch.exe` drive via FlaUI** — UI Automation walk: open → backup-mode → select categories PhoneFork doesn't cover → confirm → wait-for-done. Treat as subprocess in PhoneFork's audit log. Version-pin the locator strategy per Smart Switch major.
- **Smart Switch intermediate-cache opportunistic read** at `~/Documents/Samsung/SmartSwitch/backup/` BEFORE Samsung's final encryption pass. Source: Cellebrite goldmine.
- **`.bk` file read-only import** — AES-256-CBC + PBKDF2-HMAC-SHA1 decryption per Hur 2021. User-supplied passphrase (Samsung-account PIN at backup time). Extract Messages/Contacts/Call Log SQLite DBs into the relevant tabs for selective re-apply.
- **Sandboxed parser** — run `.bk` decryption in a separate AppContainer-restricted child process. Source: Project Zero FORCEDENTRY lesson (sandbox untrusted format parsing).

### v0.9.0 — Backup-format compatibility _(target: post-v0.8)_

**Theme**: Interop + Reusability. Sources: [AppManager](https://github.com/MuntashirAkon/AppManager), [abe](https://github.com/nelenkov/android-backup-extractor), [Android 16 QPR2 cross-platform-transfer](https://android-developers.googleblog.com/2025/12/android-16-qpr2-is-released.html), [delta research §Seedvault](docs/research-delta-2026-05-14.md).

- **Write AppManager-format backups** — `<pkg>/<ts>/{base.apk, split_*.apk, data.tar.gz.0, meta.am.v5, checksums.txt, rules.am.tsv, permissions.am.tsv}`. Two-way (PhoneFork backups restorable in AppManager and vice versa). GPL-3.0 schema reuse, no code linkage. AppManager v4.0.5 (2025-07) confirmed frozen schema by delta research.
- **Legacy `.ab` import** — port `abe`'s decrypt in C# for Android ≤11 archives. Closes Neo Backup `#885`.
- **Snapshot retention** — N (default 3) snapshots per device per source serial; tags + filters per AppManager Profiles model.
- **Configurable retention** — N snapshots, max-N-days, max-N-GB (Seedvault `#977`).
- **Android 16 QPR2 `<cross-platform-transfer platform="ios">` emit** in `meta.am.v5` so future iOS-target migrations get hinted at correctly.
- **Seedvault v1 / Restic-inspired format compatibility deferred** to v1.x — delta research confirms the new repos/chunks/blobs schema is too divergent to wedge into v0.9 without a parser-pair lift; ship a README note explaining v0 only.

### v1.0.0 — Polish + signed release + WCAG 2.2 + tests + CI + i18n _(target: post-v0.9)_

**Theme**: Trust + Reach.

- **Microsoft Trusted Signing (renamed Azure Artifact Signing per delta research) for the EXE** — $10/mo managed cert, no USB token, full SmartScreen reputation post-March 2024. Replaces the prior EV-cert plan. Sources: [Microsoft signing options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options) + delta research §code-signing.
- **CA/B Forum 460-day maximum cert lifetime + mandatory timestamping** (2026-03-01) — pin timestamp authority in CI signing step (RFC 3161). Source: delta research §CA/B Forum.
- **Reproducible builds** — `<Deterministic>true</Deterministic>`, `<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>`, SourceLink, GitHub `actions/attest-build-provenance` emitting SLSA L2 + Sigstore bundle. Free on public repos.
- **Velopack auto-update** — pin to a commit SHA, not a release tag (delta research §Velopack: still pre-release rolling builds only). GitHub Releases as the update source. Bundles `.NET 10` prereq install.
- **Catppuccin Latte/Frappé/Macchiato variants** behind a Settings dropdown — same 26-color schema, swap `ResourceDictionary`. Honors CLAUDE.md "include light theme when practical". No official `catppuccin/wpf` port has landed (delta research confirms).
- **WCAG 2.2 audit pass** — focus-visible (2.4.11), 24×24px target (2.5.8), `AutomationProperties.Name`/`HelpText`/`LiveSetting.Polite` on the live device list, full Narrator scan via AccScope + UIA Verify. Source: [W3C WCAG 2.2](https://www.w3.org/TR/WCAG22/).
- **Localization scaffolding** — externalize ~140 user-facing strings (current LOC count) to `Resources.resx`; `dotnet xliff` round-trip; en-US baseline + ko-KR (Samsung's home market) + pt-BR (Brazilian Samsung market). Source: [.NET 10 localization tooling](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/).
- **xUnit test project under `tests/PhoneFork.Core.Tests/`** — cover: `pm path` regex parsing of base + split-config lines, NDJSON event serialization shape, `MediaDiffer`/`SettingsDiffer` bucketing math with the new `GroupBy().Last()` dedup, debloat-list JSON-schema validation, `AdbShell.Arg` escape correctness (the `'\''` dance), `LocalPathNames.SafeFileName` Windows-reserved + path-escape edge cases. Target ~70% line coverage of Core. UI tests deferred to v1.1.
- **GitHub Actions CI** — `windows-latest`. Steps: `dotnet restore`, `dotnet build -c Release`, `dotnet test`, `dotnet list package --vulnerable --include-transitive` fail-on-found, lint `*.json` debloat-dataset assets, `dotnet publish` artifact upload, sign + attest, draft release.
- **Pre-merge `cosign verify-blob` smoke** on attached artefacts to confirm SLSA round-trip.
- **DPI-aware screenshots for README** — repo-global 125% DPI rule.
- **Branding** — 5-prompt RGBA logo flow (Minimal/App/Wordmark/Emblem/Abstract), 512×512 PNG. `branding/` folder + the prompt scaffold already committed (commit `d7ba677`).
- **Inno Setup installer** alongside ZIP — for users who want a Start-Menu shortcut.
- **CONTRIBUTING.md** at repo root — repo conventions, build instructions, issue filing. Includes "no Co-Authored-By" rule per repo policy.
- **GitHub Discussions enabled** for community feature requests + show-and-tell with debloat profiles.
- **Dependency upgrades from delta research**:
  - Serilog 4.3.1 (was 4.2.0) — first-class `IBatchedLogEventSink`, dotted-property names.
  - Spectre.Console 0.55.2 (was 0.55.0) — patch fixes.
  - Optional adoption: WPF-UI 4.3.0 if we want Fluent navigation; defer if Catppuccin contrast is enough.

---

## Later (post-v1.0) — _stretch and direction-setting_

### v1.x — Pre-flight scan ("Honesty pass")

Single-screen pre-flight modal before any migration starts. Sources: [community signal §1/§6/§7/§9](docs/community-signal.md), [Kaspersky on Android 14 Restricted Settings](https://www.kaspersky.com/blog/android-restricted-settings/49991/), [Knox SDK 3.12](https://docs.samsungknox.com/dev/knox-sdk/release-notes/knox-sdk-3-12/), [Pieterse 2023](https://doi.org/10.1016/j.fsidi.2023.301506).

- **2FA Authenticator audit** — scan installed packages for Google Authenticator, Authy, Microsoft Authenticator, Duo, FIDO2 hardware-bound apps; block migration with a wizard per-app.
- **Banking + DRM app warning** — list apps that re-auth on a new device-ID.
- **Secure Folder / Samsung Pass / Wallet / Bixby Routines detector** — surface deep-links to in-app export tools BEFORE wiping source.
- **CSC / locale / region mismatch banner** — already exists in v0.5.0 Wi-Fi tab; lift to its own pre-flight surface.
- **`allowBackup=false` enumerator** — parse manifest of every user app via AlphaOmega.ApkReader, list those Google Restore would skip.
- **Knox warranty-bit / bootloader-state check** — `getprop ro.boot.warranty_bit`, `ro.boot.flash.locked`, `keystore2 attestKey`. Refuse-or-warn-only options.

### v1.x — Profiles + lifecycle hooks ([AppManager Profiles](https://github.com/MuntashirAkon/AppManager), [Borgmatic hooks](https://torsion.org/borgmatic/docs/how-to/add-preparation-and-cleanup-steps-to-backups/))

- **Saved op-sets** — `phonefork apply --plan family-handoff.json`.
- **Before/after PowerShell hooks** per migration domain.
- **Healthchecks / webhook POST on done** ([Neo Backup #717](https://github.com/NeoApplications/Neo-Backup/issues/717)).
- **Windows toast notification on completion** ([LocalSend #2995](https://github.com/localsend/localsend/issues/2995)).
- **Running-jobs panel** ([Neo Backup #763](https://github.com/NeoApplications/Neo-Backup/issues/763)).

### v1.x — App scanner column ([AppManager](https://github.com/MuntashirAkon/AppManager))

- **Tracker + native-library report per APK** via the `android-libraries` + `android-debloat-list` dataset cross-reference (datasets we already ship).

### v1.x — Per-package signature verification on apply

`apksigner verify --print-certs` (or AlphaOmega.ApkReader's `SigningBlock`) on every base APK. Refuse-if-mismatch toggle on the Apps tab.

### v2.0 — Cross-platform (Avalonia 12)

- **Avalonia XPF compatibility smoke test** ([Avalonia 12 blog](https://avaloniaui.net/)) — run the WPF binary unchanged on macOS/Linux.
- **Native Avalonia port** if XPF perf is unacceptable — ~9 h/view per the Avalonia "expert guide".
- **WebUSB browser fallback** ([ya-webadb](https://github.com/yume-chan/ya-webadb)).

### v2+ — Bridge mode (cross-OS migration)

- **iOS source → Samsung sink** — `mautrix-imessage`-style puppet-thread mapping.
- **Stretch**: Beeper/Matrix bridge for iMessage → Samsung Messages round-trip.

### v2+ — Multi-source consolidation

Merge state from N source phones into one destination ([iMobie PhoneTrans](https://www.imobie.com/) pattern).

### v2+ — Plugin model

OEM-specific tabs as separable plugins ([KDE Connect](https://invent.kde.org/network/kdeconnect-kde) pattern). Pixel, OnePlus/Oppo, Xiaomi, Motorola each load a `phonefork-oem-<x>.dll` from `%LOCALAPPDATA%\PhoneFork\plugins\`.

### v2+ — Forensic-friendly receipts

- **Migration manifest with SHA-256 per file + Ed25519 signature** using a per-session keypair. Sources: [Pieterse 2023](https://doi.org/10.1016/j.fsidi.2023.301506), SWGDE 3.5.
- **UFDR-lite JSON + CSV exports** for cross-tool verification (Cellebrite, Magnet AXIOM). Source: SWGDE 2024.
- **Replay capability** — `phonefork apply --replay manifest.json`.

### v2+ — Headless / fleet / MSP mode

- **`phonefork --headless --plan plan.json --device <serial>`** for unattended workflows.
- **Optional Kestrel-on-localhost web UI** (Syncthing split pattern).
- **HTTP API mode on the helper APK** ([android-sms-gateway pattern](https://github.com/capcom6/android-sms-gateway)).

---

## Under Consideration

- **Discovery grid** ([LocalSend](https://github.com/localsend/localsend) pattern) — mDNS-discovered phones on the LAN as visual tiles. Contradicts USB-only default trust posture; needs explicit opt-in.
- **Live device dashboard cards** ([3uTools](https://3u.com/) pattern) — battery health, storage, IMEI, root-state, Knox warranty bit. Doubles parsing surface area against `dumpsys` formats that change per One UI minor.
- **In-app debloat-list editor / annotator** — UAD-NG already ships this; PhoneFork could either PR upstream improvements and re-sync, or build a parallel tool. Lean toward upstream.
- **Audio cast over ADB for ringtone preview** ([Adb-Device-Manager-2](https://github.com/Shrey113/Adb-Device-Manager-2)) — listen to migrated ringtone on PC speakers. Niche but cheap.
- **OpenCLI `--help-dump-opencli`** from Spectre.Console 0.52+ — surface CLI as machine-readable for AI agent integrations. Zero-cost; defer to v1.x.
- **CRC32 vs SHA-256 verification mode** for media sync — CRC32 fast but birthday-bound; SHA-256 correct but CPU-bound. Ship both with CRC32 default.

## Rejected (explicit, with reasoning)

- **Run as a WinUI 3 / MAUI app instead of WPF.** Rejected: WinUI 3 packaging friction on LTSC IoT boxes; MAUI is iOS-first via Mac Catalyst with no Linux. Stack-consistent with PatientImage / FileOrganizer.UI / OrganizeContacts / Snapture / Devicer / TeamStation.
- **Use `adb backup` for app data.** Rejected: dead since Android 12. Most apps target API 31+ and are excluded by default. Replaced by helper-APK content-provider reads (v0.7) + Smart Switch handoff (v0.8). Source: [Mobile Pentesting 101](https://securitycafe.ro/2026/02/02/mobile-pentesting-101-the-death-of-adb-backup-modern-data-extraction-in-2026/).
- **Use `bmgr restore` to apply D2D-transport backups.** Rejected: `bmgr backupnow` works but `bmgr restore` only delivers via OOBE — destination is past OOBE by the time PhoneFork opens. Source: [Lucid Co D2D testing recipe](https://lucid.co/techblog/2022/11/14/testing-android-device-to-device-transfer).
- **Drive Samsung Cloud REST API directly.** Rejected: no publicly documented REST surface; verified against Samsung Developer portal.
- **Run on Pixel/Calyx/Graphene as a Seedvault frontend.** Rejected: Seedvault is a system-signed backup transport that won't load on One UI.
- **Single Samsung-account login flow.** Rejected: zero-Samsung-account is the headline differentiator vs Smart Switch (community `§4`).
- **`adb tcpip 5555` legacy wireless mode.** Rejected: TLS-less, exposed port. Use only `adb pair` (Wireless Debugging) on Android 11+. Source: [arXiv 2401.08961](https://arxiv.org/abs/2401.08961).
- **Re-implement Samsung Themes purchases transfer.** Rejected: account-bound, encrypted under `/data/data/com.samsung.android.themecenter/` (root-only), unredeemable on a different account.
- **Reuse Knox SDK at consumer scope.** Rejected: Android 15+ requires AE Device Owner / Profile Owner status. Source: [Knox SDK 3.12 notes](https://docs.samsungknox.com/dev/knox-sdk/release-notes/knox-sdk-3-12/).
- **Use Frida runtime hooking to dump app keys.** Rejected from v1; tracked as a v2 "advanced mode" stretch only (requires root + Frida server).
- **Bundle MaterialDesign3 wholesale.** Rejected: M3 aesthetic clashes with Catppuccin Mocha. Use MaterialDesignThemes 5.3.2 selectively for DataGrid + dialogs only.
- **Default to Velopack at v0.2.** Rejected: PhoneFork is a "run twice in a phone-lifecycle" tool, not a daily driver — auto-update infra premature until v1.0.
- **EV code-signing certificate.** Rejected (delta research): March 2024 SmartScreen change neutralized EV's instant-reputation advantage. Microsoft Trusted Signing / Azure Artifact Signing is the right path at $10/mo with equivalent SmartScreen behavior + no USB token. EV reserved only for kernel-mode drivers (irrelevant here).
- **Seedvault v1 (Restic-inspired) format compatibility in v0.9.** Rejected from v0.9 (delta research): new repos/chunks/blobs schema too divergent. README note explaining v0 only; v1 compat tracked as v1.x stretch.

---

## What could derail this roadmap

Risk catalogue. Probability × impact, descending.

- **Smart Switch UI Automation breaks on a Samsung update** (high prob, medium impact). FlaUI-driven control is fragile. **Now compounded by the MS-Store migration** — dual locator strategy needed (delta research §Smart Switch). Mitigation: v0.8 ships with both locators + version-pinned UIA paths + a fallback "drive via shell-out to legacy MSI exe args" path.
- **Android 17 stable lands with new shell-UID restrictions** (medium prob, medium impact). Beta 3 hit Platform Stability May 2026 but a stable could ship surprise gates. Mitigation: per-Android-version regression-test corpus, declare `ACCESS_LOCAL_NETWORK` on helper APK now.
- **One UI minor bumps a Samsung settings key out of shell-UID write allowlist** (high prob, low impact). Settings tab `settings put` fails loud but won't crash. Mitigation: per-One-UI version regression corpus.
- **Wireless ADB pairing reliability over Wi-Fi 6E / Wi-Fi 7 enterprise networks** (medium prob, medium impact). USB fallback is automatic in PhoneFork.
- **Helper APK signature mismatch breaking install-update flow** (medium prob, high impact). Mitigation: keystore backed up to two independent locations + recovery procedure in CONTRIBUTING.md.
- **AdvancedSharpAdbClient maintainer drops support** (low prob, high impact). Only mature .NET ADB client. Mitigation: vendor a local copy at v1.0; have a CliWrap-driven `adb.exe` fallback already partially in place (`AdbPairingService` precedent).
- **AppManager backup-format breaks compat between v5 and v6** (low prob, medium impact). Delta research confirms `meta.am.v5` stable since 2025-07. Mitigation: integration tests against committed sample backups.
- **Samsung adds Knox-level USB lockdown** (low prob, very high impact). Knox MDM can blacklist `com.sec.android.easyMover` in enterprise tenants. PhoneFork unaffected on consumer phones. Document the scope.
- **CA/B Forum tightens further** (medium prob, low impact) — the 460-day cap is already enacted (delta research). Trusted Signing remains unaffected; switch providers if not.
- **Velopack stays pre-release indefinitely** (medium prob, low impact). Delta research: still rolling pre-release builds. Mitigation: pin to commit SHA at ship time, or substitute with a hand-rolled GitHub-Releases-poll updater.

---

## Appendix — Sources

All Now/Next claims trace to one of these. Roughly grouped. Prior-research artefacts contain the deeper citation graph (~60 sources each); this Appendix curates the load-bearing ones.

### Repo-local research artefacts
- [docs/oss-dependencies.md](docs/oss-dependencies.md) — Windows/.NET OSS library research (16 sections).
- [docs/oss-references.md](docs/oss-references.md) — Android-side OSS reference implementations.
- [docs/competitor-research.md](docs/competitor-research.md) — Commercial competitor deep-dive (21 tools).
- [docs/community-signal.md](docs/community-signal.md) — 11 pain-point categories with 70+ source URLs.
- [docs/migration-feasibility.md](docs/migration-feasibility.md) — Original feasibility memo.
- [docs/research-delta-2026-05-14.md](docs/research-delta-2026-05-14.md) — Incremental delta refresh (22 sources, 7 material findings).
- [CHANGELOG.md](CHANGELOG.md) — Per-version detail.
- [CLAUDE.md](CLAUDE.md) (local-only) — Repo working notes; gotchas (sync-over-async deadlock, XAML unicode-entity).

### Direct OSS competitors
- **Universal Android Debloater Next Generation** — https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation
- **AppManager** — https://github.com/MuntashirAkon/AppManager (release v4.0.5; backup format `meta.am.v5` confirmed frozen per delta research)
- **Neo Backup** — https://github.com/NeoApplications/Neo-Backup (issues #525, #688, #709, #717, #719, #738-744, #752, #758, #763, #766, #773, #780, #788, #795, #820, #822, #844, #885, #906, #908, #909, #977)
- **Migrate (BaltiApps)** — https://github.com/BaltiApps/Migrate-OSS
- **Seedvault** — https://github.com/seedvault-app/seedvault (v1 Restic-inspired format per delta research)
- **Shizuku** — https://github.com/RikkaApps/Shizuku ([v13.6.0 release](https://github.com/RikkaApps/Shizuku/releases/tag/v13.6.0)) + https://github.com/RikkaApps/Shizuku-API
- **scrcpy** — https://github.com/Genymobile/scrcpy ([v4.0 SDL3 release per delta research](https://github.com/Genymobile/scrcpy/releases), [develop.md](https://github.com/Genymobile/scrcpy/blob/master/doc/develop.md))
- **KDE Connect** — https://invent.kde.org/network/kdeconnect-kde, https://invent.kde.org/network/kdeconnect-android
- **LocalSend** — https://github.com/localsend/localsend (issues #2995, #3007, #3014, #3024, #3036, #3041, #3071; [protocol-v2.md](https://github.com/localsend/localsend/blob/main/documentation/protocol-v2.md))
- **ya-webadb (Tango)** — https://github.com/yume-chan/ya-webadb
- **Android Backup Extractor** — https://github.com/nelenkov/android-backup-extractor
- **Canta** — https://github.com/samolego/Canta
- **itxjobe/samsungdebloat** — https://github.com/itxjobe/samsungdebloat/tree/main/
- **gonodono/adbsms** — https://github.com/gonodono/adbsms
- **WallpaperUnbricker** — https://github.com/adryzz/WallpaperUnbricker
- **android-sms-gateway** — https://github.com/capcom6/android-sms-gateway
- **Adb-Device-Manager-2** — https://github.com/Shrey113/Adb-Device-Manager-2
- **Syncthing BEP v1** — https://docs.syncthing.net/specs/bep-v1.html
- **restic design** — https://restic.readthedocs.io/en/stable/100_references.html#design
- **Borgmatic hooks** — https://torsion.org/borgmatic/docs/how-to/add-preparation-and-cleanup-steps-to-backups/
- **iMazing transfer guide** — https://imazing.com/guides/how-to-transfer-data-from-one-iphone-to-another

### Commercial competitors
- **Samsung Smart Switch** — https://www.samsung.com/us/support/owners/app/smart-switch (MS Store migration per delta research)
- Smart Switch backup format wiki — http://fileformats.archiveteam.org/wiki/Samsung_Smart_Switch_backup
- **Cellebrite Smart Switch goldmine** — https://cellebrite.com/en/samsung-smart-switch-a-forensic-goldmine/
- **Wondershare MobileTrans** — https://mobiletrans.wondershare.com/
- **iMobie DroidKit / AnyTrans / PhoneTrans** — https://www.imobie.com/
- **MOBILedit Phone Manager / Forensic** — https://www.mobiledit.com/

### Community signal (representative — full set in docs/community-signal.md)
- **HN — Setting up phones is a nightmare** — https://news.ycombinator.com/item?id=47170958 + https://joelchrono.xyz/blog/setting-up-phones-is-a-nightmare/
- **HN — How do you backup Android (2024)** — https://news.ycombinator.com/item?id=42648597
- **Samsung Community: app data missing** — https://forums.androidcentral.com/threads/samsung-smart-switch-does-not-transfer-apps.1020392/
- **Samsung Community: Wi-Fi passwords** — https://eu.community.samsung.com/t5/galaxy-s23-series/how-to-restore-wifi-passwords-from-previous-device/td-p/10785508
- **Samsung Community: stuck at 99%** — https://us.community.samsung.com/t5/Galaxy-S24/Samsung-Switch-hangs-at-99-9-and-1-minute-remainin-when-trying/td-p/2877811
- **Samsung Community: Secure Folder** — https://xdaforums.com/t/secure-folder-not-restoring-by-smart-switch.4665109/
- **Samsung Community: NEW→OLD direction** — https://eu.community.samsung.com/t5/questions/no-option-to-downgrade-on-smartswitch/td-p/4300646
- **XDA: Samsung debloat threads** — https://xdaforums.com/t/s25-ultra-debloat-and-privacy-list.4716655/

### Standards, APIs, platform
- **AOSP shell-cmd surface** — https://source.android.com/docs/core/tests/vts/shell-commands
- **Android Auto Backup docs** — https://developer.android.com/identity/data/autobackup
- **Android 16 QPR2 announce** — https://android-developers.googleblog.com/2025/12/android-16-qpr2-is-released.html
- **Android 17 platform versions** — https://developer.android.com/about/versions/17 (Beta 3 Platform Stability per delta research)
- **Android 15 Foreground Service types** — https://developer.android.com/about/versions/15/changes/foreground-service-types
- **Android 14 Restricted Settings (Kaspersky)** — https://www.kaspersky.com/blog/android-restricted-settings/49991/
- **Lucid Co — D2D bmgr testing recipe** — https://lucid.co/techblog/2022/11/14/testing-android-device-to-device-transfer
- **LineageOS adb_wifi.md** — https://github.com/LineageOS/android_packages_modules_adb/blob/lineage-23.2/docs/dev/adb_wifi.md
- **Samsung Knox SDK 3.12 release notes** — https://docs.samsungknox.com/dev/knox-sdk/release-notes/knox-sdk-3-12/
- **Mobile-hacker — Shizuku capabilities (Jul 2025)** — https://www.mobile-hacker.com/2025/07/14/shizuku-unlocking-advanced-android-capabilities-without-root/

### Dependency releases (versions per delta research)
- **AdvancedSharpAdbClient** — https://github.com/SharpAdb/AdvancedSharpAdbClient/releases (3.6.16, no material updates)
- **Serilog 4.3.1** — https://github.com/serilog/serilog/releases (was 4.2.0)
- **CommunityToolkit.Mvvm 8.4.2** — https://devblogs.microsoft.com/dotnet/announcing-the-dotnet-community-toolkit-840/
- **WPF-UI 4.3.0** — https://github.com/lepoco/wpfui/releases (delta research §WPF-UI)
- **MaterialDesignThemes 5.3.2** — https://www.nuget.org/packages/MaterialDesignThemes/
- **Spectre.Console 0.55.2** — https://spectreconsole.net/blog/2025-10-10-spectre-console-0-52-released (delta research)
- **Velopack** — https://velopack.io/ + https://github.com/velopack/velopack (still pre-release per delta research; pin SHA)
- **QRCoder 1.6.0** — https://www.nuget.org/packages/qrcoder/
- **.NET 10 announce** — https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/

### Security / signing / standards
- **GHSA-5crp-9r3c-p9vr (Newtonsoft.Json)** — https://github.com/advisories/GHSA-5crp-9r3c-p9vr
- **Microsoft code-signing options (Trusted Signing → Azure Artifact Signing)** — https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options (rename + 460-day cap per delta research)
- **Sigstore + cosign bundles** — https://blog.sigstore.dev/cosign-verify-bundles/
- **W3C WCAG 2.2** — https://www.w3.org/TR/WCAG22/
- **Avalonia 12** — https://avaloniaui.net/ + https://avaloniaui.net/blog/the-expert-guide-to-porting-wpf-applications-to-avalonia
- **Catppuccin palette** — https://catppuccin.com/palette/

### Academic + forensics (load-bearing only — full set in docs/oss-references.md)
- **Hur, Lee, Cha (2021)** — "Forensic analysis of Samsung Smart Switch backup files", *FSI:DI* vol. 37, https://doi.org/10.1016/j.fsidi.2021.301172
- **Pieterse, Olivier, van Heerden (2023)** — "Authenticity of smartphone evidence", *FSI:DI* vol. 44, https://doi.org/10.1016/j.fsidi.2023.301506
- **Mayrhofer, Stoep, Brubaker, Kralevich (2021)** — "The Android Platform Security Model", *ACM TOPS* 24(3), https://doi.org/10.1145/3448609
- **NIST SP 800-101 Rev.1 (2014)** — https://doi.org/10.6028/NIST.SP.800-101r1
- **SWGDE Mobile Device Evidence Collection v3.0 (2023)** — https://swgde.org/documents/published-by-committee/mobile-devices/
- **Project Zero FORCEDENTRY (2022)** — https://googleprojectzero.blogspot.com/2022/03/forcedentry-sandbox-escape.html
- **arXiv 2401.08961 — ADB security empirical study (2024)** — https://arxiv.org/abs/2401.08961
- **Mobile Pentesting 101 — Death of adb backup (2026)** — https://securitycafe.ro/2026/02/02/mobile-pentesting-101-the-death-of-adb-backup-modern-data-extraction-in-2026/
- **MasonFlint44 split-APK install gist** — https://gist.github.com/MasonFlint44/4b32d86da40f79d12355bb993fe23953
- **AnatomicJC ADB app-backup gist** — https://gist.github.com/AnatomicJC/e773dd55ae60ab0b2d6dd2351eb977c1

---

## Notes on this document

- **Versioning**: `vMAJOR.MINOR.PATCH`. This roadmap iterates as a single document; the date in the v-line at top tracks the last rewrite. **2026.05.14b** = the second rewrite that day (after v0.6.5 ship + hardening pass + Phase-1 delta research).
- **Now / Next / Later / Under Consideration / Rejected** tiering. Every Now/Next item maps to ≥1 source URL above.
- **Self-audit ledger** (Phase 5 of the research protocol):
  - Security: ✅ Trust theme + sandbox parsers + USB-only + per-install RSA key + Trusted Signing + SLSA L2.
  - Accessibility: ✅ v1.0 WCAG 2.2 audit with concrete actions.
  - i18n/l10n: ✅ v1.0 localization scaffolding (en/ko/pt-BR baseline).
  - Observability: ✅ NDJSON audit log (shipped) + Healthchecks webhook (v1.x).
  - Testing: ✅ v1.0 xUnit Core suite with explicit coverage list.
  - Docs: ✅ this ROADMAP + 6 docs/ research artefacts + per-release CHANGELOG.
  - Distribution/packaging: ✅ v1.0 Velopack + Inno Setup + ZIP.
  - Plugin ecosystem: ✅ v2+ OEM plugin model.
  - Mobile: ✅ helper APK in v0.7.
  - Offline/resilience: ✅ resumable transfers (shipped) + checkpoints + USB-only.
  - Multi-user/collab: ✅ partial (fleet/MSP mode at v2+).
  - Migration paths: ✅ this whole tool.
  - Upgrade strategy: ✅ Velopack at v1.0 (commit-SHA pinned per delta research).
