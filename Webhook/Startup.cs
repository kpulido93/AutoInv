using System;
using System.IO;

using Amazon;
using Amazon.Lambda;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Services;

using Webhook.Options;
using Webhook.Services;

namespace Webhook
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_configuration);
            services.AddControllers().AddNewtonsoftJson();
            services.AddRazorPages();
            services.AddHealthChecks();
            services.AddMemoryCache();
            services.AddSingleton<EventStore>();
            services.AddOptions<AutoInventarioOptions>()
                .Bind(_configuration.GetSection(AutoInventarioOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey),
                    $"{AutoInventarioOptions.SectionName}:ApiKey is required.")
                .ValidateOnStart();
            services.AddOptions<ConnectionStringsOptions>()
                .Bind(_configuration.GetSection(ConnectionStringsOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Postgres),
                    $"{ConnectionStringsOptions.SectionName}:Postgres is required.")
                .ValidateOnStart();
            services.AddOptions<SecurityOptions>()
                .Bind(_configuration.GetSection(SecurityOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.PrivateKeyPath),
                    $"{SecurityOptions.SectionName}:PrivateKeyPath is required.")
                .Validate(options => File.Exists(options.PrivateKeyPath),
                    "Configured private key file was not found.")
                .ValidateOnStart();
            services.PostConfigure<SecurityOptions>(options =>
            {
                if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
                {
                    options.PrivateKeyPath = GetDefaultPrivateKeyPath();
                }
            });
            services.AddOptions<InventoryProcessingOptions>()
                .Bind(_configuration.GetSection(InventoryProcessingOptions.SectionName))
                .Validate(options =>
                {
                    var mode = options.Mode?.Trim();
                    return string.IsNullOrWhiteSpace(mode) ||
                           string.Equals(mode, InventoryProcessingModes.Local, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(mode, InventoryProcessingModes.AwsLambda, StringComparison.OrdinalIgnoreCase);
                }, "InventoryProcessing:Mode must be Local or AwsLambda.")
                .Validate(options =>
                {
                    if (!string.Equals(options.Mode, InventoryProcessingModes.AwsLambda, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    return !string.IsNullOrWhiteSpace(options.AwsLambda.FunctionName);
                }, "InventoryProcessing:AwsLambda:FunctionName is required when InventoryProcessing:Mode is AwsLambda.")
                .ValidateOnStart();
            services.AddOptions<AwsOptions>()
                .Bind(_configuration.GetSection(AwsOptions.SectionName))
                .Validate(
                    _ => !HasForbiddenAwsCredentialConfiguration(_configuration.GetSection(AwsOptions.SectionName)),
                    "Do not configure AWS credentials under InventoryProcessing:AwsLambda. Use IAM Role, AWS SDK profiles, or the default credential chain.")
                .ValidateOnStart();
            services.AddOptions<SecretProviderOptions>()
                .Bind(_configuration.GetSection(SecretProviderOptions.SectionName))
                .Validate(options =>
                {
                    var mode = options.Mode?.Trim();
                    return string.IsNullOrWhiteSpace(mode) ||
                           string.Equals(mode, SecretProviderModes.Environment, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(mode, SecretProviderModes.File, StringComparison.OrdinalIgnoreCase);
                }, "SecretProvider:Mode must be Environment or File.")
                .Validate(options =>
                {
                    if (!string.Equals(options.Mode, SecretProviderModes.File, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    return !string.IsNullOrWhiteSpace(options.File.BasePath);
                }, "SecretProvider:File:BasePath is required when SecretProvider:Mode is File.")
                .ValidateOnStart();
            services.Configure<ManageEngineOptions>(
                _configuration.GetSection(ManageEngineOptions.SectionName));
            services.Configure<LicenseOptions>(
                _configuration.GetSection(LicenseOptions.SectionName));
            services.AddSingleton<EnvironmentSecretProvider>();
            services.AddSingleton<FileSecretProvider>();
            services.AddSingleton<ISecretProvider>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<SecretProviderOptions>>().Value;
                var mode = string.IsNullOrWhiteSpace(options.Mode)
                    ? SecretProviderModes.Environment
                    : options.Mode.Trim();

                if (string.Equals(mode, SecretProviderModes.File, StringComparison.OrdinalIgnoreCase))
                {
                    return serviceProvider.GetRequiredService<FileSecretProvider>();
                }

                if (string.Equals(mode, SecretProviderModes.Environment, StringComparison.OrdinalIgnoreCase))
                {
                    return serviceProvider.GetRequiredService<EnvironmentSecretProvider>();
                }

                throw new InvalidOperationException(
                    $"Unsupported secret provider mode '{options.Mode}'. Use Environment or File.");
            });
            services.AddSingleton<IDecryptionService, DecryptionService>();
            services.AddSingleton<ILicenseValidator, OfflineLicenseValidator>();
            services.AddHostedService<LicenseStartupValidationService>();
            services.AddSingleton<IInventoryNormalizer, InventoryNormalizationService>();
            services.AddSingleton<IAssetConnector, ManageEngineConnector>();
            services.AddHttpClient(ManageEngineConnector.HttpClientName, (serviceProvider, httpClient) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ManageEngineOptions>>().Value;
                if (options.TimeoutSeconds > 0)
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                }
            });
            services.AddSingleton<LocalInventoryProcessor>();
            services.AddSingleton<AwsLambdaInventoryProcessor>();
            services.AddSingleton(serviceProvider => new Lazy<IAmazonLambda>(() => CreateLambdaClient(serviceProvider)));
            services.AddSingleton<IInventoryProcessor>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<InventoryProcessingOptions>>().Value;
                var mode = string.IsNullOrWhiteSpace(options.Mode)
                    ? InventoryProcessingModes.AwsLambda
                    : options.Mode.Trim();

                if (string.Equals(mode, InventoryProcessingModes.Local, StringComparison.OrdinalIgnoreCase))
                {
                    return serviceProvider.GetRequiredService<LocalInventoryProcessor>();
                }

                if (string.Equals(mode, InventoryProcessingModes.AwsLambda, StringComparison.OrdinalIgnoreCase))
                {
                    return serviceProvider.GetRequiredService<AwsLambdaInventoryProcessor>();
                }

                throw new InvalidOperationException(
                    $"Unsupported inventory processing mode '{options.Mode}'. Use Local or AwsLambda.");
            });

            // Para que respete X-Forwarded-Proto / IP real cuando hay proxy/LB
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddHsts(options =>
            {
                options.Preload = true;
                options.IncludeSubDomains = true;
                options.MaxAge = TimeSpan.FromDays(365);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseSecurityHeaders();

            app.UseMiddleware<ApiKeyMiddleware>();

            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health");
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });
        }

        private static IAmazonLambda CreateLambdaClient(IServiceProvider serviceProvider)
        {
            var options = serviceProvider.GetRequiredService<IOptions<AwsOptions>>().Value;
            var regionName = options.Region;

            if (string.IsNullOrWhiteSpace(regionName))
            {
                return new AmazonLambdaClient();
            }

            return new AmazonLambdaClient(new AmazonLambdaConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(regionName)
            });
        }

        private static string GetDefaultPrivateKeyPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "mrbyte",
                "Webhook",
                "private.key");
        }

        private static bool HasForbiddenAwsCredentialConfiguration(IConfigurationSection awsSection)
        {
            foreach (var child in awsSection.GetChildren())
            {
                foreach (var forbiddenKey in AwsOptions.ForbiddenCredentialKeys)
                {
                    if (string.Equals(child.Key, forbiddenKey, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(child.Value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
