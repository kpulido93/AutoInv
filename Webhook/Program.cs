using System.Collections.Generic;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Webhook.Options;

namespace Webhook
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((_, configBuilder) =>
                {
                    var overrides = new Dictionary<string, string>();

                    AddEnvironmentOverride(
                        overrides,
                        "WEBHOOK_PRIVATE_KEY_PATH",
                        $"{SecurityOptions.SectionName}:{nameof(SecurityOptions.PrivateKeyPath)}");
                    AddEnvironmentOverride(
                        overrides,
                        "SECRET_PROVIDER_MODE",
                        $"{SecretProviderOptions.SectionName}:{nameof(SecretProviderOptions.Mode)}");
                    AddEnvironmentOverride(
                        overrides,
                        "SECRET_PROVIDER_FILE_BASE_PATH",
                        $"{SecretProviderOptions.SectionName}:{nameof(SecretProviderOptions.File)}:{nameof(FileSecretProviderOptions.BasePath)}");
                    AddEnvironmentOverride(
                        overrides,
                        "AUTOINVENTARIO_LAMBDA_NAME",
                        $"{AwsOptions.SectionName}:{nameof(AwsOptions.FunctionName)}");
                    AddFirstAvailableEnvironmentOverride(
                        overrides,
                        $"{AwsOptions.SectionName}:{nameof(AwsOptions.Region)}",
                        "AWS_REGION",
                        "AWS_DEFAULT_REGION");

                    if (overrides.Count > 0)
                    {
                        configBuilder.AddInMemoryCollection(overrides);
                    }
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        serverOptions.AddServerHeader = false;
                    });

                    webBuilder.UseStartup<Startup>();
                });

        private static void AddEnvironmentOverride(
            IDictionary<string, string> overrides,
            string environmentVariableName,
            string configurationKey)
        {
            var value = System.Environment.GetEnvironmentVariable(environmentVariableName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                overrides[configurationKey] = value;
            }
        }

        private static void AddFirstAvailableEnvironmentOverride(
            IDictionary<string, string> overrides,
            string configurationKey,
            params string[] environmentVariableNames)
        {
            foreach (var environmentVariableName in environmentVariableNames)
            {
                var value = System.Environment.GetEnvironmentVariable(environmentVariableName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    overrides[configurationKey] = value;
                    return;
                }
            }
        }
    }
}
