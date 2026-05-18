# Checklist de release comercial privada

Usar este documento como criterio de `go / no-go` para una primera PoC comercial privada de AutoInventario Enterprise On-Prem.

Reglas de uso:

- Todos los items `Bloqueante` deben quedar en estado esperado antes de ofrecer una PoC vendible.
- Los items `Recomendado` admiten una desviacion explicitamente aceptada por producto, AppSec y release management.
- Los items `Futuro` no bloquean la primera release privada, pero deben quedar registrados como deuda o roadmap.
- Si un item no aplica al alcance comercial acordado, marcar `N/A` con motivo y aprobador.

Estados sugeridos:

- `Completado`
- `Completado con evidencia`
- `Aprobado con excepcion`
- `No iniciado`

## Bloqueante

| ID | Area | Item | Estado esperado | Comando o evidencia | Responsable sugerido |
| --- | --- | --- | --- | --- | --- |
| B01 | Seguridad | El arbol de trabajo y el contenido versionado no exponen secretos reales. | `Completado con evidencia` | `powershell -ExecutionPolicy Bypass -File scripts/ci/Invoke-SecretScan.ps1 -Root .` con exit code `0`. Adjuntar resultado del gate equivalente en CI. | AppSec lead |
| B02 | Secretos | Cualquier secreto previamente expuesto queda rotado o formalmente reemplazado antes de entregar PoC. | `Completado con evidencia` | Checklist de rotacion firmado contra [SECRET-ROTATION.md](SECRET-ROTATION.md) y constancia de actualizacion en Azure DevOps/AWS/host local sin pegar valores. | DevSecOps + owner del secreto |
| B03 | Build | La solucion compila en `Debug` y los binarios a distribuir se publican en `Release` sin errores. | `Completado con evidencia` | `dotnet build AutoInventario.sln -c Debug`; `dotnet build AutoInventario.csproj -c Debug`; `dotnet build AutoInventario.Updater\AutoInventario.Updater.csproj -c Debug`; `dotnet build Webhook\Webhook-Inventario.csproj -c Debug`; `dotnet publish AutoInventario.csproj -c Release`; `dotnet publish AutoInventario.Updater\AutoInventario.Updater.csproj -c Release`; `dotnet publish Webhook\Webhook-Inventario.csproj -c Release`. | Build/Release engineer |
| B04 | Tests | Las pruebas existentes del agente y del Webhook pasan y la Lambda compila sintacticamente. | `Completado con evidencia` | `dotnet test AutoInventario.Tests\AutoInventario.Tests.csproj -c Debug`; `dotnet test Webhook.Tests\Webhook.Tests.csproj -c Debug`; `python -m py_compile Lambda-Inventario\lambda_function.py`. | QA lead |
| B05 | Dependencias | No hay vulnerabilidades NuGet activas en los proyectos entregados al cliente. | `Completado con evidencia` | `dotnet list AutoInventario.csproj package --vulnerable --include-transitive`; `dotnet list Webhook\Webhook-Inventario.csproj package --vulnerable --include-transitive`. Guardar salida o captura del pipeline. | AppSec lead |
| B06 | Pipeline | El commit candidato pasa los stages relevantes del pipeline de producto. | `Completado con evidencia` | Ejecucion verde de `ProductGates` y, si se van a distribuir binarios firmados, tambien `Package` en `azure-pipelines.yml`. Referenciar run ID. | Release manager |
| B07 | Seguridad | `POST /webhooks` exige API key y no registra el valor del secreto. | `Completado con evidencia` | Prueba manual o automatizada con `401` sin `X-AutoInventario-Key`, `403` con valor invalido y revision de logs sin exponer el header. | AppSec lead + owner Webhook |
| B08 | Logging | Los logs y tickets de error no imprimen payloads completos, tokens, API keys, connection strings ni BitLocker recovery keys. | `Completado con evidencia` | Revision de `docs/SECURITY.md`, corrida controlada de agente/Webhook/Lambda y muestra de logs redactados. | AppSec lead |
| B09 | Firma de binarios | Los ejecutables distribuidos al cliente estan firmados y los hashes se generan despues de firmar. | `Completado con evidencia` | Run de `Package` con descarga de PFX desde Secure Files, artefactos firmados y `SHA256SUMS.txt`. Validar que `latest.json` coincide con los binarios publicados. | Release manager |
| B10 | Instalador/actualizacion | El agente y el updater se instalan o actualizan correctamente en un host limpio de prueba. | `Completado con evidencia` | Evidencia manual en Windows: instalacion en `C:\ProgramData\AutoInventario`, creacion de tarea programada, actualizacion mediante `latest.json` y desinstalacion controlada con `-del`. | Owner de endpoint Windows |
| B11 | Despliegue on-prem | Existe al menos una ruta de despliegue validada para la PoC del cliente: Docker o IIS/Windows Server. | `Completado con evidencia` | Docker: `docker compose --env-file .env.example config` y build local. IIS: `dotnet publish Webhook\Webhook-Inventario.csproj -c Release` y validacion con [DEPLOYMENT-IIS.md](DEPLOYMENT-IIS.md). Registrar ruta elegida para el cliente. | Infraestructura / Platform owner |
| B12 | Documentacion | El paquete documental minimo esta completo y alineado con la release candidata. | `Completado con evidencia` | Revisar [PRODUCT.md](PRODUCT.md), [PRICING-MODEL.md](PRICING-MODEL.md), [DEPLOYMENT-ONPREM.md](DEPLOYMENT-ONPREM.md), [DEPLOYMENT-IIS.md](DEPLOYMENT-IIS.md), [SECURITY.md](SECURITY.md), [SECRET-ROTATION.md](SECRET-ROTATION.md) y [AGENT-WINDOWS-SERVER.md](AGENT-WINDOWS-SERVER.md). | Product owner + release manager |
| B13 | Windows Server | Si la PoC incluye servidores, el soporte basico queda explicado y validado sobre el entorno objetivo del cliente. | `Completado con evidencia` | Guia [AGENT-WINDOWS-SERVER.md](AGENT-WINDOWS-SERVER.md) entregada y prueba manual en Windows Server 2019/2022/2025 o en la version real del cliente. | Owner de endpoint Windows |
| B14 | Rollback | Existe rollback documentado y verificado para Webhook y agente. | `Completado con evidencia` | IIS/Docker: release anterior preservada y pasos de rollback confirmados con [DEPLOYMENT-IIS.md](DEPLOYMENT-IIS.md) o [DEPLOYMENT-ONPREM.md](DEPLOYMENT-ONPREM.md). Agente: binario anterior y procedimiento de reinstalacion disponibles. | Release manager + Infraestructura |
| B15 | Privacidad | El cliente conoce que datos se recolectan, cuales no y bajo que base operacional se procesan. | `Completado con evidencia` | Revisar [SECURITY.md](SECURITY.md), [AGENT-WINDOWS-SERVER.md](AGENT-WINDOWS-SERVER.md) y aprobacion interna del cliente sobre tratamiento, retencion y acceso. | Product owner + legal/privacidad |

## Recomendado

| ID | Area | Item | Estado esperado | Comando o evidencia | Responsable sugerido |
| --- | --- | --- | --- | --- | --- |
| R01 | AWS opcional | Si se va a ofrecer modo `AwsLambda`, el flujo AWS se valida aparte de la PoC local. | `Completado con evidencia` | `dotnet build Webhook\Webhook-Inventario.csproj -c Debug` con `InventoryProcessing:Mode=AwsLambda` configurado y smoke test controlado del invoke. | Owner Webhook + owner AWS |
| R02 | Terraform | La infraestructura AWS del repo formatea y valida si el cliente contratara esa opcion. | `Completado con evidencia` | `terraform -chdir=Infraestructura-Terraform fmt -check -recursive`; `terraform -chdir=Infraestructura-Terraform validate -no-color` despues de `terraform init`. | Owner Infraestructura |
| R03 | Soporte | Existe runbook operativo, canal de soporte y matriz de escalamiento para la PoC. | `Completado con evidencia` | Documento de handoff o ticket interno con on-call, horario y responsables de L1/L2/L3. | Release manager + soporte |
| R04 | Observabilidad | El entorno PoC tiene health check, acceso a logs y correlacion minima de incidentes. | `Completado con evidencia` | `GET /` o endpoint de salud del Webhook, ubicacion de logs y prueba de correlacion de un evento de inventario. | Infraestructura / SRE |
| R05 | Secretos | Las variables de entorno y archivos de configuracion del cliente viven fuera del repo y con ACL adecuadas. | `Completado con evidencia` | Revision de host o pipeline: sin `.env` reales versionados, sin `appsettings` con secretos, permisos revisados en ruta de clave privada. | DevSecOps |
| R06 | Windows Server | Se valida el comportamiento del agente al menos en una workstation y en un Windows Server real del entorno objetivo. | `Completado con evidencia` | Payload recibido con `is_server=false` en cliente Windows y `is_server=true` en Windows Server; si aplica, `roles_features` visible cuando WMI lo permite. | QA + owner de endpoint Windows |
| R07 | Documentacion cliente | Existe un paquete de onboarding corto para el cliente con prerequisitos, variables y limitaciones conocidas. | `Completado con evidencia` | Entrega de [ONBOARDING-CORPORATE.md](ONBOARDING-CORPORATE.md) o documento derivado adaptado al cliente. | Product owner |
| R08 | Privacidad | Se registran responsables de acceso a inventario y periodo de retencion para la PoC. | `Completado con evidencia` | Aprobacion en ticket, correo o minuta del cliente; no almacenar datos personales fuera del entorno acordado. | Product owner + privacidad |

## Futuro

| ID | Area | Item | Estado esperado | Comando o evidencia | Responsable sugerido |
| --- | --- | --- | --- | --- | --- |
| F01 | Cifrado | Migrar de cifrado sin autenticacion explicita a AEAD o equivalente. | `No iniciado` o `Planificado` | ADR o issue tecnico ligado a [AUDIT.md](AUDIT.md) y [SECURITY-FINDINGS.md](SECURITY-FINDINGS.md). | Arquitectura + AppSec |
| F02 | Procesamiento local | Reemplazar el stub `Local` por procesamiento self-hosted completo sin AWS. | `No iniciado` o `Planificado` | Issue o epic que cubra normalizacion, conectores y pruebas de paridad con Lambda. | Arquitectura + owner Webhook |
| F03 | Instalador | Definir un empaquetado comercial mas robusto para despliegues masivos del agente. | `No iniciado` o `Planificado` | RFC o issue para MSI, bootstrapper corporativo o integracion con herramientas de software distribution. | Release engineering |
| F04 | Hosting | Agregar validacion formal para Kubernetes u otros entornos de hosting corporativo. | `No iniciado` o `Planificado` | ADR o backlog de despliegue adicional; no bloquear la primera PoC on-prem. | Arquitectura + plataforma |
| F05 | Calidad | Automatizar pruebas de aceptacion end-to-end para workstation, Windows Server y actualizaciones. | `No iniciado` o `Planificado` | Plan de QA con matriz de versiones y evidencias repetibles por release. | QA lead |
