using System;

namespace Webhook.Options;

public sealed class LicenseOptions
{
    public const string SectionName = "License";

    public string Edition { get; set; } = ProductEditions.CommunityInternal;

    public string LicenseFilePath { get; set; } = string.Empty;

    public string PublicKeyPath { get; set; } = string.Empty;

    public string PublicKeyPem { get; set; } = string.Empty;

    public bool AllowUnsignedDevelopmentLicense { get; set; } = true;

    public bool ValidateOnStartup { get; set; } = true;

    public int DevelopmentMaxEndpoints { get; set; } = 25;

    public string DevelopmentCustomer { get; set; } = "Development";
}

public static class ProductEditions
{
    public const string CommunityInternal = "Community/Internal";
    public const string Professional = "Professional";
    public const string Enterprise = "Enterprise";

    public static bool RequiresSignedLicense(string edition)
        => string.Equals(edition, Professional, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(edition, Enterprise, StringComparison.OrdinalIgnoreCase);

    public static bool IsKnown(string edition)
        => string.Equals(edition, CommunityInternal, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(edition, Professional, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(edition, Enterprise, StringComparison.OrdinalIgnoreCase);
}
