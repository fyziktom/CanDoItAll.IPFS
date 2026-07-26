[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [string[]]$ComposeFile = @('compose.yaml'),

    [string]$EnvFile = '.env.example',

    [switch]$RunBuildChecks,

    [switch]$Smoke,

    [ValidateRange(1, 3600)]
    [int]$WaitTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $RepositoryRoot $Path)).Path
}

function Assert-ModelProperty {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($null -eq $InputObject.PSObject.Properties[$PropertyName]) {
        throw "Resolved Compose model is missing $Description ('$PropertyName')."
    }
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$canonicalCompose = Join-Path $repositoryRoot 'compose.yaml'
$composePaths = @(
    foreach ($path in $ComposeFile) {
        Resolve-RepositoryPath -Path $path -RepositoryRoot $repositoryRoot
    }
)
if ($composePaths.Count -eq 0 -or $composePaths[0] -ne $canonicalCompose) {
    throw "The canonical first Compose file must be '$canonicalCompose'."
}

$envPath = Resolve-RepositoryPath -Path $EnvFile -RepositoryRoot $repositoryRoot
$requiredFiles = @(
    (Join-Path $repositoryRoot '.dockerignore')
    (Join-Path $repositoryRoot '.env.example')
    (Join-Path $repositoryRoot 'docker\Dockerfile.ipfs-node')
    (Join-Path $repositoryRoot 'docker\Dockerfile.node-control')
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "Required Docker input was not found: '$requiredFile'."
    }
}

$composeText = Get-Content -Raw -LiteralPath $canonicalCompose
if ($composeText -match '(?im)^\s*version\s*:') {
    throw "The obsolete top-level Compose 'version' property is not allowed."
}
if ($composeText -match '(?im)^\s*container_name\s*:') {
    throw "Compose container_name overrides are not allowed."
}
if ($composeText -match '(?im)^\s*image\s*:\s*[^\r\n#]*(?::latest(?:\s|$)|(?<![:}])\s*$)') {
    throw 'Committed Compose images must have explicit non-latest tags or digests.'
}

$dockerignoreText = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot '.dockerignore')
foreach ($requiredIgnore in @('.env', 'bundles', 'docker/local-packages')) {
    if ($dockerignoreText -notmatch [regex]::Escape($requiredIgnore)) {
        throw "The root .dockerignore does not cover '$requiredIgnore'."
    }
}

$docker = Get-Command docker -ErrorAction Stop
$composeArguments = [System.Collections.Generic.List[string]]::new()
$composeArguments.Add('compose')
$composeArguments.Add('--env-file')
$composeArguments.Add($envPath)
foreach ($composePath in $composePaths) {
    $composeArguments.Add('-f')
    $composeArguments.Add($composePath)
}

& $docker.Source @composeArguments config --quiet
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose configuration validation failed with exit code $LASTEXITCODE."
}

$resolvedJson = (& $docker.Source @composeArguments config --format json) -join [Environment]::NewLine
if ($LASTEXITCODE -ne 0) {
    throw "Docker Compose JSON resolution failed with exit code $LASTEXITCODE."
}
$model = $resolvedJson | ConvertFrom-Json
if ($model.name -notmatch '^candoitall-ipfs(?:-[a-z0-9-]+)?$') {
    throw (
        "Compose project name '$($model.name)' does not follow the " +
        "'candoitall-ipfs[-<short-id>]' convention."
    )
}

foreach ($serviceName in @('ipfs-node', 'node-control')) {
    $serviceProperty = $model.services.PSObject.Properties[$serviceName]
    if ($null -eq $serviceProperty) {
        throw "Resolved Compose model is missing service '$serviceName'."
    }

    $service = $serviceProperty.Value
    Assert-ModelProperty -InputObject $service -PropertyName 'healthcheck' -Description "$serviceName healthcheck"
    Assert-ModelProperty -InputObject $service -PropertyName 'mem_limit' -Description "$serviceName memory limit"
    Assert-ModelProperty -InputObject $service -PropertyName 'cpus' -Description "$serviceName CPU limit"
    Assert-ModelProperty -InputObject $service -PropertyName 'pids_limit' -Description "$serviceName PID limit"
    Assert-ModelProperty -InputObject $service -PropertyName 'stop_grace_period' -Description "$serviceName stop grace period"

    if ($service.restart -ne 'no') {
        throw "Development service '$serviceName' must use restart: no."
    }
    if ($service.logging.driver -ne 'local') {
        throw "Development service '$serviceName' must use bounded local logging."
    }
    if (@($service.cap_drop) -notcontains 'ALL') {
        throw "Development service '$serviceName' must drop all Linux capabilities."
    }
    if (@($service.security_opt) -notcontains 'no-new-privileges:true') {
        throw "Development service '$serviceName' must enable no-new-privileges."
    }

    foreach ($port in @($service.ports)) {
        if ($port.host_ip -notin @('127.0.0.1', '::1')) {
            throw "Service '$serviceName' publishes port '$($port.published)' outside loopback."
        }
    }
}

$nodeControlDependency = $model.services.'node-control'.depends_on.'ipfs-node'
if ($nodeControlDependency.condition -ne 'service_healthy') {
    throw 'NodeControl must wait for the IPFS node to become healthy.'
}
foreach ($volumeName in @('ipfs-node-data', 'node-control-data')) {
    if ($null -eq $model.volumes.PSObject.Properties[$volumeName]) {
        throw "Resolved Compose model is missing named volume '$volumeName'."
    }
}

$dockerfiles = @(
    (Join-Path $repositoryRoot 'docker\Dockerfile.ipfs-node')
    (Join-Path $repositoryRoot 'docker\Dockerfile.node-control')
)
foreach ($dockerfile in $dockerfiles) {
    $dockerfileText = Get-Content -Raw -LiteralPath $dockerfile
    if (([regex]::Matches($dockerfileText, '(?im)^\s*FROM\s+')).Count -lt 2) {
        throw "Dockerfile '$dockerfile' must use a multi-stage build."
    }
    if ($dockerfileText -notmatch '(?im)^\s*USER\s+\$APP_UID\s*$') {
        throw "Dockerfile '$dockerfile' must finish as the .NET image application user."
    }
    if ($dockerfileText -notmatch '(?im)^\s*ENTRYPOINT\s+\[') {
        throw "Dockerfile '$dockerfile' must use JSON/exec-form ENTRYPOINT."
    }

    if ($RunBuildChecks) {
        & $docker.Source build --check --file $dockerfile $repositoryRoot
        if ($LASTEXITCODE -ne 0) {
            throw "docker build --check failed for '$dockerfile'."
        }
    }
}

$sharedInfoCandidates = @(
    $env:CANDOITALL_SHAREDINFO_ROOT
    (Join-Path (Split-Path -Parent $repositoryRoot) 'CanDoItAll.SharedInfo')
) | Where-Object { $_ }

$sharedValidator = $null
foreach ($candidate in $sharedInfoCandidates) {
    $validator = Join-Path $candidate 'tools\validation\Test-DockerConventions.ps1'
    if (Test-Path -LiteralPath $validator -PathType Leaf) {
        $sharedValidator = (Resolve-Path -LiteralPath $validator).Path
        break
    }
}

if ($sharedValidator) {
    & $sharedValidator `
        -RepositoryPath $repositoryRoot `
        -ComposeFile $composePaths `
        -EnvFile $envPath `
        -RequireDocker `
        -WarningsAsErrors
}
else {
    Write-Warning (
        'CanDoItAll.SharedInfo was not found. Product-specific Docker checks passed, ' +
        'but the additional shared policy validator was skipped.'
    )
}

if (-not $Smoke) {
    [pscustomobject]@{
        Repository = Split-Path $repositoryRoot -Leaf
        ComposeFiles = $composePaths
        BuildChecks = [bool]$RunBuildChecks
        Smoke = $false
        Status = 'Succeeded'
    }
    return
}

$projectName = "candoitall-ipfs-validation-$PID"
$imageTags = @(
    "candoitall-ipfs-node:validation-$PID"
    "candoitall-ipfs-node-control:validation-$PID"
)
if (-not $PSCmdlet.ShouldProcess(
        $projectName,
        'Build, start, and wait for the disposable stack, then remove its containers, volumes, and validation images'
    )) {
    return
}

$smokeEnvironment = @{
    IPFS_NODE_API_PORT = '0'
    NODE_CONTROL_PORT = '0'
    IPFS_NODE_IMAGE = $imageTags[0]
    NODE_CONTROL_IMAGE = $imageTags[1]
}
$previousEnvironment = @{}
foreach ($name in $smokeEnvironment.Keys) {
    $previousEnvironment[$name] = [System.Environment]::GetEnvironmentVariable(
        $name,
        [System.EnvironmentVariableTarget]::Process
    )
    [System.Environment]::SetEnvironmentVariable(
        $name,
        $smokeEnvironment[$name],
        [System.EnvironmentVariableTarget]::Process
    )
}

$started = $false
try {
    $started = $true
    & $docker.Source @composeArguments -p $projectName up -d --build --wait --wait-timeout $WaitTimeoutSeconds
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose smoke startup failed with exit code $LASTEXITCODE."
    }

    & $docker.Source @composeArguments -p $projectName ps --all
    if ($LASTEXITCODE -ne 0) {
        throw "Docker Compose status failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($started) {
        & $docker.Source @composeArguments -p $projectName down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Smoke teardown failed for '$projectName' with exit code $LASTEXITCODE."
        }

        & $docker.Source image rm --force @imageTags
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Validation image cleanup failed with exit code $LASTEXITCODE."
        }
    }

    foreach ($name in $previousEnvironment.Keys) {
        [System.Environment]::SetEnvironmentVariable(
            $name,
            $previousEnvironment[$name],
            [System.EnvironmentVariableTarget]::Process
        )
    }
}

[pscustomobject]@{
    Repository = Split-Path $repositoryRoot -Leaf
    ComposeFiles = $composePaths
    BuildChecks = [bool]$RunBuildChecks
    Smoke = $true
    Status = 'Succeeded'
}
