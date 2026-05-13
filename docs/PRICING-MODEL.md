# Pricing Model

Este documento define la estructura comercial inicial. No implementa cobro online ni precios finales.

## Metricas

La metrica principal es cantidad de endpoints inventariados. El archivo de licencia offline incluye `max_endpoints`; el validador puede recibir el conteo actual y rechazar excedentes cuando el flujo comercial lo conecte a la operacion.

Metricas secundarias para contratos Enterprise:

- Ambientes: produccion, staging y DR.
- Conectores: ManageEngine incluido como base; otros conectores pueden cotizarse.
- Soporte: horario laboral, extendido o 24x7.
- Servicios: instalacion, migracion, hardening y entrenamiento.

## Ediciones

| Edicion | Perfil | Limites iniciales |
| --- | --- | --- |
| `Community/Internal` | Desarrollo, demos internas, PoC. | Limite configurable de desarrollo; sin SLA comercial. |
| `Professional` | Instalacion on-prem unica. | Limite por endpoints y fecha de expiracion. |
| `Enterprise` | Corporativos regulados o multi-area. | Limite por endpoints, cliente, vencimiento y soporte contractual. |

## Licenciamiento

- Las ediciones comerciales usan licencia offline firmada con RSA-SHA256.
- La clave privada de emision no vive en el repositorio ni en el Webhook.
- El Webhook usa `License:PublicKeyPath` o `License:PublicKeyPem` para verificar la firma.
- La validacion no requiere internet.

## Renovaciones

La renovacion consiste en reemplazar el archivo de licencia por uno nuevo firmado para el mismo cliente o contrato. No se requiere cambiar binarios si el formato se mantiene.

## Politicas iniciales

- No detener `Community/Internal` por falta de licencia.
- No publicar precios dentro del repositorio.
- No mezclar secretos comerciales, PFX ni claves privadas de emision con configuracion de despliegue.
- Mantener los limites de producto documentados antes de aplicar enforcement a rutas criticas.
