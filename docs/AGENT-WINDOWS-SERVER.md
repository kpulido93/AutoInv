# Agente en Windows Server

Guia operativa del soporte actual del agente AutoInventario en Windows Server.

## Estado actual

Estado del soporte:

- Soporte basico para detectar Windows Server y mantener compatibilidad con el payload actual del agente.
- Recoleccion adicional solo cuando el sistema operativo se identifica como servidor.
- `roles_features` se incluyen segun disponibilidad de WMI local; si no se pueden leer, el inventario sigue saliendo.
- No hay soporte especifico por rol de servidor, hardening profile o producto corporativo concreto.

No prometer hoy:

- cobertura completa de todas las ediciones y workloads de Windows Server;
- inventario profundo de IIS, SQL Server, AD DS, DHCP o DNS;
- diagnostico automatico de permisos o de salud de WMI;
- soporte comercial ampliado mas alla del baseline descrito aqui.

## Versiones objetivo

Objetivo actual de validacion manual:

- Windows Server 2019
- Windows Server 2022
- Windows Server 2025

Tambien se contemplan Domain Controllers porque `Win32_OperatingSystem.ProductType=2` se trata como servidor.

El soporte en estas versiones es basico. Cada entorno corporativo debe validar manualmente su propia baseline, GPOs y restricciones WMI antes de considerar el despliegue estable.

## Deteccion

La deteccion usa WMI local:

```text
Win32_OperatingSystem.ProductType
```

Valores tratados:

- `1`: Windows Workstation
- `2`: Domain Controller
- `3`: Windows Server

Comportamiento:

- si `ProductType` es `2` o `3`, el agente serializa `workstation.is_server=true`;
- si `ProductType` es `1`, el agente serializa `workstation.is_server=false`;
- si no se puede determinar por un fallo recuperable de WMI, el agente usa el valor seguro `is_server=false` y registra solo un log no sensible con el tipo de error.

## Datos recolectados

En todos los Windows compatibles, el payload principal incluye:

- `workstation.os_family`
- `workstation.os_caption`
- `workstation.os_version`
- `workstation.os_build`
- `workstation.is_server`

Cuando `is_server=true`, el agente puede agregar `workstation.server_inventory` con:

- `os_caption`
- `os_version`
- `os_build`
- `part_of_domain`
- `domain` o `workgroup`
- `last_boot_time_utc`
- `uptime_seconds`
- `roles_features`
- `tracked_services`

`roles_features` intenta leer solo:

- `name`
- `state`

Hoy `state` se serializa como `Installed` porque `Win32_ServerFeature` devuelve elementos ya instalados.

`tracked_services` intenta leer solo:

- `name`
- `display_name`
- `state`
- `start_mode`

## Datos no recolectados

El soporte actual en Windows Server no recolecta:

- passwords, tokens o claves privadas;
- BitLocker recovery passwords;
- claves de licencia de Windows;
- cuentas de servicio o credenciales de servicios;
- command line o ruta binaria de servicios;
- configuracion detallada de roles IIS, SQL Server, AD DS, DHCP o DNS;
- bindings, certificados o configuracion interna de sitios IIS;
- secretos de Active Directory;
- inventario de features desinstaladas o estados mas finos que `Installed`.

## Permisos requeridos

Lecturas esperadas:

- WMI `root\cimv2` para `Win32_OperatingSystem`, `Win32_ComputerSystem`, `Win32_ServerFeature` y `Win32_Service`;
- lectura local de informacion del sistema operativo, hardware y servicios ya cubierta por el agente Windows.

Perfil de permisos:

- el agente no deberia requerir privilegios administrativos dedicados solo para detectar `is_server`;
- algunos entornos endurecidos pueden bloquear `Win32_ServerFeature` o `Win32_Service`;
- si la lectura de roles/features falla, el agente mantiene el inventario basico y omite solo ese bloque adicional.

## Instalacion recomendada

Instalacion recomendada para servidor:

1. Instalar el binario del agente en `C:\ProgramData\AutoInventario`.
2. Ejecutarlo con los mismos parametros base que en workstation:
   - `-client_id <id>`
   - `-url <base_url>`
3. Dejar que el flujo normal del agente mantenga la tarea programada `AutoInventario`.
4. Configurar `AUTOINVENTARIO_WEBHOOK_API_KEY` si el Webhook requiere API key.
5. Configurar opcionalmente `AUTOINVENTARIO_SERVER_SERVICES` si se quieren servicios concretos en `tracked_services`.

Notas:

- el agente en Windows Server usa el mismo ejecutable que en Windows cliente;
- no cambia endpoint, cifrado ni contrato del Webhook;
- la ejecucion recomendada sigue siendo no interactiva, mediante la tarea programada del agente.

## Limitaciones

Limitaciones conocidas:

- `roles_features` depende de WMI y puede venir vacio aunque `is_server=true`;
- no hay telemetria separada por rol ni inventario enriquecido por workload;
- el agente no distingue entre distintas ediciones de Windows Server mas alla de `Caption`, `Version` y `Build`;
- el soporte actual es de compatibilidad operativa, no de descubrimiento profundo del servidor.

## Troubleshooting

Escenarios comunes:

1. `is_server=false` en un Windows Server.
   Causa probable: WMI no pudo devolver `ProductType`.
   Revisar acceso a `Win32_OperatingSystem` y logs locales del agente.

2. `is_server=true` pero `roles_features` vacio.
   Causa probable: `Win32_ServerFeature` no disponible o bloqueado.
   El comportamiento es esperado: el agente debe seguir enviando inventario basico.

3. `tracked_services` incompleto.
   Causa probable: servicio no instalado, nombre no incluido en `AUTOINVENTARIO_SERVER_SERVICES` o lectura WMI limitada.

4. El agente corre pero no se ve el payload esperado en el backend.
   Revisar conectividad al Webhook, `AUTOINVENTARIO_WEBHOOK_API_KEY`, `client_id`, logs en `C:\ProgramData\AutoInventario\logs.txt` y el JSON descifrado del lado servidor si existe un entorno de pruebas.

5. Se sospecha que el agente esta recolectando demasiado.
   Revisar esta guia: el baseline actual esta acotado a estado de servidor, datos de SO, uptime, roles/features instalados y servicios rastreados.

## Validaciones manuales

Comandos base:

```powershell
dotnet build AutoInventario.csproj -c Debug
dotnet test AutoInventario.Tests\AutoInventario.Tests.csproj -c Debug
```

Prueba manual recomendada en Windows Server 2019/2022/2025:

1. Configurar una URL de Webhook de pruebas.
2. Configurar `AUTOINVENTARIO_WEBHOOK_API_KEY` si aplica.
3. Configurar opcionalmente `AUTOINVENTARIO_SERVER_SERVICES`, por ejemplo:

```powershell
$env:AUTOINVENTARIO_SERVER_SERVICES = "W3SVC,DNS,Spooler"
```

4. Ejecutar el agente con un `client_id` de pruebas.
5. Verificar que el JSON descifrado contiene:
   - `workstation.is_server=true`
   - `workstation.os_family=windows_server`
   - `workstation.os_caption`, `os_version` y `os_build`
6. Verificar que `server_inventory` contiene OS, dominio/workgroup y uptime.
7. Verificar que `roles_features` aparece con pares `name/state` cuando WMI responde.
8. Si `roles_features` no aparece o viene vacio, verificar que el inventario igual se envio y que no hubo fallo total del agente.

Prueba manual recomendada en Windows cliente:

1. Ejecutar el agente en Windows 10/11.
2. Verificar que el JSON contiene:
   - `workstation.is_server=false`
   - `workstation.os_family=windows_workstation`
3. Verificar que no se agrega `server_inventory`.

## Mejoras futuras

Mejoras razonables a futuro, no implementadas hoy:

- normalizar mejor diferencias entre Server 2019/2022/2025;
- incluir un estado mas preciso de roles/features si una fuente estable lo permite;
- ampliar validaciones sobre Domain Controller sin recolectar datos sensibles;
- agregar pruebas manuales automatizadas en hosts Windows Server reales;
- documentar patrones de despliegue del agente especificamente para servidores de aplicacion, archivos, IIS o bases de datos.
