# AutoInventario

AutoInventario es una suite para recopilar inventario de equipos Windows, cifrar el payload localmente y sincronizarlo con ManageEngine ServiceDesk Plus a traves de un Webhook ASP.NET Core y una Lambda en AWS.

## Estado auditado

Auditoria realizada el 2026-05-09 sobre el arbol de trabajo local.

El repositorio contiene una reubicacion incompleta del agente: Git marca como eliminada la carpeta historica `Autoinventario/` y aparecen archivos equivalentes sin seguimiento en la raiz (`Program.cs`, `Services/`, `Helpers/`, `Models/`, `Resources/`, etc.). Por ese motivo la solucion principal no compila hasta restaurar la ruta historica o actualizar referencias de solucion, proyectos, tests y pipeline.

Resumen de validaciones ejecutadas:

| Validacion | Resultado |
| --- | --- |
| `dotnet build AutoInventario.sln -c Debug` | Falla: falta `AutoInventario/AutoInventario.csproj`. |
| `dotnet build AutoInventario.csproj -c Debug` | Falla: recurso no texto requiere `System.Resources.Extensions`; tambien hay advertencia NU1603. |
| `dotnet build AutoInventario.Updater/AutoInventario.Updater.csproj -c Debug` | Correcto. |
| `dotnet build Webhook/Webhook-Inventario.csproj -c Debug` | Correcto con avisos: `net6.0` sin soporte y nullable fuera de contexto. |
| `dotnet test AutoInventario.Tests/AutoInventario.Tests.csproj -c Debug` | Pasa 1 test vacio; avisa que el proyecto referenciado no existe. |
| `python -m py_compile Lambda-Inventario/lambda_function.py` | Correcto. |
| `terraform fmt -check -recursive` | Falla formato en `Infraestructura-Terraform/main.tf`. |
| `terraform validate -no-color` | Falla porque falta inicializar el provider AWS con `terraform init`. |
| `dotnet list package --vulnerable --include-transitive` | Vulnerabilidades transitivas en agente y Webhook. |

El informe completo esta en [docs/AUDIT.md](docs/AUDIT.md).

## Arquitectura

```mermaid
flowchart LR
    Agent["Agente Windows .NET 8"] -->|"JSON inventario + AES/RSA"| Webhook["Webhook ASP.NET Core"]
    Webhook -->|"Invoke Lambda"| Lambda["AWS Lambda Python 3.12"]
    Lambda -->|"Create/Update workstation"| ManageEngine["ManageEngine ServiceDesk Plus"]
    Lambda -->|"Secrets"| Secrets["AWS Secrets Manager"]
    Lambda -->|"Consulta auxiliar"| S3SSM["S3 + SSM/EC2"]
    Webhook -->|"Clientes"| Postgres["PostgreSQL ManageEngine"]
    Pipeline["Azure Pipelines"] -->|"Publica EXE y Web"| Webhook
    Terraform["Terraform"] -->|"Infra AWS"| Lambda
```

Componentes principales:

- Agente Windows: ejecutable .NET 8 que recopila datos por WMI, registro y APIs de Windows, cifra el inventario y lo envia al Webhook.
- Updater: ejecutable .NET 8 que reemplaza el agente instalado y relanza la tarea programada.
- Webhook: API ASP.NET Core que recibe eventos cifrados, expone clientes y manifiesto de actualizaciones.
- Lambda: funcion Python que descifra, normaliza y crea/actualiza workstations en ManageEngine.
- Terraform: definicion base de rol IAM, Secrets Manager y Lambda.
- Azure Pipelines: restore, tests, SonarCloud, publish, firma de ejecutables y empaquetado del sitio Webhook.

## Estructura del repositorio

| Ruta | Proposito |
| --- | --- |
| `AutoInventario.csproj` | Proyecto del agente en la raiz. Actualmente no esta alineado con la solucion. |
| `Program.cs`, `Helpers/`, `Models/`, `Services/`, `Resources/` | Codigo del agente en el arbol de trabajo actual. |
| `AutoInventario.Updater/` | Updater independiente del agente. |
| `AutoInventario.Tests/` | Proyecto xUnit. Hoy contiene una prueba placeholder. |
| `Webhook/` | API ASP.NET Core, pagina de estado, endpoints de clientes y actualizaciones. |
| `Lambda-Inventario/` | Lambda Python, requirements y script de empaquetado/despliegue. |
| `Infraestructura-Terraform/` | Infraestructura AWS declarativa. |
| `.azure-pipelines/pipeline.yml` | Pipeline Terraform. |
| `azure-pipelines.yml` | Pipeline principal de calidad, build, firma y artefactos. |
| `docs/` | Auditoria, arquitectura y operacion. |
| `AGENTS.MD` | Plantilla de instrucciones para agentes de IA/coding assistants. |

## Requisitos

- Windows para ejecutar y validar completamente el agente.
- .NET SDK 8 para agente, updater y tests.
- .NET SDK/runtime 6 para el Webhook actual.
- Python 3.12 para Lambda.
- Terraform >= 1.5 para infraestructura.
- AWS CLI configurado para despliegues Lambda/Terraform.
- Credenciales y secretos gestionados fuera del repositorio.

## Configuracion

No guardes secretos reales en el repositorio. Cualquier valor que ya haya sido versionado debe considerarse comprometido y rotarse.

Webhook:

- `ConnectionStrings:Postgres`: conexion de lectura a PostgreSQL/ManageEngine.
- `AutoInventario:ApiKey`: clave esperada por `X-AutoInventario-Key` para rutas protegidas.
- `Security:PrivateKeyPath`: ruta local de la clave privada. Preferible por variable `WEBHOOK_PRIVATE_KEY_PATH`.
- `AWS_REGION`: region usada por el AWS SDK.
- `AUTOINVENTARIO_LAMBDA_NAME`: nombre de la Lambda a invocar.
- Credenciales AWS: usar IAM Role, variables de entorno o provider chain de AWS SDK; no claves en codigo.

Lambda:

- `manageengine_api_key`: token de ManageEngine en AWS Secrets Manager.
- `autoinventario/private_key`: clave privada en AWS Secrets Manager.
- Ajustar regiones y permisos IAM para Secrets Manager, S3 y SSM segun el entorno real.

Agente:

- `-client_id <id>`: cliente/organizacion ManageEngine.
- `-url <base_url>`: URL base del Webhook. El endpoint final es `/webhooks`.
- `-del`: desinstala el agente y elimina la tarea programada.
- `AUTOINVENTARIO_WEBHOOK_API_KEY`: API key enviada por el agente al Webhook cuando el endpoint la requiera.

## Compilacion y pruebas

Estado actual recomendado para diagnostico:

```powershell
dotnet build AutoInventario.sln -c Debug
dotnet build AutoInventario.csproj -c Debug
dotnet build AutoInventario.Updater\AutoInventario.Updater.csproj -c Debug
dotnet build Webhook\Webhook-Inventario.csproj -c Debug
dotnet test AutoInventario.Tests\AutoInventario.Tests.csproj -c Debug
python -m py_compile Lambda-Inventario\lambda_function.py
terraform -chdir=Infraestructura-Terraform fmt -check -recursive
terraform -chdir=Infraestructura-Terraform validate -no-color
```

Antes de tratar el build como estable, resolver:

- Rutas obsoletas hacia `Autoinventario/AutoInventario.csproj`.
- Recurso publico embebido del agente.
- Exclusion de subproyectos desde el proyecto raiz si se mantiene `AutoInventario.csproj` en la raiz.
- Migracion del Webhook desde .NET 6 a una version soportada.
- Tests reales para cifrado, serializacion, updater, endpoints y Lambda.

## Despliegue

Pipeline principal:

1. Restaura y prueba proyectos .NET.
2. Ejecuta SonarCloud y coverage.
3. Publica agente, updater y Webhook.
4. Firma los ejecutables con PFX seguro de Azure DevOps.
5. Copia artefactos de actualizacion a `wwwroot/updates`.
6. Genera `latest.json` con version, URLs y hashes SHA256.

Lambda:

```bash
cd Lambda-Inventario
bash deploy_lambda.sh
```

Terraform:

```bash
cd Infraestructura-Terraform
terraform init
terraform fmt -recursive
terraform plan
terraform apply
```

Revisar [docs/OPERATIONS.md](docs/OPERATIONS.md) antes de desplegar.

## Seguridad

El proyecto maneja datos sensibles: identificadores de hardware, usuarios locales, IPs, licencias y contrasenas de recuperacion BitLocker. La operacion debe tener base legal, retencion definida y controles de acceso.

Hallazgos criticos de la auditoria:

- Hay secretos, tokens, claves privadas y credenciales AWS en archivos del repo/arbol local.
- El POST `/webhooks` no valida actualmente la API key en el controlador.
- Hay logging de claves o datos sensibles en varios flujos.
- El cifrado usa confidencialidad, pero no autenticidad del payload; conviene migrar a AEAD o agregar firma/HMAC.
- Dependencias y frameworks tienen avisos de soporte/vulnerabilidad.

Consulta [docs/AUDIT.md](docs/AUDIT.md) para prioridades y remediacion.
