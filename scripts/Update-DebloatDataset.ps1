<#
.SYNOPSIS
    Regenerates the embedded AppManagerNG / UAD-NG debloat dataset under assets/debloat/.

.DESCRIPTION
    Downloads resources/assets/uad_lists.json from a pinned upstream commit, verifies its
    SHA-256, and splits it into the five per-bucket files PhoneFork embeds. The upstream
    commit and file hash are recorded in assets/debloat/dataset-source.json so a release
    can prove which snapshot it shipped.

    Upstream keys the file by package id and classifies removal as
    Recommended / Advanced / Expert / Unsafe. PhoneFork's DebloatEntry.Tier accepts both
    that vocabulary and the older delete / replace / caution / unsafe spelling.

.PARAMETER Commit
    Upstream commit SHA to pull from. Defaults to the commit recorded in dataset-source.json.

.PARAMETER ExpectedSha256
    SHA-256 of the downloaded uad_lists.json. When omitted the script prints the hash it
    saw and writes it into dataset-source.json.

.EXAMPLE
    pwsh scripts/Update-DebloatDataset.ps1 -Commit d1297f17b73b84f355b2a1f9df4a734d592c63a7
#>
[CmdletBinding()]
param(
    [string]$Commit,
    [string]$ExpectedSha256
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetDir = Join-Path $repoRoot 'assets/debloat'
$sourceFile = Join-Path $assetDir 'dataset-source.json'

if (-not $Commit) {
    if (-not (Test-Path $sourceFile)) {
        throw "No -Commit given and $sourceFile does not exist. Pass an upstream commit SHA."
    }
    $Commit = (Get-Content $sourceFile -Raw | ConvertFrom-Json).upstreamCommit
    Write-Host "Using commit from dataset-source.json: $Commit"
}

if ($Commit -notmatch '^[0-9a-f]{40}$') {
    throw "Commit must be a full 40-character SHA, got '$Commit'."
}

$url = "https://raw.githubusercontent.com/Universal-Debloater-Alliance/universal-android-debloater-next-generation/$Commit/resources/assets/uad_lists.json"
$temp = Join-Path ([IO.Path]::GetTempPath()) "uad_lists_$Commit.json"

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $temp -UseBasicParsing

$actualSha = (Get-FileHash -Path $temp -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Upstream SHA-256: $actualSha"
if ($ExpectedSha256 -and $ExpectedSha256.ToLowerInvariant() -ne $actualSha) {
    throw "Checksum mismatch: expected $ExpectedSha256, got $actualSha."
}

$upstream = Get-Content $temp -Raw | ConvertFrom-Json -AsHashtable
Write-Host "Upstream entries: $($upstream.Keys.Count)"

# Upstream `list` values map one-to-one onto PhoneFork's five embedded files.
$buckets = @{
    'Oem'     = 'oem.json'
    'Google'  = 'google.json'
    'Carrier' = 'carrier.json'
    'Aosp'    = 'aosp.json'
    'Misc'    = 'misc.json'
}

$grouped = @{}
foreach ($file in $buckets.Values) { $grouped[$file] = [System.Collections.Generic.List[object]]::new() }

$unknownLists = [System.Collections.Generic.HashSet[string]]::new()
foreach ($packageId in ($upstream.Keys | Sort-Object)) {
    $row = $upstream[$packageId]
    $listName = [string]$row.list
    if (-not $buckets.ContainsKey($listName)) {
        [void]$unknownLists.Add($listName)
        $listName = 'Misc'
    }

    # Emit only the fields DebloatEntry binds. Empty collections are dropped so the
    # generated files stay close in size to the hand-captured ones they replace.
    $entry = [ordered]@{ id = $packageId }
    if ($row.description) { $entry['description'] = [string]$row.description }
    $entry['removal'] = [string]$row.removal
    if ($row.labels -and $row.labels.Count -gt 0) { $entry['tags'] = @($row.labels) }
    if ($row.dependencies -and $row.dependencies.Count -gt 0) { $entry['dependencies'] = @($row.dependencies) }
    if ($row.neededBy -and $row.neededBy.Count -gt 0) { $entry['required_by'] = @($row.neededBy) }

    $grouped[$buckets[$listName]].Add([pscustomobject]$entry)
}

if ($unknownLists.Count -gt 0) {
    Write-Warning "Unrecognised upstream list buckets folded into misc.json: $($unknownLists -join ', ')"
}

foreach ($file in ($buckets.Values | Sort-Object)) {
    $path = Join-Path $assetDir $file
    $rows = $grouped[$file]
    # ConvertTo-Json unrolls a single-element array, so force an array wrapper.
    $json = ConvertTo-Json -InputObject @($rows) -Depth 6
    Set-Content -Path $path -Value $json -Encoding utf8NoBOM
    Write-Host ("  {0,-14} {1,5} entries" -f $file, $rows.Count)
}

$source = [ordered]@{
    '$schema'        = 'phonefork-debloat-source-v1'
    upstreamRepo     = 'Universal-Debloater-Alliance/universal-android-debloater-next-generation'
    upstreamPath     = 'resources/assets/uad_lists.json'
    upstreamCommit   = $Commit
    upstreamSha256   = $actualSha
    upstreamEntries  = $upstream.Keys.Count
    capturedAt       = (Get-Date -Format 'yyyy-MM-dd')
    removalVocabulary = @('Recommended', 'Advanced', 'Expert', 'Unsafe')
}
Set-Content -Path $sourceFile -Value (ConvertTo-Json -InputObject $source -Depth 4) -Encoding utf8NoBOM
Write-Host "Wrote $sourceFile"

Remove-Item $temp -Force
