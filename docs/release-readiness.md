# Release Readiness

Verified: 2026-08-29

This note records the local release checks for PhoneFork.

## Current Release State

- Prerelease `v0.9.3-pre` is published at `https://github.com/SysAdminDoc/PhoneFork/releases/tag/v0.9.3-pre`.
- `README.md` distinguishes the unsigned Windows programs from the signed helper APK.
- Builds, tests, and packaging run locally. The repository has no GitHub Actions workflow.
- Windows publish outputs remain unsigned until a local signing certificate is available.
- The helper APK is signed with an external release key. No private key is committed or attached to a release.

## Local Publish Gate

Run these from the repository root before tagging:

```powershell
dotnet restore PhoneFork.slnx
pwsh scripts\Test-VersionConsistency.ps1
dotnet build PhoneFork.slnx -c Release --no-restore
dotnet test tests\PhoneFork.Core.Tests\PhoneFork.Core.Tests.csproj -c Release --no-build
cd helper-apk
.\gradlew.bat clean test lint assembleRelease --no-daemon
cd ..
# Sign and align app-release-unsigned.apk with an external Android release key.
pwsh scripts\Stage-HelperApk.ps1 -ApkPath <signed-helper-apk>
dotnet publish src\PhoneFork.App\PhoneFork.App.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish\wpf
dotnet publish src\PhoneFork.Cli\PhoneFork.Cli.csproj -c Release -r win-x64 --self-contained false -o artifacts\publish\cli
```

Current screenshot:

- `docs/screenshots/phonefork-main-2026-08-29.png`

Release notes draft:

- `docs/releases/v0.9.3-pre.md`

Expected outputs:

- `artifacts/publish/wpf/PhoneFork.exe`
- `artifacts/publish/cli/phonefork.exe`
- bundled `tools/adb.exe` beside each host output
- `assets/helper/PhoneForkHelper.apk`, signed and verified before the desktop publish
- `PhoneForkHelper-v0.9.3-pre-release.apk` in the final release directory

## Artifact Trust Policy

Windows prerelease ZIPs:

- Allowed only if release notes and the generated `ARTIFACT-TRUST.txt` say the
  ZIPs are unsigned.
- Release notes must tell users to expect Windows SmartScreen friction.
- Release notes must not claim Authenticode trust unless a local signing step
  actually ran and `signtool verify /pa /v` passed.

Android helper APK:

- Must be aligned and signed locally with an external release key.
- Must pass `apksigner verify --verbose --print-certs`, metadata checks, and an
  emulator installation before upload.
- The private key must never be committed, copied into a ZIP, or attached to a
  GitHub release.

Future Authenticode signing:

- Use a local Windows code-signing certificate, then verify every EXE with
  `signtool verify /pa /v` before packaging.
- A new publisher identity can still encounter SmartScreen warnings while its
  reputation develops.

## Release Artifact Verification

Compare each downloaded file with its entry in `SHA256SUMS`. The helper APK must also pass Android signature verification:

```powershell
Get-FileHash .\PhoneFork-vX.Y.Z-wpf-win-x64.zip -Algorithm SHA256
apksigner verify --verbose --print-certs .\PhoneForkHelper-vX.Y.Z-release.apk
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

## Remaining Release Inputs

- Add local Authenticode signing when a Windows code-signing certificate is available.
- Re-run the local publish gate before the next tag.
- Document at least one real two-phone Samsung migration smoke test before a signed public v1 release.
