# Cifrado del Payload

AutoInventario usa cifrado hibrido para enviar inventario al Webhook:

- RSA-OAEP-SHA256 cifra la clave simetrica por mensaje.
- AES-256-GCM cifra y autentica el JSON de inventario.
- El Webhook conserva lectura legacy AES-CBC durante la transicion.

No registrar claves, payloads cifrados completos, plaintext, tags ni material de error que pueda incluir datos sensibles.

## Formato Actual

`crypto_version=2`:

```json
{
  "clientID": "<id-cliente>",
  "crypto_version": "2",
  "ciphertext": "<base64>",
  "encrypted_key": "<base64>",
  "nonce": "<base64>",
  "tag": "<base64>"
}
```

Campos:

| Campo | Descripcion |
| --- | --- |
| `clientID` | Identificador de cliente. Se autentica como AAD. |
| `crypto_version` | Version del formato. Valor actual: `2`. Se autentica como AAD. |
| `ciphertext` | JSON de inventario cifrado con AES-GCM. |
| `encrypted_key` | Clave AES de 256 bits cifrada con RSA-OAEP-SHA256. |
| `nonce` | Nonce AES-GCM de 96 bits. No es secreto, pero debe ser unico por clave. |
| `tag` | Tag AES-GCM de 128 bits. |

AAD:

```text
AutoInventario|<crypto_version>|<clientID>
```

Si se altera `clientID`, `crypto_version`, `ciphertext`, `nonce` o `tag`, el descifrado falla. Las versiones desconocidas se rechazan; el fallback legacy solo aplica cuando `crypto_version` no esta presente.

## Compatibilidad Legacy

Durante la transicion, el Webhook y la Lambda aceptan el formato antiguo:

```json
{
  "clientID": "<id-cliente>",
  "data": "<base64>",
  "key": "<base64>",
  "iv": "<base64>"
}
```

Ese formato usa AES-CBC y no aporta autenticidad. Debe retirarse cuando todos los agentes desplegados emitan `crypto_version=2`.

## Rotacion

- La clave publica embebida en el agente cifra claves por mensaje.
- La clave privada debe estar fuera del repositorio y protegida con ACL/secret store.
- Si la clave privada se expone, rotar par RSA y redistribuir agente con nueva clave publica.
- El Webhook debe rechazar payloads alterados; no hacer fallback silencioso de `crypto_version=2` a legacy.

## Validacion

Tests esperados:

```powershell
dotnet test
python -m py_compile Lambda-Inventario\lambda_function.py
```

Cobertura actual:

- Roundtrip AES-GCM valido.
- Rechazo de `ciphertext` alterado.
- Rechazo de `tag` alterado.
- Lectura legacy temporal.
