# Auditoria del proyecto AutoInventario

Fecha: 2026-05-09
Alcance: estructura del repositorio, proyectos .NET/Python/Terraform, pipelines, documentacion, configuracion, seguridad y validaciones locales.

## Resumen ejecutivo

El proyecto implementa un flujo completo de inventario Windows hacia ManageEngine, pero el estado actual del arbol de trabajo no es liberable. El mayor bloqueo funcional es la reubicacion incompleta del agente: la solucion y los tests siguen apuntando a `Autoinventario/AutoInventario.csproj`, mientras el codigo equivalente aparece en la raiz como archivos no versionados.

El mayor bloqueo de seguridad es la presencia de secretos reales en archivos del repositorio o del arbol local. Esos valores deben considerarse comprometidos, rotarse y eliminarse del historial antes de publicar o compartir el repositorio.

## Estado Git observado

Rama: `dev...origin/dev`.

Cambios preexistentes relevantes:

- Eliminados para Git: `Autoinventario/AutoInventario.csproj`, `Autoinventario/Program.cs`, `Autoinventario/Helpers/*`, `Autoinventario/Services/*`, `Autoinventario/Models/*`, `Autoinventario/Resources/*` y otros archivos de esa carpeta.
- Sin seguimiento: `Program.cs`, `ConfigForm.*`, `Helpers/`, `Models/`, `Properties/`, `Resources/`, `Services/`, `SystemInfo.cs`, `Webhook/Resources/`.
- Modificado: `Webhook/appsettings.json` contiene valores sensibles en el arbol local.

No se han revertido ni normalizado estos cambios durante la auditoria.

## Inventario tecnico

| Area | Tecnologia | Observaciones |
| --- | --- | --- |
| Agente | .NET 8, WinForms, WMI, registro Windows | Recopila inventario, cifra y envia a Webhook. |
| Updater | .NET 8 | Compila correctamente; reemplaza ejecutable instalado y relanza tarea. |
| Webhook | ASP.NET Core `net6.0` | Compila, pero usa framework sin soporte y contiene riesgos de autenticacion/configuracion. |
| Tests | xUnit `net8.0-windows` | Pasa un test placeholder; referencia proyecto inexistente. |
| Lambda | Python 3.12 | Sintaxis valida; dependencias no fijadas por version. |
| Terraform | AWS provider | Formato pendiente; validacion requiere `terraform init`; definicion IAM incompleta para el codigo Lambda actual. |
| CI/CD | Azure Pipelines | Publica agente/updater/webhook, firma EXEs y genera manifiesto de actualizaciones. |

## Validaciones ejecutadas

| Comando | Resultado | Impacto |
| --- | --- | --- |
| `dotnet build AutoInventario.sln -c Debug` | Falla por proyecto inexistente `AutoInventario/AutoInventario.csproj`. | Bloquea build de solucion y pipeline. |
| `dotnet build AutoInventario.csproj -c Debug` | Falla por recursos no texto sin `System.Resources.Extensions`; advierte NU1603. | El proyecto raiz no es buildable en su estado actual. |
| `dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c Debug` | Correcto. | Updater aislado OK. |
| `dotnet build Webhook/Webhook-Inventario.csproj -c Debug` | Correcto con avisos `NETSDK1138` y nullable. | Webhook compila, pero requiere migracion desde .NET 6. |
| `dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj -c Debug` | Pasa 1 test vacio; avisa referencia inexistente. | Senal debil; no valida comportamiento real. |
| `python -m py_compile Lambda-Inventario/lambda_function.py` | Correcto. | Sintaxis Python OK. |
| `terraform fmt -check -recursive` | Falla en `main.tf`. | Formato Terraform no consistente. |
| `terraform validate -no-color` | Falla por provider AWS no inicializado. | Ejecutar `terraform init` antes de validar. |
| `dotnet list package --vulnerable --include-transitive` | Vulnerabilidades transitivas en agente y Webhook. | Requiere actualizacion de paquetes/framework. |

## Hallazgos criticos

| Prioridad | Hallazgo | Evidencia | Recomendacion |
| --- | --- | --- | --- |
| Critica | Secretos reales presentes en repo/arbol local. | `Lambda-Inventario/lambda_function/secrets.json`, `Infraestructura-Terraform/secrets.tf`, `Webhook/Resources/private.key`, `Webhook/appsettings.json`, `Services/InventoryService.cs`, `Webhook/Services/lambda.cs`, scripts PowerShell. | Rotar todos los secretos, revocar credenciales, borrar del historial con herramienta de limpieza y usar Secrets Manager/Azure secure files/variables protegidas. |
| Critica | Credenciales AWS hard-codeadas en el invocador Lambda. | `Webhook/Services/lambda.cs`. | Usar IAM Role, variables de entorno o AWS SDK default credential chain. |
| Critica | Autenticacion del endpoint `/webhooks` deshabilitada. | `Webhook/Controllers/WebhookController.cs` tiene la comparacion de API key comentada. | Reactivar validacion, preferir middleware comun y pruebas de 401/403. |
| Critica | Build principal roto por rutas obsoletas. | `AutoInventario.sln` y `AutoInventario.Tests.csproj` apuntan a `Autoinventario/AutoInventario.csproj`. | Decidir estructura canonica y actualizar sln, tests, pipeline y project references. |
| Alta | Webhook usa `net6.0`, framework fuera de soporte. | `Webhook/Webhook-Inventario.csproj`. | Migrar a `net8.0` o version LTS vigente y actualizar paquetes. |
| Alta | Dependencias vulnerables transitivas. | `Microsoft.Extensions.Caching.Memory 8.0.0` y `System.Text.Json 8.0.0` reportadas por NuGet. | Actualizar paquetes directos o frameworks que resuelven versiones transitivas corregidas. |

## Hallazgos altos y medios

| Prioridad | Hallazgo | Evidencia | Recomendacion |
| --- | --- | --- | --- |
| Alta | Logging de valores sensibles. | Webhook registra API key recibida y datos de payload invalido; Lambda registra errores con datos de workstation. | Redactar claves, tokens, payloads y datos personales; registrar solo correlacion, hash o tamanos. |
| Alta | Cifrado sin autenticacion explicita. | Agente cifra con AES y RSA OAEP; Webhook/Lambda descifran sin firma/HMAC/AEAD. | Migrar a AES-GCM o agregar HMAC/firma sobre `data`, `key`, `iv` y metadatos. |
| Alta | Terraform IAM no cubre lo que Lambda usa. | Lambda lee `manageengine_api_key`, usa S3 y SSM; policy solo permite leer la clave privada. | Ampliar IAM con privilegio minimo para todos los recursos usados. |
| Alta | Terraform handler no coincide con el codigo. | `handler = "lambda_function.handler"` mientras el codigo expone `lambda_handler`. | Cambiar a `lambda_function.lambda_handler`. |
| Alta | Regiones inconsistentes. | Terraform default `us-east-1`; Lambda usa `eu-west-1` y `eu-south-2`; Webhook invoca EU West 1. | Definir una matriz de regiones por entorno y parametrizar. |
| Media | Dependencias Python sin version fija. | `Lambda-Inventario/requirements.txt`. | Fijar versiones y auditar dependencias en CI. |
| Media | Artefactos binarios versionados. | ZIPs y `.so` en `Lambda-Inventario/`. | Generarlos en pipeline; excluir builds locales del repo. |
| Media | `ApiKeyMiddleware` solo protege `/id-clients`; `/updates` esta comentado. | `Webhook/Controllers/ApiKeyMiddleware.cs`. | Decidir si `/updates` debe ser publico; si no, reactivar proteccion. |
| Media | La pagina de estado no muestra eventos. | `Pages/Index.cshtml.cs` no llena `Events` desde `EventStore`. | Mapear summaries no sensibles si se necesita observabilidad. |
| Media | Tests sin valor funcional. | `AutoInventario.Tests/UnitTest1.cs`. | Sustituir por tests de serializacion, cifrado, endpoints, updater y normalizacion Lambda. |
| Baja | Nombre/casing inconsistente. | `AutoInventario`, `Autoinventario`, `AutoInventario.Tests`. | Normalizar casing para evitar errores entre Windows/Linux/CI. |

## Riesgos de datos personales

El agente recopila datos que pueden ser personales o sensibles:

- Usuarios locales y ultimo usuario.
- Direcciones IP, dominio, SID y hardware.
- Licencias Windows/Office.
- Contrasena de recuperacion BitLocker.
- Numero de serie, modelo, discos, memoria, red y perifericos.

Recomendaciones:

- Documentar base legal, finalidad, retencion y acceso.
- Minimizar campos no necesarios, especialmente BitLocker.
- Cifrar en transito y en reposo.
- Evitar logs con payload completo.
- Mantener trazabilidad por identificadores no sensibles.

## Plan de remediacion sugerido

1. Rotar y revocar todos los secretos detectados.
2. Eliminar secretos del repo e historial; anadir reglas de bloqueo en pre-commit/CI.
3. Decidir estructura canonica del agente: raiz o `Autoinventario/`.
4. Actualizar `AutoInventario.sln`, `AutoInventario.Tests.csproj`, `azure-pipelines.yml` y recursos embebidos.
5. Hacer que `dotnet build AutoInventario.sln -c Release` sea verde.
6. Migrar Webhook a .NET LTS vigente y actualizar paquetes vulnerables.
7. Restaurar autenticacion de `/webhooks` y cubrirlo con tests.
8. Parametrizar credenciales AWS y nombres de Lambda/Secrets/regions.
9. Corregir Terraform (`fmt`, handler, package filename, IAM, regiones).
10. Reemplazar test placeholder por cobertura minima de rutas criticas.

## Documentacion actualizada

- [README.md](../README.md): vision general, estado, arquitectura, configuracion y comandos.
- [docs/ARCHITECTURE.md](ARCHITECTURE.md): flujo tecnico y responsabilidades.
- [docs/OPERATIONS.md](OPERATIONS.md): build, test, despliegue y runbooks.
- [AGENTS.MD](../AGENTS.MD): plantilla de trabajo para agentes.
- [Webhook/Readme.md](../Webhook/Readme.md), [Lambda-Inventario/README.md](../Lambda-Inventario/README.md), [Infraestructura-Terraform/README.md](../Infraestructura-Terraform/README.md): documentacion por componente.
