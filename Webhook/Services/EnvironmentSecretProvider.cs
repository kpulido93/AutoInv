using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Webhook.Services;

public sealed class EnvironmentSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    public EnvironmentSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return Task.FromResult<string>(null);
        }

        var environmentValue = Environment.GetEnvironmentVariable(secretName);
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return Task.FromResult(environmentValue);
        }

        // Keep legacy compatibility with existing Secrets:<name> configuration entries.
        var configuredValue = _configuration[$"Secrets:{secretName}"];
        return Task.FromResult(configuredValue);
    }
}
