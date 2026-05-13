#!/usr/bin/env bash
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

FAILURES=()

redact_stream() {
  sed -E \
    -e 's/(AKIA|ASIA)[0-9A-Z]{16}/[REDACTED AWS ACCESS KEY ID]/g' \
    -e 's/((password|passwd|pwd|secret|token|api[_-]?key|client[_-]?secret)[[:space:]]*[:=][[:space:]]*)["'\''"]?[^"'\''",;[:space:]}]+/\1[REDACTED]/Ig'
}

run_cmd() {
  local title="$1"
  shift

  printf '\n## %s\n' "$title"
  printf 'Command:'
  printf ' %q' "$@"
  printf '\n'

  set +e
  "$@" 2>&1 | redact_stream
  local status=${PIPESTATUS[0]}
  set -e

  printf 'ExitCode: %s\n' "$status"
  if [[ "$status" -ne 0 ]]; then
    FAILURES+=("$*")
  fi
}

repo_scope() {
  local path="${1#./}"
  if git ls-files --error-unmatch "$path" >/dev/null 2>&1; then
    printf 'tracked'
  elif git check-ignore -q "$path" 2>/dev/null; then
    printf 'ignored'
  else
    printf 'untracked'
  fi
}

add_finding() {
  local severity="$1"
  local scope="$2"
  local path="$3"
  local type="$4"
  printf '%s\t%s\t%s\t%s\n' "$severity" "$scope" "${path#./}" "$type"
}

secret_scan() {
  printf '\n## Secret pattern scan\n'
  printf 'No secret values are printed. Generated folders are excluded.\n'

  local tmp
  tmp="$(mktemp)"

  mapfile -t files < <(
    find . \
      \( -path './.git' -o -path './bin' -o -path './obj' -o -path './.vs' -o -path './node_modules' -o -path './packages' -o -path '*/bin/*' -o -path '*/obj/*' -o -path '*/TestResults/*' -o -path '*/__pycache__/*' \) -prune \
      -o -type f -size -5M -print
  )

  local filtered=()
  local rel
  for file in "${files[@]}"; do
    rel="${file#./}"
    case "$rel" in
      .gitleaks.toml|docs/AUDIT-CURRENT.md|docs/SECURITY-FINDINGS.md|scripts/audit-local.ps1|scripts/audit-local.sh) continue ;;
    esac
    filtered+=("$file")
  done
  files=("${filtered[@]}")

  for file in "${files[@]}"; do
    base="$(basename "$file")"
    scope="$(repo_scope "$file")"
    case "$base" in
      private.key) add_finding "Critical" "$scope" "$file" "private key filename" >> "$tmp" ;;
      secrets.json) add_finding "Critical" "$scope" "$file" "secrets json file" >> "$tmp" ;;
      secrets.tf) add_finding "Medium" "$scope" "$file" "terraform secrets file" >> "$tmp" ;;
      terraform.tfstate|terraform.tfstate.backup) add_finding "High" "$scope" "$file" "terraform state file" >> "$tmp" ;;
      appsettings*.json) add_finding "Medium" "$scope" "$file" "appsettings file" >> "$tmp" ;;
      *.tfvars) add_finding "High" "$scope" "$file" "terraform tfvars file" >> "$tmp" ;;
      *.pfx|*.p12|*.pem|*.cer|*.crt) add_finding "High" "$scope" "$file" "certificate/key container" >> "$tmp" ;;
    esac
  done

  scan_pattern() {
    local severity="$1"
    local type="$2"
    local pattern="$3"
    local file
    for file in "${files[@]}"; do
      case "$file" in
        *.dll|*.exe|*.pdb|*.ico|*.zip|*.png|*.jpg|*.jpeg|*.gif|*.so|*.pyc|*.nupkg) continue ;;
      esac
      if grep -IEqli "$pattern" "$file" 2>/dev/null; then
        add_finding "$severity" "$(repo_scope "$file")" "$file" "$type" >> "$tmp"
      fi
    done
  }

  scan_pattern "Critical" "private key material" 'BEGIN [A-Z ]*PRIVATE KEY'
  scan_pattern "Critical" "AWS access key id" '\b(AKIA|ASIA)[0-9A-Z]{16}\b'
  scan_pattern "High" "AWS credential assignment/reference" '(aws_access_key_id|aws_secret_access_key|BasicAWSCredentials|SessionAWSCredentials)'
  scan_pattern "High" "ManageEngine/API token assignment/reference" '(manageengine_api_key|manageengine.*token|api[_-]?key[[:space:]]*[:=]|access[_-]?token[[:space:]]*[:=]|authtoken[[:space:]]*[:=])'
  scan_pattern "Medium" "password/secret assignment/reference" '(password|passwd|pwd|secret)[[:space:]]*[:=]'
  scan_pattern "Medium" "connection string credential marker" '(connectionstring|server=.*;.*(password|pwd)=)'

  python - "$tmp" "${files[@]}" <<'PY'
import json
import os
import re
import subprocess
import sys

out_path = sys.argv[1]
files = sys.argv[2:]
sensitive = re.compile(r"(password|pwd|secret|token|api.?key|connection|string|private.?key|client.?secret)", re.I)
placeholder = re.compile(r"(your|example|placeholder|change[_-]?me|localhost|dummy|fake|xxx|test|ejemplo|reemplazar|tu_)", re.I)

def scope(path):
    rel = path[2:] if path.startswith("./") else path
    if subprocess.run(["git", "ls-files", "--error-unmatch", rel], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
        return "tracked"
    if subprocess.run(["git", "check-ignore", "-q", rel], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
        return "ignored"
    return "untracked"

def walk(node, prefix, rel, sink):
    if isinstance(node, dict):
        for key, value in node.items():
            name = f"{prefix}.{key}" if prefix else key
            if isinstance(value, dict):
                walk(value, name, rel, sink)
            elif sensitive.search(name):
                text = "" if value is None else str(value)
                if text.strip() and not placeholder.search(text):
                    sev = "High"
                    kind = "appsettings sensitive key non-empty"
                else:
                    sev = "Low"
                    kind = "appsettings sensitive key placeholder-or-empty"
                sink.write(f"{sev}\t{scope(rel)}\t{rel}\t{kind}\n")

with open(out_path, "a", encoding="utf-8") as sink:
    for path in files:
        rel = path[2:] if path.startswith("./") else path
        if not os.path.basename(path).lower().startswith("appsettings") or not path.lower().endswith(".json"):
            continue
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                data = json.load(handle)
        except Exception:
            continue
        walk(data, "", rel, sink)
PY

  if [[ -s "$tmp" ]]; then
    sort -u "$tmp" | awk 'BEGIN { FS="\t"; printf "%-9s %-8s %-70s %s\n", "Severity", "Scope", "Path", "Type" } { printf "%-9s %-8s %-70s %s\n", $1, $2, $3, $4 }'
  else
    printf 'No secret indicators found.\n'
  fi

  rm -f "$tmp"
}

printf 'AutoInventario local audit\n'
printf 'Root: %s\n' "$ROOT"

run_cmd "Git status" git status --short --branch
run_cmd ".NET solution build" dotnet build AutoInventario.sln -c Debug
run_cmd ".NET agent build" dotnet build AutoInventario.csproj -c Debug
run_cmd ".NET updater build" dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c Debug
run_cmd ".NET webhook build" dotnet build Webhook/Webhook-Inventario.csproj -c Debug
run_cmd ".NET tests" dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj -c Debug
run_cmd "Python syntax check" python -m py_compile Lambda-Inventario/lambda_function.py
run_cmd "Terraform fmt check" terraform -chdir=Infraestructura-Terraform fmt -check -recursive
run_cmd "Terraform validate" terraform -chdir=Infraestructura-Terraform validate -no-color
run_cmd "NuGet vulnerable packages - agent" dotnet list AutoInventario.csproj package --vulnerable --include-transitive
run_cmd "NuGet vulnerable packages - webhook" dotnet list Webhook/Webhook-Inventario.csproj package --vulnerable --include-transitive
secret_scan

printf '\n## Summary\n'
if [[ "${#FAILURES[@]}" -gt 0 ]]; then
  printf 'Failed commands:\n'
  for failure in "${FAILURES[@]}"; do
    printf -- '- %s\n' "$failure"
  done
  exit 1
fi

printf 'All audit commands completed successfully.\n'
exit 0
