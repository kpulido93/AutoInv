import json
import boto3
import requests
import base64
import time
import logging
import re
from botocore.exceptions import BotoCoreError, NoCredentialsError, ClientError
from cryptography.hazmat.primitives import serialization, hashes
from cryptography.hazmat.primitives.asymmetric import padding
from Crypto.Cipher import AES
from datetime import datetime

logger = logging.getLogger()
logger.setLevel(logging.INFO)

def replace_none_with_null(obj):
    """Recursivamente elimina claves con valores `None` en todo el JSON"""
    if isinstance(obj, dict):
        # Crear un nuevo diccionario solo con las claves que no tengan el valor `None`
        return {k: replace_none_with_null(v) for k, v in obj.items() if v is not None}
    elif isinstance(obj, list):
        # Si es una lista, eliminar elementos que sean `None`
        return [replace_none_with_null(v) for v in obj if v is not None]
    return obj

def rev_ports(workstation):
    if "ports" in workstation:
        workstation["ports"] = [
            p for p in workstation["ports"]
            if "name" in p and p["name"]
        ]
    return workstation

def rev_disk(workstation):
    if "hard_disks" in workstation:
        for disco in workstation["hard_disks"]:
            # Asegurar que 'capacity' exista
            if "capacity" not in disco:
                disco["capacity"] = 0
            
            # Limpiar y validar 'serial_number'
            serial = disco.get("serial_number", "")
            if isinstance(serial, str):
                serial_limpio = serial.strip()
                disco["serial_number"] = serial_limpio if serial_limpio else "UNKNOWN"
            else:
                disco["serial_number"] = "UNKNOWN"
    return workstation
    
def rev_phisical_drives(workstation):
    if "physical_drives" in workstation:
        for drive in workstation["physical_drives"]:
            version = drive.get("version", "")
            if isinstance(version, str):
                # Eliminar caracteres no imprimibles (incluye \x01, \n, \t, etc.)
                version_clean = re.sub(r'[^\x20-\x7E]', '', version).strip()
                
                if version_clean:
                    drive["version"] = version_clean
                else:
                    drive.pop("version", None)
            else:
                drive.pop("version", None)
    return workstation

def rev_provider_disk(workstation, max_length=99):

    if "physical_drives" in workstation:
        for drive in workstation["physical_drives"]:
            if "provider" in drive and isinstance(drive["provider"], str):
                if len(drive["provider"]) > max_length:
                    drive["provider"] = drive["provider"][:max_length]
    return workstation

def replace_defaultstring_with_name(obj, name_value):
    """Recursivamente reemplaza cualquier aparición de 'defaultstring' por 'DEFAULTSTRING-<name_value>'"""
    if isinstance(obj, dict):
        return {k: replace_defaultstring_with_name(v, name_value) for k, v in obj.items()}
    elif isinstance(obj, list):
        return [replace_defaultstring_with_name(v, name_value) for v in obj]
    elif isinstance(obj, str):
        # Reemplazar 'defaultstring' y 'SystemSerialNumber' (sin importar el caso)
        obj = re.sub(r'defaultstring', f'DEFAULTSTRING-{name_value}', obj, flags=re.IGNORECASE)
        obj = re.sub(r'SystemSerialNumber', f'SystemSerialNumber-{name_value}', obj, flags=re.IGNORECASE)
        obj = re.sub(r'TobefilledbyO.E.M', f'TobefilledbyO.E.M-{name_value}', obj, flags=re.IGNORECASE)
        return obj
    return obj

def get_api_key():
    secret_name = "manageengine_api_key"
    region_name = "eu-west-1"
    session = boto3.session.Session()
    client = session.client(service_name="secretsmanager", region_name=region_name)
    try:
        response = client.get_secret_value(SecretId=secret_name)
        return response["SecretString"]
    except (BotoCoreError, NoCredentialsError, ClientError) as e:
        logger.error(f"Error obteniendo la API Key: {str(e)}")
        return None

def report_failure_request(error_message, hostname=None, serial_number=None, workstation=None):
    url = "https://soporte.stillion.tech/api/v3/requests"
    api_key = get_api_key()
    if not api_key:
        logger.error("No se pudo obtener la API Key para reportar error.")
        return

    headers = {
        "authtoken": api_key,
        "Content-Type": "application/x-www-form-urlencoded"
    }

    descripcion = f"Error en Lambda al procesar workstation.\n\n" \
                  f"Hostname: {hostname or 'No disponible'}\n" \
                  f"Serial: {serial_number or 'No disponible'}\n" \
                  f"Mensaje de error: {error_message}\n" \
                  f"Timestamp: {datetime.utcnow().isoformat()}Z\n" \
                  f"{workstation}"
    
    subject_value = f"AutoInventario | Error al procesar equipo {hostname or serial_number or 'desconocido'}"

    request_data = {
        "request": {
            "subject": subject_value,
            "description": descripcion,
        "status": {"name": "Open"},
        "requester": {"email_id": "soporte@mrhouston.net"},
        "template": {"id": "4"},
        "priority": {"id": "2"},
        "group": {"id": "2101"},
        "request_type": {"id": "2"}
    }
    }

    try:
        r = requests.post(
            url,
            headers=headers,
            data={"input_data": json.dumps(request_data)},
            timeout=5
        )
        if r.status_code not in [200, 201]:
            logger.error(f"Fallo al crear request: {r.status_code} - {r.text}")
        else:
            logger.info(f"Request creado para error: {hostname}")
    except requests.exceptions.RequestException as e:
        logger.error(f"No se pudo enviar request de error: {str(e)}")

def get_postgres_data():
    bucket_name = "s-autoinventario"
    object_key = "workstation_data.json"
    region_name = "eu-south-2"
    
    ssm = boto3.client('ssm', region_name='eu-south-2')
    instance_id = "i-02dd3439e773e468b"
    command = "python3 /var/scripts/update_json.py"
    s3 = boto3.client("s3", region_name=region_name)

    try:

        response = ssm.send_command(
            InstanceIds=[instance_id],
            DocumentName="AWS-RunShellScript",
            Parameters={'commands': [command]},
            TimeoutSeconds=30
        )
        time.sleep(1)

        response = s3.get_object(Bucket=bucket_name, Key=object_key)
        content = response['Body'].read().decode('utf-8')
        json_data = json.loads(content)
        return json_data
    except (BotoCoreError, NoCredentialsError, json.JSONDecodeError, ClientError) as e:
        logger.error(f"Error leyendo JSON desde S3: {str(e)}")
        return None

def get_private_key():
    secret_name = "autoinventario/private_key"
    region_name = "eu-west-1"
    session = boto3.session.Session()
    client = session.client(service_name="secretsmanager", region_name=region_name)
    try:
        response = client.get_secret_value(SecretId=secret_name)
        private_key_pem = response["SecretString"]
        private_key = serialization.load_pem_private_key(private_key_pem.encode(), password=None)
        return private_key
    except (BotoCoreError, NoCredentialsError, ClientError) as e:
        logger.error(f"Error obteniendo la clave privada: {str(e)}")
        return None

def unpad(s):
    padding_len = s[-1]
    return s[:-padding_len]

def build_associated_data(crypto_version, client_id):
    return f"AutoInventario|{crypto_version}|{client_id}".encode("utf-8")

def decrypt_payload(payload, client_id=None):
    private_key = get_private_key()
    if private_key is None:
        raise Exception("No se pudo obtener la clave privada")

    if payload.get("crypto_version") == "2":
        if not client_id:
            raise ValueError("clientID no proporcionado para payload autenticado.")

        encrypted_key_b64 = payload.get("encrypted_key")
        ciphertext_b64 = payload.get("ciphertext")
        nonce_b64 = payload.get("nonce") or payload.get("iv")
        tag_b64 = payload.get("tag")

        if not encrypted_key_b64 or not ciphertext_b64 or not nonce_b64 or not tag_b64:
            raise ValueError("Faltan campos del payload autenticado.")

        encrypted_key = base64.b64decode(encrypted_key_b64)
        aes_key = private_key.decrypt(
            encrypted_key,
            padding.OAEP(
                mgf=padding.MGF1(algorithm=hashes.SHA256()),
                algorithm=hashes.SHA256(),
                label=None
            )
        )

        nonce = base64.b64decode(nonce_b64)
        ciphertext = base64.b64decode(ciphertext_b64)
        tag = base64.b64decode(tag_b64)
        if len(nonce) != 12 or len(tag) != 16:
            raise ValueError("Tamaño inválido de nonce o tag.")

        cipher = AES.new(aes_key, AES.MODE_GCM, nonce=nonce)
        cipher.update(build_associated_data("2", str(client_id)))
        plaintext = cipher.decrypt_and_verify(ciphertext, tag)
        return json.loads(plaintext.decode())

    if payload.get("crypto_version"):
        raise ValueError("crypto_version no soportada.")
    
    encrypted_key_b64 = payload.get("key")
    encrypted_key = base64.b64decode(encrypted_key_b64) if encrypted_key_b64 else None
    if not encrypted_key:
        raise ValueError("No se encontró la clave cifrada.")
    
    aes_key = private_key.decrypt(
        encrypted_key,
        padding.OAEP(
            mgf=padding.MGF1(algorithm=hashes.SHA256()),
            algorithm=hashes.SHA256(),
            label=None
        )
    )
    
    iv = base64.b64decode(payload.get("iv")) if payload.get("iv") else None
    encrypted_data = base64.b64decode(payload.get("data")) if payload.get("data") else None
    if iv is None or encrypted_data is None:
        raise ValueError("Faltan datos necesarios para la desencriptación.")
    
    cipher = AES.new(aes_key, AES.MODE_CBC, iv)
    decrypted_bytes = cipher.decrypt(encrypted_data)
    plaintext = unpad(decrypted_bytes)
    return json.loads(plaintext.decode())

def get_workstation_id(serial_number):
    postgres_data = get_postgres_data()
    for item in postgres_data["data"]:
        if item[1] == serial_number:
            return item[0]
    return None

def update_workstation(workstation, workstation_id, url, headers):
    update_data = {"workstation": workstation }
    try:
        r = requests.put(
            f"{url}/{workstation_id}",
            headers=headers,
            data={"input_data": json.dumps(update_data)},
            timeout=5
        )
        return {
            "operation": "Update Workstation",
            "statusCode": r.status_code,
            "URL": f"{url}/{workstation_id}",
            "body": r.text,
            "Data": json.dumps(update_data)
        }
    except requests.exceptions.RequestException as e:
        return {"statusCode": 500, "body": f"Error al enviar datos a API: {str(e)}"}

def lambda_handler(event, context):
    logger.info("Inicio de ejecución de Lambda")
    url = "https://soporte.stillion.tech/api/v3/workstations"
    api_key = get_api_key()
    if not api_key:
        logger.error("No se pudo obtener la API Key.")
        return {"statusCode": 500, "body": "Error obteniendo la API Key"}

    client_id = event.get("clientID")
    if not client_id:
        logger.warning("clientID no proporcionado en el evento.")
        return {"statusCode": 400, "body": "clientID no proporcionado"}
    
    headers = {"authtoken": api_key, "Content-Type": "application/x-www-form-urlencoded"}
    
    try:
        encrypted_payload = {
            "crypto_version": event.get("crypto_version"),
            "data": event.get("data"),
            "key": event.get("key"),
            "iv": event.get("iv"),
            "ciphertext": event.get("ciphertext"),
            "encrypted_key": event.get("encrypted_key"),
            "nonce": event.get("nonce"),
            "tag": event.get("tag")
        }
        decrypted_event = decrypt_payload(encrypted_payload, client_id=client_id)
        decrypted_event = replace_none_with_null(decrypted_event)

        workstation = decrypted_event.get("workstation", {})
        name_value = workstation.get("name", "")
        
        # Reemplazar 'defaultstring' en todo el JSON por 'DEFAULTSTRING-<name_value>'
        decrypted_event = replace_defaultstring_with_name(decrypted_event, name_value)
 
        logger.info("Payload desencriptado correctamente.")
    except Exception as e:
        logger.error(f"Error en desencriptación: {str(e)}")
        return {"statusCode": 400, "body": f"Error en desencriptación: {str(e)}"}
    
    workstation = decrypted_event.get("workstation", {})
    workstation = rev_ports(workstation)
    workstation = rev_disk(workstation)
    workstation = rev_phisical_drives(workstation)
    workstation = rev_provider_disk(workstation)
    serial_number = workstation.get("org_serial_number")
    hostname = workstation.get("name")
    
    if not serial_number:
        logger.warning("SerialNumber no proporcionado en el payload.")
        return {"statusCode": 400, "body": "SerialNumber no proporcionado"}
    
    try:
        client_id = int(client_id)
    except ValueError:
        logger.warning(f"clientID inválido: {client_id}")
        return {"statusCode": 400, "body": f"client_id inválido: {client_id}"}
    

    account_obj = {"id": client_id}
    workstation_id = get_workstation_id(serial_number)
    
    if workstation_id:
        logger.info(f"Actualizando workstation {workstation_id}")
        update_data = {"workstation": workstation }
        try:
            r = requests.put(
                f"{url}/{workstation_id}",
                headers=headers,
                data={"input_data": json.dumps(update_data)},
                timeout=5
            )
            if r.status_code in [200, 201]:
                logger.info(
                    f"Operation: Update Workstation {hostname} | Correcto"
                )
            else:
                logger.error(
                    f"Operation: Update Workstation {hostname} | statusCode: {r.status_code}, URL: {url}/{workstation_id}, Data: {json.dumps(update_data)}, Body: {r.text}"
                )
                report_failure_request(r.text, hostname=hostname, serial_number=serial_number, workstation=decrypted_event
                )
            return {
                "operation": "Update Workstation",
                "statusCode": r.status_code,
                "URL": f"{url}/{workstation_id}",
                "body": r.text,
                "Data": json.dumps(update_data)
            }
        except requests.exceptions.RequestException as e:
            logger.error(f"Error al actualizar Workstation {hostname}: {e}")
            report_failure_request(str(e), hostname=hostname, serial_number=serial_number, workstation=decrypted_event)
            return {"statusCode": 500, "body": f"Error al enviar datos a API: {str(e)}"}

    else:
        computer_system = workstation.get("computer_system", {}).copy()
        if "bios_date" in computer_system:
            del computer_system["bios_date"]

        new_pc_data = {
            "workstation": {
                "name": hostname,
                "org_serial_number": serial_number,
                "account": account_obj,
                "state": {
                    "name": "Pending",
                    "id": "601"
                },
                "computer_system": computer_system,
                "product": workstation.get("product", {}),
                "processors": workstation.get("processors", []),
                "physical_drives": workstation.get("physical_drives", []),
                "operating_system": workstation.get("operating_system", {}),
                "user_accounts": workstation.get("user_accounts", []),
                "workstation_udf_fields": workstation.get("workstation_udf_fields", {}),
                "asset_tag": workstation.get("asset_tag"),
                "allowed_vms": workstation.get("allowed_vms"),
                "vm_host": workstation.get("vm_host"),
                "memory": workstation.get("memory", {}),
                "last_logged_user": workstation.get("last_logged_user"),
                "sound_card": workstation.get("sound_card", {}),
                "acquisition_date": workstation.get("acquisition_date", {}),
                "logical_cpu_count": workstation.get("logical_cpu_count"),
                "is_remote_control_prompt_enabled": workstation.get("is_remote_control_prompt_enabled"),
                "is_server": workstation.get("is_server"),
            }
        }

        try:
            r = requests.post(
                url,
                headers=headers,
                data={"input_data": json.dumps(new_pc_data)},
                timeout=5
            )
            
            if r.status_code in [200, 201]:
                logger.info(f"Workstation {hostname} creada: {r.status_code}")

                workstation_id = get_workstation_id(serial_number)

                if workstation_id:   
                    
                    logger.info(f"Workstation creada con ID: {workstation_id}")
                    update_data = {"workstation": workstation }
                    try:
                        r = requests.put(
                            f"{url}/{workstation_id}",
                            headers=headers,
                            data={"input_data": json.dumps(update_data)},
                            timeout=5
                        )
                        logger.info("Actualización post-creación exitosa.")
                        return {
                            "operation": "Update Workstation",
                            "statusCode": r.status_code,
                            "URL": f"{url}/{workstation_id}",
                            "body": r.text,
                            "Data": json.dumps(update_data)
                        }
                    except requests.exceptions.RequestException as e:
                        logger.error(f"Error al actualizar tras crear: {e}")
                        return {"statusCode": 500, "body": f"Error al enviar datos a API: {str(e)}"}

                else:
                    logger.error("Error al obtener ID luego de creación.")
            else:
                logger.error(
                    f"Error al crear workstation {hostname}: {r.status_code}, {r.text}"
                )
                report_failure_request(r.text, hostname=hostname, serial_number=serial_number, workstation=decrypted_event)

            return {
                "operation": "Create Workstation",
                "statusCode": r.status_code,
                "body": r.text,
                "json_sent": new_pc_data
            }
        except requests.exceptions.RequestException as e:
            logger.error(f"Error al crear workstation: {e}")
            report_failure_request(str(e), hostname=hostname, serial_number=serial_number, workstation=decrypted_event)
            return {"statusCode": 500, "body": f"Error al enviar datos a API: {str(e)}"}
