# Seguridad

## Politica de secretos

No commitear secretos reales ni configuraciones locales con credenciales. Los valores permitidos en Git son plantillas vacias, placeholders obvios o nombres de secretos gestionados externamente.

Nunca versionar:

- `private.key`, claves RSA privadas, PEM, PFX, P12 o certificados con clave privada.
- `secrets.json`, `appsettings.json`, `appsettings.*.json` locales y `.env`.
- `*.tfvars`, `*.tfstate` o cualquier salida de Terraform con valores de entorno.
- Tokens ManageEngine, AWS access keys, API keys del Webhook y cadenas de conexion reales.
- ZIPs de Lambda, paquetes publicados, logs o dumps.

## Almacenamiento permitido

- AWS Secrets Manager para secretos usados por Lambda o infraestructura AWS.
- Azure DevOps secret variables y Secure Files para pipelines.
- Variables de entorno locales o `dotnet user-secrets` para desarrollo.
- Windows Certificate Store, HSM o Secure Files para certificados de firma.

## Plantillas seguras

- Webhook: copiar `Webhook/appsettings.example.json` a `Webhook/appsettings.Development.json` o usar variables de entorno. El archivo local esta ignorado por Git.
- Lambda: usar `Lambda-Inventario/secrets.example.json` solo como contrato de claves; cargar valores reales en Secrets Manager.
- Terraform: copiar `Infraestructura-Terraform/terraform.tfvars.example` a un `.tfvars` local ignorado o pasar variables por CI seguro.

## Deteccion local

Ejecutar antes de commitear:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/audit-local.ps1
```

Si `gitleaks` esta instalado:

```bash
gitleaks detect --config .gitleaks.toml --no-git --redact
```

El escaneo debe reportar rutas y tipos sin imprimir valores reales.

## Respuesta ante exposicion

1. No copiar el valor a issues, PRs, chats ni documentacion.
2. Rotar el secreto segun `docs/SECRET-ROTATION.md`.
3. Eliminar el archivo o reemplazarlo por una plantilla segura.
4. Agregar una regla de bloqueo en `.gitignore` o `.gitleaks.toml`.
5. Planificar limpieza de historial con `git-filter-repo` o BFG si el secreto entro en commits.
