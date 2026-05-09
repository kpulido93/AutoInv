#!/bin/bash

ZIP_FILE="lambda_package.zip"

# Instalar dependencias en un directorio temporal
mkdir -p package
pip install -r requirements.txt -t package/

# Agregar archivos de la Lambda
cd package
zip -r9 ../$ZIP_FILE .
cd ..
zip -g $ZIP_FILE lambda_function.py config.json

# Desplegar en AWS Lambda
aws lambda update-function-code --function-name LambdaInventario --zip-file fileb://$ZIP_FILE
