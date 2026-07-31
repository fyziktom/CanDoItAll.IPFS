<#
.SYNOPSIS
Builds, tests, packs, and validates the public IPFS NuGet packages.

.PARAMETER Configuration
Build configuration. The default is Release.

.PARAMETER OutputDirectory
Absolute or repository-relative package destination. When omitted, a
versioned, timestamped run directory is created below artifacts/packages.

.PARAMETER NoRestore
Skips restore when the caller guarantees it has already completed.

.PARAMETER NoBuild
Skips build and tests when the caller guarantees both have already completed.

.PARAMETER Version
Overrides the package version without editing the project files.

.PARAMETER CreateRunDirectory
Treats an explicitly supplied OutputDirectory as a root and creates a
versioned, timestamped child below it.

.EXAMPLE
.\tools\deployment\nugets\Build-NuGets.ps1 -Version '0.1.15'
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory,

    [switch]$NoRestore,

    [switch]$NoBuild,

    [string]$Version = '',

    [switch]$CreateRunDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Push-Location -LiteralPath $repositoryRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Description failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$globalJsonPath = Join-Path $repositoryRoot 'global.json'
$solutionPath = Join-Path $repositoryRoot 'CanDoItAll.IPFS.slnx'
$packageProjects = @(
    (Join-Path $repositoryRoot 'src\CanDoItAll.IPFS.Client\CanDoItAll.IPFS.Client.csproj')
    (Join-Path $repositoryRoot 'src\CanDoItAll.IPFS.Core\CanDoItAll.IPFS.Core.csproj')
    (Join-Path $repositoryRoot 'src\CanDoItAll.IPFS.Engine\CanDoItAll.IPFS.Engine.csproj')
)

$requiredPaths = @($globalJsonPath, $solutionPath) + $packageProjects
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required packaging input was not found: '$requiredPath'."
    }
}

$committedVersions = @(
    @(
        foreach ($packageProject in $packageProjects) {
            [xml]$projectXml = Get-Content -LiteralPath $packageProject -Raw
            $versionNode = $projectXml.SelectSingleNode('/Project/PropertyGroup/Version')
            if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
                throw "Package project '$packageProject' must define a non-empty Version."
            }

            $versionNode.InnerText.Trim()
        }
    ) | Select-Object -Unique
)
if ($committedVersions.Count -ne 1) {
    throw (
        'All public package projects must use the same committed Version. Found: ' +
        ($committedVersions -join ', ')
    )
}

$effectiveVersion = if ([string]::IsNullOrWhiteSpace($Version)) {
    [string]$committedVersions[0]
}
else {
    $Version.Trim()
}
$versionWasOverridden = -not [string]::IsNullOrWhiteSpace($Version)
$msbuildProperties = @()
if ($versionWasOverridden) {
    $msbuildProperties += "-p:Version=$effectiveVersion"
}

if (-not $OutputDirectory) {
    $outputRoot = Join-Path $repositoryRoot 'artifacts\packages'
    $createRunDirectory = $true
}
elseif ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $outputRoot = $OutputDirectory
    $createRunDirectory = $CreateRunDirectory.IsPresent
}
else {
    $outputRoot = Join-Path $repositoryRoot $OutputDirectory
    $createRunDirectory = $CreateRunDirectory.IsPresent
}
if ($createRunDirectory) {
    $runTimestamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
    $OutputDirectory = Join-Path $outputRoot "${effectiveVersion}_$runTimestamp"
}
else {
    $OutputDirectory = $outputRoot
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$operationParts = [System.Collections.Generic.List[string]]::new()
if (-not $NoRestore) {
    $operationParts.Add('restore')
}
if (-not $NoBuild) {
    $operationParts.Add('build and test')
}
$operationParts.Add("pack the public NuGet packages at version $effectiveVersion")
$operation = $operationParts -join ', '

if (-not $PSCmdlet.ShouldProcess($OutputDirectory, $operation)) {
    [pscustomobject]@{
        Repository = Split-Path $repositoryRoot -Leaf
        Configuration = $Configuration
        PackageVersion = $effectiveVersion
        OutputDirectory = $OutputDirectory
        Packages = @(
            $packageProjects |
                ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }
        )
        Status = 'Preview'
    }
    return
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

if (-not $NoRestore) {
    Invoke-DotNetCommand `
        -Arguments (
            @(
                'restore',
                $solutionPath,
                '--configfile',
                (Join-Path $repositoryRoot 'NuGet.config')
            ) + $msbuildProperties
        ) `
        -Description 'dotnet restore'
}

if (-not $NoBuild) {
    Invoke-DotNetCommand `
        -Arguments (
            @(
                'build',
                $solutionPath,
                '--configuration', $Configuration,
                '--no-restore',
                '-p:ContinuousIntegrationBuild=true',
                '-p:GeneratePackageOnBuild=false'
            ) + $msbuildProperties
        ) `
        -Description 'dotnet build'

    Invoke-DotNetCommand `
        -Arguments (
            @(
                'test',
                $solutionPath,
                '--configuration', $Configuration,
                '--no-build',
                '--no-restore'
            ) + $msbuildProperties
        ) `
        -Description 'dotnet test'
}

foreach ($packageProject in $packageProjects) {
    Invoke-DotNetCommand `
        -Arguments (
            @(
                'pack',
                $packageProject,
                '--configuration', $Configuration,
                '--no-build',
                '--no-restore',
                '--output', $OutputDirectory,
                '-p:ContinuousIntegrationBuild=true',
                '-p:GeneratePackageOnBuild=false'
            ) + $msbuildProperties
        ) `
        -Description (
            "dotnet pack for '$([System.IO.Path]::GetFileNameWithoutExtension($packageProject))'"
        )
}

& (Join-Path $repositoryRoot 'tools\validation\Test-NuGetPackages.ps1') `
    -PackageDirectory $OutputDirectory `
    -ExpectedPackageVersion $effectiveVersion
if ($LASTEXITCODE -ne 0) {
    throw "NuGet package validation failed with exit code $LASTEXITCODE."
}

[pscustomobject]@{
    Repository = Split-Path $repositoryRoot -Leaf
    Configuration = $Configuration
    PackageVersion = $effectiveVersion
    OutputDirectory = $OutputDirectory
    Packages = @(
        $packageProjects |
            ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }
    )
    Status = 'Succeeded'
}
