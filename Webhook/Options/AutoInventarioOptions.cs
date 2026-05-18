namespace Webhook.Options;

public sealed class AutoInventarioOptions
{
    public const string SectionName = "AutoInventario";

    public string ApiKey { get; set; } = string.Empty;

    public string VerifyToken { get; set; } = "YOUR_VERIFY_TOKEN";
}
