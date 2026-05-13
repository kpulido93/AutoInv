# Operacion

## Estado previo a cualquier cambio

Antes de tocar codigo o configuracion:

```powershell
git status --short --branch
git diff --name-status
```

La estructura canonica del agente es la raiz del repositorio. No usar rutas antiguas bajo `Autoinventario/`; la solucion, los tests y el pipeline deben apuntar a `AutoInventario.csproj`.

## Build local

Comandos utiles:

```powershell
dotnet build AutoInventario.sln -c Debug
dotnet build AutoInventario.csproj -c Debug
dotnet build AutoInventario.Updater\AutoInventario.Updater.csproj -c Debug
dotnet build Webhook\Webhook-Inventario.csproj -c Debug
dotnet test AutoInventario.Tests\AutoInventario.Tests.csproj -c Debug
dotnet test Webhook.Tests\Webhook.Tests.csproj -c Debug
python -m py_compile Lambda-Inventario\lambda_function.py
```

Estado esperado:

- La solucion compila con `dotnet build AutoInventario.sln -c Debug`.
- El proyecto raiz del agente compila con `dotnet build AutoInventario.csproj -c Debug`.
- Updater compila.
- Webhook compila en `net8.0` sin warnings.
- Tests referencian el proyecto raiz del agente.
- Lambda pasa validacion de sintaxis.

## Seguridad de secretos

Regla operativa: ningun secreto real debe estar en Markdown, codigo, Terraform, scripts, JSON o recursos embebidos.

Usar:

- AWS Secrets Manager para clave privada y token ManageEngine.
- IAM Role o AWS SDK default credential chain para Webhook/Lambda.
- Azure DevOps Secure Files para certificados PFX.
- Variable groups/secret variables para passwords y URLs sensibles.
- Variables de entorno para configuracion local.

Si un secreto se ha committeado:

1. Revocarlo o rotarlo inmediatamente.
2. Sustituirlo en todos los entornos.
3. Eliminarlo del historial si el repositorio se comparte.
4. Anadir deteccion de secretos al pipeline.

## Ejecucion del Webhook

```powershell
dotnet run --project Webhook\Webhook-Inventario.csproj
```

Configuracion esperada:

- `ConnectionStrings__Postgres`
- `AutoInventario__ApiKey`
- `Security__PrivateKeyPath` o `WEBHOOK_PRIVATE_KEY_PATH`
- `InventoryProcessing__Mode=Local` para ejecutar sin AWS.
- `ManageEngine__BaseUrl` y `ManageEngine__WorkstationsPath` para el conector local.
- `ManageEngine__ApiTokenSecretName` apuntando al nombre de variable/clave donde vive el token.
- `InventoryProcessing__Mode=AwsLambda` solo cuando se quiera invocar Lambda.
- `InventoryProcessing__AwsLambda__FunctionName` cuando el modo sea `AwsLambda`.
- `InventoryProcessing__AwsLambda__Region`, `AWS_REGION` o `AWS_DEFAULT_REGION` cuando el entorno no aporte region al SDK.
- Credenciales AWS por rol, perfil o variables de entorno seguras, no hard-codeadas.

`AUTOINVENTARIO_LAMBDA_NAME` se mantiene como fallback temporal para despliegues antiguos, pero las nuevas configuraciones deben usar `InventoryProcessing__AwsLambda__FunctionName`.

Endpoints de comprobacion:

```powershell
Invoke-RestMethod http://localhost:<puerto>/
Invoke-RestMethod http://localhost:<puerto>/updates/latest.json
Invoke-RestMethod http://localhost:<puerto>/id-clients -Headers @{ "X-AutoInventario-Key" = "<key>" }
```

## Publicacion de actualizaciones

El pipeline publica:

- `AutoInventario.exe` en `/updates/<version>/AutoInventario.exe`.
- `AutoInventario.Updater.exe` en `/updates/AutoInventario.Updater.exe`.
- `latest.json` con version, URLs y hashes SHA256.

El agente compara version local/remota, descarga ambos binarios, valida hashes y ejecuta el updater.

Checklist antes de publicar:

- Build de solucion verde.
- Tests funcionales verdes.
- EXEs firmados.
- `latest.json` generado con `baseURL` publico correcto.
- Endpoint `/updates/latest.json` accesible desde un equipo cliente.
- Hashes coinciden con los binarios publicados.

## Lambda

Empaquetado manual:

```bash
cd Lambda-Inventario
bash deploy_lambda.sh
```

Checklist:

- `requirements.txt` con versiones fijadas.
- `lambda_function.py` exporta el handler configurado.
- Secrets Manager contiene `manageengine_api_key` y `autoinventario/private_key`.
- IAM permite lectura de ambos secretos y acceso minimo a S3/SSM si se usan.
- Regiones configuradas de forma coherente.

## Terraform

```bash
cd Infraestructura-Terraform
terraform init
terraform fmt -recursive
terraform validate
terraform plan
terraform apply
```

Checklist:

- No hay claves privadas reales en `.tf`.
- `handler` coincide con Lambda.
- `filename` coincide con el ZIP generado.
- IAM cubre solo recursos necesarios.
- Variables por entorno se pasan por `*.tfvars` no versionados o pipeline secrets.

## Respuesta ante incidentes

Para exposicion de credenciales:

1. Bloquear o rotar la credencial.
2. Revisar logs de uso.
3. Reemitir secretos a entornos.
4. Invalidar artefactos firmados si incluian el secreto.
5. Documentar fecha, alcance y acciones.

Para fallos de inventario:

1. Revisar logs del agente en `C:\ProgramData\AutoInventario\logs.txt`.
2. Confirmar reachability del Webhook.
3. Validar `latest.json` y certificados TLS.
4. Revisar logs Webhook/Lambda.
5. Comprobar respuesta ManageEngine y request de error generado.
