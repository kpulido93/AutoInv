output "lambda_function_name" {
  value = aws_lambda_function.lambda_inventario.function_name
}

output "secrets_manager_arn" {
  value = aws_secretsmanager_secret.private_key.arn
}
