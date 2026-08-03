# PhoneFork Roadmap

Version: 2026.08.03

This roadmap is a point-in-time plan built from local repository reconnaissance,
project memory consolidation, dependency checks, GitHub competitor inspection,
and official Android/Samsung/Microsoft/Apple/GitHub sources. Source IDs below
refer to the historical research snapshot used to shape the plan.

## Product Thesis

PhoneFork should be the local-first Windows migration cockpit for Samsung Galaxy
users who need an honest, auditable transfer plan instead of a black-box cloud
or account-bound handoff. It should copy what Android permits without root,
explain what it cannot copy, provide safe handoffs to official tools where they
are better, and preserve reversibility for every destructive or privileged
operation.

Guardrails:

- No root requirement and no promise to read third-party private app data.
- USB-first ADB with wireless ADB opt-in, session timeout, patch gating, and
  serial-hash logging.
- Smart Switch, Google Messages, OneDrive, Quick Share, Wallet, and Android/iOS
  first-party transfer flows are complements, not enemies.
- WPF remains the v1 host. Avalonia/WebUSB/iOS-source work is v2+ unless a
  smaller compatibility bridge becomes obvious.
- Every roadmap claim should map to a local source, official platform source,
  competitor signal, dependency scan, or marked hypothesis.

## Current State

| Area | Verified state |
|---|---|
| Version | `CHANGELOG.md` is at `v0.9.1-pre`; README badge, WPF title, and manifest are synced for the local release drain. |
| Repo | `SysAdminDoc/PhoneFork`, public, MIT, default branch `main`; first unsigned prerelease `v0.9.0-pre` is published with WPF/CLI ZIPs and `ARTIFACT-TRUST.txt`. Sources: L03, L31. |
| Host stack | .NET 10, WPF, MVVM, Spectre CLI, AdvancedSharpAdbClient, bundled platform-tools. Sources: L08-L13, L26. |
| Helper APK | Gradle/Kotlin helper targets SDK 36 and now emits `phonefork.helper.v1` JSON envelopes for SMS, call log, contacts, Wi-Fi capability metadata, wallpaper metadata, ringtone defaults, and dictionary. Restore writes remain guarded/disabled. Sources: L14-L17. |
| CI/release | GitHub Actions workflows were intentionally removed. The supported lane is local restore/build/test, version consistency, unsigned WPF/CLI publish, and unsigned helper APK assembly/lint. |
| Dependency state | No vulnerable or outdated NuGet packages found after R012; `xunit` 2.9.3 remains flagged legacy and xUnit v3 is intentionally deferred. Sources: L27-L29, L38. |
| Research artifacts | The historical research snapshot was removed by repository maintenance; this roadmap and the root product docs are the maintained state. |

## Research Deltas That Changed The Plan

1. Samsung Gallery direct OneDrive sync ends on September 30, 2026, not April
   2026. The pre-flight warning should point users to OneDrive camera backup and
   distinguish existing OneDrive files from Samsung Gallery sync. Source: S10.
2. Samsung Messages is being discontinued in the US market in July 2026, with
   Samsung directing users to Google Messages; Samsung says existing
   conversations transfer between those apps but may take up to about 24 hours.
   Source: S08.
3. Android developer verification explicitly exempts ADB installs, so
   PhoneFork's helper APK sideload path remains viable. Source: S01.
4. CVE-2026-0073 is a critical `adbd` remote/proximal code execution issue
   fixed by the 2026-05-01 patch level. PhoneFork's wireless ADB patch gate is a
   core safety feature. Sources: S05, S06.
5. Quick Share now covers Android, Chromebooks, select Windows PCs,
   AirDrop-capable Apple devices on supported Android phones, QR fallback,
   24-hour encrypted server-hosted transfers, and documented size/count limits.
   PhoneFork should recommend it for specific ad hoc categories instead of
   trying to own every file-transfer case. Sources: S11, S12.
6. Apple's Android transfer support now documents iOS 26.3/iPadOS 26.3 to
   Android 17 flows including eSIM, photos, contacts, calendars, call history,
   messages, accessibility settings, home screen layout, and wallpaper. This
   makes iOS-source support a watch item for v2, not a v1 dependency. Source:
   S14.
7. Artifact Signing is the practical Windows signing path for this repo; it does
   not issue EV certificates, but Basic is $9.99/month for 5,000 signatures and
   Premium is $99.99/month for 100,000 signatures. Sources: S15, S16.

## v2 Watchlist

- Avalonia or another cross-platform host only after the WPF v1 cockpit is
  complete.
- WebUSB/WebADB companion only if browser ADB can match PhoneFork's safety and
  logging requirements.
- iOS-source migration helper after Android 17/iOS 26.3 flows stabilize and
  there is a clear gap PhoneFork can solve locally.
- Optional local HTTP helper API only after ContentProvider contract security is
  proven.
- Commercial-grade device corpus and test lab once public releases create real
  user demand.

Sources: G12, G13, S13, S14, C01-C08.

## Rejected Or Deferred Ideas

| Idea | Decision | Reason |
|---|---|---|
| Promise private `/data/data` migration without root | Reject | Android privilege boundary and project honesty stance. Sources: L22, S07. |
| Use legacy `adb tcpip 5555` as a convenience path | Reject | Conflicts with current wireless ADB security posture and CVE-2026-0073 risk. Sources: S02, S05, S06. |
| Direct Samsung Wallet/Pass token migration | Reject | OEM/account-bound and not accessible to PhoneFork. Use pre-flight guidance. Sources: S09, L21. |
| Root-first migration mode | Defer indefinitely | It contradicts the product thesis and narrows current Samsung reach. Sources: L22, G17, G21. |
| Full Smart Switch clone | Reject | Smart Switch has OEM privileges and account access PhoneFork will not have. PhoneFork should interop and warn. Source: S07. |
| Web-first rewrite | Defer | WPF is already the v1 app and current Core services are Windows-oriented. Sources: L09, L13. |

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| Helper APK grants sensitive permissions and is misused by another app | High | UID gate, shell-only queries, short install window, clear uninstall, contract tests. |
| Wireless ADB exposes a vulnerable device | High | USB-first pairing, patch-level gate, timeout, kill switch, clear CVE warning. |
| Roadmap overpromises app-private data migration | High | Keep honesty reports and category capability matrices in every user-facing flow. |
| Embedded debloat dataset becomes stale | Medium | Overlay feed, source-backed warnings, conservative defaults. |
| Release artifacts are unsigned or poorly trusted | Medium | Publish locally with explicit unsigned status and checksums; never imply code-signing trust. |
| External platform policy changes | Medium | Date-stamped research logs and source refresh before each release. |
| No real-device validation in this session | Medium | Mark hardware validation as required before public v1. |
