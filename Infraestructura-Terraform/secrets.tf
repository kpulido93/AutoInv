resource "aws_secretsmanager_secret" "private_key" {
  name = var.secrets_manager_name
}

# El valor del secreto no debe gestionarse en Git.
# Cargalo fuera de Terraform o mediante un pipeline seguro.
