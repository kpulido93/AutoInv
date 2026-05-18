namespace Webhook.Options;

public sealed class InventoryProcessingOptions
{
    public const string SectionName = "InventoryProcessing";

    public string Mode { get; set; } = InventoryProcessingModes.Local;

    public AwsOptions AwsLambda { get; set; } = new();
}

public static class InventoryProcessingModes
{
    public const string Local = "Local";
    public const string AwsLambda = "AwsLambda";
}
