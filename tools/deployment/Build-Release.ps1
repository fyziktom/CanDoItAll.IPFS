[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string[]]$Target = @('win-x64', 'linux-x64', 'linux-arm'),

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputRoot = '',

    [switch]$SkipArchive,

    [switch]$KeepCompressedStaticAssets
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-PathUnderRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Root
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $rootPrefix = $fullRoot + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify '$fullPath' because it is not below '$fullRoot'."
    }
}

function Get-TargetDefinitions {
    @{
        'win-x64' = @{
            RuntimeIdentifier = 'win-x64'
            NodeControlFramework = 'net10.0-windows'
            Platform = 'windows'
            ArchiveExtension = '.zip'
        }
        'linux-x64' = @{
            RuntimeIdentifier = 'linux-x64'
            NodeControlFramework = 'net10.0'
            Platform = 'linux'
            ArchiveExtension = '.tar.gz'
        }
        'linux-arm' = @{
            RuntimeIdentifier = 'linux-arm'
            NodeControlFramework = 'net10.0'
            Platform = 'linux'
            ArchiveExtension = '.tar.gz'
        }
    }
}

function Invoke-DotNetPublish {
    param(
        [string]$ProjectPath,
        [string]$Framework,
        [string]$RuntimeIdentifier,
        [string]$PublishDirectory,
        [string]$ArtifactsRoot
    )

    New-Item -ItemType Directory -Path $ArtifactsRoot -Force | Out-Null

    $arguments = @(
        'publish',
        $ProjectPath,
        '-c', $Configuration,
        '-f', $Framework,
        '-r', $RuntimeIdentifier,
        '--self-contained', 'true',
        '-o', $PublishDirectory,
        '--artifacts-path', $ArtifactsRoot,
        '-nologo',
        '-v', 'minimal',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false',
        '-p:GenerateDocumentationFile=false',
        '-p:GeneratePackageOnBuild=false',
        '-p:IncludeSymbols=false'
    )

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $ProjectPath ($RuntimeIdentifier)."
    }
}

function Remove-UnneededPublishFiles {
    param([string]$BundleRoot)

    $patternsToRemove = @(
        '*.pdb',
        '*.xml',
        'appsettings.Development.json',
        'web.config'
    )

    foreach ($pattern in $patternsToRemove) {
        Get-ChildItem -Path $BundleRoot -Recurse -File -Filter $pattern -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }

    if (-not $KeepCompressedStaticAssets) {
        Get-ChildItem -Path $BundleRoot -Recurse -File -Include *.br, *.gz -ErrorAction SilentlyContinue |
            Remove-Item -Force
    }
}

function New-StartupScripts {
    param(
        [string]$BundleRoot,
        [string]$Platform
    )

    $windowsScriptPath = Join-Path $BundleRoot 'Start-CanDoItAllIpfsControl.cmd'
    $linuxScriptPath = Join-Path $BundleRoot 'start-control.sh'

    $windowsScript = @'
@echo off
setlocal
set "SCRIPT_DIR=%~dp0"

if not defined IPFS_PATH set "IPFS_PATH=%SCRIPT_DIR%data\node"
if not defined ASPNETCORE_URLS set "ASPNETCORE_URLS=http://127.0.0.1:5092"
if not defined NodeSettingsDefaults__BaseUrl set "NodeSettingsDefaults__BaseUrl=http://127.0.0.1:5001/"

if not exist "%IPFS_PATH%" mkdir "%IPFS_PATH%"

if not defined IPFS_PASS (
  echo IPFS_PASS is not set. The control app will start, but the bundled node cannot unlock until a passphrase is configured.
)

start "" /D "%SCRIPT_DIR%" "%SCRIPT_DIR%CanDoItAll.IPFS.NodeControl.exe"
'@

    $linuxScript = @'
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
export IPFS_PATH="${IPFS_PATH:-$SCRIPT_DIR/data/node}"
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:5092}"
export NodeSettingsDefaults__BaseUrl="${NodeSettingsDefaults__BaseUrl:-http://127.0.0.1:5001/}"

mkdir -p "$IPFS_PATH"
chmod +x "$SCRIPT_DIR/CanDoItAll.IPFS.NodeControl" "$SCRIPT_DIR/node/CanDoItAll.IPFS.Engine" 2>/dev/null || true

if [[ -z "${IPFS_PASS:-}" ]]; then
  echo "IPFS_PASS is not set. The control app will start, but the bundled node cannot unlock until a passphrase is configured."
fi

exec "$SCRIPT_DIR/CanDoItAll.IPFS.NodeControl"
'@

    if ($Platform -eq 'windows') {
        Set-Content -Path $windowsScriptPath -Value $windowsScript -NoNewline
    }
    else {
        Set-Content -Path $linuxScriptPath -Value $linuxScript -NoNewline
    }
}

function Copy-WindowsFrameworkAssets {
    param(
        [string]$NodeControlProject,
        [string]$RuntimeIdentifier,
        [string]$ReleaseRoot,
        [string]$ArtifactsRoot,
        [string]$BundleRoot
    )

    $assetPublishRoot = Join-Path $ReleaseRoot "framework-assets\$RuntimeIdentifier"
    Assert-PathUnderRoot -Path $assetPublishRoot -Root $ReleaseRoot
    if (Test-Path $assetPublishRoot) {
        Remove-Item -LiteralPath $assetPublishRoot -Recurse -Force
    }

    Invoke-DotNetPublish `
        -ProjectPath $NodeControlProject `
        -Framework 'net10.0' `
        -RuntimeIdentifier $RuntimeIdentifier `
        -PublishDirectory $assetPublishRoot `
        -ArtifactsRoot $ArtifactsRoot

    $frameworkSource = Join-Path $assetPublishRoot 'wwwroot\_framework'
    $frameworkDestination = Join-Path $BundleRoot 'wwwroot\_framework'
    Assert-PathUnderRoot -Path $frameworkDestination -Root $BundleRoot
    if (-not (Test-Path $frameworkSource)) {
        throw "Could not locate $frameworkSource after the helper publish."
    }

    if (Test-Path $frameworkDestination) {
        Remove-Item -LiteralPath $frameworkDestination -Recurse -Force
    }

    New-Item -ItemType Directory -Path (Split-Path -Path $frameworkDestination -Parent) -Force | Out-Null
    Copy-Item -Path $frameworkSource -Destination $frameworkDestination -Recurse -Force
}

function New-TargetArchive {
    param(
        [string]$BundleRoot,
        [string]$ArchivePath,
        [string]$ArchiveExtension
    )

    if (Test-Path $ArchivePath) {
        Remove-Item -LiteralPath $ArchivePath -Force
    }

    if ($ArchiveExtension -eq '.zip') {
        Compress-Archive -Path $BundleRoot -DestinationPath $ArchivePath -CompressionLevel Optimal
        return
    }

    $bundleParent = Split-Path -Path $BundleRoot -Parent
    $bundleName = Split-Path -Path $BundleRoot -Leaf
    & tar -czf $ArchivePath -C $bundleParent $bundleName
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed while creating $ArchivePath."
    }
}

$targetDefinitions = Get-TargetDefinitions
$unknownTargets = @($Target | Where-Object { -not $targetDefinitions.ContainsKey($_) })
if ($unknownTargets.Count -gt 0) {
    throw "Unknown target(s): $($unknownTargets -join ', '). Valid targets: $($targetDefinitions.Keys -join ', ')."
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$nodeControlProject = Join-Path $repoRoot 'src\CanDoItAll.IPFS.NodeControl\CanDoItAll.IPFS.NodeControl.csproj'
$engineProject = Join-Path $repoRoot 'src\CanDoItAll.IPFS.Engine\CanDoItAll.IPFS.Engine.csproj'

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot '.artifacts\releases'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot $OutputRoot
}
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)

$releaseStamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$releaseRoot = Join-Path $OutputRoot $releaseStamp
$publishRoot = Join-Path $releaseRoot 'publish'
$packagesRoot = Join-Path $releaseRoot 'packages'

if (-not $PSCmdlet.ShouldProcess(
        $releaseRoot,
        "Build release bundles for targets: $($Target -join ', ')"
    )) {
    [pscustomobject]@{
        Configuration = $Configuration
        OutputRoot = $releaseRoot
        Targets = $Target
        Status = 'Preview'
    }
    return
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $packagesRoot -Force | Out-Null

$results = New-Object System.Collections.Generic.List[object]
foreach ($targetName in $Target) {
    $definition = $targetDefinitions[$targetName]
    $bundleName = "CanDoItAll.IPFS-$targetName"
    $bundleRoot = Join-Path $publishRoot $bundleName
    $enginePublishRoot = Join-Path $bundleRoot 'node'
    Assert-PathUnderRoot -Path $bundleRoot -Root $publishRoot

    if (Test-Path $bundleRoot) {
        Remove-Item -LiteralPath $bundleRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Path $bundleRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $enginePublishRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $bundleRoot 'data\node') -Force | Out-Null

    $targetArtifactsRoot = Join-Path $releaseRoot "dotnet-artifacts\$targetName"
    Invoke-DotNetPublish `
        -ProjectPath $nodeControlProject `
        -Framework $definition.NodeControlFramework `
        -RuntimeIdentifier $definition.RuntimeIdentifier `
        -PublishDirectory $bundleRoot `
        -ArtifactsRoot $targetArtifactsRoot
    Invoke-DotNetPublish `
        -ProjectPath $engineProject `
        -Framework 'net10.0' `
        -RuntimeIdentifier $definition.RuntimeIdentifier `
        -PublishDirectory $enginePublishRoot `
        -ArtifactsRoot $targetArtifactsRoot

    if ($definition.Platform -eq 'windows') {
        Copy-WindowsFrameworkAssets `
            -NodeControlProject $nodeControlProject `
            -RuntimeIdentifier $definition.RuntimeIdentifier `
            -ReleaseRoot $releaseRoot `
            -ArtifactsRoot (Join-Path $targetArtifactsRoot 'framework-assets') `
            -BundleRoot $bundleRoot
    }

    Remove-UnneededPublishFiles -BundleRoot $bundleRoot
    New-StartupScripts -BundleRoot $bundleRoot -Platform $definition.Platform

    $archivePath = Join-Path $packagesRoot ($bundleName + $definition.ArchiveExtension)
    Assert-PathUnderRoot -Path $archivePath -Root $packagesRoot
    if (-not $SkipArchive) {
        New-TargetArchive -BundleRoot $bundleRoot -ArchivePath $archivePath -ArchiveExtension $definition.ArchiveExtension
    }

    $fileCount = (Get-ChildItem -Path $bundleRoot -Recurse -File | Measure-Object).Count
    $results.Add([pscustomobject]@{
        Target = $targetName
        BundleRoot = $bundleRoot
        ArchivePath = if ($SkipArchive) { '' } else { $archivePath }
        FileCount = $fileCount
    }) | Out-Null
}

$results | Format-Table -AutoSize
Write-Host ""
Write-Host "Release output root: $releaseRoot"
