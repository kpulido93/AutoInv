using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Webhook.Options;
using Webhook.Services;

namespace Webhook.Tests;

public class InventoryProcessorRegistrationTests
{
    [Fact]
    public void Unspecified_mode_resolves_lambda_processor_to_preserve_current_behavior()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["InventoryProcessing:AwsLambda:FunctionName"] = "test-function",
            ["InventoryProcessing:AwsLambda:Region"] = "us-east-1"
        });

        var processor = provider.GetRequiredService<IInventoryProcessor>();

        Assert.IsType<AwsLambdaInventoryProcessor>(processor);
    }

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

    [Fact]
    public void Inline_aws_credentials_are_rejected_from_configuration()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["InventoryProcessing:Mode"] = "AwsLambda",
            ["InventoryProcessing:AwsLambda:FunctionName"] = "test-function",
            ["InventoryProcessing:AwsLambda:Region"] = "us-east-1",
            ["InventoryProcessing:AwsLambda:AccessKeyId"] = "DO_NOT_USE",
            ["InventoryProcessing:AwsLambda:SecretAccessKey"] = "DO_NOT_USE"
        });

        var exception = Assert.Throws<OptionsValidationException>(
            () => _ = provider.GetRequiredService<IOptions<AwsOptions>>().Value);

        Assert.Contains("default credential chain", exception.Message, StringComparison.OrdinalIgnoreCase);
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
