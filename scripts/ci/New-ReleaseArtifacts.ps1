[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AgentPublishDir,
    [Parameter(Mandatory = $true)][string]$UpdaterPublishDir,
    [Parameter(Mandatory = $true)][string]$WebhookPublishDir,
    [Parameter(Mandatory = $true)][string]$BaseUrl,
    [Parameter(Mandatory = $true)][string]$SigningCertificatePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-RelativePathCompat {
    param(
        [Parameter(Mandatory = $true)][string]$BasePath,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $baseFullPath = (Resolve-Path -LiteralPath $BasePath).Path.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar
    $targetFullPath = (Resolve-Path -LiteralPath $Path).Path
    $baseUri = [System.Uri]::new($baseFullPath)
    $targetUri = [System.Uri]::new($targetFullPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace("/", [System.IO.Path]::DirectorySeparatorChar)
}

function Find-SignTool {
    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $signtool = Get-ChildItem -Path $kitsRoot -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($signtool) {
            return $signtool.FullName
        }
    }

    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($vsPath) {
            $signtool = Get-ChildItem -Path $vsPath -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match "\\x64\\signtool\.exe$" } |
                Sort-Object FullName -Descending |
                Select-Object -First 1
            if ($signtool) {
                return $signtool.FullName
            }
        }
    }

    return $null
}

function Add-ChecksumFile {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath
    )

    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $FilePath).Hash.ToLowerInvariant()
    $fileName = Split-Path -Leaf $FilePath
    Set-Content -LiteralPath "$FilePath.sha256" -Encoding ASCII -Value "$hash  $fileName"
    return $hash
}

function Write-DirectoryChecksums {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][string]$OutputFile
    )

    if (Test-Path -LiteralPath $OutputFile) {
        Remove-Item -LiteralPath $OutputFile -Force
    }

    $rootPath = (Resolve-Path -LiteralPath $Directory).Path
    $entries = Get-ChildItem -LiteralPath $rootPath -Recurse -File |
        Where-Object {
            $_.FullName -ne $OutputFile -and
            $_.Name -ne "SHA256SUMS.txt" -and
            $_.Extension -ne ".sha256"
        } |
        Sort-Object FullName |
        ForEach-Object {
            $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant()
            $relativePath = (Get-RelativePathCompat -BasePath $rootPath -Path $_.FullName).Replace("\", "/")
            "$hash  $relativePath"
        }

    $entries | Set-Content -LiteralPath $OutputFile -Encoding ASCII
}

if ([string]::IsNullOrWhiteSpace($env:PFX_PASSWORD)) {
    throw "PFX_PASSWORD secret variable is required for signing."
}

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw "BaseUrl is required for latest.json."
}

if (-not (Test-Path -LiteralPath $SigningCertificatePath)) {
    throw "Signing certificate secure file was not found."
}

$agentExe = Join-Path $AgentPublishDir "AutoInventario.exe"
$updaterExe = Join-Path $UpdaterPublishDir "AutoInventario.Updater.exe"

if (-not (Test-Path -LiteralPath $agentExe)) {
    throw "Agent executable was not found in $AgentPublishDir."
}

if (-not (Test-Path -LiteralPath $updaterExe)) {
    throw "Updater executable was not found in $UpdaterPublishDir."
}

$signtool = Find-SignTool
if (-not $signtool) {
    throw "signtool.exe was not found on the build agent."
}

$timestampUrl = if ([string]::IsNullOrWhiteSpace($env:TIMESTAMP_URL)) {
    "http://timestamp.sectigo.com"
}
else {
    $env:TIMESTAMP_URL
}

Write-Host "Signing product binaries with Secure File certificate."
& $signtool sign /f "$SigningCertificatePath" /p "$env:PFX_PASSWORD" /tr "$timestampUrl" /td sha256 /fd sha256 "$agentExe"
if ($LASTEXITCODE -ne 0) {
    throw "Agent signing failed with exit code $LASTEXITCODE."
}

& $signtool sign /f "$SigningCertificatePath" /p "$env:PFX_PASSWORD" /tr "$timestampUrl" /td sha256 /fd sha256 "$updaterExe"
if ($LASTEXITCODE -ne 0) {
    throw "Updater signing failed with exit code $LASTEXITCODE."
}

$version = (Get-Item -LiteralPath $agentExe).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.0.0.0"
}

$normalizedBaseUrl = $BaseUrl.TrimEnd("/")

$updatesRoot = Join-Path $WebhookPublishDir "wwwroot/updates"
$versionDir = Join-Path $updatesRoot $version
New-Item -ItemType Directory -Force -Path $versionDir | Out-Null

$agentUpdatePath = Join-Path $versionDir "AutoInventario.exe"
$updaterUpdatePath = Join-Path $updatesRoot "AutoInventario.Updater.exe"
Copy-Item -LiteralPath $agentExe -Destination $agentUpdatePath -Force
Copy-Item -LiteralPath $updaterExe -Destination $updaterUpdatePath -Force

$hashMain = Add-ChecksumFile -FilePath $agentExe
$hashUpdater = Add-ChecksumFile -FilePath $updaterExe
Add-ChecksumFile -FilePath $agentUpdatePath | Out-Null
Add-ChecksumFile -FilePath $updaterUpdatePath | Out-Null

$manifest = [ordered]@{
    version = $version
    files   = [ordered]@{
        main    = "$normalizedBaseUrl/updates/$version/AutoInventario.exe"
        updater = "$normalizedBaseUrl/updates/AutoInventario.Updater.exe"
    }
    sha256  = [ordered]@{
        main    = $hashMain
        updater = $hashUpdater
    }
}

$latestPath = Join-Path $updatesRoot "latest.json"
($manifest | ConvertTo-Json -Depth 10) | Out-File -Encoding UTF8 -LiteralPath $latestPath

Write-DirectoryChecksums -Directory $AgentPublishDir -OutputFile (Join-Path $AgentPublishDir "SHA256SUMS.txt")
Write-DirectoryChecksums -Directory $UpdaterPublishDir -OutputFile (Join-Path $UpdaterPublishDir "SHA256SUMS.txt")
Write-DirectoryChecksums -Directory $WebhookPublishDir -OutputFile (Join-Path $WebhookPublishDir "SHA256SUMS.txt")

Write-Host "Generated update manifest and SHA256 checksum files."
