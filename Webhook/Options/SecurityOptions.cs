namespace Webhook.Options;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public string PrivateKeyPath { get; set; } = string.Empty;
}
