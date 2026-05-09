# Webhook-Inventario

API ASP.NET Core que recibe inventario cifrado desde el agente AutoInventario, expone clientes de ManageEngine y publica manifiestos de actualizacion.

## Requisitos

- .NET SDK/runtime 6 para el estado actual del proyecto.
- PostgreSQL accesible para `GET /id-clients`.
- AWS credentials por IAM Role, perfil o variables de entorno para invocar Lambda.
- Clave privada fuera del repositorio, referenciada por `Security:PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH`.

Nota: `net6.0` esta fuera de soporte. La migracion a `net8.0` o LTS vigente debe tratarse como prioridad.

## Configuracion

Claves esperadas:

| Clave | Uso |
| --- | --- |
| `ConnectionStrings:Postgres` | Conexion de lectura para listar clientes. |
| `AutoInventario:ApiKey` | Valor esperado por `X-AutoInventario-Key` en rutas protegidas. |
| `Security:PrivateKeyPath` | Ruta de clave privada local para descifrado. |
| `WEBHOOK_PRIVATE_KEY_PATH` | Override por variable de entorno. |
| `AWS_REGION` | Region AWS para invocar Lambda. |
| `AUTOINVENTARIO_LAMBDA_NAME` | Nombre de la Lambda de inventario. |

No guardar valores reales en `appsettings.json`.

## Endpoints

| Metodo | Ruta | Descripcion |
| --- | --- | --- |
| `GET` | `/` | Pagina de estado del servicio. |
| `GET` | `/id-clients` | Lista clientes desde PostgreSQL. Requiere `X-AutoInventario-Key`. |
| `POST` | `/webhooks` | Recibe payload cifrado del agente. |
| `GET` | `/updates/latest.json` | Devuelve version, URLs y hashes para autoupdate. |
| `GET` | `/updates/...` | Sirve binarios publicados. |

## Build y ejecucion

```powershell
dotnet build Webhook\Webhook-Inventario.csproj -c Debug
dotnet run --project Webhook\Webhook-Inventario.csproj
```

Validacion auditada: compila con avisos de framework sin soporte y anotaciones nullable fuera de contexto.

## Riesgos actuales

- El controlador de `/webhooks` tiene comentada la comparacion de API key.
- El middleware solo protege `/id-clients`; la proteccion de `/updates` esta comentada.
- Hay logs que pueden incluir API keys o payloads.
- La invocacion Lambda no debe usar credenciales hard-codeadas.
- `appsettings.json` local contiene secretos y debe limpiarse/rotarse.
