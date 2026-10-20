# Rotacion de secretos

Fecha: 2026-05-18

Todo secreto que haya estado en Git, en un artefacto compartido, en logs, en capturas, en un chat, en variables expuestas del pipeline o en un arbol local fuera de store seguro debe tratarse como comprometido. Este documento no incluye valores reales.

## Cuando un secreto se considera comprometido

Tratar el secreto como comprometido si ocurrio cualquiera de estas situaciones:

- Se commiteo en cualquier rama o quedo en el historial Git.
- Aparecio en un PR, issue, wiki, documento, correo o chat.
- Quedo en `.env`, `appsettings.json`, `secrets.json`, `tfvars`, `tfstate`, logs, artefactos ZIP, publish o dumps.
- Se cargo en una maquina local sin ACL suficientes o fuera de store seguro.
- Se compartio con terceros sin aprobacion formal.
- Se uso en un pipeline o script que lo imprime o lo deja en texto plano.

La limpieza de historial no elimina el riesgo por si sola. Si el secreto se versiono o se compartio, debe rotarse aunque luego se borre del historial.

## Almacenes seguros recomendados

| Tipo de secreto | Donde debe vivir |
| --- | --- |
| AWS access keys | Preferir IAM Role, service connection o federacion. Si una key estatica es inevitable: Azure DevOps secret variables o store seguro temporal. |
| ManageEngine API tokens | AWS Secrets Manager o variable secreta del entorno/host. |
| Claves privadas RSA | AWS Secrets Manager, Azure DevOps Secure Files o store seguro del host con ACL estrictas. |
| API keys del Webhook | Variable de entorno del host, Azure DevOps secret variables o store corporativo. |
| Cadenas de conexion | Variable de entorno, secret manager o store seguro del host. |
| Certificados PFX | Azure DevOps Secure Files, HSM/KMS o store de certificados del sistema. |
| Secretos Terraform | `TF_VAR_*`, Azure DevOps secret variables, AWS Secrets Manager o backend remoto seguro. Nunca en `*.tfvars` versionados. |
| Variables secretas de Azure DevOps | Variable Groups marcados como secret, Secure Files o service connections. |

## Matriz de rotacion

| Secreto | Que se considera comprometido | Revocacion | Regeneracion | Validacion posterior | Responsable sugerido |
| --- | --- | --- | --- | --- | --- |
| AWS access keys | Cualquier `AWS_ACCESS_KEY_ID` o `AWS_SECRET_ACCESS_KEY` en Git, logs, YAML, artefactos o variables visibles. | Deshabilitar o borrar la key en IAM. Revisar CloudTrail y sesiones activas si aplica. | Crear credencial nueva con privilegio minimo o migrar a IAM Role/service connection sin key estatica. Actualizar Azure DevOps y hosts. | `aws sts get-caller-identity`, ejecucion controlada de pipeline Terraform/Lambda y confirmacion de que la key anterior falla. | Owner AWS/Infraestructura. |
| ManageEngine API tokens | Token en codigo, `.env`, `secrets.json`, logs, tickets o chat. | Revocar token en ManageEngine o invalidar la sesion/token asociado. | Emitir token nuevo y cargarlo en AWS Secrets Manager o variable secreta referenciada por `ManageEngine__ApiTokenSecretName`. | Probar una operacion autorizada minima y confirmar que el token anterior devuelve rechazo. Revisar logs del conector. | Owner de integracion ManageEngine. |
| Claves privadas RSA | `private.key`, PEM, contenido base64 o copia local fuera de store seguro. | Retirar la clave privada anterior de todos los hosts, Secure Files y secret stores. Invalidar cualquier despliegue que la use. | Generar un par nuevo. Publicar solo la clave publica necesaria para los consumidores y cargar la privada en store seguro. | Probar cifrado/descifrado con la nueva pareja y confirmar que la clave antigua ya no descifra trafico nuevo. | Owner de AppSec o backend del Webhook. |
| API keys del Webhook | Valor expuesto en `appsettings`, `.env`, pipeline, logs o clientes compartidos. | Invalidar la key antigua en configuracion del host y retirar clientes que la usen. | Generar una key nueva de alta entropia y almacenarla como `AutoInventario__ApiKey` o equivalente seguro. | Probar `401/403` con la key antigua y `200` con la nueva sobre `/webhooks` y `/id-clients`. | Owner del servicio Webhook. |
| Cadenas de conexion | Connection string real en Git, logs, `appsettings`, `.env`, PRs o tickets. | Rotar password o usuario en la base de datos. Revocar credenciales antiguas y sesiones abiertas si la plataforma lo permite. | Crear credencial nueva con permisos minimos y actualizar `ConnectionStrings__Postgres` en store seguro. | Ejecutar consulta minima de salud con la nueva conexion y confirmar fallo con la anterior. | Owner de base de datos o backend. |
| Certificados PFX | PFX, password o clave privada expuestos en repo, pipeline, correo o almacenamiento local inseguro. | Revocar certificado si la CA o politica corporativa lo requiere. Retirar el PFX anterior de Secure Files y hosts. | Emitir certificado nuevo, cargarlo en Secure Files/HSM y actualizar `PFX_PASSWORD` o referencia equivalente. | Ejecutar una firma controlada, verificar cadena de confianza y validar que el certificado anterior no se sigue usando. | Owner de release/signing. |
| Secretos Terraform | Valores reales en `terraform.tfvars`, `*.tf`, `*.tfstate`, planes, logs o variables expuestas. | Invalidar el secreto en el sistema origen y eliminar el acceso a artefactos de Terraform que lo contengan. | Regenerar el secreto y pasarlo por `TF_VAR_*`, secret variables o secret manager. Si el secreto vive en AWS, recrearlo alli. | `terraform plan`/`apply` controlados sin diff inesperado y verificacion de que no aparecen valores en salida o estado compartido. | Owner IaC/Infraestructura. |
| Variables secretas de Azure DevOps | Secret variable visible en logs, variable group exportado, Secure File expuesto o YAML con valor real. | Revocar o eliminar la variable/secure file/service connection comprometidos. Revisar autorizaciones del pipeline. | Crear variable secreta o Secure File nuevos, reasignar permisos minimos y actualizar los pipelines que dependan de ellos. | Ejecutar pipeline de prueba, confirmar acceso correcto y revisar logs para verificar redaccion. | Owner DevSecOps/Azure DevOps. |

## Procedimiento minimo de respuesta

1. Confirmar el alcance: repo, historial, rama, pipeline, host local, artefactos y canales de comparticion.
2. Clasificar el secreto afectado y el entorno impactado: desarrollo, CI, staging o produccion.
3. Revocar primero el secreto anterior o bloquear su uso antes de asumir que dejo de ser valido.
4. Generar un secreto nuevo con privilegio minimo y nombre por entorno.
5. Cargarlo solo en el almacen seguro correspondiente. No usar commits para distribuirlo.
6. Desplegar o reiniciar solo los componentes necesarios para recoger el valor nuevo.
7. Validar acceso autorizado, rechazo del valor antiguo y ausencia de filtracion en logs.
8. Registrar incidente, fecha, alcance, responsable y acciones tomadas sin anotar el secreto.

## Guia de limpieza de historial

No limpiar historial en este commit. La limpieza requiere coordinacion porque reescribe commits y obliga a actualizar todos los clones y forks.

### Opcion A: git-filter-repo

Preparacion:

1. Congelar merges y pushes sobre la rama afectada.
2. Rotar primero los secretos comprometidos.
3. Trabajar en un clon limpio y actualizado.

Ejemplos:

```bash
git filter-repo --path Webhook/Resources/private.key --path Lambda-Inventario/secrets.json --invert-paths
```

```bash
git filter-repo --replace-text replacements.txt
```

Ejemplo de `replacements.txt`:

```text
old-secret-pattern==>REMOVED_SECRET
old-private-key-snippet==>REMOVED_PRIVATE_KEY
```

Despues:

```bash
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

### Opcion B: BFG Repo-Cleaner

Preparacion:

1. Crear un mirror del repositorio.
2. Rotar secretos antes del rewrite.
3. Mantener una ventana de cambio controlada.

Ejemplos:

```bash
bfg --delete-files private.key --delete-files secrets.json --delete-files '*.pfx' --delete-files '*.pem'
```

```bash
bfg --replace-text replacements.txt
```

Despues:

```bash
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

## Coordinacion obligatoria para force push

La limpieza de historial requiere force push controlado y coordinado:

- Acordar una ventana de cambio con los equipos que usan el repo.
- Bloquear merges automergers y pipelines que hagan push.
- Comunicar el commit de corte y el nuevo commit base.
- Pedir a cada colaborador que reclone o resincronice segun instrucciones formales.
- Limpiar forks, mirrors, caches de CI y artefactos publicados si almacenaron el secreto.

## Checklist posterior a la rotacion

- El valor antiguo falla.
- El valor nuevo funciona en el entorno correcto.
- Los pipelines pasan con el secreto nuevo.
- Los logs no muestran el secreto ni derivados obvios.
- `.gitignore`, `.gitleaks.toml` y plantillas `.example` bloquean una reintroduccion trivial.
- Si hubo secreto en historial, existe plan aprobado para `git-filter-repo` o BFG y force push controlado.
