# Deployment On-Prem

Este documento describe despliegues corporativos sin AWS para el Webhook de AutoInventario. No guardar secretos reales en este repositorio; usar `.env` local, variables protegidas del servidor, secret store corporativo o herramientas de gestion de secretos.

## Variables

| Variable | Uso | Secreta |
| --- | --- | --- |
| `ASPNETCORE_HTTP_PORTS` | Puerto HTTP interno del contenedor Docker. Default `8080`; se puede sobreescribir sin rebuild. | No |
| `ASPNETCORE_URLS` | URL de escucha para Kestrel o contenedor. | No |
| `AutoInventario__ApiKey` | API key requerida por `POST /webhooks` y `GET /id-clients`. | Si |
| `Security__PrivateKeyPath` / `WEBHOOK_PRIVATE_KEY_PATH` | Ruta de la clave privada RSA fuera del repo. | Sensible |
| `InventoryProcessing__Mode` | `Local` para el stub inicial sin AWS; `AwsLambda` solo si se usa AWS. | No |
| `ManageEngine__BaseUrl` | Reservado para una futura sincronizacion local con ManageEngine. | No |
| `ManageEngine__WorkstationsPath` | Reservado para la futura sincronizacion local. | No |
| `ManageEngine__ApiTokenSecretName` | Reservado para la futura sincronizacion local. | No |
| `MANAGEENGINE_API_TOKEN` | Token real si se habilita una futura sincronizacion local. | Si |
| `ManageEngine__TimeoutSeconds` | Timeout previsto para la futura sincronizacion local. | No |
| `License__Edition` | `Community/Internal`, `Professional` o `Enterprise`. | No |
| `License__LicenseFilePath` | Ruta local del archivo de licencia offline. | Sensible |
| `License__PublicKeyPath` | Ruta local de la clave publica de verificacion. | No |
| `License__AllowUnsignedDevelopmentLicense` | Permite licencia fake solo en `Community/Internal`. | No |
| `LICENSE_EDITION`, `LICENSE_FILE_PATH`, `LICENSE_PUBLIC_KEY_PATH` | Equivalentes usados por `docker-compose.yml`. | Segun path |
| `ConnectionStrings__Postgres` | Conexion para `GET /id-clients`. | Si |

El endpoint de salud es:

```text
GET /health
```

## Imagen Docker

El `Webhook/Dockerfile` usa cuatro etapas explicitas: `restore`, `build`, `publish` y `runtime`, todas alineadas con .NET 8.

Build recomendado:

```powershell
docker build -f Webhook\Dockerfile -t autoinventario-webhook:local .
```

La primera construccion necesita acceso de red a `mcr.microsoft.com` para descargar `mcr.microsoft.com/dotnet/sdk:8.0` y `mcr.microsoft.com/dotnet/aspnet:8.0` si no estan cacheadas en el host.

La imagen no copia `appsettings.json` reales y espera configuracion por variables de entorno o archivos montados fuera del repo. El contenedor escucha por defecto en `8080`; para cambiar el puerto interno sin rebuild, sobreescribir `ASPNETCORE_HTTP_PORTS`.

## Docker Compose

1. Crear configuracion local ignorada por Git:

```powershell
Copy-Item .env.example .env
```

2. Editar `.env` con valores del entorno. No imprimir ni compartir el contenido si contiene secretos.

3. Colocar la clave privada fuera del repo o en `.\secrets\private.key`. La ruta se configura con `WEBHOOK_PRIVATE_KEY_PATH`.

4. Validar la configuracion:

```powershell
docker compose --env-file .env.example config
```

Para entornos reales usar `.env`, pero no copiar la salida de `docker compose config` en tickets o reportes porque expande variables.

5. Construir y levantar:

```powershell
docker build -f Webhook\Dockerfile -t autoinventario-webhook:local .
docker compose --env-file .env up -d --build
```

6. Comprobar salud:

```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

El `docker-compose.yml` incluye:

- `webhook`: ASP.NET Core `net8.0` con stub local inicial.
- `postgres`: base PostgreSQL para PoC de `/id-clients`.
- Health checks para Webhook y PostgreSQL.
- Volumen para `wwwroot/updates`.

En produccion se puede sustituir `postgres` por una base corporativa y configurar `ConnectionStrings__Postgres` directamente.

## IIS en Windows Server

Prerequisitos:

- Windows Server con IIS.
- .NET 8 Hosting Bundle instalado.
- Publicacion del Webhook generada fuera del repo o en una carpeta de despliegue.
- Secretos configurados como variables protegidas, store corporativo o archivos con ACL restringidas.

Publicar:

```powershell
dotnet publish Webhook\Webhook-Inventario.csproj -c Release -o C:\inetpub\AutoInventarioWebhook
```

Instalar o actualizar sitio IIS:

```powershell
.\deploy\windows\install-webhook-iis.ps1 `
  -PublishPath C:\inetpub\AutoInventarioWebhook `
  -SiteName AutoInventarioWebhook `
  -AppPoolName AutoInventarioWebhook `
  -Port 8080
```

El script crea o actualiza el App Pool y el sitio, pero no escribe secretos. Configurar antes de recibir trafico:

- `AutoInventario__ApiKey`
- `Security__PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH`
- `InventoryProcessing__Mode=Local`
- `ManageEngine__BaseUrl` y `ManageEngine__ApiTokenSecretName` solo si se prepara una futura sincronizacion local
- `ConnectionStrings__Postgres`, si se usa `/id-clients`
- `License__Edition=Enterprise`, `License__LicenseFilePath` y `License__PublicKeyPath` si el despliegue es comercial.

Comprobar:

```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
```

## Windows Service

Usar esta opcion cuando no se quiere exponer el Webhook mediante IIS y se prefiere Kestrel como servicio Windows.

Publicar:

```powershell
dotnet publish Webhook\Webhook-Inventario.csproj -c Release -o C:\Services\AutoInventarioWebhook
```

Crear o actualizar servicio:

```powershell
.\deploy\windows\install-worker-service.ps1 `
  -PublishPath C:\Services\AutoInventarioWebhook `
  -ServiceName AutoInventarioWebhook `
  -Urls http://+:8080 `
  -InventoryProcessingMode Local
```

El script registra variables no secretas en el entorno del servicio. Los secretos deben configurarse fuera del script con el mecanismo corporativo elegido. Reiniciar el servicio despues de cambiar variables de entorno.

## Validacion Recomendada

```powershell
dotnet publish Webhook\Webhook-Inventario.csproj -c Release
docker compose --env-file .env.example config
docker build -f Webhook\Dockerfile -t autoinventario-webhook:local .
```

Para una prueba funcional local con Docker, crear `.env` desde `.env.example`, montar una clave privada de prueba y enviar un payload cifrado valido a `POST /webhooks` con `X-AutoInventario-Key`.
