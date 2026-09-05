$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Read-RepoText {
    param([string]$RelativePath)
    return Get-Content -LiteralPath (Join-Path $repoRoot $RelativePath) -Raw
}

function Add-Failure {
    param([string]$Message)
    $failures.Add($Message)
}

function Normalize-BadgeVersion {
    param([string]$Value)
    return $Value.Replace("--", "-")
}

$changelog = Read-RepoText "CHANGELOG.md"
if ($changelog -notmatch "(?m)^##\s+v(?<version>\d+\.\d+\.\d+(?:-[A-Za-z0-9.]+)?)\s+") {
    throw "Could not find top changelog version."
}

$version = $Matches["version"]
if ($version -notmatch "^(?<numeric>\d+\.\d+\.\d+)(?:-.+)?$") {
    throw "Changelog version '$version' is not semver-like."
}

$numericVersion = $Matches["numeric"]
$manifestVersion = "$numericVersion.0"

$buildProps = Read-RepoText "Directory.Build.props"
if ($buildProps -notmatch "<Version>(?<build>[^<]+)</Version>") {
    Add-Failure "Directory.Build.props Version is missing."
} elseif ($Matches["build"] -ne $version) {
    Add-Failure "Directory.Build.props Version '$($Matches["build"])' does not match changelog '$version'."
}

foreach ($property in @("AssemblyVersion", "FileVersion")) {
    if ($buildProps -notmatch "<$property>(?<value>[^<]+)</$property>") {
        Add-Failure "Directory.Build.props $property is missing."
    } elseif ($Matches["value"] -ne $manifestVersion) {
        Add-Failure "Directory.Build.props $property '$($Matches["value"])' does not match expected numeric '$manifestVersion'."
    }
}

if ($buildProps -notmatch "<InformationalVersion>(?<informational>[^<]+)</InformationalVersion>") {
    Add-Failure "Directory.Build.props InformationalVersion is missing."
} elseif ($Matches["informational"] -ne $version) {
    Add-Failure "Directory.Build.props InformationalVersion '$($Matches["informational"])' does not match changelog '$version'."
}

$readme = Read-RepoText "README.md"
if ($readme -notmatch "badge/version-(?<badge>.+?)-blue\.svg") {
    Add-Failure "README version badge is missing."
} else {
    $badge = Normalize-BadgeVersion $Matches["badge"]
    if ($badge -ne $version) {
        Add-Failure "README badge version '$badge' does not match changelog '$version'."
    }
}

$mainWindow = Read-RepoText "src/PhoneFork.App/Views/MainWindow.xaml"
if ($mainWindow -notmatch "Title=""PhoneFork\s+&#x00B7;\s+v(?<title>[^""]+)""") {
    Add-Failure "MainWindow Title version is missing."
} elseif ($Matches["title"] -ne $version) {
    Add-Failure "MainWindow Title version '$($Matches["title"])' does not match changelog '$version'."
}

if ($mainWindow -notmatch "Text="" v(?<header>\d+\.\d+\.\d+(?:-[A-Za-z0-9.]+)?)""") {
    Add-Failure "MainWindow visible header version is missing."
} elseif ($Matches["header"] -ne $version) {
    Add-Failure "MainWindow visible header version '$($Matches["header"])' does not match changelog '$version'."
}

$manifest = Read-RepoText "src/PhoneFork.App/app.manifest"
if ($manifest -notmatch "assemblyIdentity\s+version=""(?<manifest>[^""]+)""") {
    Add-Failure "App manifest assemblyIdentity version is missing."
} elseif ($Matches["manifest"] -ne $manifestVersion) {
    Add-Failure "App manifest version '$($Matches["manifest"])' does not match expected numeric '$manifestVersion'."
}

$helperGradle = Read-RepoText "helper-apk/app/build.gradle.kts"
if ($helperGradle -notmatch "versionName\s+=\s+""(?<helper>[^""]+)""") {
    Add-Failure "Helper APK versionName is missing."
} elseif ($Matches["helper"] -ne $version) {
    Add-Failure "Helper APK versionName '$($Matches["helper"])' does not match changelog '$version'."
}

$releaseWorkflowPath = Join-Path $repoRoot ".github/workflows/release.yml"
if (Test-Path -LiteralPath $releaseWorkflowPath) {
    $releaseWorkflow = Read-RepoText ".github/workflows/release.yml"
    if ($releaseWorkflow -notmatch "tags:\s*\r?\n\s*-\s+'v\*'") {
        Add-Failure "Release workflow does not advertise v* tag triggering."
    }
} else {
    Write-Host "No GitHub release workflow found; accepting the local-only release lane."
}

# F110 — the embedded debloat dataset gates a destructive operation, and upstream reclassifies
# packages continuously. Refuse to ship a snapshot that has gone unreviewed for a year, and
# nag well before that. Refresh with scripts/Update-DebloatDataset.ps1.
$datasetSourcePath = Join-Path $repoRoot "assets/debloat/dataset-source.json"
if (-not (Test-Path -LiteralPath $datasetSourcePath)) {
    Add-Failure "assets/debloat/dataset-source.json is missing; the debloat dataset has no recorded provenance."
} else {
    $datasetSource = Get-Content -LiteralPath $datasetSourcePath -Raw | ConvertFrom-Json
    if (-not $datasetSource.upstreamCommit -or $datasetSource.upstreamCommit -notmatch '^[0-9a-f]{40}$') {
        Add-Failure "Debloat dataset provenance has no valid upstream commit SHA."
    }
    $capturedAt = [datetime]::MinValue
    if (-not [datetime]::TryParse($datasetSource.capturedAt, [ref]$capturedAt)) {
        Add-Failure "Debloat dataset provenance has an unparseable capturedAt '$($datasetSource.capturedAt)'."
    } else {
        $ageDays = [int]((Get-Date).Date - $capturedAt.Date).TotalDays
        if ($ageDays -gt 365) {
            Add-Failure "Debloat dataset snapshot is $ageDays days old (captured $($capturedAt.ToString('yyyy-MM-dd'))). Run scripts/Update-DebloatDataset.ps1."
        } elseif ($ageDays -gt 90) {
            Write-Warning "Debloat dataset snapshot is $ageDays days old (captured $($capturedAt.ToString('yyyy-MM-dd'))). Consider scripts/Update-DebloatDataset.ps1."
        } else {
            Write-Host "Debloat dataset snapshot is $ageDays day(s) old (upstream $($datasetSource.upstreamCommit.Substring(0,8)))."
        }
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Version consistency check failed with $($failures.Count) issue(s)."
}

Write-Host "Version consistency OK: $version (manifest $manifestVersion)."
