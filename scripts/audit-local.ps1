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

function Convert-ToRepoRelativePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    return $fullPath.Substring($rootPath.Length + 1).Replace('\', '/')
}

function Get-RepoScope {
    param(
        [Parameter(Mandatory = $true)][string]$RelativePath,
        [Parameter(Mandatory = $true)][hashtable]$Tracked,
        [Parameter(Mandatory = $true)][hashtable]$Ignored
    )

    $normalized = $RelativePath.Replace('\', '/')
    if ($Tracked.ContainsKey($normalized)) {
        return "tracked"
    }
    if ($Ignored.ContainsKey($normalized)) {
        return "ignored"
    }
    return "untracked"
}

function Add-SecretFinding {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Findings,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Type,
        [Parameter(Mandatory = $true)][string]$Scope,
        [Parameter(Mandatory = $true)][string]$Severity
    )

    $key = "$Path|$Type|$Scope|$Severity"
    if (-not $script:SeenSecretFindings.ContainsKey($key)) {
        $script:SeenSecretFindings[$key] = $true
        $Findings.Add([pscustomobject]@{
            Severity = $Severity
            Scope    = $Scope
            Path     = $Path
            Type     = $Type
        }) | Out-Null
    }
}

function Test-PlaceholderValue {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return $true
    }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return $true
    }
    return ($text -match '(?i)(your|example|placeholder|change[_-]?me|localhost|dummy|fake|xxx|test|ejemplo|reemplazar|tu_)')
}

function Add-AppSettingsFindings {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo]$File,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][System.Collections.Generic.List[object]]$Findings,
        [Parameter(Mandatory = $true)][hashtable]$Tracked,
        [Parameter(Mandatory = $true)][hashtable]$Ignored
    )

    try {
        $json = Get-Content -LiteralPath $File.FullName -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return
    }

    $relative = Convert-ToRepoRelativePath $File.FullName
    $scope = Get-RepoScope -RelativePath $relative -Tracked $Tracked -Ignored $Ignored

    function Walk-JsonObject {
        param(
            [AllowNull()][object]$Node,
            [string]$Prefix
        )

        if ($null -eq $Node) {
            return
        }

        foreach ($property in $Node.PSObject.Properties) {
            $name = if ($Prefix) { "$Prefix.$($property.Name)" } else { $property.Name }
            $value = $property.Value

            if ($value -is [System.Management.Automation.PSCustomObject]) {
                Walk-JsonObject -Node $value -Prefix $name
                continue
            }

            if ($name -match '(?i)(password|pwd|secret|token|api.?key|connection|string|private.?key|client.?secret)') {
                if (Test-PlaceholderValue $value) {
                    Add-SecretFinding -Findings $Findings -Path $relative -Type "appsettings sensitive key placeholder-or-empty" -Scope $scope -Severity "Low"
                }
                else {
                    Add-SecretFinding -Findings $Findings -Path $relative -Type "appsettings sensitive key non-empty" -Scope $scope -Severity "High"
                }
            }
        }
    }

    Walk-JsonObject -Node $json -Prefix ""
}

function Invoke-SecretScan {
    Write-Host ""
    Write-Host "## Secret pattern scan"
    Write-Host "No secret values are printed. Generated folders are excluded."

    $excludedDirs = @(".git", "bin", "obj", ".vs", "node_modules", "packages", "TestResults", "__pycache__")
    $excludedFiles = @(".gitleaks.toml", "docs/AUDIT-CURRENT.md", "docs/SECURITY-FINDINGS.md", "scripts/audit-local.ps1", "scripts/audit-local.sh")
    $binaryExtensions = @(".dll", ".exe", ".pdb", ".ico", ".zip", ".png", ".jpg", ".jpeg", ".gif", ".so", ".pyc", ".nupkg")

    $tracked = @{}
    foreach ($item in (& git ls-files 2>$null)) {
        $tracked[$item.Replace('\', '/')] = $true
    }

    $ignored = @{}
    foreach ($item in (& git ls-files --others --ignored --exclude-standard 2>$null)) {
        $ignored[$item.Replace('\', '/')] = $true
    }

    $files = Get-ChildItem -LiteralPath $Root -Recurse -Force -File | Where-Object {
        $relative = Convert-ToRepoRelativePath $_.FullName
        $parts = $relative -split '/'
        -not ($parts | Where-Object { $excludedDirs -contains $_ }) -and
            $excludedFiles -notcontains $relative -and
            $_.Length -lt 5MB
    }

    $findings = New-Object System.Collections.Generic.List[object]
    $script:SeenSecretFindings = @{}

    foreach ($file in $files) {
        $relative = Convert-ToRepoRelativePath $file.FullName
        $scope = Get-RepoScope -RelativePath $relative -Tracked $tracked -Ignored $ignored
        $name = $file.Name

        switch -Regex ($name) {
            '(?i)^private\.key$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "private key filename" -Scope $scope -Severity "Critical"
            }
            '(?i)^secrets\.json$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "secrets json file" -Scope $scope -Severity "Critical"
            }
            '(?i)^secrets\.tf$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "terraform secrets file" -Scope $scope -Severity "Medium"
            }
            '(?i)\.tfvars$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "terraform tfvars file" -Scope $scope -Severity "High"
            }
            '(?i)^terraform\.tfstate(\.backup)?$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "terraform state file" -Scope $scope -Severity "High"
            }
            '(?i)^appsettings(\..*)?\.json$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "appsettings file" -Scope $scope -Severity "Medium"
                Add-AppSettingsFindings -File $file -Findings $findings -Tracked $tracked -Ignored $ignored
            }
            '(?i)\.(pfx|p12|pem|cer|crt)$' {
                Add-SecretFinding -Findings $findings -Path $relative -Type "certificate/key container" -Scope $scope -Severity "High"
            }
        }
    }

    $patterns = @(
        [pscustomobject]@{ Type = "private key material"; Severity = "Critical"; Pattern = '-----BEGIN [A-Z ]*PRIVATE KEY-----' },
        [pscustomobject]@{ Type = "AWS access key id"; Severity = "Critical"; Pattern = '\b(AKIA|ASIA)[0-9A-Z]{16}\b' },
        [pscustomobject]@{ Type = "AWS credential assignment/reference"; Severity = "High"; Pattern = '(?i)(aws_access_key_id|aws_secret_access_key|BasicAWSCredentials|SessionAWSCredentials)' },
        [pscustomobject]@{ Type = "ManageEngine/API token assignment/reference"; Severity = "High"; Pattern = '(?i)(manageengine_api_key|manageengine.*token|api[_-]?key\s*[:=]|access[_-]?token\s*[:=]|authtoken\s*[:=])' },
        [pscustomobject]@{ Type = "password/secret assignment/reference"; Severity = "Medium"; Pattern = '(?i)(password|passwd|pwd|secret)\s*[:=]' },
        [pscustomobject]@{ Type = "connection string credential marker"; Severity = "Medium"; Pattern = '(?i)(connectionstring|server=.*;.*(password|pwd)=)' }
    )

    $textFiles = $files | Where-Object { $binaryExtensions -notcontains $_.Extension.ToLowerInvariant() }
    foreach ($pattern in $patterns) {
        foreach ($match in (Select-String -Path $textFiles.FullName -Pattern $pattern.Pattern -AllMatches -ErrorAction SilentlyContinue)) {
            $relative = Convert-ToRepoRelativePath $match.Path
            $scope = Get-RepoScope -RelativePath $relative -Tracked $tracked -Ignored $ignored
            Add-SecretFinding -Findings $findings -Path $relative -Type $pattern.Type -Scope $scope -Severity $pattern.Severity
        }
    }

    $sortedFindings = $findings |
        Sort-Object @{ Expression = { @("Critical", "High", "Medium", "Low").IndexOf($_.Severity) } }, Scope, Path, Type

    if ($sortedFindings.Count -eq 0) {
        Write-Host "No secret indicators found."
        return
    }

    Write-Output "Severity`tScope`tPath`tType"
    foreach ($finding in $sortedFindings) {
        Write-Output ("{0}`t{1}`t{2}`t{3}" -f $finding.Severity, $finding.Scope, $finding.Path, $finding.Type)
    }
}

Write-Host "AutoInventario local audit"
Write-Host ("Root: {0}" -f $Root)

Invoke-AuditCommand "Git status" @("git", "status", "--short", "--branch")
Invoke-AuditCommand ".NET solution build" @("dotnet", "build", "AutoInventario.sln", "-c", "Debug")
Invoke-AuditCommand ".NET agent build" @("dotnet", "build", "AutoInventario.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET updater build" @("dotnet", "build", "AutoInventario.Updater/AutoInventario.Updater.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET webhook build" @("dotnet", "build", "Webhook/Webhook-Inventario.csproj", "-c", "Debug")
Invoke-AuditCommand ".NET tests" @("dotnet", "test", "AutoInventario.Tests/AutoInventario.Tests.csproj", "-c", "Debug")
Invoke-AuditCommand "Python syntax check" @("python", "-m", "py_compile", "Lambda-Inventario/lambda_function.py")
Invoke-AuditCommand "Terraform fmt check" @("terraform", "-chdir=Infraestructura-Terraform", "fmt", "-check", "-recursive")
Invoke-AuditCommand "Terraform validate" @("terraform", "-chdir=Infraestructura-Terraform", "validate", "-no-color")
Invoke-AuditCommand "NuGet vulnerable packages - agent" @("dotnet", "list", "AutoInventario.csproj", "package", "--vulnerable", "--include-transitive")
Invoke-AuditCommand "NuGet vulnerable packages - webhook" @("dotnet", "list", "Webhook/Webhook-Inventario.csproj", "package", "--vulnerable", "--include-transitive")
Invoke-SecretScan

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
