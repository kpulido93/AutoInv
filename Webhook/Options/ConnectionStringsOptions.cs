namespace Webhook.Options;

public sealed class ConnectionStringsOptions
{
    public const string SectionName = "ConnectionStrings";

    public string Postgres { get; set; } = string.Empty;
}
