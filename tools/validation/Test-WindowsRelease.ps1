[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,

    [string]$ControlUrl = 'http://127.0.0.1:5192',

    [string]$NodeUrl = 'http://127.0.0.1:5101/',

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Passphrase,

    [switch]$StopAfterProbe
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Wait-ForUrl {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                return
            }
        }
        catch {
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Timed out waiting for $Url to answer."
}

$packageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
$controlExecutable = Join-Path $packageRoot 'CanDoItAll.IPFS.NodeControl.exe'
if (-not (Test-Path -LiteralPath $controlExecutable)) {
    throw "Could not find $controlExecutable."
}

$testDataRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $packageRoot 'data\release-smoke')
)
$packagePrefix = $packageRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar
) + [System.IO.Path]::DirectorySeparatorChar
if (-not $testDataRoot.StartsWith(
        $packagePrefix,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
    throw "Refusing to reset '$testDataRoot' because it is outside '$packageRoot'."
}

if (-not $PSCmdlet.ShouldProcess(
        $testDataRoot,
        'Reset release smoke data and start the packaged NodeControl application'
    )) {
    return
}

if (Test-Path -LiteralPath $testDataRoot) {
    Remove-Item -LiteralPath $testDataRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testDataRoot -Force | Out-Null

$startInfo = [System.Diagnostics.ProcessStartInfo]::new($controlExecutable)
$startInfo.WorkingDirectory = $packageRoot
$startInfo.UseShellExecute = $false
$startInfo.Environment['ASPNETCORE_URLS'] = $ControlUrl
$startInfo.Environment['NodeSettingsDefaults__BaseUrl'] = $NodeUrl
$startInfo.Environment['IPFS_PASS'] = $Passphrase
$startInfo.Environment['IPFS_PATH'] = $testDataRoot
$startInfo.Environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $packageRoot 'bundle-cache'

$process = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $process) {
    throw 'Could not start the published control app.'
}

try {
    Wait-ForUrl -Url $ControlUrl
    Write-Host "ProcessId=$($process.Id)"
    Write-Host "ControlUrl=$ControlUrl"
    Write-Host "NodeUrl=$NodeUrl"
    Write-Host "DataRoot=$testDataRoot"
}
finally {
    if ($StopAfterProbe -and -not $process.HasExited) {
        $process.Kill($true)
        $process.WaitForExit(5000) | Out-Null
    }

    $process.Dispose()
}
