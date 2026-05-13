using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Models;

using Services;
using Webhook.Services;

namespace Controllers
{
    [ApiController]
    [Route("webhooks")]
    public class WebhookController : ControllerBase
    {
        private const string VerifyToken = "YOUR_VERIFY_TOKEN";
        private readonly EventStore _eventStore;
        private readonly IInventoryProcessor _inventoryProcessor;
        private readonly IDecryptionService _decryptionService;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(
            EventStore eventStore,
            IInventoryProcessor inventoryProcessor,
            IDecryptionService decryptionService,
            ILogger<WebhookController> logger,
            IConfiguration config)
        {
            _eventStore = eventStore;
            _inventoryProcessor = inventoryProcessor;
            _decryptionService = decryptionService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Verify([FromQuery(Name = "hub.mode")] string hub_mode,
                                    [FromQuery(Name = "hub.challenge")] string hub_challenge,
                                    [FromQuery(Name = "hub.verify_token")] string hub_verify_token)
        {
            _logger.LogInformation(
                "Verificación de Webhook recibida. Mode: {Mode}, Token presente: {HasToken}",
                hub_mode,
                !string.IsNullOrWhiteSpace(hub_verify_token));

            if (hub_mode == "subscribe" && hub_verify_token == VerifyToken)
            {
                _logger.LogInformation("Token verificado correctamente.");
                return Ok(hub_challenge);
            }

            _logger.LogWarning("Token incorrecto. Acceso denegado.");
            return Forbid();
        }

        [HttpPost]
        public async Task<IActionResult> HandleEvent([FromBody] WebhookEvent webhookEvent)
        {
            var correlationId = HttpContext.Items.TryGetValue("X-Correlation-ID", out var value)
                ? value?.ToString()
                : HttpContext.TraceIdentifier;
            _logger.LogInformation("Solicitud POST recibida. CorrelationId: {CorrelationId}", correlationId);

            if (webhookEvent == null ||
                string.IsNullOrWhiteSpace(webhookEvent.ClientID) ||
                !HasEncryptedPayload(webhookEvent))
            {
                _logger.LogWarning(
                    "Solicitud con formato inválido. HasClientId: {HasClientId}, CryptoVersion: {CryptoVersion}",
                    !string.IsNullOrWhiteSpace(webhookEvent?.ClientID),
                    webhookEvent?.CryptoVersion ?? "legacy");
                return BadRequest("Formato inválido.");
            }

            DecryptionResult decryptionResult;
            try
            {
                _logger.LogInformation("Iniciando desencriptado de datos. CryptoVersion: {CryptoVersion}", webhookEvent.CryptoVersion ?? "legacy");
                decryptionResult = _decryptionService.Decrypt(webhookEvent);
                _logger.LogInformation("Datos desencriptados exitosamente. CryptoVersion: {CryptoVersion}", decryptionResult.CryptoVersion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Error al desencriptar los datos. ErrorType: {ErrorType}, CryptoVersion: {CryptoVersion}",
                    ex.GetType().Name,
                    webhookEvent.CryptoVersion ?? "legacy");
                return BadRequest("No se pudo desencriptar los datos. Verifica la clave y el formato.");
            }

            _eventStore.AddEvent(webhookEvent.ClientID, decryptionResult.Plaintext);
            _logger.LogInformation("Evento guardado localmente.");

            var processingResult = await _inventoryProcessor.ProcessAsync(
                new InventoryProcessingRequest(
                    webhookEvent.ClientID,
                    decryptionResult.CryptoVersion,
                    GetEncryptedData(webhookEvent),
                    GetEncryptedKey(webhookEvent),
                    GetIvOrNonce(webhookEvent),
                    webhookEvent.Tag ?? string.Empty,
                    decryptionResult.Plaintext,
                    correlationId),
                HttpContext.RequestAborted);

            _logger.LogInformation("Evento procesado correctamente.");
            return Ok(processingResult.ResponseMessage);
        }

        private static bool HasEncryptedPayload(WebhookEvent webhookEvent)
        {
            if (string.Equals(webhookEvent.CryptoVersion, DecryptionService.CurrentCryptoVersion, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(webhookEvent.Ciphertext) &&
                       !string.IsNullOrWhiteSpace(webhookEvent.EncryptedKey) &&
                       !string.IsNullOrWhiteSpace(webhookEvent.Nonce) &&
                       !string.IsNullOrWhiteSpace(webhookEvent.Tag);
            }

            if (!string.IsNullOrWhiteSpace(webhookEvent.CryptoVersion))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(webhookEvent.Data) &&
                   !string.IsNullOrWhiteSpace(webhookEvent.Key) &&
                   !string.IsNullOrWhiteSpace(webhookEvent.IV);
        }

        private static string GetEncryptedData(WebhookEvent webhookEvent)
            => string.Equals(webhookEvent.CryptoVersion, DecryptionService.CurrentCryptoVersion, StringComparison.OrdinalIgnoreCase)
                ? webhookEvent.Ciphertext
                : webhookEvent.Data;

        private static string GetEncryptedKey(WebhookEvent webhookEvent)
            => string.Equals(webhookEvent.CryptoVersion, DecryptionService.CurrentCryptoVersion, StringComparison.OrdinalIgnoreCase)
                ? webhookEvent.EncryptedKey
                : webhookEvent.Key;

        private static string GetIvOrNonce(WebhookEvent webhookEvent)
            => string.Equals(webhookEvent.CryptoVersion, DecryptionService.CurrentCryptoVersion, StringComparison.OrdinalIgnoreCase)
                ? webhookEvent.Nonce
                : webhookEvent.IV;
    }
}
