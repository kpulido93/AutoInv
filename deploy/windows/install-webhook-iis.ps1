#requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [string]$SiteName = "AutoInventarioWebhook",

    [string]$AppPoolName = "AutoInventarioWebhook",

    [int]$Port = 8080,

    [string]$HostHeader = "",

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

function Enable-IisFeature {
    if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
        Install-WindowsFeature Web-Server, Web-Mgmt-Tools | Out-Null
        return
    }

    if (Get-Command Enable-WindowsOptionalFeature -ErrorAction SilentlyContinue) {
        Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-ManagementConsole -All -NoRestart | Out-Null
    }
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

Enable-IisFeature
Import-Module WebAdministration

if (-not (Get-WebGlobalModule -Name AspNetCoreModuleV2 -ErrorAction SilentlyContinue)) {
    Write-Warning "AspNetCoreModuleV2 was not found. Install the .NET 8 Hosting Bundle before serving ASP.NET Core through IIS."
}

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    if ($PSCmdlet.ShouldProcess("IIS app pool $AppPoolName", "Create")) {
        New-WebAppPool -Name $AppPoolName | Out-Null
    }
}

Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name processModel.identityType -Value "ApplicationPoolIdentity"

$site = Get-Website -Name $SiteName -ErrorAction SilentlyContinue
if ($null -eq $site) {
    if ($PSCmdlet.ShouldProcess("IIS site $SiteName", "Create")) {
        if ([string]::IsNullOrWhiteSpace($HostHeader)) {
            New-Website -Name $SiteName -PhysicalPath $resolvedPublishPath -Port $Port -ApplicationPool $AppPoolName | Out-Null
        }
        else {
            New-Website -Name $SiteName -PhysicalPath $resolvedPublishPath -Port $Port -HostHeader $HostHeader -ApplicationPool $AppPoolName | Out-Null
        }
    }
}
else {
    if ($PSCmdlet.ShouldProcess("IIS site $SiteName", "Update physical path and app pool")) {
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $resolvedPublishPath
        Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    }
}

Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
Start-Website -Name $SiteName -ErrorAction SilentlyContinue

Write-Host "Required configuration must be supplied by environment variables or secure configuration providers:"
Write-Host "  AutoInventario__ApiKey"
Write-Host "  Security__PrivateKeyPath or WEBHOOK_PRIVATE_KEY_PATH"
Write-Host "  InventoryProcessing__Mode"
Write-Host "  ManageEngine__BaseUrl"
Write-Host "  ManageEngine__ApiTokenSecretName and the matching secret value"
Write-Host "  ConnectionStrings__Postgres, if /id-clients is enabled"

if (-not $SkipHealthCheck) {
    Test-HealthEndpoint -Uri "http://localhost:$Port/health"
}
