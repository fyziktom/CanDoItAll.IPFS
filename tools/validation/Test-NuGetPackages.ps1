[CmdletBinding()]
param(
    [string]$PackageDirectory = 'artifacts\packages',

    [string[]]$ExpectedPackageId = @(
        'CanDoItAll.IPFS.Client',
        'CanDoItAll.IPFS.Core',
        'CanDoItAll.IPFS.Engine'
    ),

    [string]$ExpectedPackageVersion = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-ZipEntryBytes {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Entry
    )

    $entryStream = $Entry.Open()
    $memoryStream = [System.IO.MemoryStream]::new()
    try {
        $entryStream.CopyTo($memoryStream)
        return ,$memoryStream.ToArray()
    }
    finally {
        $entryStream.Dispose()
        $memoryStream.Dispose()
    }
}

function Get-Sha256 {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.BitConverter]::ToString($sha256.ComputeHash($Bytes)).Replace('-', '')
    }
    finally {
        $sha256.Dispose()
    }
}

function Get-PngDimensions {
    param(
        [Parameter(Mandatory = $true)]
        [byte[]]$Bytes
    )

    if ($Bytes.Length -lt 24) {
        throw 'PNG data is too short to contain an IHDR chunk.'
    }

    $signature = (
        $Bytes[0..7] |
            ForEach-Object { $_.ToString('X2') }
    ) -join ''
    if ($signature -ne '89504E470D0A1A0A') {
        throw 'Package icon is not a valid PNG file.'
    }

    [pscustomobject]@{
        Width = (
            ([int]$Bytes[16] -shl 24) -bor
            ([int]$Bytes[17] -shl 16) -bor
            ([int]$Bytes[18] -shl 8) -bor
            [int]$Bytes[19]
        )
        Height = (
            ([int]$Bytes[20] -shl 24) -bor
            ([int]$Bytes[21] -shl 16) -bor
            ([int]$Bytes[22] -shl 8) -bor
            [int]$Bytes[23]
        )
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$packagePath = if ([System.IO.Path]::IsPathRooted($PackageDirectory)) {
    (Resolve-Path -LiteralPath $PackageDirectory).Path
}
else {
    (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $PackageDirectory)).Path
}

$repositoryLicensePath = Join-Path $repositoryRoot 'LICENSE'
$repositoryLicenseBytes = [System.IO.File]::ReadAllBytes($repositoryLicensePath)
$repositoryLicenseHash = Get-Sha256 -Bytes $repositoryLicenseBytes
$repositoryIconPath = Join-Path $repositoryRoot 'docs\package-icon.png'
$repositoryIconBytes = [System.IO.File]::ReadAllBytes($repositoryIconPath)
$repositoryIconHash = Get-Sha256 -Bytes $repositoryIconBytes
$expectedCorporateIconHash = (
    '02B338424A63193ECE3E25BC7E15A1E8F382E3E64C6DF80D24279C0C0FDA130E'
)
if ($repositoryIconHash -ne $expectedCorporateIconHash) {
    throw (
        "Repository package icon '$repositoryIconPath' is not the approved " +
        'CanDoItAll corporate favicon.'
    )
}
if ($repositoryIconBytes.Length -gt 1MB) {
    throw "Repository package icon '$repositoryIconPath' exceeds the NuGet 1 MB limit."
}
$repositoryIconDimensions = Get-PngDimensions -Bytes $repositoryIconBytes
if (
    $repositoryIconDimensions.Width -ne 256 -or
    $repositoryIconDimensions.Height -ne 256
) {
    throw (
        "Repository package icon must be 256x256, but is " +
        "$($repositoryIconDimensions.Width)x$($repositoryIconDimensions.Height)."
    )
}
$repositoryUrl = 'https://github.com/fyziktom/CanDoItAll.IPFS'
$projectUrl = 'https://aicandoitall.com'
$packageAuthors = 'fyziktom'
$packageCopyright = (
    'Copyright (c) 2026 fyziktom. Portions copyright (c) 2018 Richard Schneider.'
)
$upstreamRepositoryUrl = 'https://github.com/richardschneider/'
$expectedTargetFramework = 'net10.0'

$packages = @(
    Get-ChildItem -LiteralPath $packagePath -File -Filter '*.nupkg' |
        Where-Object { $_.Name -notlike '*.symbols.nupkg' } |
        Sort-Object Name
)
if ($packages.Count -eq 0) {
    throw "No NuGet packages were found in '$packagePath'."
}

$validatedIds = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::Ordinal
)
$results = [System.Collections.Generic.List[object]]::new()

foreach ($package in $packages) {
    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    }
    catch {
        throw "Package '$($package.Name)' is not a readable NuGet ZIP archive: $($_.Exception.Message)"
    }
    try {
        $nuspecEntries = @(
            $archive.Entries |
                Where-Object {
                    $_.FullName -notmatch '/' -and
                    $_.FullName.EndsWith('.nuspec', [System.StringComparison]::OrdinalIgnoreCase)
                }
        )
        if ($nuspecEntries.Count -ne 1) {
            throw "Package '$($package.Name)' must contain exactly one root .nuspec."
        }

        $nuspecBytes = Get-ZipEntryBytes -Entry $nuspecEntries[0]
        $nuspecText = [System.Text.Encoding]::UTF8.GetString($nuspecBytes)
        if ($nuspecText.Length -gt 0 -and $nuspecText[0] -eq [char]0xFEFF) {
            $nuspecText = $nuspecText.Substring(1)
        }
        [xml]$nuspec = $nuspecText
        $metadata = $nuspec.SelectSingleNode(
            "/*[local-name()='package']/*[local-name()='metadata']"
        )
        if ($null -eq $metadata) {
            throw "Package '$($package.Name)' has no NuGet metadata node."
        }

        $id = $metadata.SelectSingleNode("*[local-name()='id']").InnerText
        if ($ExpectedPackageId -notcontains $id) {
            throw "Unexpected package '$id' was produced in '$packagePath'."
        }
        if (-not $validatedIds.Add($id)) {
            throw (
                "Package directory '$packagePath' contains more than one archive for '$id'. " +
                'Use a clean package output directory.'
            )
        }

        $version = $metadata.SelectSingleNode("*[local-name()='version']").InnerText
        if (
            -not [string]::IsNullOrWhiteSpace($ExpectedPackageVersion) -and
            $version -ne $ExpectedPackageVersion
        ) {
            throw (
                "Package '$id' has version '$version'; expected " +
                "'$ExpectedPackageVersion'."
            )
        }

        $authorsNode = $metadata.SelectSingleNode("*[local-name()='authors']")
        if ($null -eq $authorsNode -or $authorsNode.InnerText -ne $packageAuthors) {
            throw "Package '$id' authors must be '$packageAuthors'."
        }

        $copyrightNode = $metadata.SelectSingleNode("*[local-name()='copyright']")
        if (
            $null -eq $copyrightNode -or
            $copyrightNode.InnerText -ne $packageCopyright
        ) {
            throw "Package '$id' copyright metadata must be '$packageCopyright'."
        }

        $licenseNode = $metadata.SelectSingleNode("*[local-name()='license']")
        if (
            $null -eq $licenseNode -or
            $licenseNode.GetAttribute('type') -ne 'file' -or
            $licenseNode.InnerText -ne 'LICENSE'
        ) {
            throw "Package '$id' must declare <license type=`"file`">LICENSE</license>."
        }

        $projectUrlNode = $metadata.SelectSingleNode("*[local-name()='projectUrl']")
        if (
            $null -eq $projectUrlNode -or
            $projectUrlNode.InnerText.TrimEnd('/') -ne $projectUrl
        ) {
            throw "Package '$id' projectUrl must be '$projectUrl'."
        }

        $repositoryNode = $metadata.SelectSingleNode("*[local-name()='repository']")
        if (
            $null -eq $repositoryNode -or
            $repositoryNode.GetAttribute('type') -ne 'git' -or
            $repositoryNode.GetAttribute('url') -ne $repositoryUrl
        ) {
            throw "Package '$id' repository metadata must identify '$repositoryUrl' as git."
        }

        $readmeNode = $metadata.SelectSingleNode("*[local-name()='readme']")
        if ($null -eq $readmeNode -or $readmeNode.InnerText -ne 'README.md') {
            throw "Package '$id' must declare README.md as its package readme."
        }

        $licenseEntry = $archive.Entries |
            Where-Object { $_.FullName.TrimStart('/') -eq 'LICENSE' } |
            Select-Object -First 1
        if ($null -eq $licenseEntry) {
            throw "Package '$id' does not contain a package-root LICENSE."
        }
        $packageLicenseBytes = Get-ZipEntryBytes -Entry $licenseEntry
        if ((Get-Sha256 -Bytes $packageLicenseBytes) -ne $repositoryLicenseHash) {
            throw "Package '$id' LICENSE is not byte-identical to the repository LICENSE."
        }

        $licenseText = [System.Text.Encoding]::UTF8.GetString($packageLicenseBytes)
        if ($licenseText -notmatch 'https://aicandoitall\.com') {
            throw "Package '$id' LICENSE does not contain the fixed CanDoItAll website link."
        }

        $readmeEntry = $archive.Entries |
            Where-Object { $_.FullName.TrimStart('/') -eq 'README.md' } |
            Select-Object -First 1
        if ($null -eq $readmeEntry) {
            throw "Package '$id' does not contain a package-root README.md."
        }
        $packageReadmeBytes = Get-ZipEntryBytes -Entry $readmeEntry
        $packageReadmeText = [System.Text.Encoding]::UTF8.GetString($packageReadmeBytes)
        if (
            $packageReadmeText -notmatch [regex]::Escape($upstreamRepositoryUrl) -or
            $packageReadmeText -notmatch 'Many thanks to\s+Richard Schneider'
        ) {
            throw (
                "Package '$id' README must thank Richard Schneider and link to an " +
                'original upstream repository.'
            )
        }

        $libTargetFrameworks = @(
            $archive.Entries |
                ForEach-Object {
                    if ($_.FullName -match '^lib/([^/]+)/') {
                        $Matches[1]
                    }
                } |
                Sort-Object -Unique
        )
        if (
            $libTargetFrameworks.Count -ne 1 -or
            $libTargetFrameworks[0] -ne $expectedTargetFramework
        ) {
            throw (
                "Package '$id' must contain only lib/$expectedTargetFramework assets; " +
                "found: $($libTargetFrameworks -join ', ')."
            )
        }

        $iconNode = $metadata.SelectSingleNode("*[local-name()='icon']")
        if ($null -eq $iconNode -or $iconNode.InnerText -ne 'package-icon.png') {
            throw "Package '$id' must declare <icon>package-icon.png</icon>."
        }

        $iconEntries = @(
            $archive.Entries |
                Where-Object { $_.FullName.TrimStart('/') -eq 'package-icon.png' }
        )
        if ($iconEntries.Count -ne 1) {
            throw "Package '$id' must contain exactly one package-root package-icon.png."
        }
        $packageIconBytes = Get-ZipEntryBytes -Entry $iconEntries[0]
        if ((Get-Sha256 -Bytes $packageIconBytes) -ne $repositoryIconHash) {
            throw "Package '$id' does not contain the approved repository package icon."
        }

        $results.Add([pscustomobject]@{
            PackageId = $id
            PackageVersion = $version
            TargetFramework = $expectedTargetFramework
            Authors = $packageAuthors
            Copyright = $packageCopyright
            Archive = $package.Name
            Icon = 'package-icon.png (256x256 corporate favicon)'
            License = 'Repository file'
            ProjectUrl = $projectUrl
            RepositoryUrl = $repositoryUrl
            Status = 'Valid'
        }) | Out-Null
    }
    finally {
        $archive.Dispose()
    }
}

$missingIds = @($ExpectedPackageId | Where-Object { -not $validatedIds.Contains($_) })
if ($missingIds.Count -gt 0) {
    throw "Expected package(s) were not produced: $($missingIds -join ', ')."
}
if ($packages.Count -ne $ExpectedPackageId.Count) {
    throw (
        "Expected exactly $($ExpectedPackageId.Count) package archives, " +
        "but found $($packages.Count) in '$packagePath'."
    )
}

$results
