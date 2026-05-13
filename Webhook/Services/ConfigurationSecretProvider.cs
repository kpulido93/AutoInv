using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Webhook.Services;

public sealed class ConfigurationSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationSecretProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return Task.FromResult<string>(null);
        }

        var value = _configuration[$"Secrets:{secretName}"];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult(value);
        }

        return Task.FromResult(Environment.GetEnvironmentVariable(secretName));
    }
}
