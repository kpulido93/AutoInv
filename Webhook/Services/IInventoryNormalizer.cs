using Webhook.Models;

namespace Webhook.Services;

public interface IInventoryNormalizer
{
    InventoryNormalizationResult Normalize(string decryptedJson, string clientId);
}
