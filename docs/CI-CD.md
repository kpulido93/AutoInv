# CI/CD

AutoInventario usa Azure Pipelines como puerta de producto. El pipeline principal falla ante builds rotos, tests fallidos, indicadores de secretos, vulnerabilidades NuGet criticas y Terraform sin formato.

## Pipeline principal

Archivo: `azure-pipelines.yml`.

Stages:

- `ProductGates`: ejecuta escaneo de secretos, escaneo NuGet vulnerable, build de solucion, build de agente/updater/webhook, tests .NET, compilacion Python de Lambda y `terraform fmt -check`.
- `Package`: solo para ramas no PR. Publica agente, updater y Webhook, firma binarios con certificado de Secure Files, genera hashes SHA256 y crea `wwwroot/updates/latest.json`.

El stage `Package` depende de `ProductGates`; no publica artefactos si un gate falla.

## Scripts CI

Los gates viven en `scripts/ci/` para que puedan ejecutarse fuera de Azure Pipelines:

```powershell
pwsh ./scripts/ci/Invoke-SecretScan.ps1
pwsh ./scripts/ci/Test-NuGetVulnerabilities.ps1 -MinimumSeverity Critical
pwsh ./scripts/ci/Invoke-ProductGates.ps1 -BuildConfiguration Release
```

`Invoke-SecretScan.ps1` usa `gitleaks detect --redact` si `gitleaks` esta instalado. Si no esta disponible, usa un escaneo local equivalente por patrones y reporta solo ruta y tipo de hallazgo.

## Variables requeridas

Configurar en Azure DevOps Library, sin valores en YAML:

| Nombre | Tipo | Uso |
| --- | --- | --- |
| `VG_PfxPassword` | Variable group | Grupo referenciado por el job de empaquetado. |
| `PfxPassword` | Secret variable | Password del PFX de firma. Se expone solo como `PFX_PASSWORD` al script de firma. |
| `SigningCertificateSecureFile` | Variable | Nombre del Secure File PFX autorizado para el pipeline. |
| `baseURL` | Variable | URL publica base para `latest.json`, por ejemplo el host del Webhook. |
| `TimestampUrl` | Variable opcional | TSA para firma Authenticode. Por defecto usa `http://timestamp.sectigo.com`. |
| `NuGetVulnerabilityThreshold` | Variable opcional | Umbral que falla el gate NuGet. Valor esperado: `Critical`. |

El certificado PFX debe estar cargado en Azure DevOps Secure Files y autorizado para este pipeline. No almacenar PFX, passwords ni certificados privados en el repositorio.

## Artefactos

El pipeline publica tres artefactos:

- `agent`: salida `dotnet publish` del agente Windows, con `AutoInventario.exe.sha256` y `SHA256SUMS.txt`.
- `updater`: salida `dotnet publish` del updater, con `AutoInventario.Updater.exe.sha256` y `SHA256SUMS.txt`.
- `webhook`: sitio ASP.NET Core publicado, `wwwroot/updates/latest.json`, binarios firmados de actualizacion y `SHA256SUMS.txt`.

Los hashes se calculan despues de firmar los ejecutables.

## Pipeline Terraform

Archivo: `.azure-pipelines/pipeline.yml`.

Este pipeline separado valida Terraform con `fmt`, `init -backend=false` y `validate`. Para ejecuciones no PR empaqueta la Lambda en `Infraestructura-Terraform/lambda_package.zip`, crea un plan y solo aplica en `main` tras aprobacion manual.

Variables AWS requeridas para plan/apply deben configurarse como secret variables o mediante una service connection equivalente:

- `AWS_ACCESS_KEY_ID`
- `AWS_SECRET_ACCESS_KEY`
- `AWS_SESSION_TOKEN` si se usa credencial temporal

No usar `*.tfvars` versionados con valores reales. Si se requieren variables Terraform por entorno, pasarlas como variables secretas `TF_VAR_*` o mediante el mecanismo seguro del entorno de despliegue. El lock file `.terraform.lock.hcl` se versiona para fijar providers; los directorios `.terraform/`, planes, estados y ZIPs generados no se versionan.

## Validacion operativa

Antes de considerar una ejecucion apta para release:

- Confirmar que `ProductGates` esta en verde en rama `DEV`/`dev`.
- Revisar que los logs no imprimen API keys, PFX password, tokens, payloads cifrados completos ni connection strings reales.
- Descargar artefactos y verificar `SHA256SUMS.txt`.
- Confirmar que `latest.json` apunta al `baseURL` correcto y que sus hashes coinciden con los binarios firmados.
