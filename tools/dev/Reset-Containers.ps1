[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^candoitall-ipfs(?:-[a-z0-9-]+)?$')]
    [string]$ProjectName,

    [string]$EnvFile = '.env'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$composePath = Join-Path $repositoryRoot 'compose.yaml'
if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Canonical Compose file was not found: '$composePath'."
}

$envPath = if ([System.IO.Path]::IsPathRooted($EnvFile)) {
    (Resolve-Path -LiteralPath $EnvFile).Path
}
else {
    (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $EnvFile)).Path
}

$dataDescription = (
    "Remove containers and the '$ProjectName' project volumes " +
    "'ipfs-node-data' and 'node-control-data'. This permanently deletes node and " +
    'NodeControl data'
)
if (-not $PSCmdlet.ShouldProcess($ProjectName, $dataDescription)) {
    return
}

Write-Warning (
    "Deleting all project-scoped durable data for '$ProjectName': " +
    'ipfs-node-data and node-control-data.'
)

& docker compose `
    --env-file $envPath `
    -f $composePath `
    -p $ProjectName `
    down --volumes --remove-orphans
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose reset failed with exit code $LASTEXITCODE."
}
