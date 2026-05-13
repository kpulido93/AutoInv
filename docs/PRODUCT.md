# Producto

AutoInventario queda preparado como producto self-hosted para entornos corporativos. La oferta inicial no depende de cobro online ni de llamadas externas obligatorias: el Webhook, el worker local, el agente Windows y el updater pueden operar on-prem.

## Componentes comerciales

- Agente Windows: recopila inventario y envia payload cifrado.
- Updater: actualiza binarios del agente usando `latest.json` y SHA256.
- Webhook: API ASP.NET Core, autenticacion por API key y publicacion de updates.
- Worker local: normaliza inventario y lo envia a conectores configurables.
- Conector ManageEngine: primer conector corporativo soportado.
- Licencia offline: archivo local validado con firma RSA-SHA256 para ediciones comerciales.

## Ediciones

| Edicion | Uso esperado | Licencia |
| --- | --- | --- |
| `Community/Internal` | Laboratorios, PoC, uso interno no comercial y desarrollo. | No requiere archivo firmado por defecto. Puede usar licencia fake de desarrollo. |
| `Professional` | Empresas pequenas o medianas con despliegue on-prem unico. | Requiere licencia offline firmada. |
| `Enterprise` | Corporativos con mas endpoints, instalacion asistida, conectores y soporte operacional. | Requiere licencia offline firmada. |

## Principios

- Sin dependencia de internet para validar licencias.
- Sin bloqueo por licencia en modo `Community/Internal`.
- Sin secretos en archivos versionados.
- La firma de licencias usa una clave privada externa al repo; el despliegue solo necesita la clave publica.
- Los limites comerciales iniciales son `customer`, `edition`, `expires_on` y `max_endpoints`.

## Formato de licencia offline

El archivo de licencia es JSON:

```json
{
  "algorithm": "RS256",
  "payload": "<base64url-json-payload>",
  "signature": "<base64url-rsa-sha256-signature>"
}
```

El `payload` decodificado contiene:

```json
{
  "license_id": "lic-...",
  "customer": "Customer Name",
  "edition": "Enterprise",
  "expires_on": "2027-12-31T23:59:59Z",
  "max_endpoints": 500
}
```

Para `Community/Internal` se permite `algorithm=none` solo cuando `License:AllowUnsignedDevelopmentLicense=true`. No usar ese modo en produccion.

## Roadmap comercial fuera de este commit

- Portal de cliente y cobro online.
- Emision automatizada de licencias.
- Telemetria opcional y deshabilitable.
- Enforcement granular por feature.
- Renovacion asistida y alertas de expiracion.
