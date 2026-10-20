# Deployment IIS / Windows Server

Guia de despliegue del Webhook de AutoInventario en IIS para entornos corporativos sin dependencia obligatoria de AWS.

## Alcance

Este flujo cubre:

- Publicacion del Webhook ASP.NET Core `net8.0`.
- Hosting en IIS con `AspNetCoreModuleV2`.
- Configuracion por variables de entorno y archivos protegidos fuera del repositorio.
- Modo `InventoryProcessing__Mode=Local` como baseline on-prem.

No cubre:

- Provision automatizada de certificados publicos.
- Integracion AWS obligatoria.
- Provision de PostgreSQL, salvo la configuracion necesaria para `/id-clients`.

## Requisitos

- Windows Server 2019, 2022 o superior con parches vigentes.
- IIS con `Web Server` y `Management Tools`.
- Hosting Bundle de ASP.NET Core .NET 8 instalado en el servidor.
- Publicacion generada con `dotnet publish`.
- Un Application Pool dedicado, preferiblemente con `ApplicationPoolIdentity` o una identidad de servicio corporativa.
- Certificado TLS instalado en el store del servidor si el sitio se expone por HTTPS.
- Clave privada RSA, licencia offline y otros secretos fuera del repositorio, con ACL restringidas.

## Variables y archivos sensibles

Configurar fuera de Git y fuera del publish:

- `AutoInventario__ApiKey`
- `AutoInventario__VerifyToken`
- `Security__PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH`
- `SecretProvider__Mode=Environment`
- `InventoryProcessing__Mode=Local`
- `ConnectionStrings__Postgres`
- `License__Edition`
- `License__LicenseFilePath`
- `License__PublicKeyPath`
- `License__AllowUnsignedDevelopmentLicense`

Notas operativas:

- `ConnectionStrings__Postgres` sigue siendo obligatoria al iniciar por validacion temprana. Si la instalacion no va a usar `GET /id-clients`, puede dejarse un placeholder no vacio para la PoC, pero un despliegue corporativo debe apuntar a una base real antes de habilitar ese endpoint.
- Para el modo `Local` actual no hace falta configurar ManageEngine todavia; el procesador local es un stub que acepta el payload y responde sin sincronizacion externa.
- El proveedor recomendado es `SecretProvider__Mode=Environment`. El modo `File` queda solo para desarrollo local controlado.

## Estructura recomendada en servidor

Ejemplo:

```text
C:\inetpub\AutoInventarioWebhook\
C:\ProgramData\AutoInventario\
  private.key
  license.json
  license-public.pem
```

Buenas practicas:

- El directorio publicado bajo `C:\inetpub\...` solo debe contener binarios y contenido publico del sitio.
- La clave privada, licencias y cualquier otro secreto deben vivir en `C:\ProgramData\AutoInventario\` u otra ruta con ACL restringidas.
- Conceder acceso minimo al App Pool o a la identidad de servicio sobre esos archivos sensibles.

## Publish

Desde el repositorio:

```powershell
dotnet publish Webhook\Webhook-Inventario.csproj -c Release -o C:\inetpub\AutoInventarioWebhook
```

Validacion minima posterior:

```powershell
Get-ChildItem C:\inetpub\AutoInventarioWebhook
Test-Path C:\inetpub\AutoInventarioWebhook\Webhook-Inventario.dll
```

## Instalacion con script idempotente

El repositorio incluye un script idempotente para crear o actualizar App Pool y sitio IIS:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\windows\install-webhook-iis.ps1 `
  -PublishPath C:\inetpub\AutoInventarioWebhook `
  -SiteName AutoInventarioWebhook `
  -AppPoolName AutoInventarioWebhook `
  -Port 8080
```

Opciones utiles:

- `-HostHeader webhook.interno.example`
- `-SkipHealthCheck`
- `-WhatIf` para revisar cambios antes de aplicarlos

Nota:

- El script valida privilegios de administrador al inicio. Incluso con `-WhatIf`, debe ejecutarse desde una PowerShell elevada.

El script:

- habilita IIS si falta el rol o la consola de administracion;
- comprueba que `Webhook-Inventario.dll` exista en el publish path;
- crea o actualiza el App Pool dedicado;
- crea o actualiza el sitio IIS apuntando al publish path;
- advierte si falta `AspNetCoreModuleV2`.

## Configuracion segura en IIS

Recomendaciones:

1. Usar HTTPS con un certificado corporativo instalado en `LocalMachine\My`.
2. Hacer el binding HTTPS en IIS y limitar HTTP a red interna o a una redirección controlada.
3. Ejecutar el App Pool con `No Managed Code`.
4. Mantener `ApplicationPoolIdentity` salvo que una politica corporativa exija otra identidad.
5. Aplicar ACL de solo lectura para el App Pool sobre:
   - el directorio publicado;
   - `private.key`;
   - `license.json` y `license-public.pem`, si aplican.
6. Configurar variables de entorno o el mecanismo corporativo de secretos fuera del repositorio.
7. No escribir secretos en `web.config`, `appsettings.json` ni scripts del repo.

## Configuracion de variables de entorno

Opciones comunes:

- Variables de entorno del sistema.
- Variables de entorno del App Pool o del servicio host segun la politica corporativa.
- Store corporativo o herramienta de configuracion que inyecte variables al proceso.

Ejemplo de baseline para una PoC interna:

```powershell
[Environment]::SetEnvironmentVariable("AutoInventario__ApiKey", "CHANGE_ME_WEBHOOK_API_KEY", "Machine")
[Environment]::SetEnvironmentVariable("AutoInventario__VerifyToken", "CHANGE_ME_WEBHOOK_VERIFY_TOKEN", "Machine")
[Environment]::SetEnvironmentVariable("Security__PrivateKeyPath", "C:\ProgramData\AutoInventario\private.key", "Machine")
[Environment]::SetEnvironmentVariable("SecretProvider__Mode", "Environment", "Machine")
[Environment]::SetEnvironmentVariable("InventoryProcessing__Mode", "Local", "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__Postgres", "Host=CHANGE_ME_DB_HOST;Port=5432;Database=CHANGE_ME_DB_NAME;Username=CHANGE_ME_DB_USER;Password=CHANGE_ME_DB_PASSWORD", "Machine")
```

Despues de cambiar variables de entorno de sistema o App Pool, reciclar el pool o reiniciar el sitio.

## TLS

Checklist:

- Instalar el certificado en el store correcto del servidor.
- Asociar el binding HTTPS en IIS al sitio del Webhook.
- Confirmar que el nombre del certificado coincida con el host header esperado.
- Probar reachability y cadena de confianza desde un cliente corporativo.

## Validacion posterior

Verificaciones sugeridas:

```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
Invoke-WebRequest "http://localhost:8080/webhooks?hub.mode=subscribe&hub.challenge=123&hub.verify_token=CHANGE_ME_WEBHOOK_VERIFY_TOKEN" -UseBasicParsing
```

Si el sitio publica HTTPS:

```powershell
Invoke-WebRequest https://webhook.interno.example/health -UseBasicParsing
```

Resultado esperado:

- `/health` responde `200`.
- `GET /webhooks` responde `200` con `hub.challenge` cuando el verify token coincide.
- `POST /webhooks` sin API key responde `401`.

## Rollback basico

Mantener al menos una carpeta de publish anterior, por ejemplo:

```text
C:\inetpub\releases\AutoInventarioWebhook-20260518-1\
C:\inetpub\releases\AutoInventarioWebhook-20260518-2\
```

Flujo sugerido:

1. Detener o reciclar el sitio/App Pool.
2. Reapuntar el sitio IIS al publish path anterior o restaurar el contenido previo.
3. Volver a iniciar el sitio.
4. Verificar `/health`.
5. Confirmar logs, API key y bindings TLS.

Si se usa el script del repo, el rollback se puede hacer reaplicandolo con el publish path anterior:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\windows\install-webhook-iis.ps1 `
  -PublishPath C:\inetpub\releases\AutoInventarioWebhook-20260518-1 `
  -SiteName AutoInventarioWebhook `
  -AppPoolName AutoInventarioWebhook `
  -Port 8080 `
  -SkipHealthCheck
```

## Riesgos conocidos

- El Webhook sigue validando `ConnectionStrings__Postgres` en startup aunque la PoC no use `/id-clients`.
- El modo `Local` actual no sincroniza todavia con ManageEngine.
- Si falta el Hosting Bundle de .NET 8, IIS no cargara `AspNetCoreModuleV2` correctamente.
