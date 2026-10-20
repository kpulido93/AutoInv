namespace Webhook.Options;

public sealed class SecretProviderOptions
{
    public const string SectionName = "SecretProvider";

    public string Mode { get; set; } = SecretProviderModes.Environment;

    public FileSecretProviderOptions File { get; set; } = new();
}

public sealed class FileSecretProviderOptions
{
    public string BasePath { get; set; } = string.Empty;
}

public static class SecretProviderModes
{
    public const string Environment = "Environment";
    public const string File = "File";
}
