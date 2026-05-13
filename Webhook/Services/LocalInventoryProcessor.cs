using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Webhook.Services;

public sealed class LocalInventoryProcessor : IInventoryProcessor
{
    private readonly IInventoryNormalizer _normalizer;
    private readonly IAssetConnector _assetConnector;
    private readonly ILogger<LocalInventoryProcessor> _logger;

    public LocalInventoryProcessor(
        IInventoryNormalizer normalizer,
        IAssetConnector assetConnector,
        ILogger<LocalInventoryProcessor> logger)
    {
        _normalizer = normalizer;
        _assetConnector = assetConnector;
        _logger = logger;
    }

    public Task<InventoryProcessingResult> ProcessAsync(
        InventoryProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        return ProcessInternalAsync(request, cancellationToken);
    }

    private async Task<InventoryProcessingResult> ProcessInternalAsync(
        InventoryProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var normalizationResult = _normalizer.Normalize(request.DecryptedData, request.ClientId);
        if (!normalizationResult.Succeeded)
        {
            _logger.LogWarning(
                "Local inventory processing rejected payload. ErrorCode: {ErrorCode}. CorrelationId: {CorrelationId}",
                normalizationResult.ErrorCode,
                request.CorrelationId);

            throw new InvalidOperationException($"Inventory payload rejected: {normalizationResult.ErrorCode}.");
        }

        var connectorResult = await _assetConnector.SubmitAsync(normalizationResult.Inventory, cancellationToken);
        if (!connectorResult.Succeeded)
        {
            _logger.LogWarning(
                "Local inventory connector failed. Operation: {Operation}. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}",
                connectorResult.Operation,
                connectorResult.StatusCode,
                request.CorrelationId);

            throw new InvalidOperationException("Inventory connector failed.");
        }

        _logger.LogInformation(
            "Inventory payload processed locally. Operation: {Operation}. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}",
            connectorResult.Operation,
            connectorResult.StatusCode,
            request.CorrelationId);

        return new InventoryProcessingResult("Evento procesado correctamente.");
    }
}
