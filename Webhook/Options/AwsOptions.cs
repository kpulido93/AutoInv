namespace Webhook.Options;

public sealed class AwsOptions
{
    public const string SectionName = "InventoryProcessing:AwsLambda";
    public static readonly string[] ForbiddenCredentialKeys =
    [
        "AccessKeyId",
        "SecretAccessKey",
        "SessionToken"
    ];

    public string FunctionName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;
}
