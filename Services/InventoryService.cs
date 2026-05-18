using System.Text;
using System.Text.Json;
using System.Linq;

using AutoInventario.Helpers;

namespace AutoInventario.Services
{
    public static class InventoryService
    {
        public static async Task ExecuteAsync(string clientID, string webhookUrl)
        {
            var systemInfo = Systeminfo.GenerateWorkstationJson(clientID);

            using var doc = JsonDocument.Parse(systemInfo);
            string json = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });

            string publicKey = CryptoService.LoadPublicKey();
            var encryptedPayload = CryptoService.EncryptAuthenticatedPayload(json, publicKey, clientID);

            await SendDataToWebhook(clientID, encryptedPayload, webhookUrl);
        }

        static async Task SendDataToWebhook(
            string clientID,
            EncryptedInventoryPayload encryptedPayload,
            string webhookUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                var apiKey = Environment.GetEnvironmentVariable("AUTOINVENTARIO_WEBHOOK_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var payload = new
                {
                    clientID = clientID,
                    crypto_version = encryptedPayload.CryptoVersion,
                    ciphertext = encryptedPayload.Ciphertext,
                    encrypted_key = encryptedPayload.EncryptedKey,
                    nonce = encryptedPayload.Nonce,
                    tag = encryptedPayload.Tag
                };

                string jsonPayload = JsonSerializer.Serialize(payload);

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(webhookUrl, content);

                string responseText = await response.Content.ReadAsStringAsync();
                string correlationId = TryGetCorrelationId(response);
                Console.WriteLine(
                    $"Respuesta del servidor: {response.StatusCode} (correlation_id={correlationId}, body_length={responseText.Length})");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Datos enviados correctamente al webhook. CorrelationId={correlationId}");
                }
                else
                {
                    Console.WriteLine(
                        $"Error al enviar los datos al webhook. StatusCode={response.StatusCode}, correlation_id={correlationId}, body_length={responseText.Length}");
                }
            }

        }

        private static string TryGetCorrelationId(HttpResponseMessage response)
        {
            if (response.Headers.TryGetValues("X-Correlation-ID", out var values))
            {
                var correlationId = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(correlationId))
                {
                    return correlationId;
                }
            }

            return "n/a";
        }
    }
}
