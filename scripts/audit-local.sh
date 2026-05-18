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
  printf '\n## Secret scan\n'
  printf 'No secret values are printed.\n'

  if command -v gitleaks >/dev/null 2>&1; then
    set +e
    gitleaks detect --source "$ROOT" --config "$ROOT/.gitleaks.toml" --redact --no-git --exit-code 1 2>&1 | redact_stream
    local status=${PIPESTATUS[0]}
    set -e
    printf 'ExitCode: %s\n' "$status"
    if [[ "$status" -ne 0 ]]; then
      FAILURES+=("gitleaks detect --source \"$ROOT\" --config \"$ROOT/.gitleaks.toml\" --redact --no-git --exit-code 1")
    fi
    return
  fi

  printf 'gitleaks is not installed. Running CI-aligned fallback secret scan.\n'

  set +e
  python - "$ROOT" <<'PY' | redact_stream
import os
import re
import subprocess
import sys

root = os.path.abspath(sys.argv[1])
excluded_prefixes = (
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
    "AutoInventario.Updater/obj/",
)
excluded_files = {
    ".gitleaks.toml",
    "docs/AUDIT.md",
    "docs/AUDIT-CURRENT.md",
    "docs/SECURITY-FINDINGS.md",
}
binary_extensions = {
    ".dll", ".exe", ".pdb", ".ico", ".zip", ".png", ".jpg", ".jpeg", ".gif",
    ".so", ".pyc", ".nupkg", ".snupkg",
}
allowed_line = re.compile(
    r"(?i)(<[^>\r\n]+>|CHANGE_ME(_[A-Z0-9]+)*|change-me|replace|set-with-secret-variable|"
    r"set-via-(environment|user-secrets|secret-manager)|placeholder|example|dummy|fake|"
    r"test-secret|Environment\.GetEnvironmentVariable|\$\([^)]+\)|\$\{[^}]+\})"
)
patterns = [
    ("private key material", re.compile(r"-----BEGIN [A-Z ]*PRIVATE KEY-----")),
    ("AWS access key id", re.compile(r"\b(AKIA|ASIA)[0-9A-Z]{16}\b")),
    ("AWS credential assignment", re.compile(r"(?i)(aws_access_key_id|aws_secret_access_key)\s*[:=]\s*[\"']?([A-Za-z0-9/+=]{16,})")),
    ("AWS credential constructor", re.compile(r"(?i)new\s+(BasicAWSCredentials|SessionAWSCredentials)\s*\(")),
    ("ManageEngine/API token assignment", re.compile(r"(?i)(manageengine[_-]?(api[_-]?key|token)|authtoken)\s*[:=]\s*[\"']?([A-Za-z0-9_./+=-]{16,})")),
    ("generic secret assignment", re.compile(r"(?i)(api[_-]?key|access[_-]?token|client[_-]?secret|password|passwd|pwd|secret)\s*[:=]\s*[\"']?([A-Za-z0-9_./+=-]{16,})")),
    ("connection string password", re.compile(r"(?i)(Host|Server|Data Source)=.+;(Password|Pwd)=([^;}\r\n]{8,})")),
]

output = subprocess.check_output(
    ["git", "ls-files", "--cached", "--others", "--exclude-standard"],
    cwd=root,
    text=True,
    stderr=subprocess.DEVNULL,
)
findings = set()
for rel in output.splitlines():
    rel = rel.replace("\\", "/")
    if not rel or rel in excluded_files or rel.startswith(excluded_prefixes):
        continue

    full_path = os.path.join(root, rel)
    if not os.path.isfile(full_path):
        continue

    _, ext = os.path.splitext(full_path)
    if ext.lower() in binary_extensions:
        continue

    try:
        with open(full_path, "r", encoding="utf-8", errors="ignore") as handle:
            lines = handle.readlines()
    except OSError:
        continue

    for line in lines:
        for finding_type, pattern in patterns:
            if not pattern.search(line):
                continue
            if rel == "Webhook.Tests/DecryptionServiceTests.cs" and finding_type == "private key material":
                continue
            if allowed_line.search(line):
                continue
            findings.add((rel, finding_type))
            break

if findings:
    print("Secret indicators found:")
    for rel, finding_type in sorted(findings):
        print(f"- {rel}: {finding_type}")
    raise SystemExit(1)

print("No secret indicators found.")
PY
  local status=${PIPESTATUS[0]}
  set -e
  printf 'ExitCode: %s\n' "$status"
  if [[ "$status" -ne 0 ]]; then
    FAILURES+=("fallback secret scan")
  fi
}

printf 'AutoInventario local audit\n'
printf 'Root: %s\n' "$ROOT"

run_cmd "Git status" git status --short --branch
run_cmd ".NET solution build" dotnet build AutoInventario.sln -c Debug
run_cmd ".NET agent build" dotnet build AutoInventario.csproj -c Debug
run_cmd ".NET updater build" dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c Debug
run_cmd ".NET webhook build" dotnet build Webhook/Webhook-Inventario.csproj -c Debug
run_cmd ".NET agent tests" dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj -c Debug
run_cmd ".NET webhook tests" dotnet test Webhook.Tests/Webhook.Tests.csproj -c Debug
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
