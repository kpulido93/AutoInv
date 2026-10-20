# Deploy Windows

Scripts auxiliares para despliegue del Webhook en Windows.

## Archivos

- `install-webhook-iis.ps1`: crea o actualiza un sitio IIS y su App Pool para el Webhook.
- `install-worker-service.ps1`: crea o actualiza un servicio Windows para ejecutar el Webhook con Kestrel sin IIS.

## Uso recomendado

Probar primero con `-WhatIf`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\windows\install-webhook-iis.ps1 `
  -PublishPath C:\inetpub\AutoInventarioWebhook `
  -WhatIf `
  -SkipHealthCheck
```

Incluso en `-WhatIf`, el script exige una PowerShell elevada porque valida privilegios de administrador al inicio.

Despues aplicar el cambio real:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\windows\install-webhook-iis.ps1 `
  -PublishPath C:\inetpub\AutoInventarioWebhook `
  -SiteName AutoInventarioWebhook `
  -AppPoolName AutoInventarioWebhook `
  -Port 8080
```

Para detalle operativo y configuracion segura, ver `docs/DEPLOYMENT-IIS.md`.
