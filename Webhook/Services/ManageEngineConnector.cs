using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Webhook.Models;
using Webhook.Options;

namespace Webhook.Services;

public sealed class ManageEngineConnector : IAssetConnector
{
    public const string HttpClientName = "ManageEngine";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ManageEngineOptions> _options;
    private readonly ISecretProvider _secretProvider;
    private readonly ILogger<ManageEngineConnector> _logger;

    public ManageEngineConnector(
        IHttpClientFactory httpClientFactory,
        IOptions<ManageEngineOptions> options,
        ISecretProvider secretProvider,
        ILogger<ManageEngineConnector> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _secretProvider = secretProvider;
        _logger = logger;
    }

    public async Task<AssetConnectorResult> SubmitAsync(
        NormalizedInventory inventory,
        CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveWorkstationsEndpoint();
        var apiToken = await _secretProvider.GetSecretAsync(_options.Value.ApiTokenSecretName, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiToken))
        {
            throw new InvalidOperationException("ManageEngine API token is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.TryAddWithoutValidation("authtoken", apiToken);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["input_data"] = inventory.ConnectorPayload.ToJsonString()
        });

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var succeeded = (int)response.StatusCode is 200 or 201;

        _logger.LogInformation(
            "ManageEngine connector completed. Operation: {Operation}. StatusCode: {StatusCode}. Succeeded: {Succeeded}",
            "Create Workstation",
            (int)response.StatusCode,
            succeeded);

        return new AssetConnectorResult("Create Workstation", (int)response.StatusCode, succeeded);
    }

    private Uri ResolveWorkstationsEndpoint()
    {
        var options = _options.Value;
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("ManageEngine:BaseUrl must be an absolute URL.");
        }

        if (Uri.TryCreate(options.WorkstationsPath, UriKind.Absolute, out var absoluteEndpoint))
        {
            return absoluteEndpoint;
        }

        var path = string.IsNullOrWhiteSpace(options.WorkstationsPath)
            ? "/api/v3/workstations"
            : options.WorkstationsPath;

        return new Uri(baseUri, path);
    }
}
