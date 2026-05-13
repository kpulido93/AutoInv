#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [string]$ServiceName = "AutoInventarioWebhook",

    [string]$DisplayName = "AutoInventario Webhook",

    [string]$DotNetPath = "dotnet",

    [string]$Urls = "http://+:8080",

    [ValidateSet("Local", "AwsLambda")]
    [string]$InventoryProcessingMode = "Local",

    [switch]$SkipStart,

    [switch]$SkipHealthCheck
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this script from an elevated PowerShell session."
    }
}

function Resolve-DotNetPath {
    param([string]$Path)

    if (Test-Path -LiteralPath $Path) {
        return (Resolve-Path -LiteralPath $Path).Path
    }

    $command = Get-Command $Path -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw "dotnet executable was not found. Pass -DotNetPath with the full path."
    }

    return $command.Source
}

function Get-LocalHealthUri {
    param([string]$ConfiguredUrls)

    $firstUrl = ($ConfiguredUrls -split ";")[0]
    if ($firstUrl -match ":(\d+)") {
        return "http://localhost:$($Matches[1])/health"
    }

    return "http://localhost:8080/health"
}

function Test-HealthEndpoint {
    param([string]$Uri)

    for ($i = 1; $i -le 10; $i++) {
        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
                Write-Host "Health check OK: $Uri"
                return
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    throw "Health check failed: $Uri"
}

Assert-Administrator

$resolvedPublishPath = (Resolve-Path -LiteralPath $PublishPath).Path
$dllPath = Join-Path $resolvedPublishPath "Webhook-Inventario.dll"
if (-not (Test-Path -LiteralPath $dllPath)) {
    throw "Webhook-Inventario.dll was not found in $resolvedPublishPath. Run dotnet publish first."
}

$resolvedDotNet = Resolve-DotNetPath -Path $DotNetPath
$binaryPath = "`"$resolvedDotNet`" `"$dllPath`""
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($null -eq $service) {
    if ($PSCmdlet.ShouldProcess("Windows service $ServiceName", "Create")) {
        New-Service -Name $ServiceName -DisplayName $DisplayName -BinaryPathName $binaryPath -StartupType Automatic | Out-Null
    }
}
else {
    if ($service.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
    }

    if ($PSCmdlet.ShouldProcess("Windows service $ServiceName", "Update binary path")) {
        & sc.exe config $ServiceName binPath= $binaryPath start= delayed-auto | Out-Null
    }
}

$serviceRegistryPath = "HKLM:\SYSTEM\CurrentControlSet\Services\$ServiceName"
$environment = @(
    "ASPNETCORE_URLS=$Urls",
    "ASPNETCORE_ENVIRONMENT=Production",
    "DOTNET_ENVIRONMENT=Production",
    "InventoryProcessing__Mode=$InventoryProcessingMode"
)

if ($PSCmdlet.ShouldProcess("Windows service $ServiceName", "Set non-secret environment variables")) {
    New-ItemProperty -Path $serviceRegistryPath -Name Environment -PropertyType MultiString -Value $environment -Force | Out-Null
}

Write-Host "Secret values are not written by this script."
Write-Host "Configure these outside source control before starting production traffic:"
Write-Host "  AutoInventario__ApiKey"
Write-Host "  Security__PrivateKeyPath or WEBHOOK_PRIVATE_KEY_PATH"
Write-Host "  ManageEngine__BaseUrl"
Write-Host "  ManageEngine__ApiTokenSecretName and the matching secret value"
Write-Host "  ConnectionStrings__Postgres, if /id-clients is enabled"

if (-not $SkipStart) {
    Start-Service -Name $ServiceName
}

if (-not $SkipHealthCheck) {
    Test-HealthEndpoint -Uri (Get-LocalHealthUri -ConfiguredUrls $Urls)
}
