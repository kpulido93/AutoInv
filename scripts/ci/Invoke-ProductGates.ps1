[CmdletBinding()]
param(
    [string]$Root,
    [string]$BuildConfiguration = "Release",
    [string]$TestResultsDirectory,
    [switch]$RunTerraformValidate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($TestResultsDirectory)) {
    $TestResultsDirectory = Join-Path $Root "TestResults"
}

Set-Location -LiteralPath $Root

function Invoke-Gate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$ScriptBlock
    )

    Write-Host ""
    Write-Host "## $Name"
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Force -Path $TestResultsDirectory | Out-Null

Invoke-Gate "Restore solution" {
    dotnet restore AutoInventario.sln
}

Invoke-Gate "Build solution" {
    dotnet build AutoInventario.sln -c $BuildConfiguration --no-restore
}

Invoke-Gate "Build agent" {
    dotnet build AutoInventario.csproj -c $BuildConfiguration --no-restore
}

Invoke-Gate "Build updater" {
    dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c $BuildConfiguration --no-restore
}

Invoke-Gate "Build webhook" {
    dotnet build Webhook/Webhook-Inventario.csproj -c $BuildConfiguration --no-restore
}

Invoke-Gate "Test agent" {
    dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj `
        -c $BuildConfiguration `
        --no-build `
        --logger "trx;LogFileName=agent-tests.trx" `
        --results-directory $TestResultsDirectory
}

Invoke-Gate "Test webhook" {
    dotnet test Webhook.Tests/Webhook.Tests.csproj `
        -c $BuildConfiguration `
        --no-build `
        --logger "trx;LogFileName=webhook-tests.trx" `
        --results-directory $TestResultsDirectory
}

Invoke-Gate "Python Lambda syntax" {
    python -m py_compile Lambda-Inventario/lambda_function.py
}

Invoke-Gate "Terraform format" {
    terraform -chdir=Infraestructura-Terraform fmt -check -recursive
}

if ($RunTerraformValidate) {
    Invoke-Gate "Terraform init" {
        terraform -chdir=Infraestructura-Terraform init -backend=false -input=false -no-color
    }

    Invoke-Gate "Terraform validate" {
        terraform -chdir=Infraestructura-Terraform validate -no-color
    }
}
