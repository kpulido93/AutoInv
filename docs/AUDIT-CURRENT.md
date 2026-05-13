# Auditoria reproducible del estado actual

Fecha: 2026-05-12
Rama observada: `dev`
Commit observado: `aca87cd`
Alcance: auditoria local de build, test, Terraform, vulnerabilidades NuGet y presencia de secretos por patrones. No se hicieron correcciones funcionales.

## Estado Git

Comando inicial ejecutado:

```powershell
git status --short --branch
```

Resultado:

```text
## dev...origin/dev [ahead 1]
```

## Entorno observado

| Herramienta | Version observada |
| --- | --- |
| .NET SDK | `9.0.313` |
| Python | `Python 3.12.4` |
| Terraform | `v1.14.2 on windows_386` |

## Comandos ejecutados

| Comando | Resultado exacto | Error clave | Severidad | Recomendacion |
| --- | --- | --- | --- | --- |
| `dotnet build AutoInventario.sln -c Debug` | Falla, exit code `1`. | `MSB3202: No se encuentra el archivo del proyecto "D:\repos\AutoInventario\AutoInventario\AutoInventario.csproj"`. | Critica | Corregir la ruta del proyecto en la solucion o restaurar la estructura esperada antes de cualquier release. |
| `dotnet build AutoInventario.csproj -c Debug` | Falla, exit code `1`; emite `2 Advertencia(s)` y `1 Errores`. | `MSB3822: Los recursos que no son de cadena requieren el ensamblado System.Resources.Extensions en tiempo de ejecucion`. Tambien aparece `NU1603` para `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.3`, resuelto como `8.0.4`. | Critica | Resolver la configuracion de recursos/referencias y fijar una version de paquete resoluble. |
| `dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c Debug` | Correcto, exit code `0`; `0 Advertencia(s)`, `0 Errores`. | No aplica. | Baja | Mantener en matriz de build. |
| `dotnet build Webhook/Webhook-Inventario.csproj -c Debug` | Correcto, exit code `0`; `5 Advertencia(s)`, `0 Errores`. | `NETSDK1138` por `net6.0` fuera de soporte; `CS8632` por anotaciones nullable fuera de contexto nullable. | Alta | Migrar el Webhook a una version LTS soportada y normalizar nullable. |
| `dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj -c Debug` | Correcto, exit code `0`; `Superado: 1`, `Total: 1`. | Se omite `D:\repos\AutoInventario\Autoinventario\AutoInventario.csproj` porque no existe; `MSB9008` por ProjectReference inexistente. | Alta | Corregir el ProjectReference y reemplazar el test placeholder por cobertura de comportamiento real. |
| `python -m py_compile Lambda-Inventario/lambda_function.py` | Correcto, exit code `0`, sin salida. | No aplica. | Baja | Mantener esta validacion en auditoria local/CI. |
| `terraform -chdir=Infraestructura-Terraform fmt -check -recursive` | Falla, exit code `3` en el script local. | Salida: `main.tf`. | Media | Ejecutar `terraform fmt -recursive` en un commit de remediacion posterior. |
| `terraform -chdir=Infraestructura-Terraform validate -no-color` | Falla, exit code `1`. | `Missing required provider`; requiere `registry.terraform.io/hashicorp/aws` y sugiere `terraform init`. | Media | Ejecutar `terraform init` antes de validar; despues corregir errores semanticos si aparecen. |
| `dotnet list AutoInventario.csproj package --vulnerable --include-transitive` | Correcto, exit code `0`, con vulnerabilidad reportada. | `Microsoft.Extensions.Caching.Memory 8.0.0`, gravedad `High`, `GHSA-qj66-m88j-hmgj`. | Alta | Actualizar dependencias directas/transitivas hasta resolver el advisory. |
| `dotnet list Webhook/Webhook-Inventario.csproj package --vulnerable --include-transitive` | Correcto, exit code `0`, con vulnerabilidades reportadas. | `System.Text.Json 8.0.0`, gravedad `High`, `GHSA-hh2w-p6rv-4g7w` y `GHSA-8g4q-xg66-9fp4`. | Alta | Actualizar framework/paquetes para resolver ambos advisories. |

## Bloqueadores por severidad

### Criticos

- La solucion no compila porque referencia `AutoInventario/AutoInventario.csproj`, que no existe en el arbol actual.
- El proyecto raiz del agente no compila por `MSB3822`.
- Hay material secreto o archivos sensibles presentes en el arbol local ignorado por Git; ver `docs/SECURITY-FINDINGS.md`.
- La validacion de API key del endpoint de Webhook esta comentada en `Webhook/Controllers/WebhookController.cs`.

### Altos

- Webhook compila sobre `net6.0`, plataforma sin soporte de seguridad.
- Hay vulnerabilidades NuGet `High` en agente y Webhook.
- Los tests pasan solo un test placeholder y omiten el proyecto del agente por referencia inexistente.
- El Webhook registra la API key recibida en logs, segun indicador en `Webhook/Controllers/WebhookController.cs`.

### Medios

- Terraform no pasa `fmt -check`.
- Terraform `validate` requiere `terraform init` antes de poder validar.
- El agente tiene warning `NU1603` por version de paquete no encontrada.
- Webhook emite warnings nullable `CS8632`.

### Bajos

- Updater compila correctamente.
- `py_compile` de Lambda es correcto.
- El SDK .NET informa actualizaciones de workload disponibles, sin impacto directo en esta auditoria.

## Reproduccion local

Windows:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/audit-local.ps1
```

Linux o WSL:

```bash
bash scripts/audit-local.sh
```

Los scripts continuan aunque un comando falle y devuelven exit code distinto de cero si hay checks fallidos. Tambien ejecutan un escaneo de secretos que reporta solo ruta, tipo, alcance y severidad; no imprime valores.

## Verificacion de scripts

| Script | Resultado | Nota |
| --- | --- | --- |
| `powershell -ExecutionPolicy Bypass -File scripts/audit-local.ps1` | Ejecutado; exit code `1`. | El script completo termina por los bloqueadores conocidos: build de solucion, build de agente, `terraform fmt` y `terraform validate`. |
| `bash scripts/audit-local.sh` | No ejecutable con el `bash` del PATH en este host. | El lanzador WSL intento `/bin/bash`, pero la distribucion disponible no lo tenia. |
| `C:\Program Files\Git\bin\bash.exe scripts/audit-local.sh` | Ejecutado; exit code `1`. | Misma lista de bloqueadores conocidos; no fallo por error del script. |
