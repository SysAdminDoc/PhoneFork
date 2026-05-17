[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$SolutionPath = "PhoneFork.slnx",
    [string]$PackageName = "PhoneFork",
    [string]$Supplier = "Organization: SysAdminDoc",
    [string]$RepositoryUrl = "https://github.com/SysAdminDoc/PhoneFork",

    [Parameter(Mandatory = $true, ValueFromRemainingArguments = $true)]
    [string[]]$ArtifactPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-ExistingFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = @(Resolve-Path -LiteralPath $Path -ErrorAction Stop)
    if ($resolved.Count -ne 1) {
        throw "Expected one file for '$Path', found $($resolved.Count)."
    }
    if (-not (Test-Path -LiteralPath $resolved.Path -PathType Leaf)) {
        throw "Expected file path '$Path'."
    }
    return Get-Item -LiteralPath $resolved.Path
}

function ConvertTo-SpdxIdPart {
    param([Parameter(Mandatory = $true)][string]$Value)

    $id = $Value -replace '[^A-Za-z0-9.-]', '-'
    if ([string]::IsNullOrWhiteSpace($id)) {
        return "unnamed"
    }
    return $id
}

function New-Package {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$SpdxId,
        [string]$VersionInfo,
        [string]$DownloadLocation = "NOASSERTION",
        [object[]]$ExternalRefs = @()
    )

    $package = [ordered]@{
        name = $Name
        SPDXID = $SpdxId
        downloadLocation = $DownloadLocation
        filesAnalyzed = $false
        licenseConcluded = "NOASSERTION"
        licenseDeclared = "NOASSERTION"
        copyrightText = "NOASSERTION"
    }
    if (-not [string]::IsNullOrWhiteSpace($VersionInfo)) {
        $package.versionInfo = $VersionInfo
    }
    if ($ExternalRefs.Count -gt 0) {
        $package.externalRefs = $ExternalRefs
    }
    return $package
}

$artifacts = foreach ($path in $ArtifactPath) {
    Resolve-ExistingFile -Path $path
}

$restoreOutput = & dotnet restore $SolutionPath 2>&1
$restoreExit = $LASTEXITCODE
if ($restoreExit -ne 0) {
    throw "dotnet restore failed with exit code $restoreExit.`n$($restoreOutput -join [Environment]::NewLine)"
}

$listOutput = & dotnet list $SolutionPath package --include-transitive --format json 2>&1
$dotnetExit = $LASTEXITCODE
if ($dotnetExit -ne 0) {
    throw "dotnet list package failed with exit code $dotnetExit.`n$($listOutput -join [Environment]::NewLine)"
}
$listJson = $listOutput -join [Environment]::NewLine
$packageGraph = $listJson | ConvertFrom-Json

$nugetPackages = @{}
foreach ($project in $packageGraph.projects) {
    $projectPath = [string]$project.path
    if ($projectPath -match '[/\\]tests[/\\]') {
        continue
    }

    foreach ($framework in $project.frameworks) {
        $allPackages = @()
        if ($framework.topLevelPackages) {
            $allPackages += $framework.topLevelPackages
        }
        if ($framework.transitivePackages) {
            $allPackages += $framework.transitivePackages
        }

        foreach ($pkg in $allPackages) {
            $id = [string]$pkg.id
            $resolvedVersion = [string]$pkg.resolvedVersion
            if ([string]::IsNullOrWhiteSpace($id) -or [string]::IsNullOrWhiteSpace($resolvedVersion)) {
                continue
            }
            $key = "$id@$resolvedVersion"
            if (-not $nugetPackages.ContainsKey($key)) {
                $nugetPackages[$key] = [ordered]@{
                    Id = $id
                    Version = $resolvedVersion
                }
            }
        }
    }
}

$rootPackageId = "SPDXRef-Package-$((ConvertTo-SpdxIdPart -Value $PackageName))"
$packages = @(
    [ordered]@{
        name = $PackageName
        SPDXID = $rootPackageId
        versionInfo = $Version
        supplier = $Supplier
        downloadLocation = $RepositoryUrl
        filesAnalyzed = $false
        licenseConcluded = "MIT"
        licenseDeclared = "MIT"
        copyrightText = "NOASSERTION"
    }
)

$relationships = @()
$files = @()

foreach ($entry in ($nugetPackages.Values | Sort-Object Id, Version)) {
    $idPart = ConvertTo-SpdxIdPart -Value "$($entry.Id)-$($entry.Version)"
    $spdxId = "SPDXRef-Package-$idPart"
    $packages += New-Package `
        -Name $entry.Id `
        -SpdxId $spdxId `
        -VersionInfo $entry.Version `
        -DownloadLocation "https://www.nuget.org/packages/$($entry.Id)/$($entry.Version)" `
        -ExternalRefs @(
            [ordered]@{
                referenceCategory = "PACKAGE-MANAGER"
                referenceType = "purl"
                referenceLocator = "pkg:nuget/$($entry.Id)@$($entry.Version)"
            }
        )

    $relationships += [ordered]@{
        spdxElementId = $rootPackageId
        relationshipType = "DEPENDS_ON"
        relatedSpdxElement = $spdxId
    }
}

foreach ($artifact in $artifacts) {
    $hash = Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256
    $fileId = "SPDXRef-File-$((ConvertTo-SpdxIdPart -Value $artifact.Name))"
    $files += [ordered]@{
        fileName = $artifact.Name
        SPDXID = $fileId
        checksums = @(
            [ordered]@{
                algorithm = "SHA256"
                checksumValue = $hash.Hash.ToLowerInvariant()
            }
        )
        licenseConcluded = "NOASSERTION"
        copyrightText = "NOASSERTION"
    }
    $relationships += [ordered]@{
        spdxElementId = $rootPackageId
        relationshipType = "CONTAINS"
        relatedSpdxElement = $fileId
    }
}

$safeVersion = ConvertTo-SpdxIdPart -Value $Version
$document = [ordered]@{
    spdxVersion = "SPDX-2.3"
    dataLicense = "CC0-1.0"
    SPDXID = "SPDXRef-DOCUMENT"
    name = "$PackageName $Version release"
    documentNamespace = "$RepositoryUrl/spdx/$PackageName-$safeVersion-$([guid]::NewGuid().ToString('N'))"
    creationInfo = [ordered]@{
        created = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
        creators = @(
            "Tool: scripts/New-ReleaseSbom.ps1",
            $Supplier
        )
    }
    documentDescribes = @($rootPackageId)
    packages = $packages
    files = $files
    relationships = $relationships
}

$parent = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($parent)) {
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
}

$document | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Wrote SPDX SBOM: $OutputPath"
