using System.Threading;
using System.Threading.Tasks;

namespace Webhook.Services;

public interface IInventoryProcessor
{
    Task<InventoryProcessingResult> ProcessAsync(InventoryProcessingRequest request, CancellationToken cancellationToken = default);
}

public sealed record InventoryProcessingRequest(
    string ClientId,
    string CryptoVersion,
    string EncryptedData,
    string EncryptedKey,
    string IV,
    string Tag,
    string DecryptedData,
    string CorrelationId);

public sealed record InventoryProcessingResult(string ResponseMessage);
