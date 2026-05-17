# Security And Dependency Review - 2026-05-17

## Local Package Scan

Commands:

```powershell
dotnet list PhoneFork.slnx package --vulnerable --include-transitive
dotnet list PhoneFork.slnx package --deprecated
dotnet list PhoneFork.slnx package --outdated
```

Findings:

- No vulnerable NuGet packages were reported.
- App, CLI, and Core had no deprecated package reports.
- Tests use `xunit` 2.9.3, which NuGet marks as legacy with `xunit.v3` as the alternative.
- R012 applied all non-xUnit outdated candidates:
  - QRCoder 1.6.0 -> 1.8.0.
  - Spectre.Console 0.55.0 -> 0.55.2.
  - JsonSchema.Net 7.3.0 -> 9.2.1.
  - Serilog.Sinks.File 6.0.0 -> 7.0.0.
  - coverlet.collector 6.0.4 -> 10.0.0.
  - Microsoft.NET.Test.Sdk 18.0.1 -> 18.5.1.
- Post-update `dotnet list PhoneFork.slnx package --outdated` reports no package updates.

Operational note:

- Running solution-level `dotnet list package --outdated` and `--vulnerable`
  concurrently failed once with `Cannot create a file when that file already
  exists`. Sequential reruns succeeded. Keep those scans sequential in local
  scripts.

Sources: L27-L29.

## Android ADB Security

The Android May 2026 bulletin documents CVE-2026-0073 as a critical System
component issue in `adbd` that can lead to remote/proximal code execution as
the shell user without user interaction. Patch levels of 2026-05-01 or later
address the bulletin issue.

Implications:

- PhoneFork's wireless ADB feature must remain USB-first, opt-in, patch-gated,
  session-limited, and kill-switchable.
- Devices below 2026-05-01 should be refused for wireless flows unless the user
  makes an explicit documented override.
- Reports should distinguish USB ADB from wireless ADB risk.
- The helper APK should not encourage broad LAN exposure.

Sources: S02, S05, S06.

## Helper APK Security

Current state:

- Helper providers include a shell/system UID gate.
- Helper providers emit versioned JSON envelopes for SMS, call log, contacts,
  Wi-Fi capability metadata, wallpaper metadata, ringtone defaults, and user
  dictionary rows.
- Restore endpoints remain guarded and intentionally disabled pending host-side
  destructive-action confirmation.
- Sensitive permissions are present in the manifest.

Risks:

- A sensitive helper with SMS/contacts/call-log permissions can become a data
  exposure surface if the UID gate, export state, authority routing, or install
  lifetime is wrong.
- Restore endpoints can modify sensitive user data and need explicit category
  confirmation, request signing/checksums, and audit entries.

Required hardening:

- Keep providers inaccessible to third-party app callers.
- Keep install windows short and uninstall/cleanup visible.
- Add provider contract tests for denied callers, malformed input, pagination,
  empty data, large data, and permission-denied Android states.
- Include helper version, package, signature, targetSdk, and minSdk in host
  probe output.
- Keep the host packaging path behind `scripts/Stage-HelperApk.ps1` so copied
  helper APKs have validated package metadata and signatures before use.

Sources: L14-L18, L30, G20.

## Windows Release And Supply Chain

Current state:

- Release workflow publishes WPF and CLI ZIPs on `v*` tags.
- Signing and provenance steps exist, but signing secrets are not provisioned.
- No GitHub releases or tags exist yet.

Recommendations:

- Use Microsoft Artifact Signing for Windows release artifacts when a paid Azure
  subscription and identity validation are available.
- Basic SKU appears sufficient for early releases at $9.99/month and 5,000
  signatures/month; Premium is $99.99/month and 100,000 signatures/month.
- Artifact Signing does not issue EV certificates, so docs should set realistic
  SmartScreen expectations.
- Keep GitHub artifact attestations enabled for public release ZIPs and SBOMs.
- Verify release files with `signtool verify /v /debug /pa`.

Sources: L19, S15, S16, S17, S18.

## Privacy Review

Current good posture:

- No telemetry or cloud dependency is part of the product thesis.
- Serial hashing exists in project context and prior implementation history.
- Debloat actions are reversible by default.

Risks:

- Logs can still leak package lists, path names, app names, or content category
  metadata even when serials are hashed.
- Backup archives can contain sensitive data and should never be uploaded by
  default.
- Helper SMS/contact/call-log flows need local-only export warnings.

Recommendations:

- Add a migration receipt export that is safe by default and excludes raw
  content unless explicitly requested.
- Add a redaction test fixture for logs and reports.
- Keep any future issue-template guidance from asking users to upload raw
  backups or logs without redaction.

## Dependency Upgrade Plan

1. Completed: Spectre.Console 0.55.2 and QRCoder 1.8.0.
2. Completed: Microsoft.NET.Test.Sdk 18.5.1 and coverlet.collector 10.0.0.
3. Completed: JsonSchema.Net 9.2.1 with schema compatibility coverage and Serilog.Sinks.File 7.0.0 with audit-log write/hash coverage.
4. Deferred: xUnit v3 migration after release blockers are handled.

## Security Roadmap Links

- R001 helper provider contract.
- R002 helper APK CI build/verify.
- R010 debloat overlay feed.
- R011 release signing/provenance.
- R012 dependency maintenance.
