using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Webhook.Services;

public sealed class LocalInventoryProcessor : IInventoryProcessor
{
    private readonly ILogger<LocalInventoryProcessor> _logger;

    public LocalInventoryProcessor(ILogger<LocalInventoryProcessor> logger)
    {
        _logger = logger;
    }

    public Task<InventoryProcessingResult> ProcessAsync(
        InventoryProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        // Local mode currently acknowledges the decrypted payload without calling external systems.
        _logger.LogInformation(
            "Inventory payload accepted by local stub processor. CryptoVersion: {CryptoVersion}. CorrelationId: {CorrelationId}",
            request.CryptoVersion,
            request.CorrelationId);

        return Task.FromResult(new InventoryProcessingResult(
            "Procesado localmente en modo stub. No sincronizado aun con ManageEngine."));
    }
}
