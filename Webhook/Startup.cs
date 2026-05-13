using System;

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
            services.Configure<InventoryProcessingOptions>(
                _configuration.GetSection(InventoryProcessingOptions.SectionName));
            services.Configure<ManageEngineOptions>(
                _configuration.GetSection(ManageEngineOptions.SectionName));
            services.Configure<LicenseOptions>(
                _configuration.GetSection(LicenseOptions.SectionName));
            services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();
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
                var mode = options.Mode?.Trim();

                if (string.Equals(mode, InventoryProcessingModes.Local, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(mode))
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
            var options = serviceProvider.GetRequiredService<IOptions<InventoryProcessingOptions>>().Value;
            var regionName = options.AwsLambda.Region;

            if (string.IsNullOrWhiteSpace(regionName))
            {
                regionName = Environment.GetEnvironmentVariable("AWS_REGION");
            }

            if (string.IsNullOrWhiteSpace(regionName))
            {
                regionName = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");
            }

            if (string.IsNullOrWhiteSpace(regionName))
            {
                return new AmazonLambdaClient();
            }

            return new AmazonLambdaClient(new AmazonLambdaConfig
            {
                RegionEndpoint = RegionEndpoint.GetBySystemName(regionName)
            });
        }
    }
}
