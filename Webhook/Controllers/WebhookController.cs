using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Models;

using Services;

namespace Controllers
{
    [ApiController]
    [Route("webhooks")]
    public class WebhookController : ControllerBase
    {
        private const string VerifyToken = "YOUR_VERIFY_TOKEN";
        private readonly EventStore _eventStore;
        private readonly ILogger<WebhookController> _logger;
        private readonly IConfiguration _config;
        private string? _cachedPrivateKey;

        public WebhookController(EventStore eventStore, ILogger<WebhookController> logger, IConfiguration config)
        {
            _eventStore = eventStore;
            _logger = logger;
            _config = config;
        }

        [HttpGet]
        public IActionResult Verify([FromQuery(Name = "hub.mode")] string hub_mode,
                                    [FromQuery(Name = "hub.challenge")] string hub_challenge,
                                    [FromQuery(Name = "hub.verify_token")] string hub_verify_token)
        {
            _logger.LogInformation("Verificación de Webhook recibida. Mode: {Mode}, Token: {Token}", hub_mode, hub_verify_token);

            if (hub_mode == "subscribe" && hub_verify_token == VerifyToken)
            {
                _logger.LogInformation("Token verificado correctamente.");
                return Ok(hub_challenge);
            }

            _logger.LogWarning("Token incorrecto. Acceso denegado.");
            return Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> HandleEvent(
            [FromBody] WebhookEvent webhookEvent,
            [FromHeader(Name = "x-api-key")] string apiKey)
        {
            var secretKey = _config["Autoinventario:AWSKeyId"];

            _logger.LogInformation("Solicitud POST recibida. API Key: {ApiKey}", apiKey ?? "No enviada");

            //if (apiKey != secretKey)
            //{
            //    _logger.LogWarning("Acceso no autorizado. API Key incorrecta.");
            //    return Unauthorized("Acceso no autorizado.");
            //}

            if (webhookEvent == null || string.IsNullOrEmpty(webhookEvent.Data) ||
                string.IsNullOrEmpty(webhookEvent.Key) || string.IsNullOrEmpty(webhookEvent.IV))
            {
                _logger.LogError("Solicitud con formato inválido. Datos: {Data}, Key: {Key}, IV: {IV}",
                                 webhookEvent?.Data ?? "NULL",
                                 webhookEvent?.Key ?? "NULL",
                                 webhookEvent?.IV ?? "NULL");
                return BadRequest("Formato inválido.");
            }

            string decryptedData;
            try
            {
                _logger.LogInformation("Iniciando desencriptado de datos...");
                decryptedData = DecryptData(webhookEvent.Data, webhookEvent.Key, webhookEvent.IV);
                _logger.LogInformation("Datos desencriptados exitosamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Error al desencriptar los datos: {Error}", ex.Message);
                return BadRequest("No se pudo desencriptar los datos. Verifica la clave y el formato.");
            }

            _eventStore.AddEvent(webhookEvent.ClientID, decryptedData);
            _logger.LogInformation("Evento guardado localmente.");

            var lambdaInvoker = new LambdaInvoker();
            await lambdaInvoker.InvokeLambdaAsync(new
            {
                clientID = webhookEvent.ClientID,
                data = webhookEvent.Data,
                key = webhookEvent.Key,
                iv = webhookEvent.IV
            });

            _logger.LogInformation("Evento enviado correctamente a Lambda.");
            return Ok("Evento procesado correctamente y enviado a Lambda.");
        }

        private string ResolvePrivateKeyPath()
        {
            var path = Environment.GetEnvironmentVariable("WEBHOOK_PRIVATE_KEY_PATH");

            if (string.IsNullOrWhiteSpace(path))
                path = _config["Security:PrivateKeyPath"];

            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "mrbyte", "Webhook", "private.key"
                );

            return path;
        }

        private string LoadPrivateKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedPrivateKey))
                return _cachedPrivateKey;

            var path = ResolvePrivateKeyPath();

            _logger.LogInformation("Cargando clave privada desde: {Path}", path);

            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidOperationException("No hay ruta configurada para la clave privada.");

            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException($"No se encontró private.key en: {path}", path);

            _cachedPrivateKey = System.IO.File.ReadAllText(path);
            _logger.LogInformation("Clave privada cargada correctamente.");

            return _cachedPrivateKey;
        }

        private string DecryptData(string encryptedData, string encryptedKey, string ivBase64)
        {
            string privateKey = LoadPrivateKey();

            _logger.LogInformation("Iniciando desencriptado con RSA y AES...");

            byte[] encryptedAesKey = Convert.FromBase64String(encryptedKey);
            byte[] iv = Convert.FromBase64String(ivBase64);
            byte[] cipherText = Convert.FromBase64String(encryptedData);

            byte[] aesKey;
            try
            {
                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportFromPem(privateKey.ToCharArray());
                    aesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
                    _logger.LogInformation("Clave AES desencriptada con RSA.");
                }

                using (Aes aes = Aes.Create())
                {
                    aes.Key = aesKey;
                    aes.IV = iv;
                    using (MemoryStream ms = new MemoryStream(cipherText))
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    using (StreamReader reader = new StreamReader(cs))
                    {
                        string decryptedText = reader.ReadToEnd();
                        _logger.LogInformation("Datos desencriptados correctamente.");
                        return decryptedText;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error al desencriptar los datos: {Error}", ex.Message);
                throw;
            }
        }
    }
}
