# Webhook-Inventario

API ASP.NET Core que recibe inventario cifrado desde el agente AutoInventario, expone clientes de ManageEngine y publica manifiestos de actualizacion.

## Requisitos

- .NET SDK/runtime 8.
- PostgreSQL accesible para `GET /id-clients`.
- AWS credentials por IAM Role, perfil o variables de entorno solo si `InventoryProcessing:Mode=AwsLambda`.
- Clave privada fuera del repositorio, referenciada por `Security:PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH`.

El proyecto compila en `net8.0`. Mantener dependencias actualizadas y validar vulnerabilidades NuGet antes de publicar.

## Compatibilidad .NET

- El proyecto canonico del servicio es `Webhook/Webhook-Inventario.csproj` y apunta a `net8.0`.
- `Program.cs` mantiene el patron `Startup` sobre ASP.NET Core 8 para conservar endpoints, middleware y configuracion sin cambiar contratos.
- La configuracion sensible se centraliza con Options Pattern; `Program.cs` concentra los overrides de variables de entorno legadas y `Startup` valida la configuracion critica al iniciar.
- `Dockerfile` usa `mcr.microsoft.com/dotnet/sdk:8.0` y `mcr.microsoft.com/dotnet/aspnet:8.0`, alineado con el runtime soportado.

## Configuracion

Claves esperadas:

| Clave | Uso |
| --- | --- |
| `ConnectionStrings:Postgres` | Conexion de lectura para listar clientes. |
| `AutoInventario:ApiKey` | Valor esperado por `X-AutoInventario-Key` en rutas protegidas. |
| `AutoInventario:VerifyToken` | Token esperado por `GET /webhooks` para la verificacion inicial del proveedor. |
| `Security:PrivateKeyPath` | Ruta de clave privada local para descifrado. |
| `WEBHOOK_PRIVATE_KEY_PATH` | Override por variable de entorno. |
| `InventoryProcessing:Mode` | `Local` para levantar el Webhook sin AWS usando el stub inicial; `AwsLambda` para invocar Lambda y mantener el flujo actual. |
| `InventoryProcessing:AwsLambda:FunctionName` | Nombre de la Lambda cuando el modo es `AwsLambda`. |
| `InventoryProcessing:AwsLambda:Region` | Region AWS opcional para el cliente Lambda. Si se omite, se usa la cadena de configuracion del SDK. |
| `ManageEngine:BaseUrl` | Reservado para una futura sincronizacion local con ManageEngine. El stub actual no lo usa. |
| `ManageEngine:WorkstationsPath` | Reservado para la futura sincronizacion local. Por defecto `/api/v3/workstations`. |
| `ManageEngine:ApiTokenSecretName` | Reservado para la futura sincronizacion local. |
| `ManageEngine:TimeoutSeconds` | Timeout previsto para la futura sincronizacion local. |
| `License:Edition` | `Community/Internal`, `Professional` o `Enterprise`. |
| `License:LicenseFilePath` | Ruta del archivo de licencia offline. Obligatoria para ediciones comerciales. |
| `License:PublicKeyPath` | Ruta de la clave publica para validar licencias firmadas. |
| `License:AllowUnsignedDevelopmentLicense` | Permite licencias fake `algorithm=none` solo en `Community/Internal`. |
| `AWS_REGION` / `AWS_DEFAULT_REGION` | Region usada por el SDK si no se configura `InventoryProcessing:AwsLambda:Region`. |
| `AUTOINVENTARIO_LAMBDA_NAME` | Fallback temporal para clientes/despliegues antiguos que aun no usan `InventoryProcessing:AwsLambda:FunctionName`. |

No guardar valores reales en `appsettings.json`.

Opciones tipadas actuales:

- `AutoInventarioOptions`: API key y verify token.
- `SecurityOptions`: ruta efectiva de la clave privada.
- `ConnectionStringsOptions`: cadena `Postgres`.
- `InventoryProcessingOptions` y `AwsOptions`: modo de procesamiento, Lambda name y region.

Variables de entorno recomendadas:

- `ConnectionStrings__Postgres`
- `AutoInventario__ApiKey`
- `AutoInventario__VerifyToken`
- `Security__PrivateKeyPath`
- `InventoryProcessing__Mode`
- `InventoryProcessing__AwsLambda__FunctionName`
- `InventoryProcessing__AwsLambda__Region`

Compatibilidad legada mantenida:

- `WEBHOOK_PRIVATE_KEY_PATH` sigue sobreescribiendo `Security:PrivateKeyPath`.
- `AUTOINVENTARIO_LAMBDA_NAME` sigue poblando `InventoryProcessing:AwsLambda:FunctionName`.
- `AWS_REGION` y `AWS_DEFAULT_REGION` siguen poblando `InventoryProcessing:AwsLambda:Region`.

Regla de credenciales AWS:

- `InventoryProcessing:AwsLambda` solo admite `FunctionName` y `Region`; no se aceptan `AccessKeyId`, `SecretAccessKey` ni `SessionToken` en `appsettings` o configuracion inline.
- En AWS, desplegar con IAM Role, instance profile o task role para que el SDK resuelva credenciales sin secretos en el Webhook.
- Fuera de AWS, usar `AWS_PROFILE` u otro mecanismo estandar soportado por la default credential chain del AWS SDK en el host, nunca credenciales embebidas en JSON o codigo.

Validacion en arranque:

- El servicio falla temprano si faltan `AutoInventario:ApiKey`, `ConnectionStrings:Postgres` o una ruta valida de `Security:PrivateKeyPath`.
- Si `InventoryProcessing:Mode=AwsLambda`, tambien exige `InventoryProcessing:AwsLambda:FunctionName`.
- Si no se configura `Security:PrivateKeyPath`, el servicio intenta `%ProgramData%\mrbyte\Webhook\private.key` antes de validar.

En despliegues y desarrollo local, establecer `AutoInventario__ApiKey` mediante variable de entorno, secret store o variable secreta del pipeline.

## Procesamiento de inventario

El Webhook selecciona el destino de procesamiento con `InventoryProcessing:Mode`:

- `AwsLambda`: modo por defecto temporal para preservar el comportamiento actual. Invoca la Lambda configurada con `InventoryProcessing:AwsLambda:FunctionName`. Las credenciales se resuelven con la AWS SDK credential chain; usar IAM Role, perfil local, variables de entorno seguras o secret store, nunca access keys hard-codeadas.
- `Local`: acepta el payload ya descifrado y responde con un mensaje explicito de stub. No sincroniza todavia con ManageEngine y no requiere AWS.

Ejemplo con variables de entorno:

```powershell
$env:InventoryProcessing__Mode = "AwsLambda"
$env:InventoryProcessing__AwsLambda__FunctionName = "<lambda-name>"
$env:InventoryProcessing__AwsLambda__Region = "<aws-region>"
```

Para desarrollo sin AWS:

```powershell
$env:InventoryProcessing__Mode = "Local"
```

El ejemplo versionado deja `InventoryProcessing:Mode=Local` para facilitar un arranque inicial sin AWS. Si se omite el modo por completo, el runtime conserva `AwsLambda` como default temporal por compatibilidad.

La futura sincronizacion local con ManageEngine seguira usando `Secrets:<ManageEngine:ApiTokenSecretName>` o una variable de entorno con ese nombre. No guardar tokens reales en archivos versionados.

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

Ruta no protegida por API key:

- `GET /webhooks` para la verificacion inicial del proveedor; usa `hub.verify_token` y se valida contra `AutoInventario:VerifyToken`.

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
dotnet restore Webhook\Webhook-Inventario.csproj
dotnet build Webhook\Webhook-Inventario.csproj -c Debug
dotnet list Webhook\Webhook-Inventario.csproj package --vulnerable --include-transitive
dotnet test Webhook.Tests\Webhook.Tests.csproj -c Debug
dotnet run --project Webhook\Webhook-Inventario.csproj
```

Validacion esperada: `restore` y `build` finalizan correctamente en `net8.0`, sin warning `NETSDK1138`, y el scan NuGet no reporta vulnerabilidades activas en los origenes configurados.

Para despliegue Docker Compose, IIS o Windows Service, ver `docs/DEPLOYMENT-ONPREM.md`.

## Pruebas manuales

Sin credencial:

```powershell
Invoke-WebRequest http://localhost:<puerto>/webhooks -Method Post -Body '{}' -ContentType 'application/json'
```

Resultado esperado: `401 Unauthorized`.

Credencial invalida:

```powershell
Invoke-WebRequest http://localhost:<puerto>/webhooks -Method Post -Body '{}' -ContentType 'application/json' -Headers @{ "X-AutoInventario-Key" = "invalid" }
```

Resultado esperado: `403 Forbidden`.

Credencial valida:

```powershell
Invoke-WebRequest http://localhost:<puerto>/webhooks -Method Post -Body '<payload-cifrado>' -ContentType 'application/json' -Headers @{ "X-AutoInventario-Key" = "<api-key>"; "X-Correlation-ID" = "<correlation-id>" }
```

Verificacion GET del proveedor:

```powershell
Invoke-WebRequest "http://localhost:<puerto>/webhooks?hub.mode=subscribe&hub.challenge=123&hub.verify_token=<verify-token>"
```

## Riesgos actuales

- `/updates` permanece publico para autoupdate; revisar si el entorno requiere proteccion adicional.
- La invocacion Lambda no debe usar credenciales hard-codeadas y solo debe activarse con `InventoryProcessing:Mode=AwsLambda`.
- `appsettings.json` local no debe versionarse; usar variables de entorno, secret store o una copia local ignorada por Git.
