# Hallazgos de seguridad actuales

Fecha: 2026-05-18
Alcance: revision AppSec del codigo trackeado, configuracion de pipeline y scripts de gate. No se copiaron ni documentaron valores reales de secretos.

## Criticos

No se confirmaron secretos reales commiteados ni bypasses criticos de autenticacion en el codigo trackeado durante esta auditoria.

Observacion:

- `scripts/ci/Invoke-SecretScan.ps1 -Root .` termino sin indicadores y no hubo archivos `tfstate`, `tfvars`, `pfx/p12` o `secrets.json` versionados en el arbol trackeado.

## Altos

| Ruta afectada | Tipo de riesgo | Evidencia segura | Remediacion recomendada |
| --- | --- | --- | --- |
| `AutoInventario.csproj` | Dependencia NuGet vulnerable | `dotnet list AutoInventario.csproj package --vulnerable --include-transitive` reporta `Microsoft.Extensions.Caching.Memory 8.0.0` con advisory `GHSA-qj66-m88j-hmgj` (`High`). | Actualizar la cadena de dependencias hasta una version corregida y repetir el gate NuGet. |
| `Lambda-Inventario/lambda_function.py` | Exposicion de payload desencriptado y datos operativos en flujo de error | `report_failure_request(...)` envia `workstation` completo, hostname, serial y mensaje de error a un ticket externo; varias respuestas/errores tambien devuelven cuerpos operativos completos. | Redactar payloads, limitar los campos enviados fuera del proceso y sustituir datos sensibles por `correlation_id` o resumenes minimizados. |

## Medios

| Ruta afectada | Tipo de riesgo | Evidencia segura | Remediacion recomendada |
| --- | --- | --- | --- |
| `azure-pipelines.yml` | Gate NuGet demasiado permisivo | `NuGetVulnerabilityThreshold` esta configurado en `Critical`, por lo que el advisory `High` actual del agente no bloquearia CI. | Bajar el umbral a `High` o introducir una excepcion documentada por advisory con vencimiento. |
| `Webhook/Controllers/WebhookController.cs` | Token de verificacion hard-codeado | La verificacion GET usa `private const string VerifyToken = "YOUR_VERIFY_TOKEN"` en lugar de leer un secret/configuracion externa. | Mover el token a configuracion segura o variable de entorno y agregar pruebas de configuracion ausente/presente. |
| `Webhook/Services/OfflineLicenseValidator.cs` y `Webhook/appsettings.example.json` | Modo de licencia de desarrollo permisivo si se despliega sin endurecer | `AllowUnsignedDevelopmentLicense` existe y el ejemplo la deja en `true`; la validacion acepta `algorithm=none` para ediciones que no requieren licencia firmada. | Asegurar `false` fuera de desarrollo, validar por entorno en arranque y documentar explicitamente el riesgo operacional. |
| `Lambda-Inventario/lambda_function.py` | Configuracion sensible y de entorno embebida en codigo | Regiones AWS, bucket, instance ID y URLs externas estan codificadas en el handler. | Mover valores de entorno a variables/secret store y validar su presencia al iniciar. |
| `Infraestructura-Terraform/main.tf` y `Lambda-Inventario/lambda_function.py` | Deriva entre permisos declarados e integraciones usadas | Terraform concede lectura de un secreto, pero el codigo Lambda usa tambien otro secreto, S3 y SSM. | Revisar permisos IAM reales requeridos, minimizar privilegios y mantener Terraform y codigo alineados. |

## Bajos

| Ruta afectada | Tipo de riesgo | Evidencia segura | Remediacion recomendada |
| --- | --- | --- | --- |
| `Infraestructura-Terraform/secrets.tf` | Recurso de secretos versionado sin valores | El archivo define recursos/referencias de secretos, no se observaron valores reales en el contenido auditado. | Mantener solo referencias, nunca valores, y revisar historial si alguna vez se cargaron secretos reales. |
| `Resources/public.key` | Material criptografico publico versionado | Es una clave publica embebida por diseno; no es material privado. | Aceptable si la rotacion y la huella esperada estan documentadas. |
| `Webhook.Tests/DecryptionServiceTests.cs` | Fixture criptografico de prueba | El test genera un PEM temporal en runtime; no es una clave privada operativa. | Mantenerlo fuera de escaneos de secreto operativos para evitar falsos positivos. |

## Hallazgos descartados por esta auditoria

- `Webhook.Services.ApiKeyMiddleware` si esta activo en `Startup` y protege `/webhooks` y `/id-clients`.
- `Webhook/Webhook-Inventario.csproj` ya apunta a `net8.0` y el scan NuGet actual no reporta advisories activos.
- `Webhook/appsettings.example.json`, `.env.example` y la documentacion auditada contienen placeholders o nombres de variables, no secretos reales.

## Reglas de manejo

- No publicar ni copiar valores reales en issues, PRs, logs o reportes.
- Rotar cualquier secreto que haya sido commiteado, compartido o usado fuera de almacenamiento seguro.
- Usar AWS Secrets Manager, Azure DevOps secret variables, Secure Files, variables de entorno locales o store seguro del sistema.
- Mantener deteccion de secretos en CI y, cuando sea posible, instalar `gitleaks` tambien en entornos locales.
