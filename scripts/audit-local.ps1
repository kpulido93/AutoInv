[CmdletBinding()]
param(
    [string]$Root
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

Set-Location -LiteralPath $Root

$script:Failures = New-Object System.Collections.Generic.List[string]

function Redact-Text {
    param([AllowNull()][object]$InputObject)

    if ($InputObject -is [System.Management.Automation.ErrorRecord]) {
        $text = $InputObject.Exception.Message
    }
    else {
        $text = [string]$InputObject
    }
    $text = $text -replace '-----BEGIN [^-]+PRIVATE KEY-----[\s\S]*?-----END [^-]+PRIVATE KEY-----', '[REDACTED PRIVATE KEY]'
    $text = $text -replace '\b(AKIA|ASIA)[0-9A-Z]{16}\b', '[REDACTED AWS ACCESS KEY ID]'
    $text = $text -replace '(?i)((password|passwd|pwd|secret|token|api[_-]?key|client[_-]?secret)\s*[:=]\s*)["'']?[^"'',;\s}]+', '$1[REDACTED]'
    return $text
}

function Invoke-AuditCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string[]]$Command
    )

    Write-Host ""
    Write-Host "## $Title"
    Write-Host ("Command: {0}" -f ($Command -join " "))

    $exitCode = 0
    try {
        $output = & $Command[0] @($Command | Select-Object -Skip 1) 2>&1
        if ($null -ne $LASTEXITCODE) {
            $exitCode = [int]$LASTEXITCODE
        }
    }
    catch {
        $output = @($_)
        $exitCode = 127
    }

    foreach ($line in $output) {
        Write-Output (Redact-Text $line)
    }

    Write-Host ("ExitCode: {0}" -f $exitCode)
    if ($exitCode -ne 0) {
        $script:Failures.Add(($Command -join " ")) | Out-Null
    }
}

Write-Host "AutoInventario local audit"
Write-Host ("Root: {0}" -f $Root)

Invoke-AuditCommand "Git status" @("git", "status", "--short", "--branch")
Invoke-AuditCommand ".NET solution build" @("dotnet", "build", "AutoInventario.sln", "-c", "Debug")
Invoke-AuditCommand ".NET agent build" @("dotnet", "build", "AutoInventario.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET updater build" @("dotnet", "build", "AutoInventario.Updater/AutoInventario.Updater.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET webhook build" @("dotnet", "build", "Webhook/Webhook-Inventario.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET agent tests" @("dotnet", "test", "AutoInventario.Tests/AutoInventario.Tests.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET webhook tests" @("dotnet", "test", "Webhook.Tests/Webhook.Tests.csproj", "-c", "Debug")
Invoke-AuditCommand "Python syntax check" @("python", "-m", "py_compile", "Lambda-Inventario/lambda_function.py")
Invoke-AuditCommand "Terraform fmt check" @("terraform", "-chdir=Infraestructura-Terraform", "fmt", "-check", "-recursive")
Invoke-AuditCommand "Terraform validate" @("terraform", "-chdir=Infraestructura-Terraform", "validate", "-no-color")
Invoke-AuditCommand "NuGet vulnerable packages - agent" @("dotnet", "list", "AutoInventario.csproj", "package", "--vulnerable", "--include-transitive")
Invoke-AuditCommand "NuGet vulnerable packages - webhook" @("dotnet", "list", "Webhook/Webhook-Inventario.csproj", "package", "--vulnerable", "--include-transitive")
Invoke-AuditCommand "Secret scan" @("powershell", "-ExecutionPolicy", "Bypass", "-File", "scripts/ci/Invoke-SecretScan.ps1", "-Root", $Root)

Write-Host ""
Write-Host "## Summary"
if ($script:Failures.Count -gt 0) {
    Write-Host "Failed commands:"
    foreach ($failure in $script:Failures) {
        Write-Host ("- {0}" -f $failure)
    }
    exit 1
}

Write-Host "All audit commands completed successfully."
exit 0
