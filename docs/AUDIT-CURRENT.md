# Auditoria reproducible del estado actual

Fecha: 2026-05-18
Rama observada: `dev`
Commit observado: `80a0ba2`
Alcance: auditoria local reproducible de build, tests, Lambda, Terraform, dependencias, seguridad y gates de pipeline. No se modifico codigo productivo en este commit.

## Estado Git inicial

Comando ejecutado:

```powershell
git status --short --branch
```

Resultado:

```text
## dev...github/DEV
```

## Entorno observado

| Herramienta | Version observada | Nota |
| --- | --- | --- |
| .NET SDK | `9.0.314` | Builds `Debug` ejecutados localmente. |
| Python | `Python 3.12.4` | `py_compile` correcto. |
| Terraform | `v1.14.2 on windows_386` | `fmt` correcto; `validate` bloqueado por bootstrap de provider/red. |
| Bash | `C:\Program Files\Git\bin\bash.exe` | `C:\Windows\System32\bash.exe` apunta a WSL sin distro utilizable en este host. |

## Build y tests

| Comando | Resultado | Error clave si falla | Impacto | Siguiente accion recomendada |
| --- | --- | --- | --- | --- |
| `dotnet build AutoInventario.sln -c Debug` | Correcto, exit code `0`; `0` warnings y `0` errores en la ultima corrida validada. | No aplica. | Bajo. La solucion compila y deja artefactos `Debug` validos. | Mantener este gate como referencia de build integral. |
| `dotnet build AutoInventario.csproj -c Debug` | Correcto, exit code `0`; `0` warnings y `0` errores. | No aplica. | Bajo. El agente Windows compila correctamente. | Mantener como gate local y de CI. |
| `dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c Debug` | Correcto, exit code `0`; `0` warnings y `0` errores. | No aplica. | Bajo. El updater esta estable en `Debug`. | Mantener en la matriz de build. |
| `dotnet build Webhook/Webhook-Inventario.csproj -c Debug` | Correcto, exit code `0`; `0` warnings y `0` errores. | No aplica. | Bajo. El webhook compila en `net8.0`. | Mantener. |
| `dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj -c Debug` | Correcto, exit code `0`; `9/9` pruebas correctas. | No aplica. | Bajo. La suite del agente pasa localmente. | Mantener y ampliar cobertura funcional. |
| `dotnet test Webhook.Tests/Webhook.Tests.csproj -c Debug` | Correcto, exit code `0`; `20/20` pruebas correctas. | No aplica. | Bajo. La suite del webhook pasa localmente. | Mantener y ampliar cobertura de integracion. |

## Lambda

| Comando | Resultado | Error clave si falla | Impacto | Siguiente accion recomendada |
| --- | --- | --- | --- | --- |
| `python -m py_compile Lambda-Inventario/lambda_function.py` | Correcto, exit code `0`, sin salida. | No aplica. | Bajo. No hay errores de sintaxis Python en el handler actual. | Mantener este check en auditoria local y CI. |

## Terraform

| Comando | Resultado | Error clave si falla | Impacto | Siguiente accion recomendada |
| --- | --- | --- | --- | --- |
| `terraform -chdir=Infraestructura-Terraform fmt -check -recursive` | Correcto, exit code `0`. | No aplica. | Bajo. El codigo Terraform esta formateado. | Mantener el gate. |
| `terraform -chdir=Infraestructura-Terraform validate -no-color` | Falla, exit code `1`. | `there is no package for registry.terraform.io/hashicorp/aws 6.44.0 cached in .terraform\providers`. | Medio. No se puede validar semanticamente desde este host sin provider descargado. | Ejecutar `terraform init -backend=false -input=false -no-color` en un host con acceso DNS/salida a `registry.terraform.io`. |
| `terraform -chdir=Infraestructura-Terraform init -backend=false -input=false -no-color` | Falla, exit code `1`. Ejecutado solo como diagnostico adicional. | `could not connect to registry.terraform.io ... lookup registry.terraform.io: no such host`. | Medio. El bloqueo actual es de bootstrap/red local, no un error de formato. | Verificar DNS/salida de red local o validar en CI/host con conectividad. |

Observacion adicional:

- El lock file `Infraestructura-Terraform/.terraform.lock.hcl` fija `hashicorp/aws` en `6.44.0`.
- El pipeline Terraform de `.azure-pipelines/pipeline.yml` si ejecuta `terraform init -backend=false` antes de `validate`.

## Dependencias

| Comando | Resultado | Error clave si falla | Impacto | Siguiente accion recomendada |
| --- | --- | --- | --- | --- |
| `dotnet list AutoInventario.csproj package --vulnerable --include-transitive` | Correcto, exit code `0`. | No se reportaron paquetes vulnerables. | Bajo. El agente no presenta advisories NuGet activos en este check. | Mantener el scan en CI y revisar cambios de paquetes antes de subir de major. |
| `dotnet list Webhook/Webhook-Inventario.csproj package --vulnerable --include-transitive` | Correcto, exit code `0`. | No se reportaron paquetes vulnerables. | Bajo. El webhook no presenta advisories NuGet activos en este check. | Mantener el scan en CI. |

## Seguridad

| Comando o inspeccion | Resultado | Error clave si falla | Impacto | Siguiente accion recomendada |
| --- | --- | --- | --- | --- |
| `powershell -ExecutionPolicy Bypass -File scripts/ci/Invoke-SecretScan.ps1 -Root .` | Correcto, exit code `0`. | No aplica. | Bajo. No se confirmaron secretos reales en archivos trackeados con el escaneo alineado a CI. | Mantener `gitleaks` o el fallback actual como gate obligatorio. |
| Revision estatica focalizada de webhook, Lambda y pipeline | Hallazgos reales documentados en `docs/SECURITY-FINDINGS.md`. | No aplica. | Variable segun hallazgo. | Priorizar manejo de datos desencriptados en Lambda, hardening de configuracion del webhook y el umbral NuGet del pipeline. |

## Pipeline

| Archivo o gate | Estado observado | Impacto | Siguiente accion recomendada |
| --- | --- | --- | --- |
| `azure-pipelines.yml` | `ProductGates` ejecuta secret scan, NuGet scan, build/test .NET, `py_compile` y `terraform fmt -check`. | Bueno para reproducibilidad del producto. | Mantener scripts de `scripts/ci/` como fuente de verdad del pipeline. |
| `azure-pipelines.yml` | `NuGetVulnerabilityThreshold` esta en `Critical`. | Medio. Un advisory `High` futuro no bloquearia CI automaticamente. | Bajar el umbral a `High` o justificar una excepcion temporal documentada. |
| `.azure-pipelines/pipeline.yml` | El pipeline Terraform si hace `init -backend=false` antes de `validate`. | Bueno para validar providers en CI. | Contrastar su resultado con el fallo local de DNS si aparecen errores similares en Azure. |

## Reproduccion local

Windows PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/audit-local.ps1
```

Linux, WSL o Git Bash:

```bash
bash scripts/audit-local.sh
```

Los scripts continuan aunque un comando falle, devuelven exit code distinto de cero si hay checks fallidos y reutilizan las reglas de secret scan del CI para no reportar placeholders, docs ni fixtures como incidentes.

## Verificacion de scripts

| Script | Resultado | Nota |
| --- | --- | --- |
| `powershell -ExecutionPolicy Bypass -File scripts/audit-local.ps1` | Ejecutado; exit code `1`. | Falla solo por `terraform validate`; el resto del bloque principal completa correctamente. |
| `bash scripts/audit-local.sh` | No ejecutable con el `bash` del PATH en este host. | `C:\Windows\System32\bash.exe` redirige a WSL y no habia `/bin/bash` disponible. |
| `C:\Program Files\Git\bin\bash.exe scripts/audit-local.sh` | Ejecutado; exit code `1`. | Misma causa funcional que PowerShell: `terraform validate` falla por provider no cacheado y falta de resolucion DNS al hacer `init`. |

## Estado general y siguiente accion recomendada

- El codigo productivo compila y las pruebas del agente y del webhook pasan en local.
- El unico bloqueo tecnico reproducible del flujo base es Terraform desde este host por bootstrap/red hacia `registry.terraform.io`.
- La deuda de seguridad mas clara hoy esta en el manejo de datos desencriptados en Lambda, el hardening de configuracion del webhook y el umbral NuGet del pipeline.
