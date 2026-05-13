# Lambda-Inventario

Funcion AWS Lambda en Python que recibe el payload cifrado de AutoInventario, lo descifra, normaliza campos y crea o actualiza workstations en ManageEngine ServiceDesk Plus.

## Requisitos

- Python 3.12.
- AWS CLI configurado para despliegue.
- AWS Secrets Manager con secretos:
  - `autoinventario/private_key`
  - `manageengine_api_key`
- Permisos IAM para Secrets Manager y, si se mantiene el flujo actual, S3 y SSM.

## Dependencias

`requirements.txt` contiene:

- `boto3`
- `pycryptodome`
- `requests`
- `cryptography`

Pendiente: fijar versiones exactas y auditar vulnerabilidades en CI.

## Handler

El codigo expone:

```python
lambda_handler(event, context)
```

La configuracion Terraform debe usar `lambda_function.lambda_handler`.

## Evento esperado

Formato actual autenticado:

```json
{
  "clientID": "1",
  "crypto_version": "2",
  "ciphertext": "<payload cifrado base64>",
  "encrypted_key": "<clave AES cifrada base64>",
  "nonce": "<nonce base64>",
  "tag": "<tag base64>"
}
```

Formato legacy aceptado temporalmente:

```json
{
  "clientID": "1",
  "data": "<payload cifrado base64>",
  "key": "<clave AES cifrada base64>",
  "iv": "<iv base64>"
}
```

Ver `docs/CRYPTO.md` para el contrato criptografico y el plan de retirada del formato legacy.

## Despliegue manual

```bash
cd Lambda-Inventario
bash deploy_lambda.sh
```

El script instala dependencias en `package/`, crea `lambda_package.zip` y ejecuta `aws lambda update-function-code`.

## Configuracion operativa

Revisar antes de desplegar:

- Region de Secrets Manager.
- Nombre de Lambda.
- Token ManageEngine en Secrets Manager.
- Region y permisos de S3/SSM usados para datos auxiliares.
- URL de ManageEngine.

## Riesgos actuales

- Existen archivos locales con secretos y paquetes generados que no deben versionarse.
- `requirements.txt` no fija versiones.
- La region esta hard-codeada en varias funciones.
- Terraform no concede actualmente todos los permisos que el codigo usa.
- La normalizacion de payload y llamadas a ManageEngine no tienen tests automatizados.
