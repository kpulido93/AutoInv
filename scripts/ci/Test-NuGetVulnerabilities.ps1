[CmdletBinding()]
param(
    [string]$Root,
    [string[]]$Projects,
    [ValidateSet("Low", "Moderate", "High", "Critical")]
    [string]$MinimumSeverity = "Critical"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if (-not $Projects -or $Projects.Count -eq 0) {
    $Projects = @(
        "AutoInventario.csproj",
        "AutoInventario.Updater/AutoInventario.Updater.csproj",
        "Webhook/Webhook-Inventario.csproj",
        "AutoInventario.Tests/AutoInventario.Tests.csproj",
        "Webhook.Tests/Webhook.Tests.csproj"
    )
}

Set-Location -LiteralPath $Root

$severityRank = @{
    Low      = 0
    Baja     = 0
    Moderate = 1
    Moderada = 1
    Medium   = 1
    Media    = 1
    High     = 2
    Alta     = 2
    Critical = 3
    Critica  = 3
    Crítica  = 3
}

$threshold = $severityRank[$MinimumSeverity]
$violations = New-Object System.Collections.Generic.List[object]

foreach ($project in $Projects) {
    Write-Host ""
    Write-Host "## NuGet vulnerability scan: $project"
    $output = & dotnet list $project package --vulnerable --include-transitive 2>&1
    $exitCode = $LASTEXITCODE

    foreach ($line in $output) {
        Write-Output $line

        foreach ($severity in $severityRank.Keys) {
            if ($line -match "\b$([regex]::Escape($severity))\b" -and
                $severityRank[$severity] -ge $threshold) {
                $violations.Add([pscustomobject]@{
                    Project  = $project
                    Severity = $severity
                    Line     = $line
                }) | Out-Null
            }
        }
    }

    if ($exitCode -ne 0) {
        throw "dotnet list package failed for $project with exit code $exitCode."
    }
}

if ($violations.Count -gt 0) {
    Write-Host ""
    Write-Host "NuGet vulnerabilities at or above threshold '$MinimumSeverity' were found:"
    foreach ($violation in ($violations | Sort-Object Project, Severity -Unique)) {
        Write-Host ("- {0}: {1}" -f $violation.Project, $violation.Severity)
    }
    exit 1
}

Write-Host ""
Write-Host "No NuGet vulnerabilities at or above threshold '$MinimumSeverity' were found."
exit 0
