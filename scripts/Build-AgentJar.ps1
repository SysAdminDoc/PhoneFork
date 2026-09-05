<#
.SYNOPSIS
    Builds helper-apk/agent into phonefork-agent.jar and stages it for the host (F115).

.DESCRIPTION
    app_process executes DEX, not JVM classfiles, so the agent is compiled against android.jar,
    run through d8, and packaged as a JAR whose payload is classes.dex. That is the same shape
    scrcpy uses for its pushed server.

    The Gradle module at helper-apk/agent performs the identical steps and is the canonical
    definition; this script exists because PhoneFork's release lane is local and script-driven,
    and because it runs without a Gradle daemon on a memory-constrained machine.

.PARAMETER ApiLevel
    Android platform whose android.jar to compile against. Must match the helper APK's compileSdk.

.PARAMETER MinApi
    Minimum API for d8. Must match the helper APK's minSdk.

.PARAMETER JdkHome
    JDK to compile with. Defaults to a Gradle-compatible JDK on PATH. Android Studio's bundled
    JBR is deliberately not used: it is a JDK 25 build that Gradle 8.14 rejects.

.PARAMETER Stage
    Copy the built JAR to assets/helper/ so the CLI's default --jar path resolves.

.EXAMPLE
    pwsh scripts/Build-AgentJar.ps1 -Stage
#>
[CmdletBinding()]
param(
    [int]$ApiLevel = 36,
    [int]$MinApi = 30,
    [string]$JdkHome,
    [switch]$Stage
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$agentDir = Join-Path $repoRoot 'helper-apk/agent'
$buildDir = Join-Path $agentDir 'build'

$sdk = $env:ANDROID_HOME
if (-not $sdk) { $sdk = $env:ANDROID_SDK_ROOT }
if (-not $sdk) {
    $localProperties = Join-Path $repoRoot 'helper-apk/local.properties'
    if (Test-Path -LiteralPath $localProperties) {
        $match = Select-String -Path $localProperties -Pattern '^sdk\.dir=(.+)$'
        if ($match) { $sdk = $match.Matches[0].Groups[1].Value.Replace('\:', ':').Replace('\\', '\') }
    }
}
if (-not $sdk -or -not (Test-Path -LiteralPath $sdk)) {
    throw "Android SDK not found. Set ANDROID_HOME or add sdk.dir to helper-apk/local.properties."
}

$androidJar = Join-Path $sdk "platforms/android-$ApiLevel/android.jar"
if (-not (Test-Path -LiteralPath $androidJar)) {
    throw "android.jar for API $ApiLevel not found at $androidJar."
}

if (-not $JdkHome) {
    $javaOnPath = (Get-Command java -ErrorAction SilentlyContinue).Source
    if (-not $javaOnPath) { throw "No java on PATH. Pass -JdkHome." }
    $JdkHome = Split-Path (Split-Path $javaOnPath -Parent) -Parent
}
$javac = Join-Path $JdkHome 'bin/javac.exe'
$jar = Join-Path $JdkHome 'bin/jar.exe'
foreach ($tool in @($javac, $jar)) {
    if (-not (Test-Path -LiteralPath $tool)) { throw "Not found: $tool" }
}

# Newest build-tools directory that actually contains d8.
$d8 = Get-ChildItem (Join-Path $sdk 'build-tools') -Directory |
    Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'd8.bat' } |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1
if (-not $d8) { throw "No Android build-tools with d8.bat found under $sdk\build-tools." }

if (Test-Path -LiteralPath $buildDir) { Remove-Item -LiteralPath $buildDir -Recurse -Force }
$classesDir = Join-Path $buildDir 'classes'
$dexDir = Join-Path $buildDir 'dex'
$outDir = Join-Path $buildDir 'agent'
New-Item -ItemType Directory -Force -Path $classesDir, $dexDir, $outDir | Out-Null

$sources = @(Get-ChildItem (Join-Path $agentDir "src/main/java") -Recurse -Filter *.java |
    ForEach-Object { $_.FullName })
if (-not $sources) { throw "No agent sources found under $agentDir/src/main/java." }

Write-Host "Compiling $($sources.Count) source file(s) against API $ApiLevel"
& $javac --release 17 -classpath $androidJar -d $classesDir $sources
if ($LASTEXITCODE -ne 0) { throw "javac failed with exit code $LASTEXITCODE." }

$classFiles = @(Get-ChildItem $classesDir -Recurse -Filter *.class | ForEach-Object { $_.FullName })
if (-not $classFiles) { throw "javac produced no class files." }

Write-Host "Dexing $($classFiles.Count) class file(s) at min-api $MinApi"
& $d8 --lib $androidJar --min-api $MinApi --output $dexDir $classFiles
if ($LASTEXITCODE -ne 0) { throw "d8 failed with exit code $LASTEXITCODE." }

$dexPath = Join-Path $dexDir 'classes.dex'
if (-not (Test-Path -LiteralPath $dexPath)) { throw "d8 did not produce classes.dex." }

$jarPath = Join-Path $outDir 'phonefork-agent.jar'
& $jar --create --file $jarPath -C $dexDir .
if ($LASTEXITCODE -ne 0) { throw "jar failed with exit code $LASTEXITCODE." }

# app_process loads classes.dex out of the JAR; a JAR of .class files would fail at runtime
# with a ClassNotFoundException that is hard to diagnose from the host.
$entries = & $jar --list --file $jarPath
if ($entries -notcontains 'classes.dex') {
    throw "phonefork-agent.jar does not contain classes.dex; app_process would not be able to run it."
}

$size = (Get-Item $jarPath).Length
Write-Host "Built $jarPath ($size bytes, contains classes.dex)"

if ($Stage) {
    $staged = Join-Path $repoRoot 'assets/helper/phonefork-agent.jar'
    New-Item -ItemType Directory -Force -Path (Split-Path $staged -Parent) | Out-Null
    Copy-Item -LiteralPath $jarPath -Destination $staged -Force
    $hash = (Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -Path "$staged.sha256" -Value "$hash  phonefork-agent.jar" -Encoding utf8NoBOM
    Write-Host "Staged to $staged"
    Write-Host "SHA-256 $hash"
}
