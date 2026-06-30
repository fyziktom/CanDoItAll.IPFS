$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$project = "candoitallipfssb09"
$composeFile = Join-Path $PSScriptRoot "docker-compose.e2e.yml"
$payloadPath = Join-Path $PSScriptRoot "docker-e2e-payload.txt"
$resultPath = Join-Path $PSScriptRoot "docker-multinode-e2e-summary.json"
$env:IPFS_PASS = "sb09-local-validation-passphrase"

Set-Content -Path $payloadPath -Value "SB09 docker multinode pin/unpin proof $(Get-Date -Format o)" -NoNewline

function Invoke-IpfsApi {
    param(
        [Parameter(Mandatory = $true)] [string] $BaseUrl,
        [Parameter(Mandatory = $true)] [string] $Path,
        [hashtable] $Query = @{}
    )

    $builder = [System.UriBuilder]::new("$BaseUrl/api/v0/$Path")
    if ($Query.Count -gt 0) {
        $pairs = foreach ($key in $Query.Keys) {
            "$([System.Uri]::EscapeDataString($key))=$([System.Uri]::EscapeDataString([string]$Query[$key]))"
        }
        $builder.Query = [string]::Join("&", $pairs)
    }

    try {
        Invoke-RestMethod -Method Post -Uri $builder.Uri.AbsoluteUri -TimeoutSec 60
    }
    catch {
        $body = $null
        if ($_.Exception.Response) {
            try {
                $stream = $_.Exception.Response.GetResponseStream()
                $reader = [System.IO.StreamReader]::new($stream)
                $body = $reader.ReadToEnd()
                $reader.Dispose()
            }
            catch {
                $body = $null
            }
        }

        throw "IPFS API call '$Path' failed at '$($builder.Uri.AbsoluteUri)'. $body"
    }
}

function Wait-Node {
    param([string] $BaseUrl)

    $deadline = (Get-Date).AddMinutes(3)
    do {
        try {
            return Invoke-IpfsApi -BaseUrl $BaseUrl -Path "id"
        }
        catch {
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    throw "Node $BaseUrl did not become ready."
}

function Add-File {
    param(
        [string] $BaseUrl,
        [string] $FilePath
    )

    $client = [System.Net.Http.HttpClient]::new()
    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $stream = [System.IO.File]::OpenRead($FilePath)
    try {
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $content.Add($fileContent, "file", [System.IO.Path]::GetFileName($FilePath))
        $response = $client.PostAsync("$BaseUrl/api/v0/add?pin=true&progress=false", $content).GetAwaiter().GetResult()
        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) {
            throw "Add failed with HTTP $([int]$response.StatusCode): $body"
        }

        foreach ($jsonLine in ($body -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
            $node = $jsonLine | ConvertFrom-Json
            if (-not [string]::IsNullOrWhiteSpace($node.Hash)) {
                return $node.Hash
            }
        }

        throw "Add response did not contain a final Hash value: $body"
    }
    finally {
        $content.Dispose()
        $client.Dispose()
        $stream.Dispose()
    }
}

function Get-DialAddress {
    param($PeerInfo)

    $address = @($PeerInfo.Addresses) |
        Where-Object { $_ -match "/ip4/" -and $_ -notmatch "/127\.0\.0\.1/" -and $_ -notmatch "/0\.0\.0\.0/" } |
        Select-Object -First 1
    if (-not $address) {
        $address = @($PeerInfo.Addresses) | Where-Object { $_ -match "/ip4/" } | Select-Object -First 1
    }
    if (-not $address) {
        throw "No dialable IPv4 address was returned for peer $($PeerInfo.ID)."
    }

    if ($address -notmatch "/(ipfs|p2p)/") {
        $address = "$address/p2p/$($PeerInfo.ID)"
    }
    return $address
}

function Get-PinnedCidNames {
    param([string] $BaseUrl)

    $pins = Invoke-IpfsApi -BaseUrl $BaseUrl -Path "pin/ls"
    if ($null -eq $pins.Keys) {
        return @()
    }

    return @($pins.Keys.PSObject.Properties.Name)
}

function Wait-SwarmPeer {
    param(
        [string] $BaseUrl,
        [string] $PeerId,
        [string] $Label
    )

    $deadline = (Get-Date).AddSeconds(90)
    do {
        $peers = Invoke-IpfsApi -BaseUrl $BaseUrl -Path "swarm/peers"
        if (@($peers.Peers | Where-Object { $_.Peer -eq $PeerId }).Count -gt 0) {
            return
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$Label did not show connected peer $PeerId."
}

function Assert-Pinned {
    param(
        [string] $BaseUrl,
        [string] $Cid,
        [string] $Label
    )

    $deadline = (Get-Date).AddSeconds(120)
    do {
        try {
            $pins = Get-PinnedCidNames -BaseUrl $BaseUrl
            if ($pins -contains $Cid) {
                return
            }
        }
        catch {
            if ((Get-Date) -ge $deadline) {
                throw
            }
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$Label does not list pinned CID $Cid."
}

function Assert-Unpinned {
    param(
        [string] $BaseUrl,
        [string] $Cid,
        [string] $Label
    )

    $deadline = (Get-Date).AddSeconds(60)
    do {
        $pins = Get-PinnedCidNames -BaseUrl $BaseUrl
        if (-not ($pins -contains $Cid)) {
            return
        }

        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)

    throw "$Label still lists unpinned CID $Cid."
}

Write-Host "Starting two-node docker compose stack."
docker compose -p $project -f $composeFile up -d --build | Out-Host

Write-Host "Waiting for node APIs."
$nodeA = Wait-Node "http://127.0.0.1:5101"
$nodeB = Wait-Node "http://127.0.0.1:5102"
Write-Host "Adding pinned file to node A."
$cid = Add-File "http://127.0.0.1:5101" $payloadPath
Assert-Pinned "http://127.0.0.1:5101" $cid "node A before restart"

Write-Host "Restarting both nodes and checking node A persistence."
docker compose -p $project -f $composeFile restart ipfs-a ipfs-b | Out-Host
$nodeA = Wait-Node "http://127.0.0.1:5101"
$nodeB = Wait-Node "http://127.0.0.1:5102"
Assert-Pinned "http://127.0.0.1:5101" $cid "node A after restart"

Write-Host "Connecting node B to node A and pinning node A content from node B."
$dialA = Get-DialAddress $nodeA
Invoke-IpfsApi -BaseUrl "http://127.0.0.1:5102" -Path "swarm/connect" -Query @{ arg = $dialA } | Out-Null
Wait-SwarmPeer -BaseUrl "http://127.0.0.1:5102" -PeerId $nodeA.ID -Label "node B"
$pinAdd = Invoke-IpfsApi -BaseUrl "http://127.0.0.1:5102" -Path "pin/add" -Query @{ arg = $cid; recursive = "true" }
if (-not (@($pinAdd.Pins) -contains $cid)) {
    throw "node B pin/add did not return pinned CID $cid."
}
Assert-Pinned "http://127.0.0.1:5102" $cid "node B after remote pin"

Write-Host "Unpinning from node B and verifying node A remains pinned."
Invoke-IpfsApi -BaseUrl "http://127.0.0.1:5102" -Path "pin/rm" -Query @{ arg = $cid; recursive = "true" } | Out-Null
Assert-Unpinned "http://127.0.0.1:5102" $cid "node B after unpin"
Assert-Pinned "http://127.0.0.1:5101" $cid "node A after node B unpin"

Write-Host "Rebuilding/recreating compose stack and checking node A persistence."
docker compose -p $project -f $composeFile up -d --build | Out-Host
Wait-Node "http://127.0.0.1:5101" | Out-Null
Assert-Pinned "http://127.0.0.1:5101" $cid "node A after rebuild"

$summary = [ordered]@{
    project = $project
    nodeA = $nodeA.ID
    nodeB = $nodeB.ID
    cid = $cid
    nodeAPersistentAfterRestart = $true
    nodeAPersistentAfterRebuild = $true
    nodeBPinAddVerified = $true
    nodeBUnpinVerified = $true
    completedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
}
$summary | ConvertTo-Json -Depth 6 | Set-Content -Path $resultPath
$summary | ConvertTo-Json -Depth 6

docker compose -p $project -f $composeFile down | Out-Host
