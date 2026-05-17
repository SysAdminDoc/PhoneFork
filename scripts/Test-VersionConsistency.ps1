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

$releaseWorkflow = Read-RepoText ".github/workflows/release.yml"
if ($releaseWorkflow -notmatch "tags:\s*\r?\n\s*-\s+'v\*'") {
    Add-Failure "Release workflow does not advertise v* tag triggering."
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ }
    throw "Version consistency check failed with $($failures.Count) issue(s)."
}

Write-Host "Version consistency OK: $version (manifest $manifestVersion)."
