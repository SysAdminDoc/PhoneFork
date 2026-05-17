# Release Readiness

Verified: 2026-05-17

This note is the pre-release checklist for the first public PhoneFork artifact.
It separates local publish readiness from the public tag/release decision.

## Current Release State

- Unsigned prerelease `v0.9.0-pre` is published at `https://github.com/SysAdminDoc/PhoneFork/releases/tag/v0.9.0-pre`.
- No signed public release is intentionally shipped yet.
- `README.md` distinguishes the unsigned prerelease from source builds.
- The release workflow produces framework-dependent WPF and CLI ZIPs on `v*`
  tags.
- GitHub build provenance attestation is wired for release ZIPs.
- Azure Artifact Signing is wired but inactive until repository secrets are
  provisioned.
- Helper APK release signing is not wired. CI verifies helper metadata and
  staging with a CI debug-key-signed release APK only.

## Local Publish Gate

Run these from the repository root before tagging:

```powershell
dotnet restore PhoneFork.slnx
pwsh scripts\Test-VersionConsistency.ps1
dotnet build PhoneFork.slnx -c Release --no-restore
dotnet test tests\PhoneFork.Core.Tests\PhoneFork.Core.Tests.csproj -c Release --no-build
dotnet publish src\PhoneFork.App\PhoneFork.App.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish\wpf
dotnet publish src\PhoneFork.Cli\PhoneFork.Cli.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish\cli
```

Current screenshot:

- `docs/screenshots/phonefork-main-2026-05-17.png`

Release notes draft:

- `docs/releases/v0.9.0-pre.md`

Expected outputs:

- `artifacts/publish/wpf/PhoneFork.exe`
- `artifacts/publish/cli/phonefork.exe`
- bundled `tools/adb.exe` beside each host output

## Artifact Trust Policy

Unsigned prerelease:

- Allowed only if release notes and the generated `ARTIFACT-TRUST.txt` say the
  ZIPs are unsigned.
- Release notes must tell users to expect Windows SmartScreen friction.
- No release notes may imply Microsoft/Azure code-signing trust unless the
  signing step actually ran.

Signed release:

- Requires `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET`,
  `AZURE_SIGNING_ACCOUNT`, and `AZURE_SIGNING_PROFILE` repository secrets.
- Verify locally or in CI with `signtool verify /v /debug /pa` after signing.
- Keep GitHub artifact attestations enabled.

## Release Notes Draft Guardrails

Use accurate capability language:

- PhoneFork migrates user apps, split APKs, media, selected settings, default
  roles, reversible debloat state, and local trust/pre-flight reports where
  Android permits it over ADB.
- PhoneFork does not migrate third-party app private data without root.
- PhoneFork does not migrate Knox-bound data such as Secure Folder or Samsung
  Wallet payment tokens.
- Smart Switch remains the recommended companion for Samsung/OEM-private
  categories.
- Helper APK provider exports are implemented, but restore writes remain
  intentionally disabled until host-side destructive-action confirmation ships.

## Remaining Signed-Release Inputs

- Provision Azure Artifact Signing repository secrets.
- Re-run the local publish gate before the next tag.
- Verify signed artifacts with `signtool verify /v /debug /pa`.
- Document at least one real two-phone Samsung migration smoke test before a
  signed public v1 release.
