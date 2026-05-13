using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Webhook.Options;

namespace Webhook.Services;

public sealed class AwsLambdaInventoryProcessor : IInventoryProcessor, IDisposable
{
    private readonly Lazy<IAmazonLambda> _lambdaClient;
    private readonly IOptions<InventoryProcessingOptions> _options;
    private readonly ILogger<AwsLambdaInventoryProcessor> _logger;

    public AwsLambdaInventoryProcessor(
        Lazy<IAmazonLambda> lambdaClient,
        IOptions<InventoryProcessingOptions> options,
        ILogger<AwsLambdaInventoryProcessor> logger)
    {
        _lambdaClient = lambdaClient;
        _options = options;
        _logger = logger;
    }

    public async Task<InventoryProcessingResult> ProcessAsync(
        InventoryProcessingRequest request,
        CancellationToken cancellationToken = default)
    {
        var functionName = ResolveFunctionName();
        var payload = JsonSerializer.Serialize(BuildLambdaPayload(request));

        var invokeRequest = new InvokeRequest
        {
            FunctionName = functionName,
            Payload = payload
        };

        var response = await _lambdaClient.Value.InvokeAsync(invokeRequest, cancellationToken);
        if (response.StatusCode != 200)
        {
            throw new InvalidOperationException(
                $"AWS Lambda inventory processor returned HTTP status {response.StatusCode}.");
        }

        _logger.LogInformation(
            "Inventory payload sent to AWS Lambda. StatusCode: {StatusCode}. CorrelationId: {CorrelationId}",
            response.StatusCode,
            request.CorrelationId);

        return new InventoryProcessingResult("Evento procesado correctamente y enviado a Lambda.");
    }

    private static object BuildLambdaPayload(InventoryProcessingRequest request)
    {
        if (string.Equals(request.CryptoVersion, DecryptionService.CurrentCryptoVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                clientID = request.ClientId,
                crypto_version = request.CryptoVersion,
                ciphertext = request.EncryptedData,
                encrypted_key = request.EncryptedKey,
                nonce = request.IV,
                tag = request.Tag
            };
        }

        return new
        {
            clientID = request.ClientId,
            data = request.EncryptedData,
            key = request.EncryptedKey,
            iv = request.IV
        };
    }

    private string ResolveFunctionName()
    {
        var functionName = _options.Value.AwsLambda.FunctionName;
        if (string.IsNullOrWhiteSpace(functionName))
        {
            functionName = System.Environment.GetEnvironmentVariable("AUTOINVENTARIO_LAMBDA_NAME");
        }

        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw new InvalidOperationException(
                "InventoryProcessing:AwsLambda:FunctionName is required when InventoryProcessing:Mode is AwsLambda.");
        }

        return functionName;
    }

    public void Dispose()
    {
        if (_lambdaClient.IsValueCreated)
        {
            _lambdaClient.Value.Dispose();
        }
    }
}
