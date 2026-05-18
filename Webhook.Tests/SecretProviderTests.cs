using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Options;
using Webhook.Services;

namespace Webhook.Tests;

public class SecretProviderTests
{
    [Fact]
    public async Task Environment_provider_falls_back_to_legacy_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:MANAGEENGINE_API_TOKEN"] = "from-config"
            })
            .Build();

        var provider = new EnvironmentSecretProvider(configuration);

        var secret = await provider.GetSecretAsync("MANAGEENGINE_API_TOKEN");

        Assert.Equal("from-config", secret);
    }

    [Fact]
    public async Task File_provider_reads_secret_from_base_path()
    {
        var tempDirectory = Directory.CreateTempSubdirectory();
        try
        {
            var secretPath = Path.Combine(tempDirectory.FullName, "MANAGEENGINE_API_TOKEN");
            await File.WriteAllTextAsync(secretPath, "file-secret-value\r\n");
            var options = Microsoft.Extensions.Options.Options.Create(new SecretProviderOptions
            {
                Mode = SecretProviderModes.File,
                File = new FileSecretProviderOptions
                {
                    BasePath = tempDirectory.FullName
                }
            });
            var provider = new FileSecretProvider(options);

            var secret = await provider.GetSecretAsync("MANAGEENGINE_API_TOKEN");

            Assert.Equal("file-secret-value", secret);
        }
        finally
        {
            tempDirectory.Delete(true);
        }
    }

    [Fact]
    public void Startup_registers_file_secret_provider_when_mode_is_file()
    {
        var privateKeyPath = Path.GetTempFileName();
        var secretsDirectory = Directory.CreateTempSubdirectory();

        try
        {
            File.WriteAllText(privateKeyPath, "development-only");

            using var provider = BuildProvider(new Dictionary<string, string?>
            {
                ["AutoInventario:ApiKey"] = "test-api-key",
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["Security:PrivateKeyPath"] = privateKeyPath,
                ["InventoryProcessing:Mode"] = "Local",
                ["SecretProvider:Mode"] = "File",
                ["SecretProvider:File:BasePath"] = secretsDirectory.FullName
            });

            Assert.IsType<FileSecretProvider>(provider.GetRequiredService<ISecretProvider>());
        }
        finally
        {
            File.Delete(privateKeyPath);
            secretsDirectory.Delete(true);
        }
    }

    private static ServiceProvider BuildProvider(IDictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        var startup = new Webhook.Startup(configuration);
        startup.ConfigureServices(services);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
