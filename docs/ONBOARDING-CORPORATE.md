# Onboarding Corporativo

Guia para preparar una instalacion self-hosted comercial de AutoInventario.

## Prerrequisitos

- Windows Server, IIS o contenedor corporativo.
- .NET 8 runtime/hosting bundle.
- PostgreSQL o base compatible para endpoints que lo requieran.
- URL interna para el Webhook.
- API key del Webhook almacenada como secreto.
- Clave privada RSA del Webhook fuera del repositorio.
- Archivo de licencia offline para `Professional` o `Enterprise`.
- Clave publica de verificacion de licencias distribuida por el proveedor.

## Flujo de alta

1. Definir edicion: `Community/Internal`, `Professional` o `Enterprise`.
2. Confirmar cantidad maxima de endpoints.
3. Emitir licencia offline con `customer`, `edition`, `expires_on` y `max_endpoints`.
4. Instalar Webhook y worker local siguiendo `docs/DEPLOYMENT-ONPREM.md`.
5. Configurar secretos mediante variables protegidas, store corporativo o mecanismo equivalente.
6. Configurar licenciamiento:

```powershell
$env:License__Edition = "Enterprise"
$env:License__LicenseFilePath = "C:\ProgramData\AutoInventario\license.json"
$env:License__PublicKeyPath = "C:\ProgramData\AutoInventario\license-public.pem"
```

7. Validar salud:

```powershell
Invoke-WebRequest http://localhost:8080/health -UseBasicParsing
dotnet test Webhook.Tests\Webhook.Tests.csproj -c Debug
```

## Prueba manual con licencia fake de desarrollo

Este ejemplo no usa secretos reales y solo aplica para `Community/Internal`.

```powershell
$payload = @{
  license_id = "dev-fake"
  customer = "Development"
  edition = "Community/Internal"
  expires_on = (Get-Date).AddDays(30).ToUniversalTime().ToString("o")
  max_endpoints = 25
} | ConvertTo-Json -Compress

$payloadBytes = [Text.Encoding]::UTF8.GetBytes($payload)
$payload64 = [Convert]::ToBase64String($payloadBytes).TrimEnd("=").Replace("+", "-").Replace("/", "_")

$license = @{
  algorithm = "none"
  payload = $payload64
  signature = ""
} | ConvertTo-Json

New-Item -ItemType Directory -Force C:\ProgramData\AutoInventario | Out-Null
$license | Set-Content C:\ProgramData\AutoInventario\license.dev.json -Encoding UTF8

$env:License__Edition = "Community/Internal"
$env:License__LicenseFilePath = "C:\ProgramData\AutoInventario\license.dev.json"
$env:License__AllowUnsignedDevelopmentLicense = "true"
```

No usar `algorithm=none` en `Professional` o `Enterprise`; el validador lo rechaza para ediciones comerciales.

## Checklist de entrega

- `AutoInventario__ApiKey` configurada fuera del repo.
- `Security__PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH` apunta a una clave privada con ACL restringidas.
- `InventoryProcessing__Mode=Local` si no se usa AWS.
- `ManageEngine__ApiTokenSecretName` apunta a un secreto real fuera del repo.
- `License__Edition` coincide con el archivo de licencia.
- `License__LicenseFilePath` apunta a un archivo local protegido.
- `License__PublicKeyPath` apunta a la clave publica de verificacion.
- `/health` responde correctamente.
- Logs revisados sin API keys, tokens, payloads completos ni connection strings.

## Renovacion

1. Recibir nuevo `license.json` firmado.
2. Copiarlo al mismo path protegido o actualizar `License__LicenseFilePath`.
3. Reiniciar el Webhook si se valida en arranque.
4. Confirmar `/health` y pruebas funcionales de `POST /webhooks`.
