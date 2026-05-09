# Infraestructura Terraform

Modulo Terraform para provisionar recursos AWS relacionados con AutoInventario.

## Recursos definidos

- IAM Role para Lambda.
- IAM Policy para lectura de Secrets Manager.
- Attachment role/policy.
- Lambda function.
- Secret en AWS Secrets Manager.

## Requisitos

- Terraform >= 1.5.
- AWS CLI o credenciales de provider configuradas.
- ZIP de Lambda generado en la ruta que Terraform espera.

## Uso

```bash
terraform init
terraform fmt -recursive
terraform validate
terraform plan
terraform apply
```

## Variables

| Variable | Descripcion | Valor actual por defecto |
| --- | --- | --- |
| `aws_region` | Region AWS del provider. | `us-east-1` |
| `lambda_function_name` | Nombre de Lambda. | `LambdaInventario` |
| `secrets_manager_name` | Nombre del secreto de clave privada. | `autoinventario/private_key` |

## Pendientes detectados

- Ejecutar `terraform fmt -recursive`; `main.tf` no pasa `fmt -check`.
- Ejecutar `terraform init` antes de `terraform validate`.
- Corregir handler a `lambda_function.lambda_handler`.
- Alinear `filename` con el ZIP generado por `Lambda-Inventario/deploy_lambda.sh`.
- No almacenar la clave privada real en `secrets.tf`.
- Anadir permisos IAM minimos para `manageengine_api_key`, S3 y SSM si la Lambda sigue usandolos.
- Parametrizar regiones para evitar divergencias entre Terraform, Lambda y Webhook.

## Seguridad

No versionar secretos ni material criptografico. Para cargar el valor inicial de un secreto, usar un mecanismo protegido del pipeline, `terraform.tfvars` no versionado o carga manual controlada. Si una clave privada ya fue commiteada, rotarla y limpiar historial antes de distribuir el repositorio.
