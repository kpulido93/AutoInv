# AutoInventario Enterprise On-Prem

## Proposito

Este documento define el posicionamiento base de AutoInventario como producto comercial self-hosted para entornos corporativos. No describe funcionalidades ficticias ni implica que ya exista licenciamiento, facturacion o enforcement tecnico dentro del codigo.

## Posicionamiento

AutoInventario Enterprise On-Prem es una solucion para inventario de equipos Windows que prioriza control operativo, despliegue en infraestructura del cliente y gestion de secretos fuera del repositorio.

Propuesta inicial de valor:

- Inventario de Windows Workstation con agente local .NET 8.
- Soporte basico para Windows Server, incluyendo deteccion del sistema operativo y recoleccion limitada de roles/features cuando WMI lo permite.
- Recepcion centralizada mediante Webhook ASP.NET Core `net8.0`.
- Integracion inicial con ManageEngine como sistema destino principal.
- Despliegue self-hosted, con opcion de operacion on-prem y sin dependencia obligatoria de AWS para una PoC local del Webhook.

## Cliente objetivo

Perfil inicial recomendado:

- Equipos de infraestructura, workplace o IT operations con parque Windows administrado.
- Organizaciones que requieren mantener inventario y procesamiento dentro de su propia red o tenancy.
- Clientes que ya usan ManageEngine o que necesitan una integracion inicial con ese ecosistema.
- Entornos que prefieren control operativo y cambios planificados sobre SaaS administrado.

## Alcance disponible hoy

Capacidades presentes en el repositorio y alineadas con el estado actual:

- Agente Windows en .NET 8 para recopilar inventario y cifrar el payload.
- Updater para actualizacion del agente.
- Webhook ASP.NET Core `net8.0` con proteccion por API key en `/webhooks`.
- Modo `AwsLambda` para procesamiento existente y modo `Local` inicial como stub para levantar el Webhook sin AWS.
- Configuracion segura por variables de entorno, archivos example y validaciones tempranas de opciones.
- Soporte basico de Windows Server documentado, sin prometer cobertura profunda por rol.
- Dockerfile, Docker Compose de PoC on-prem y guia de despliegue en IIS/Windows Server.

## Ediciones propuestas

Las ediciones siguientes son una propuesta comercial y documental. Hoy no existe licenciamiento implementado ni restricciones tecnicas por edicion.

| Edicion | Perfil | Alcance comercial sugerido |
| --- | --- | --- |
| `Internal/Community` | Uso interno, laboratorio, PoC y validacion tecnica. | Sin SLA comercial. Documentacion y soporte best effort. |
| `Professional` | Despliegue self-hosted de una sola organizacion o unidad operativa. | Suscripcion anual por endpoint, soporte horario laboral y servicios de implementacion opcionales. |
| `Enterprise` | Corporativos con mayores requisitos de control, volumen o formalidad operativa. | Suscripcion anual por endpoint, acuerdo de soporte ampliado, servicios profesionales y acompanamiento de despliegue/endurecimiento. |

## Lo disponible hoy vs. lo necesario para un MVP comercial

### Disponible hoy

- Base tecnica del agente, updater y Webhook compilable en `Debug`.
- Integracion inicial con ManageEngine mediante el flujo existente.
- Documentacion de seguridad, rotacion de secretos y despliegue on-prem.
- Soporte basico para Windows Server en el agente.
- Contenedorizacion del Webhook y PoC local sin AWS obligatorio.

### Requerido para un MVP comercial

Trabajo recomendado antes de vender como producto con expectativa contractual:

- Politica formal de soporte y matriz de versiones soportadas.
- Paquete de instalacion y actualizacion mas predecible para administradores corporativos.
- Observabilidad operativa mas consistente para agente, Webhook y procesamiento.
- Pruebas de aceptacion repetibles sobre Windows Server 2019/2022/2025 y escenarios representativos de cliente.
- Proceso comercial/documental para activacion de clientes, onboarding, hardening y handoff a soporte.
- Definicion cerrada de packaging comercial, contrato de soporte y criterio de escalamiento.

### Roadmap posterior al MVP comercial

Trabajo valioso pero no necesario para la primera salida comercial:

- Licenciamiento tecnico u otro mecanismo de control comercial.
- Conectores adicionales aparte de ManageEngine.
- Procesamiento local completo que reemplace el stub actual y reduzca dependencia del flujo AWS.
- Dashboards operativos, reportes y herramientas de administracion para el cliente.
- Flujos de upgrade y rollback mas automatizados para grandes parques.

## Limites que deben decirse en una conversacion comercial

Mensajes que deben mantenerse explicitos:

- AutoInventario no debe venderse hoy como plataforma de discovery universal; su foco actual es Windows.
- El soporte de Windows Server es basico y pragmaticamente acotado al inventario ya implementado.
- La integracion inicial soportada es ManageEngine; otros conectores son roadmap o trabajo profesional.
- El modo `Local` del Webhook existe para despliegue y pruebas, pero todavia no sustituye toda la logica del procesamiento actual en AWS.
- No hay licenciamiento implementado en el producto en esta etapa.

## Recomendacion de mensaje comercial inicial

Frase de posicionamiento sugerida:

> AutoInventario Enterprise On-Prem ofrece inventario de endpoints Windows y recepcion centralizada self-hosted, con integracion inicial a ManageEngine y foco en control operativo, seguridad y despliegue corporativo.
