# Agente en Windows Server

El agente AutoInventario mantiene el payload compatible de workstation y agrega un bloque opcional `server_inventory` cuando detecta Windows Server.

## Deteccion

La deteccion usa WMI local:

```text
Win32_OperatingSystem.ProductType
```

Valores:

- `1`: Windows Workstation.
- `2`: Domain Controller.
- `3`: Windows Server.

El campo existente `workstation.is_server` sigue presente. En Windows cliente se serializa como `false` y no se agrega `server_inventory`.

## Campos Server

Cuando `is_server=true`, el agente intenta agregar:

- `server_inventory.os_caption`
- `server_inventory.os_version`
- `server_inventory.os_build`
- `server_inventory.part_of_domain`
- `server_inventory.domain` o `server_inventory.workgroup`
- `server_inventory.last_boot_time_utc`
- `server_inventory.uptime_seconds`
- `server_inventory.roles_features`
- `server_inventory.tracked_services`

La lectura de roles/features usa `Win32_ServerFeature`. Si la clase no existe, el usuario no tiene permisos suficientes o WMI falla, el agente continua y reporta la lista vacia.

## Servicios Relevantes

Por defecto se intentan leer solo nombre, display name, estado y modo de arranque de servicios comunes de servidor:

```text
W3SVC,MSSQLSERVER,SQLSERVERAGENT,DNS,DHCPServer,NTDS,ADWS,TermService
```

No se recolecta ruta del ejecutable, cuenta de servicio, argumentos ni credenciales.

Para configurar la lista:

```powershell
$env:AUTOINVENTARIO_SERVER_SERVICES = "W3SVC,DNS,Spooler"
```

## Permisos

Lecturas esperadas:

- WMI `root\cimv2` para `Win32_OperatingSystem`, `Win32_ComputerSystem`, `Win32_ServerFeature` y `Win32_Service`.
- Lectura local de informacion de servicios.

No se requieren permisos de administrador para todos los campos, pero algunos entornos endurecidos pueden bloquear WMI de roles/features. El agente debe continuar aunque esas lecturas fallen.

## Seguridad

El soporte Windows Server no recolecta secretos. En particular:

- No se recolectan BitLocker recovery passwords.
- No se recolectan claves de licencia Windows.
- No se recolectan cuentas de servicio ni command lines de servicios.
- No se recolectan tokens, passwords ni claves privadas.

Los campos historicos de serializacion se mantienen para compatibilidad, pero las claves sensibles se reemplazan por estados no secretos.

## Validacion

Comandos:

```powershell
dotnet build AutoInventario.csproj -c Debug
dotnet test
```

Prueba manual recomendada en Windows Server 2019/2022/2025:

1. Configurar una URL de Webhook de pruebas.
2. Configurar `AUTOINVENTARIO_SERVER_SERVICES` si se quieren servicios concretos.
3. Ejecutar el agente.
4. Verificar que el JSON descifrado contiene `workstation.is_server=true`.
5. Verificar que `server_inventory` incluye OS, dominio/workgroup, uptime y listas vacias o pobladas segun permisos.
