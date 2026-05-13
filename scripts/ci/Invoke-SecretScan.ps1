[CmdletBinding()]
param(
    [string]$Root,
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $Root ".gitleaks.toml"
}

Set-Location -LiteralPath $Root

$gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
if ($gitleaks) {
    & $gitleaks.Source detect --source $Root --config $ConfigPath --redact --no-git --exit-code 1
    exit $LASTEXITCODE
}

Write-Host "gitleaks is not installed. Running CI fallback secret scan."
Write-Host "No secret values are printed."

$excludedPathPrefixes = @(
    ".git/",
    ".vs/",
    "bin/",
    "obj/",
    "AutoInventario.Tests/bin/",
    "AutoInventario.Tests/obj/",
    "Webhook.Tests/bin/",
    "Webhook.Tests/obj/",
    "Webhook/bin/",
    "Webhook/obj/",
    "AutoInventario.Updater/bin/",
    "AutoInventario.Updater/obj/"
)

$excludedFiles = @(
    ".gitleaks.toml",
    "docs/AUDIT.md",
    "docs/AUDIT-CURRENT.md",
    "docs/SECURITY-FINDINGS.md"
)

$binaryExtensions = @(
    ".dll", ".exe", ".pdb", ".ico", ".zip", ".png", ".jpg", ".jpeg", ".gif",
    ".so", ".pyc", ".nupkg", ".snupkg"
)

$allowedLinePattern = "(?i)(<[^>\r\n]+>|CHANGE_ME|change-me|replace|set-with-secret-variable|set-via-(environment|user-secrets|secret-manager)|placeholder|example|dummy|fake|test-secret|Environment\.GetEnvironmentVariable|\$\([^)]+\)|\$\{[^}]+\})"

$patterns = @(
    [pscustomobject]@{
        Type    = "private key material"
        Pattern = "-----BEGIN [A-Z ]*PRIVATE KEY-----"
    },
    [pscustomobject]@{
        Type    = "AWS access key id"
        Pattern = "\b(AKIA|ASIA)[0-9A-Z]{16}\b"
    },
    [pscustomobject]@{
        Type    = "AWS credential assignment"
        Pattern = "(?i)(aws_access_key_id|aws_secret_access_key)\s*[:=]\s*[""']?([A-Za-z0-9/+=]{16,})"
    },
    [pscustomobject]@{
        Type    = "AWS credential constructor"
        Pattern = "(?i)new\s+(BasicAWSCredentials|SessionAWSCredentials)\s*\("
    },
    [pscustomobject]@{
        Type    = "ManageEngine/API token assignment"
        Pattern = "(?i)(manageengine[_-]?(api[_-]?key|token)|authtoken)\s*[:=]\s*[""']?([A-Za-z0-9_./+=-]{16,})"
    },
    [pscustomobject]@{
        Type    = "generic secret assignment"
        Pattern = "(?i)(api[_-]?key|access[_-]?token|client[_-]?secret|password|passwd|pwd|secret)\s*[:=]\s*[""']?([A-Za-z0-9_./+=-]{16,})"
    },
    [pscustomobject]@{
        Type    = "connection string password"
        Pattern = "(?i)(Host|Server|Data Source)=.+;(Password|Pwd)=([^;}\r\n]{8,})"
    }
)

$trackedFiles = & git ls-files --cached --others --exclude-standard
$findings = New-Object System.Collections.Generic.List[object]

foreach ($path in $trackedFiles) {
    $normalized = $path.Replace("\", "/")
    if ($excludedFiles -contains $normalized) {
        continue
    }

    $isExcluded = $false
    foreach ($prefix in $excludedPathPrefixes) {
        if ($normalized.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $isExcluded = $true
            break
        }
    }
    if ($isExcluded) {
        continue
    }

    $fullPath = Join-Path $Root $path
    if (-not (Test-Path -LiteralPath $fullPath)) {
        continue
    }

    $extension = [System.IO.Path]::GetExtension($fullPath).ToLowerInvariant()
    if ($binaryExtensions -contains $extension) {
        continue
    }

    $lines = Get-Content -LiteralPath $fullPath -ErrorAction SilentlyContinue
    if ($null -eq $lines) {
        continue
    }

    foreach ($pattern in $patterns) {
        foreach ($line in $lines) {
            if ($line -notmatch $pattern.Pattern) {
                continue
            }

            if ($normalized -eq "Webhook.Tests/DecryptionServiceTests.cs" -and
                $pattern.Type -eq "private key material") {
                continue
            }

            if ($line -match $allowedLinePattern) {
                continue
            }

            $findings.Add([pscustomobject]@{
                Path = $normalized
                Type = $pattern.Type
            }) | Out-Null
            break
        }
    }
}

if ($findings.Count -gt 0) {
    Write-Host "Secret indicators found:"
    foreach ($finding in ($findings | Sort-Object Path, Type -Unique)) {
        Write-Host ("- {0}: {1}" -f $finding.Path, $finding.Type)
    }
    exit 1
}

Write-Host "No secret indicators found."
exit 0
