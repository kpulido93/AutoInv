# Rotacion de secretos

Fecha: 2026-05-12

Todo secreto que haya estado en Git, en un artefacto compartido o en logs debe tratarse como comprometido. Este documento no incluye valores reales.

## Secretos que deben rotarse

| Secreto | Impacto | Almacenamiento recomendado | Accion |
| --- | --- | --- | --- |
| AWS access keys | Acceso a cuenta AWS, Lambda, Secrets Manager, S3, SSM u otros recursos segun permisos. | IAM roles para workloads; AWS Secrets Manager o variables secretas solo si una key humana es inevitable. | Desactivar key anterior, crear key nueva con privilegio minimo, actualizar pipelines y eliminar variables obsoletas. |
| ManageEngine API token | Escritura/lectura contra ManageEngine. | AWS Secrets Manager con nombre por entorno, por ejemplo `manageengine_api_key`. | Revocar token anterior, emitir token nuevo, actualizar Secrets Manager y validar Lambda. |
| Claves RSA privadas | Descifrado de payloads del agente. | AWS Secrets Manager, Azure Secure Files o store seguro local fuera del repo. | Generar par nuevo, publicar solo la clave publica requerida por el agente y retirar la privada anterior. |
| API key del Webhook | Acceso a endpoints protegidos. | Azure DevOps secret variables, variables de entorno o store secreto del host. | Generar clave nueva, actualizar `AutoInventario__ApiKey`, desplegar y revocar la clave anterior. |
| Certificados/PFX de firma | Firma de binarios y confianza de actualizaciones. | Azure Secure Files o HSM/KMS segun disponibilidad. | Revocar certificado si la clave privada salio de custodia, emitir nuevo certificado y actualizar pipeline. |
| Cadenas de conexion | Acceso a base de datos. | Variables de entorno, user-secrets local o secreto gestionado por entorno. | Rotar password de usuario, validar permisos minimos y actualizar `ConnectionStrings__Postgres`. |

## Procedimiento minimo

1. Identificar el entorno afectado: local, CI, staging o produccion.
2. Revocar el secreto anterior antes de asumir que ya no puede usarse.
3. Crear un secreto nuevo con privilegio minimo y nombre por entorno.
4. Actualizar solo el gestor seguro correspondiente.
5. Desplegar y validar con una prueba de autenticacion autorizada y otra no autorizada.
6. Revisar logs para confirmar que no imprimen claves, tokens, payloads ni connection strings.
7. Registrar fecha, responsable y sistema actualizado sin anotar valores.

## Limpieza de historial

No se limpia historial en este commit. Para limpiar un repositorio que haya contenido secretos reales, usar una rama de mantenimiento y coordinar con todos los clones.

Opcion con `git-filter-repo`:

```bash
git filter-repo --path Webhook/Resources/private.key --path Lambda-Inventario/lambda_function/secrets.json --invert-paths
```

Opcion con BFG:

```bash
bfg --delete-files private.key --delete-files secrets.json --delete-files '*.pfx' --delete-files '*.pem'
```

Despues de limpiar historial:

```bash
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

La limpieza de historial requiere force push coordinado. Los secretos deben rotarse aunque el historial se limpie.
