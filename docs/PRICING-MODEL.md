# Pricing Model

## Proposito

Este documento define un modelo comercial inicial para AutoInventario Enterprise On-Prem. No publica una lista de precios cerrada ni implica que el repositorio contenga licenciamiento o facturacion implementados.

## Principio de cobro

La unidad comercial principal propuesta es el endpoint inventariado.

Definicion operativa inicial:

- Un endpoint equivale a un equipo Windows inventariado activamente por el agente.
- Para el MVP comercial, Windows Workstation y Windows Server pueden contarse bajo la misma unidad para simplificar preventa, contrato y renovacion.
- La facturacion recomendada es anual y por tramos de volumen.

## Estructura comercial sugerida

Una propuesta comercial inicial deberia separar cuatro componentes:

1. Suscripcion anual por endpoint.
2. Soporte anual.
3. Servicios profesionales de implementacion.
4. Servicios opcionales posteriores: hardening, entrenamiento, cambios controlados o acompanamiento operativo.

## Ediciones y modelo de cobro

| Edicion | Modelo sugerido | Soporte | Implementacion |
| --- | --- | --- | --- |
| `Internal/Community` | Sin cobro comercial estandar; uso interno, laboratorio o PoC. | Sin SLA. Best effort. | No incluida. |
| `Professional` | Suscripcion anual por endpoint con minimo comercial. | Horario laboral. | Opcional, con paquete base de despliegue. |
| `Enterprise` | Suscripcion anual por endpoint con bandas de volumen y contrato. | Soporte ampliado o acordado por contrato. | Recomendado: discovery, hardening, despliegue y handoff. |

## Como cotizar la suscripcion anual

Reglas simples para una primera conversacion comercial:

- Cobrar por banda de endpoints, no por instalacion individual.
- Definir un minimo anual para evitar contratos pequenos con alto costo operativo.
- Mantener el precio por endpoint decreciente a mayor volumen.
- Reservar descuentos especiales para acuerdos multi-anuales o despliegues amplios, no como punto de partida.

Bandas sugeridas para estructurar ofertas:

- `1-250` endpoints
- `251-1000` endpoints
- `1001-5000` endpoints
- `5000+` endpoints

Este repositorio no debe almacenar precios finales. La recomendacion es mantener el price book fuera del codigo fuente y versionar aqui solo el modelo.

## Soporte

El soporte debe cotizarse de forma visible y separada de la suscripcion base.

Modelo sugerido:

- `Best effort`: aplicable a `Internal/Community`; sin SLA comercial.
- `Business Hours`: opcion base para `Professional`.
- `Extended/Premium`: opcion para `Enterprise` con tiempos de respuesta, ventana horaria y canal de escalamiento acordados.

Puntos a dejar claros en propuesta:

- Alcance del soporte: producto, despliegue, configuracion y diagnostico.
- Exclusiones: cambios funcionales a medida, integraciones no soportadas y administracion del entorno del cliente fuera del alcance contratado.
- Nivel de severidad y tiempos de respuesta acordados por contrato, no prometidos desde el repositorio.

## Servicios profesionales

Los servicios de implementacion no deben esconderse dentro del precio por endpoint. Deben cotizarse como partida separada.

Paquetes recomendados:

- `Discovery y hardening inicial`
- `Instalacion del Webhook on-prem`
- `Configuracion del agente y actualizaciones`
- `Integracion inicial con ManageEngine`
- `Pruebas de aceptacion y handoff operativo`
- `Capacitacion tecnica del equipo del cliente`

## Disponible hoy, MVP comercial y roadmap

### Disponible hoy

- Documentacion y artefactos para despliegue del Webhook en Docker e IIS.
- Agente Windows, updater y flujo actual hacia ManageEngine.
- Soporte basico para Windows Server documentado.

### Requerido para MVP comercial

- Definir price book externo con importes reales por banda.
- Publicar politica de soporte, severidades y tiempos objetivo.
- Formalizar entregables de implementacion y criterios de aceptacion.
- Cerrar la narrativa comercial de `Professional` y `Enterprise` sin depender de funcionalidades futuras.

### Roadmap

- Licenciamiento tecnico o enforcement comercial.
- Packaging por features o conectores adicionales.
- Modelos de suscripcion avanzados para multi-entorno, multi-entidad o MSP.

## Reglas de mensaje comercial

Para evitar sobrerpromesas:

- No vender hoy una funcion de licenciamiento que aun no existe.
- No asumir soporte 24x7 salvo contrato y capacidad operativa real.
- No presentar conectores futuros como entregables actuales.
- No prometer paridad funcional total entre modo `Local` y el procesamiento actual basado en AWS.
