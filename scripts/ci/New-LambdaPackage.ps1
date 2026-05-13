[CmdletBinding()]
param(
    [string]$Root,
    [string]$OutputPath
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

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $Root "Infraestructura-Terraform/lambda_package.zip"
}

$lambdaDir = Join-Path $Root "Lambda-Inventario"
$requirementsPath = Join-Path $lambdaDir "requirements.txt"
$packageDir = Join-Path $lambdaDir "package"
$lambdaFunctionPath = Join-Path $lambdaDir "lambda_function.py"
$configPath = Join-Path $lambdaDir "config.json"

if (-not (Test-Path -LiteralPath $requirementsPath)) {
    throw "Lambda requirements file was not found."
}

if (-not (Test-Path -LiteralPath $lambdaFunctionPath)) {
    throw "Lambda function file was not found."
}

Remove-Item -LiteralPath $packageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null

python -m pip install -r $requirementsPath -t $packageDir --disable-pip-version-check
if ($LASTEXITCODE -ne 0) {
    throw "Python dependency install failed with exit code $LASTEXITCODE."
}

if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
}
else {
    $resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $Root $OutputPath))
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $resolvedOutput) | Out-Null
Remove-Item -LiteralPath $resolvedOutput -Force -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($resolvedOutput, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = Get-ChildItem -LiteralPath $packageDir -Recurse -File
    foreach ($file in $files) {
        $relativePath = (Get-RelativePathCompat -BasePath $packageDir -Path $file.FullName).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $file.FullName,
            $relativePath,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }

    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $lambdaFunctionPath,
        "lambda_function.py",
        [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null

    if (Test-Path -LiteralPath $configPath) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $configPath,
            "config.json",
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Lambda package generated."
