namespace Webhook.Options;

public sealed class ManageEngineOptions
{
    public const string SectionName = "ManageEngine";

    public string BaseUrl { get; set; } = string.Empty;

    public string WorkstationsPath { get; set; } = "/api/v3/workstations";

    public string ApiTokenSecretName { get; set; } = "MANAGEENGINE_API_TOKEN";

    public int TimeoutSeconds { get; set; } = 10;
}
