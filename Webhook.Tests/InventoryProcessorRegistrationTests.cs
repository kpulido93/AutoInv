using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Webhook.Services;

namespace Webhook.Tests;

public class InventoryProcessorRegistrationTests
{
    [Fact]
    public void Local_mode_resolves_local_processor()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["InventoryProcessing:Mode"] = "Local"
        });

        var processor = provider.GetRequiredService<IInventoryProcessor>();

        Assert.IsType<LocalInventoryProcessor>(processor);
    }

    [Fact]
    public void AwsLambda_mode_resolves_lambda_processor_without_invoking_aws()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["InventoryProcessing:Mode"] = "AwsLambda",
            ["InventoryProcessing:AwsLambda:FunctionName"] = "test-function",
            ["InventoryProcessing:AwsLambda:Region"] = "us-east-1"
        });

        var processor = provider.GetRequiredService<IInventoryProcessor>();

        Assert.IsType<AwsLambdaInventoryProcessor>(processor);
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
