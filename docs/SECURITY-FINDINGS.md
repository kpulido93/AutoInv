# Hallazgos de seguridad actuales

Fecha: 2026-05-12
Alcance: revision AppSec sin correcciones funcionales. No se copiaron ni documentaron valores reales de secretos.

## Criticos

| Ruta | Tipo | Evidencia segura | Recomendacion |
| --- | --- | --- | --- |
| `Lambda-Inventario/lambda_function/secrets.json` | Archivo local ignorado con material secreto | Contiene campos de token/API ManageEngine y material de clave privada segun patrones. | Considerar los valores comprometidos si salieron del entorno local, rotarlos y moverlos a AWS Secrets Manager, variables secretas o store seguro. |
| `Webhook/Resources/private.key` | Archivo local ignorado con clave privada | Detectado por nombre y por material de clave privada. | Eliminar del arbol de trabajo cuando no sea necesario, rotar la clave y cargarla solo desde almacenamiento seguro. |
| `Webhook/Controllers/WebhookController.cs:58` | Control de acceso deshabilitado | La comparacion de API key esta comentada; `Unauthorized` tambien aparece comentado en la misma rama de control. | Reactivar autenticacion/autorizacion antes de exponer `/webhooks`; agregar pruebas de acceso autorizado y no autorizado. |

## Altos

| Ruta | Tipo | Evidencia segura | Recomendacion |
| --- | --- | --- | --- |
| `Webhook/appsettings.json` | Configuracion local sensible | Appsettings ignorado con `AutoInventario.ApiKey` no vacio y ruta de clave privada no vacia; no se imprimieron valores. | Mantener fuera de Git, rotar si fue compartido y documentar variables/secret store para despliegue. |
| `Webhook/Controllers/WebhookController.cs:56` | Logging de credencial | Se registra la API key recibida, segun patron de logging. | Redactar claves y payloads; registrar solo presencia, hash corto no reversible o correlation id. |
| `AutoInventario.csproj` | Dependencia vulnerable | `Microsoft.Extensions.Caching.Memory 8.0.0`, gravedad `High`, `GHSA-qj66-m88j-hmgj`. | Actualizar dependencias directas/transitivas y repetir `dotnet list ... --vulnerable`. |
| `Webhook/Webhook-Inventario.csproj` | Dependencia vulnerable | `System.Text.Json 8.0.0`, gravedad `High`, `GHSA-hh2w-p6rv-4g7w` y `GHSA-8g4q-xg66-9fp4`. | Migrar framework/paquetes hasta una version corregida. |
| `Webhook/Webhook-Inventario.csproj` | Framework fuera de soporte | `NETSDK1138` por `net6.0`. | Migrar a una version LTS soportada y validar compatibilidad. |

## Medios

| Ruta | Tipo | Evidencia segura | Recomendacion |
| --- | --- | --- | --- |
| `Infraestructura-Terraform/secrets.tf` | Archivo Terraform de secretos versionado | Detectado por nombre `secrets.tf` y referencia de secreto. No se imprimieron valores. | Confirmar que solo declara recursos/referencias y no valores reales; si hubo secretos en historial, rotar y limpiar historial. |
| `InventarioFinal.ps1` | Script local ignorado con referencias sensibles | Detecta asignaciones tipo password y referencias de inventario sensible. | Revisar localmente, eliminar si es obsoleto y evitar copiarlo a artefactos o commits. |
| `InventarioFinal 1.ps1` | Script local ignorado con referencias sensibles | Detecta asignaciones tipo password y referencias de inventario sensible. | Revisar localmente, eliminar si es obsoleto y evitar copiarlo a artefactos o commits. |
| `Webhook/appsettings.example.json` | Ejemplo con claves sensibles por nombre | Contiene claves de conexion/API por estructura. | Mantener solo placeholders obvios y validar que no existan valores reales en ejemplos. |
| `Lambda-Inventario/lambda_function.py` | Referencias a token/API y secretos | El codigo accede a secretos de ManageEngine; no se detecto valor impreso en el reporte. | Mantener el acceso a secretos por gestor externo y redaccion estricta en logs. |

## Bajos

| Ruta | Tipo | Evidencia segura | Recomendacion |
| --- | --- | --- | --- |
| `Resources/public.key` | Clave publica versionada | Es una clave publica, no material privado. | Aceptable si forma parte del diseno; documentar rotacion y huella esperada. |
| `docs/*` y `README.md` | Referencias documentales a secretos | El escaneo amplio detecta palabras clave de secretos en documentacion. | Mantener ejemplos redactados y revisar que nunca incluyan valores reales. |

## Resultado del escaneo

- AWS access key id con patron `AKIA`/`ASIA`: no confirmado en el escaneo actual.
- PFX/P12 versionados: no confirmados en el escaneo actual.
- `tfvars`/`tfstate` versionados: no confirmados en el escaneo actual.
- Material privado local confirmado: `Lambda-Inventario/lambda_function/secrets.json` y `Webhook/Resources/private.key`.

## Reglas de manejo

- No publicar ni copiar valores reales en issues, PRs, logs o reportes.
- Rotar cualquier secreto que haya sido commiteado, compartido o usado fuera del entorno seguro.
- Usar AWS Secrets Manager, Azure DevOps secret variables, Secure Files, variables de entorno locales o store seguro del sistema.
- Agregar una puerta de CI/pre-commit con deteccion de secretos despues de limpiar los hallazgos actuales.
