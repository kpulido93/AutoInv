# Webhook-Inventario

API ASP.NET Core que recibe inventario cifrado desde el agente AutoInventario, expone clientes de ManageEngine y publica manifiestos de actualizacion.

## Requisitos

- .NET SDK/runtime 8.
- PostgreSQL accesible para `GET /id-clients`.
- AWS credentials por IAM Role, perfil o variables de entorno solo si `InventoryProcessing:Mode=AwsLambda`.
- Clave privada fuera del repositorio, referenciada por `Security:PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH`.

El proyecto compila en `net8.0`. Mantener dependencias actualizadas y validar vulnerabilidades NuGet antes de publicar.

## Configuracion

Claves esperadas:

| Clave | Uso |
| --- | --- |
| `ConnectionStrings:Postgres` | Conexion de lectura para listar clientes. |
| `AutoInventario:ApiKey` | Valor esperado por `X-AutoInventario-Key` en rutas protegidas. |
| `Security:PrivateKeyPath` | Ruta de clave privada local para descifrado. |
| `WEBHOOK_PRIVATE_KEY_PATH` | Override por variable de entorno. |
| `InventoryProcessing:Mode` | `Local` para aceptar y almacenar localmente; `AwsLambda` para invocar Lambda. |
| `InventoryProcessing:AwsLambda:FunctionName` | Nombre de la Lambda cuando el modo es `AwsLambda`. |
| `InventoryProcessing:AwsLambda:Region` | Region AWS opcional para el cliente Lambda. Si se omite, se usa la cadena de configuracion del SDK. |
| `ManageEngine:BaseUrl` | URL base del ServiceDesk Plus local o cloud cuando `InventoryProcessing:Mode=Local`. |
| `ManageEngine:WorkstationsPath` | Ruta del endpoint de workstations. Por defecto `/api/v3/workstations`. |
| `ManageEngine:ApiTokenSecretName` | Nombre de la clave donde se busca el token, por ejemplo una variable de entorno. |
| `ManageEngine:TimeoutSeconds` | Timeout HTTP para el conector local. |
| `License:Edition` | `Community/Internal`, `Professional` o `Enterprise`. |
| `License:LicenseFilePath` | Ruta del archivo de licencia offline. Obligatoria para ediciones comerciales. |
| `License:PublicKeyPath` | Ruta de la clave publica para validar licencias firmadas. |
| `License:AllowUnsignedDevelopmentLicense` | Permite licencias fake `algorithm=none` solo en `Community/Internal`. |
| `AWS_REGION` / `AWS_DEFAULT_REGION` | Region usada por el SDK si no se configura `InventoryProcessing:AwsLambda:Region`. |
| `AUTOINVENTARIO_LAMBDA_NAME` | Fallback temporal para clientes/despliegues antiguos que aun no usan `InventoryProcessing:AwsLambda:FunctionName`. |

No guardar valores reales en `appsettings.json`.

En despliegues y desarrollo local, establecer `AutoInventario__ApiKey` mediante variable de entorno, secret store o variable secreta del pipeline.

## Procesamiento de inventario

El Webhook selecciona el destino de procesamiento con `InventoryProcessing:Mode`:

- `Local`: modo por defecto. El Webhook valida, descifra, normaliza y envia el inventario al conector `IAssetConnector` configurado sin requerir AWS.
- `AwsLambda`: invoca la Lambda configurada con `InventoryProcessing:AwsLambda:FunctionName`. Las credenciales se resuelven con la AWS SDK credential chain; usar IAM Role, perfil local, variables de entorno seguras o secret store, nunca access keys hard-codeadas.

Ejemplo con variables de entorno:

```powershell
$env:InventoryProcessing__Mode = "AwsLambda"
$env:InventoryProcessing__AwsLambda__FunctionName = "<lambda-name>"
$env:InventoryProcessing__AwsLambda__Region = "<aws-region>"
```

Para desarrollo sin AWS:

```powershell
$env:InventoryProcessing__Mode = "Local"
$env:ManageEngine__BaseUrl = "<manageengine-base-url>"
$env:MANAGEENGINE_API_TOKEN = "<secret-token>"
```

El proveedor de secretos local busca `Secrets:<ManageEngine:ApiTokenSecretName>` en configuracion y, si no existe, una variable de entorno con ese nombre. No guardar tokens reales en archivos versionados.

## Licenciamiento

El modo por defecto es `Community/Internal` y puede ejecutarse sin archivo de licencia. Las ediciones `Professional` y `Enterprise` validan un archivo offline firmado con RSA-SHA256 usando `ILicenseValidator`.

Ejemplo Enterprise:

```powershell
$env:License__Edition = "Enterprise"
$env:License__LicenseFilePath = "C:\ProgramData\AutoInventario\license.json"
$env:License__PublicKeyPath = "C:\ProgramData\AutoInventario\license-public.pem"
```

No hay llamadas externas para validar licencias. Ver `docs/PRODUCT.md` y `docs/ONBOARDING-CORPORATE.md`.

## Autenticacion

Rutas protegidas por API key:

- `POST /webhooks`
- `GET /id-clients`

Header requerido:

- `X-AutoInventario-Key: <api-key>`

Compatibilidad temporal:

- `x-api-key` tambien se acepta para clientes antiguos del agente.

Comportamiento:

- `401 Unauthorized` si falta credencial.
- `403 Forbidden` si la credencial es invalida.
- `200 OK` si `/webhooks` recibe credencial valida y procesa correctamente el payload.
- `400 Bad Request` si la credencial es valida pero el payload cifrado no tiene el formato esperado.

Todas las respuestas incluyen `X-Correlation-ID`. Si el cliente envia ese header se conserva; si no, el servidor genera uno. No se registran API keys, tokens, claves ni payloads completos.

## Endpoints

| Metodo | Ruta | Descripcion |
| --- | --- | --- |
| `GET` | `/` | Pagina de estado del servicio. |
| `GET` | `/health` | Health check para contenedores, IIS y balanceadores. |
| `GET` | `/id-clients` | Lista clientes desde PostgreSQL. Requiere `X-AutoInventario-Key`. |
| `POST` | `/webhooks` | Recibe payload cifrado del agente. Requiere `X-AutoInventario-Key` o `x-api-key`. |
| `GET` | `/updates/latest.json` | Devuelve version, URLs y hashes para autoupdate. |
| `GET` | `/updates/...` | Sirve binarios publicados. |

## Build y ejecucion

```powershell
dotnet build Webhook\Webhook-Inventario.csproj -c Debug
dotnet list Webhook\Webhook-Inventario.csproj package --vulnerable --include-transitive
dotnet test Webhook.Tests\Webhook.Tests.csproj -c Debug
dotnet run --project Webhook\Webhook-Inventario.csproj
```

Validacion esperada: compila sin warning `NETSDK1138` y sin vulnerabilidades NuGet reportadas por los orígenes configurados.

Para despliegue Docker Compose, IIS o Windows Service, ver `docs/DEPLOYMENT-ONPREM.md`.

## Pruebas manuales

Sin credencial:

```powershell
Invoke-WebRequest http://localhost:<puerto>/webhooks -Method Post -Body '{}' -ContentType 'application/json'
```

Credencial invalida:

```powershell
Invoke-WebRequest http://localhost:<puerto>/webhooks -Method Post -Body '{}' -ContentType 'application/json' -Headers @{ "X-AutoInventario-Key" = "invalid" }
```

Credencial valida:

```powershell
Invoke-WebRequest http://localhost:<puerto>/webhooks -Method Post -Body '<payload-cifrado>' -ContentType 'application/json' -Headers @{ "X-AutoInventario-Key" = "<api-key>"; "X-Correlation-ID" = "<correlation-id>" }
```

## Riesgos actuales

- `/updates` permanece publico para autoupdate; revisar si el entorno requiere proteccion adicional.
- La invocacion Lambda no debe usar credenciales hard-codeadas y solo debe activarse con `InventoryProcessing:Mode=AwsLambda`.
- `appsettings.json` local no debe versionarse; usar variables de entorno, secret store o una copia local ignorada por Git.
