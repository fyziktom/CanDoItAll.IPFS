[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$OutputDirectory,

    [switch]$NoRestore
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

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$solutionPath = Join-Path $repositoryRoot 'CanDoItAll.IPFS.slnx'
$packageProjects = @(
    (Join-Path $repositoryRoot 'src\CanDoItAll.IPFS.Client\CanDoItAll.IPFS.Client.csproj')
    (Join-Path $repositoryRoot 'src\CanDoItAll.IPFS.Core\CanDoItAll.IPFS.Core.csproj')
    (Join-Path $repositoryRoot 'src\CanDoItAll.IPFS.Engine\CanDoItAll.IPFS.Engine.csproj')
)

$requiredPaths = @($solutionPath) + $packageProjects
foreach ($requiredPath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required packaging input was not found: '$requiredPath'."
    }
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\packages'
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot $OutputDirectory
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$operation = if ($NoRestore) {
    'Build, test, and pack the public NuGet packages without restore'
}
else {
    'Restore, build, test, and pack the public NuGet packages'
}

if (-not $PSCmdlet.ShouldProcess($OutputDirectory, $operation)) {
    [pscustomobject]@{
        Repository = Split-Path $repositoryRoot -Leaf
        Configuration = $Configuration
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
        -Arguments @('restore', $solutionPath, '--configfile', (Join-Path $repositoryRoot 'NuGet.config')) `
        -Description 'dotnet restore'
}

Invoke-DotNetCommand `
    -Arguments @(
        'build',
        $solutionPath,
        '--configuration', $Configuration,
        '--no-restore',
        '-p:ContinuousIntegrationBuild=true',
        '-p:GeneratePackageOnBuild=false'
    ) `
    -Description 'dotnet build'

Invoke-DotNetCommand `
    -Arguments @(
        'test',
        $solutionPath,
        '--configuration', $Configuration,
        '--no-build',
        '--no-restore'
    ) `
    -Description 'dotnet test'

foreach ($packageProject in $packageProjects) {
    Invoke-DotNetCommand `
        -Arguments @(
            'pack',
            $packageProject,
            '--configuration', $Configuration,
            '--no-build',
            '--no-restore',
            '--output', $OutputDirectory,
            '-p:ContinuousIntegrationBuild=true',
            '-p:GeneratePackageOnBuild=false'
    ) `
        -Description (
            "dotnet pack for '$([System.IO.Path]::GetFileNameWithoutExtension($packageProject))'"
        )
}

& (Join-Path $repositoryRoot 'tools\validation\Test-NuGetPackages.ps1') `
    -PackageDirectory $OutputDirectory
if ($LASTEXITCODE -ne 0) {
    throw "NuGet package validation failed with exit code $LASTEXITCODE."
}

[pscustomobject]@{
    Repository = Split-Path $repositoryRoot -Leaf
    Configuration = $Configuration
    OutputDirectory = $OutputDirectory
    Packages = @(
        $packageProjects |
            ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension($_) }
    )
    Status = 'Succeeded'
}
