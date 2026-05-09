using System.Text;
using System.Text.Json;

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
            var (encryptedJson, encryptedKey, iv) = CryptoService.EncryptData(json, publicKey);

            await SendDataToWebhook(clientID, encryptedJson, encryptedKey, iv, webhookUrl);
        }

        static async Task SendDataToWebhook(string clientID, string encryptedData, string encryptedKey, string iv, string webhookUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                var apiKey = Environment.GetEnvironmentVariable("AUTOINVENTARIO_WEBHOOK_API_KEY");
                if (!string.IsNullOrWhiteSpace(apiKey))
                    client.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var payload = new
                {
                    clientID = clientID,
                    data = encryptedData,
                    key = encryptedKey,
                    iv = iv
                };

                string jsonPayload = JsonSerializer.Serialize(payload);

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(webhookUrl, content);

                string responseText = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Respuesta del servidor: {response.StatusCode} - {responseText}");

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Datos enviados correctamente al webhook.");
                }
                else
                {
                    Console.WriteLine($"Error al enviar los datos: {response.StatusCode} - {responseText}");
                }
            }

        }
    }
}
