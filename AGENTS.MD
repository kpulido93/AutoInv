# AGENTS.md

Plantilla de instrucciones para agentes de IA y coding assistants que trabajen en AutoInventario.

## Mision del agente

Mantener AutoInventario como una suite segura y operable para inventario Windows, recepcion Webhook, procesamiento Lambda, infraestructura AWS y publicacion de actualizaciones. Priorizar build reproducible, secretos fuera del repositorio, compatibilidad Windows y documentacion actualizada.

## Contexto rapido

- Agente Windows: .NET 8, WinForms, WMI, registro, cifrado, tarea programada.
- Updater: .NET 8, reemplazo seguro de `AutoInventario.exe`.
- Webhook: ASP.NET Core `net8.0`, endpoints `/webhooks`, `/id-clients`, `/updates/latest.json`.
- Lambda: Python 3.12, descifrado y sincronizacion con ManageEngine.
- Infraestructura: Terraform AWS.
- CI/CD: Azure Pipelines con SonarCloud, firma de EXEs y artefactos.

## Reglas de trabajo

1. Ejecuta `git status --short --branch` antes de modificar archivos.
2. No reviertas cambios ajenos sin instruccion explicita.
3. No imprimas, copies ni documentes valores reales de secretos.
4. Si encuentras secretos, informa la ruta y el tipo de secreto sin revelar el valor.
5. Mantén cambios de codigo y documentacion dentro del alcance pedido.
6. Actualiza documentacion cuando cambien comandos, endpoints, configuracion, pipeline o arquitectura.
7. Prefiere fixes pequenos y verificables sobre refactors amplios.
8. No generes artefactos binarios versionables salvo que el usuario lo pida.

## Comandos de verificacion

Usa los comandos aplicables al cambio:

```powershell
dotnet build AutoInventario.sln -c Debug
dotnet build AutoInventario.csproj -c Debug
dotnet build AutoInventario.Updater\AutoInventario.Updater.csproj -c Debug
dotnet build Webhook\Webhook-Inventario.csproj -c Debug
dotnet test AutoInventario.Tests\AutoInventario.Tests.csproj -c Debug
dotnet test Webhook.Tests\Webhook.Tests.csproj -c Debug
python -m py_compile Lambda-Inventario\lambda_function.py
terraform -chdir=Infraestructura-Terraform fmt -check -recursive
terraform -chdir=Infraestructura-Terraform validate -no-color
dotnet list AutoInventario.csproj package --vulnerable --include-transitive
dotnet list Webhook\Webhook-Inventario.csproj package --vulnerable --include-transitive
```

Si un comando falla por un problema preexistente, documenta el fallo exacto y evita ocultarlo.

## Seguridad obligatoria

- Secretos permitidos solo en gestores externos: AWS Secrets Manager, Azure DevOps secret variables, Secure Files, variables de entorno locales o store seguro del sistema.
- Nunca commitear: `private.key`, `secrets.json`, tokens ManageEngine, AWS access keys, PFX, passwords, connection strings reales.
- Redactar logs de API keys, payloads, BitLocker recovery passwords, datos personales y credenciales.
- Para cambios de cifrado, preferir AES-GCM o autenticacion adicional del payload.
- Para endpoints protegidos, incluir pruebas de acceso autorizado y no autorizado.

## Estructura canonica esperada

La estructura canonica del agente es la raiz del repositorio.

```text
AutoInventario.csproj
Program.cs
ConfigForm.cs
Helpers/
Models/
Resources/
Services/
AutoInventario.Updater/
AutoInventario.Tests/
Webhook/
Lambda-Inventario/
Infraestructura-Terraform/
docs/
```

## Checklist antes de entregar

- `git diff` revisado.
- Build/test relevante ejecutado o bloqueo documentado.
- No se han agregado secretos.
- Documentacion actualizada si cambio comportamiento.
- Hallazgos de seguridad comunicados sin exponer valores.
- Archivos generados innecesarios no incluidos.

## Plantilla de resumen final

```text
Cambios:
- ...

Verificacion:
- ...

Riesgos o pendientes:
- ...
```
