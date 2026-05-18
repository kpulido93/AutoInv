# Seguridad

## Politica de secretos

No commitear secretos reales ni configuraciones locales con credenciales. Los valores permitidos en Git son placeholders obvios, ejemplos seguros o nombres de secretos gestionados externamente.

Nunca versionar:

- `private.key`, PEM, PFX, P12, certificados con clave privada o passwords asociados.
- `secrets.json`, `appsettings.json`, `appsettings.Development.json`, `appsettings.Local.json` y `.env` reales.
- `*.tfvars`, `*.tfstate`, planes Terraform y salidas que contengan variables sensibles.
- AWS access keys, ManageEngine API tokens, API keys del Webhook y connection strings reales.
- ZIPs generados, publish output, logs, dumps o artefactos con secretos embebidos.

Todo secreto que haya sido versionado, compartido o almacenado fuera de un store seguro debe tratarse como comprometido y rotarse.

## Almacenamiento permitido

- AWS Secrets Manager para secretos usados por Lambda o integraciones AWS.
- Azure DevOps secret variables, Variable Groups, Secure Files y service connections para CI/CD.
- Variables de entorno del host, `dotnet user-secrets` o store seguro del sistema para desarrollo local.
- Windows Certificate Store, HSM o mecanismo corporativo equivalente para certificados y claves privadas.

## Plantillas seguras

- Webhook: usar `Webhook/appsettings.example.json` como contrato de configuracion, nunca como store de secretos.
- Lambda: usar `Lambda-Inventario/secrets.example.json` solo como ejemplo de estructura; los valores reales viven fuera del repo.
- Terraform: usar `Infraestructura-Terraform/terraform.tfvars.example` como referencia y pasar valores reales por `TF_VAR_*` o pipeline secrets.
- Docker/local: usar `.env.example` como plantilla y mantener `.env` local fuera de Git.

## Deteccion local y CI

Antes de commitear:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/audit-local.ps1
```

Si `gitleaks` esta instalado:

```bash
gitleaks detect --source . --config .gitleaks.toml --no-git --redact --exit-code 1
```

Si no esta instalado, usar el wrapper del repo:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/ci/Invoke-SecretScan.ps1
```

Las plantillas `.example` y la documentacion no se allowlistean por ruta completa para evitar silenciar secretos reales pegados por accidente. El escaneo debe reportar rutas y tipos sin imprimir valores reales.

## Redaccion de logs

Los logs operativos deben ser utiles sin exponer secretos, payloads completos ni datos personales sensibles.

Reglas minimas:

- No registrar API keys, tokens, `authtoken`, connection strings, PEM/PFX, claves privadas ni passwords.
- No registrar payloads completos cifrados o descifrados, respuestas completas de integraciones ni `BitLocker recovery passwords`.
- Sustituir contenido sensible por `correlation_id`, `statusCode`, longitud del cuerpo, `error type` o una huella corta no reversible cuando haga falta correlacion.
- Si una excepcion puede contener configuracion o datos de usuario, registrar el tipo de error y un detalle saneado, no el valor literal.
- Mantener la misma politica para logs locales, consola, tickets automaticos y cualquier canal de diagnostico externo.

## Respuesta ante exposicion

1. No copiar el valor a issues, PRs, chats, correos ni documentacion.
2. Rotar el secreto segun `docs/SECRET-ROTATION.md`.
3. Retirar el archivo o reemplazarlo por una plantilla segura.
4. Agregar o reforzar reglas en `.gitignore`, `.gitleaks.toml` y gates de CI.
5. Si el secreto entro en commits, planificar limpieza de historial con `git-filter-repo` o BFG.
6. Coordinar cualquier reescritura de historial con force push controlado y comunicacion a todos los clones.

## Regla operativa

La limpieza de historial no sustituye la rotacion. Si un secreto estuvo en el repositorio o fuera de store seguro, se rota primero y luego se evalua el rewrite del historial.
