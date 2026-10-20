# ADR 0001: AWS optional self-hosted architecture

- Status: Accepted
- Date: 2026-05-18

## Context

AutoInventario esta evolucionando desde una solucion interna con fuerte dependencia de AWS hacia un producto corporativo self-hosted.

El diseno historico concentra el procesamiento completo de inventario en AWS Lambda y usa AWS Secrets Manager como store principal de secretos. Ese enfoque sigue siendo valido para despliegues que ya operan en AWS, pero introduce friccion comercial y tecnica para clientes que:

- No pueden usar AWS por politica corporativa o regulatoria.
- Necesitan operar totalmente on-prem o en una nube distinta.
- Requieren portabilidad operativa entre IIS, Docker y, a futuro, otros entornos de hosting.

El codigo actual ya refleja parte de esa transicion:

- `IInventoryProcessor` permite seleccionar `AwsLambda` o `Local` por configuracion.
- `ISecretProvider` desacopla el acceso a secretos de AWS Secrets Manager.
- `IInventoryNormalizer` e `IAssetConnector` existen como puntos de extension internos.
- El modo `Local` del Webhook existe hoy, pero solo como stub inicial y todavia no reemplaza el procesamiento completo que hoy vive en Lambda.

## Decision

AutoInventario adopta una arquitectura donde AWS pasa a ser un proveedor opcional, no una dependencia obligatoria del producto.

Esto implica las siguientes decisiones:

1. El producto debe poder desplegarse y operar en una modalidad self-hosted sin requerir AWS.
2. AWS Lambda seguira soportado como implementacion de procesamiento mientras exista valor operativo para clientes que ya usan AWS.
3. El procesamiento de inventario debe quedar encapsulado detras de interfaces y seleccionarse por configuracion. `AwsLambda` es la implementacion completa actual; `Local` es la ruta soportada para evolucionar hacia procesamiento self-hosted.
4. Los secretos deben resolverse mediante interfaces y stores externos al repositorio. AWS Secrets Manager es una opcion valida, pero no exclusiva.
5. Los conectores hacia sistemas destino deben permanecer desacoplados para que ManageEngine siga siendo el primer conector soportado sin convertirlo en una dependencia rigida de arquitectura.
6. La documentacion y los despliegues deben contemplar Docker, IIS/Windows Server y otros modelos self-hosted sin eliminar la opcion AWS.

## Consequences

Consecuencias positivas:

- Reduce lock-in tecnico y comercial hacia AWS.
- Abre conversaciones con clientes corporativos que exigen despliegue on-prem o control total del entorno.
- Permite evolucionar el Webhook hacia un backend autoalojado mas completo sin cambiar contratos del agente.
- Hace viable una estrategia de empaquetado para Docker, IIS y, mas adelante, otras plataformas de hosting.

Consecuencias negativas o costos:

- Requiere mas interfaces, mas configuracion y mas pruebas de integracion.
- Obliga a mantener mas de un camino operativo para procesamiento y secretos.
- Aumenta la necesidad de observabilidad y troubleshooting por entorno.
- Exige documentar con precision que la paridad funcional entre `AwsLambda` y `Local` todavia no existe.

## Brechas actuales

Brechas que quedan explicitamente aceptadas con esta decision:

- `LocalInventoryProcessor` sigue siendo un stub y todavia no sincroniza con ManageEngine.
- El flujo AWS sigue siendo la implementacion completa de referencia para procesamiento.
- Parte de la infraestructura y de la operacion continua siendo AWS-centric, especialmente Lambda y Terraform.
- Todavia no existe una historia completa de despliegue en Kubernetes; solo quedan habilitados conceptualmente Docker e IIS.

## Alternativas consideradas

### 1. Mantener una arquitectura 100% AWS

Se descarta como direccion unica porque limita la portabilidad y deja fuera clientes que no pueden o no quieren depender de AWS.

### 2. Portar toda la solucion a Azure como reemplazo de AWS

Se descarta como estrategia primaria porque cambia un lock-in por otro. El objetivo no es moverse de AWS a otra nube concreta, sino reducir la dependencia obligatoria de cualquier proveedor.

### 3. Implementar desde ahora una arquitectura totalmente plugin-based

Se pospone. La idea es atractiva para conectores y providers, pero hoy agregaria complejidad estructural antes de completar la ruta self-hosted minima y estabilizar los contratos internos ya introducidos.

## Proximos pasos esperados

Trabajo esperado despues de esta decision:

- Completar una implementacion local real de procesamiento que deje de ser stub.
- Mantener y ampliar pruebas para `AwsLambda` y `Local`.
- Endurecer la documentacion operativa de configuracion, secretos y despliegue self-hosted.
- Revisar CI/CD para que las rutas no-AWS tengan validaciones claras y repetibles.
