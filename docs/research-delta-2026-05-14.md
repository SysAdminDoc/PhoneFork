# PhoneFork — Research Delta (2026-05-14 baseline → 2026-05-16 check)

**Scope:** Incremental check against the 5 baseline docs in `docs/`. Floor met: 20 sources confirmed.
**Methodology:** GH API on each release feed + web search for non-GH signal (Smart Switch, Android 17, CA/B Forum). Bias = "no material change" over speculation.

---

## 1. Android Migration / Backup / Debloat OSS — last 30 days

| Project | Latest | Date | Delta vs baseline |
|---|---|---|---|
| [scrcpy v4.0](https://github.com/Genymobile/scrcpy/releases/tag/v4.0) | **v4.0** | 2026-05-12 | **MATERIAL.** SDL2→SDL3 migration, **bundled platform-tools/adb bumped to 37.0.0**, flex display support, camera torch/zoom, `--background-color`, F11 fullscreen, device-serial-with-spaces fix, mDNS TCP device detection. Win console set to UTF-8. **Impact on PhoneFork v1.0:** if we end up bundling scrcpy as the optional mirroring helper (currently deferred), pin to v4.0 — older v3.x are now stale. The adb 37.0.0 bundled there is the same one I'd want to ship alongside PhoneFork's own adb. |
| [Neo-Backup 8.3.18](https://github.com/NeoApplications/Neo-Backup/releases/tag/8.3.18) | **8.3.18** | 2026-05-03 | Minor. New onboarding flow, NeoPrefs replacing legacy prefs, encryption-warning dialog removed. **No backup-format change** — v0.9.0 AppManager-compat work is unaffected; Neo-Backup uses its own tar.gz+aes format, still divergent from AppManager. Worth keeping in the dual-import compat matrix as a stretch v0.9.1 item. |
| [UAD-NG v1.2.0](https://github.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/releases/tag/v1.2.0) | v1.2.0 | 2026-01-12 | **Unchanged since baseline.** Package list still hot-fetched on launch — PhoneFork's debloat lists can stay aligned by hitting their `packages.json` raw URL (no app version coupling). |
| [AppManager v4.0.5](https://github.com/MuntashirAkon/AppManager/releases/tag/v4.0.5) | v4.0.5 | 2025-07-28 | **Unchanged since baseline.** No new release in 10+ months. v0.9.0 AppManager-format backup compat target is stable — the `.am.tar.gz` v5 schema is frozen. |
| [Shizuku v13.6.0](https://github.com/RikkaApps/Shizuku/releases/tag/v13.6.0) | v13.6.0 | 2025-05-25 | **Unchanged since baseline.** Auto-start-on-trusted-Wi-Fi (Android 13+, no root) already shipped; that's the path PhoneFork's helper-APK plan in v0.7.0 should document as the "second device" recommended path. |
| [Canta v3.2.2](https://github.com/samolego/Canta/releases/tag/v3.2.2) | v3.2.2 | 2026-03-01 | No material change since baseline (workflow-only fix). |
| [LocalSend v1.17.0](https://github.com/localsend/localsend/releases/tag/v1.17.0) | v1.17.0 | 2025-02-20 | **Unchanged since baseline.** |
| [Seedvault](https://github.com/seedvault-app/seedvault) | `android16` branch | 2026-01-14 | **MATERIAL (architectural).** New **Restic-inspired v1 backup format** (repos / chunks / blobs / snapshots / dedup) now live in `android16` branch alongside legacy v0. Extractor for v1 is WIP. **Impact on PhoneFork:** Seedvault was already out of scope for direct import (system-app only), but if any user ever asks "can you read my Seedvault backup," answer is "v0 only via the existing parser, v1 not yet stable." Note in v0.9.0 README. |

## 2. .NET / NuGet — since 2026-04-01

| Package | Pinned | Latest | Date | Action |
|---|---|---|---|---|
| [AdvancedSharpAdbClient](https://github.com/SharpAdb/AdvancedSharpAdbClient/releases/tag/v3.6.16) | 3.6.16 | 3.6.16 | 2026-01-13 | **Hold.** No new release. ADB Sync V2 already in 3.6.16. |
| [Serilog](https://github.com/serilog/serilog/releases/tag/v4.3.1) | 4.2.0 | **4.3.1** | 2026-02-10 | **Bump in v0.7.0.** Targets .NET 10, fixes trimming when transitive, reduces allocations. No breaking API changes from 4.2 → 4.3.1. |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet/releases/tag/v8.4.2) | 8.4.2 | 8.4.2 | 2026-03-25 | Hold. Current. |
| [WPF-UI](https://github.com/lepoco/wpfui/releases/tag/4.3.0) | unused | **4.3.0** | 2026-05-04 | **NEW** — relevant to v1.0 polish. Unicode tooltip fix, NumberBox compact spinner, MenuItem suggestion update. If we adopt WPF-UI for v1.0 Fluent styling, pin 4.3.0 minimum. |
| [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit/releases/tag/v5.3.2) | 5.3.2 | 5.3.2 | 2026-05-01 | Hold. Tiny patch (Clock `MinuteSelectionStep`). |
| [HandyControl](https://github.com/HandyOrg/HandyControl/releases/tag/v3.5.1) | 3.5.1 | 3.5.1 | unchanged | Hold. |
| [Spectre.Console](https://github.com/spectreconsole/spectre.console/releases/tag/0.55.2) | 0.55.0 | **0.55.2** | 2026-04-17 | **Bump in v0.7.0.** Adds default-value to selection/multi-prompts (useful for our `phonefork pair` interactive flow) + variation-selector/ZWJ/surrogate-pair length fix (matters for any emoji-bearing device names from Samsung). Still pre-release per project convention. |
| [CliWrap](https://github.com/Tyrrrz/CliWrap/releases/tag/3.10.1) | 3.10.1 | 3.10.1 | 2026-03-21 | Hold. Now AOT-clean via `LibraryImport`. |
| [QRCoder](https://github.com/codebude/QRCoder/releases/tag/v1.6.0) | 1.6.0 | 1.6.0 | unchanged | Hold. |
| [Velopack](https://github.com/velopack/velopack/releases) | planned v1.0 | rolling `0.0.1589-ga2c5a97` | 2026-04-14 | **No stable v1.x yet** — Velopack still publishes pre-release rolling builds only. Decision: hold pinning until they cut a stable v1, otherwise we sign a moving target. If v1.0 release date pressure mounts, pin commit SHA in `Directory.Packages.props`. |
| [Avalonia](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.0.3) | planned v2+ | **12.0.3** | 2026-05-11 | **NOTE for v2 port:** 12.0.3 is security-only (SharpCompress 0.41→0.48 inside VNC). v12 is the cross-platform target; track but don't adopt until PhoneFork v2 cycle. |

## 3. Security advisories / CVEs since 2026-04-01

- GH Advisory DB queried for both `SharpAdb/AdvancedSharpAdbClient` and `serilog/serilog` — **empty list** (no advisories filed).
- No `CVE-2026-*` CVEs surface for `adb` / `platform-tools`. Historical CVE-2022-20128 (directory traversal) was **rejected**, CVE-2014-1909 and CVE-2020-0409 long since patched.
- No Newtonsoft.Json transitive pull in our graph (verified — we use System.Text.Json everywhere).
- **Adjacent CVE worth flagging:** **CVE-2026-26151** (RDP spoofing, actively exploited) — patched by April 2026 Windows update, which also flips unsigned `.rdp` files to "untrusted." Doesn't touch PhoneFork directly but is relevant to anyone running the dev build over RDP to a test box; add a one-liner to internal dev-setup notes. [Source](https://blog.sonnes.cloud/your-rdp-remote-desktop-files-are-now-untrusted-after-the-april-2026-windows-patch-sign-them-with-powershell/)

## 4. Samsung Smart Switch PC — last 60 days

- **No version-numbered changelog** ever published by Samsung. Support page last refreshed 2026-01-12 (US) and 2026-05-04 (AU).
- **One material distribution-channel signal:** community report that the standalone Smart Switch PC installer shows an in-app banner saying the classic distribution is being discontinued and **migrating to Microsoft Store** for future updates. Source: [Samsung EU community thread](https://eu.community.samsung.com/t5/computers-it/new-smart-switch-pc-software-update-system/td-p/12136709).
- **Impact on PhoneFork v0.8.0:** the Smart Switch UI Automation handoff plan needs to handle two installer footprints — `C:\Program Files (x86)\Samsung\Smart Switch PC\` (legacy MSI) **and** the MS Store sandboxed package (`%LocalAppData%\Packages\SamsungElectronicsCo...`). Detection code should probe both. UIA element paths likely identical (same WPF binary), but window class names may differ when Store-hosted.
- **No backup-format change detected.** `.bak` container schema stays as-is for v0.8.0 planning. No new paywalled categories observed.

## 5. Android 16 QPR2 / Android 17 preview

- **Android 16 QPR2 already shipped** (December 2025 — predates baseline, no delta) but reconfirming the relevant bit: starting **September 2026, certified Android will require apps to be from verified developers — ADB-installed apps are explicitly exempt.** Reinforces PhoneFork's helper-APK push-via-ADB approach for v0.7.0. [Source](https://developer.android.com/about/versions/16/qpr2/release-notes)
- **Android 17 Beta 3 = Platform Stability** (April 2026 per Beta 4 post). API surface locked. New `ACCESS_LOCAL_NETWORK` permission (NEARBY_DEVICES group) — PhoneFork's LAN/wireless-ADB pairing flow will need to request it when targeting API 36+ in the helper APK. [Source](https://android-developers.googleblog.com/2026/04/the-fourth-beta-of-android-17.html)
- **No new `cmd` subcommands** surfaced for shell UID in 16 QPR2 or Android 17 betas. The `cmd package`, `cmd appops`, `cmd device_policy` surfaces PhoneFork uses today remain stable.
- **Android Canary channel replaces Developer Previews** going forward — informational; doesn't change build targets.
- **Tangential:** Google's [Android CLI v0.7 preview](https://lilting.ch/en/articles/android-cli-v0-7-agent-preview) (2026-04-16) consolidates `sdkmanager`/`adb`/`emulator` under a single `android` binary. Not a dependency, but if it stabilizes, the v0.8/v0.9 SDK-bootstrap scripts could thin out.

## 6. Catppuccin org — since 2026-05-01

- **No official `catppuccin/wpf` port exists** (verified — repo lookup returns 404). The closest in-org assets are `catppuccin/catppuccin` palette (latest 2022 tag v0.2.0) and various IDE/web ports. Mid-May 2026 updates landed on `catppuccin/fleet`, `catppuccin/vscode`, `userstyles`, `nix`, `website`, `pantone`, `monkeytype` — **nothing WPF-shaped**.
- **Impact on PhoneFork:** Catppuccin Mocha must still be hand-rolled as a ResourceDictionary (same pattern used in TeamStation / DicomUtilitySuite / Snapture). No change to v1.0 polish plan.

## 7. Code signing / Trusted Signing landscape — since 2026-04-01

- **CA/B Forum Ballot CSC-31:** publicly trusted code-signing certs capped at **460 days** validity, effective **March 1, 2026** (already in effect). Renewal cadence is now ~15 months instead of ~3 years. [Source](https://securityboulevard.com/2026/02/code-signing-certificate-validity-changes-now-in-effect-from-february-2026/)
- **Microsoft Trusted Signing → renamed "Azure Artifact Signing."** Same service, identity-validated CN must be legal entity name, US/CA/EU/UK orgs + US/CA individuals only. [Source](https://learn.microsoft.com/en-us/azure/artifact-signing/faq)
- **Impact on PhoneFork v1.0 signing:** the v1.0 polish tier needs to either (a) budget for short-cycle public CS cert renewal, or (b) adopt Azure Artifact Signing (HSM-backed, auto-renewing) — strong recommendation is (b) given ~15-month manual cycle is painful for a side-project release cadence. Add to v1.0 ROADMAP as a sub-item. Timestamping with a real TSA still preserves pre-expiry signatures — important: every signed PhoneFork build MUST be timestamped or old installers will brick after 460 days.

---

## Net roadmap impact

- **v0.7.0:** bump Serilog 4.2 → 4.3.1, Spectre.Console 0.55.0 → 0.55.2 in `Directory.Packages.props`. Document Shizuku auto-start-on-trusted-Wi-Fi as the secondary device pair path. Helper APK should declare `ACCESS_LOCAL_NETWORK` if/when we raise targetSdk to API 36.
- **v0.8.0:** Smart Switch detection logic must probe both legacy MSI install path and MS Store sandboxed package — non-trivial discovery work; add as a v0.8 ROADMAP sub-item.
- **v0.9.0:** Seedvault v1 format flagged as "out of scope until extractor stabilizes" in compat-matrix README. AppManager v5 format still frozen — no schema drift.
- **v1.0.0:** Adopt Azure Artifact Signing (preferred) or budget annual public CS renewal under 460-day rule. Mandatory timestamping. Pin WPF-UI 4.3.0+ if used. Velopack stable still not cut — either pin commit SHA or substitute Squirrel/Clowd.Squirrel for first ship.
- **v2+ (Avalonia port):** track Avalonia 12.x line; 12.0.3 is current.

**Sources confirmed:** 22 (8 GH release feeds + 11 .NET/NuGet GH release feeds + 3 web-search clusters covering Smart Switch, Android 16/17, CA/B Forum + Seedvault wiki + Shizuku).
