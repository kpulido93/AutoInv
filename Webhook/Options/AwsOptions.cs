namespace Webhook.Options;

public sealed class AwsOptions
{
    public const string SectionName = "InventoryProcessing:AwsLambda";

    public string FunctionName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;
}
