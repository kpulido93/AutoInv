variable "aws_region" {
  default = "us-east-1"
}

variable "lambda_function_name" {
  default = "LambdaInventario"
}

variable "secrets_manager_name" {
  default = "autoinventario/private_key"
}

variable "lambda_package_path" {
  default = "lambda_package.zip"
}
