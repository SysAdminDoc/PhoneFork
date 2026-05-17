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
- The release workflow emits an SPDX 2.3 SBOM, SHA-256 checksum manifest,
  GitHub provenance attestation, and GitHub SBOM attestation for release ZIPs.
- Azure Artifact Signing is wired for Windows EXE/DLL payloads before ZIP
  packaging, but inactive until repository secrets are provisioned.
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
  `AZURE_SIGNING_ENDPOINT`, `AZURE_SIGNING_ACCOUNT`, and
  `AZURE_SIGNING_PROFILE` repository secrets.
- Verify locally or in CI with `signtool verify /pa /v` after signing.
- Keep GitHub provenance and SBOM attestations enabled.
- Do not claim SmartScreen is gone immediately. Artifact Signing identifies the
  publisher, but new publisher identities may still see SmartScreen warnings
  until Microsoft reputation builds.

## Release Artifact Verification

After downloading a release ZIP, verify the GitHub attestation before trusting
the payload:

```powershell
gh attestation verify .\PhoneFork-vX.Y.Z-wpf-win-x64.zip -R SysAdminDoc/PhoneFork
gh attestation verify .\PhoneFork-vX.Y.Z-cli-win-x64.zip -R SysAdminDoc/PhoneFork
```

Verify the SBOM attestation with the SPDX predicate:

```powershell
gh attestation verify .\PhoneFork-vX.Y.Z-wpf-win-x64.zip -R SysAdminDoc/PhoneFork --predicate-type https://spdx.dev/Document/v2.3
```

For signed releases, extract the ZIP and verify Authenticode signatures on the
Windows payloads:

```powershell
signtool verify /pa /v .\PhoneFork.exe
signtool verify /pa /v .\PhoneFork.Core.dll
```

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
- Verify signed artifacts with `signtool verify /pa /v`.
- Document at least one real two-phone Samsung migration smoke test before a
  signed public v1 release.
