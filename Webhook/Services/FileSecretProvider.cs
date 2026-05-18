using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Webhook.Options;

namespace Webhook.Services;

public sealed class FileSecretProvider : ISecretProvider
{
    private readonly IOptions<SecretProviderOptions> _options;

    public FileSecretProvider(IOptions<SecretProviderOptions> options)
    {
        _options = options;
    }

    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(secretName))
        {
            return null;
        }

        var basePath = _options.Value.File.BasePath;
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return null;
        }

        var secretPath = ResolveSecretPath(basePath, secretName);
        if (!File.Exists(secretPath))
        {
            return null;
        }

        var secretValue = await File.ReadAllTextAsync(secretPath, cancellationToken);
        return secretValue.TrimEnd('\r', '\n');
    }

    private static string ResolveSecretPath(string basePath, string secretName)
    {
        if (secretName.Contains(Path.DirectorySeparatorChar) ||
            secretName.Contains(Path.AltDirectorySeparatorChar) ||
            !string.Equals(Path.GetFileName(secretName), secretName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "SecretProvider:Mode=File only supports secret names that map to a single file name.");
        }

        return Path.Combine(basePath, secretName);
    }
}
