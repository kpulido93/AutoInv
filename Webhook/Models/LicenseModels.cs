using System;
using System.Text.Json.Serialization;

namespace Webhook.Models;

public sealed class LicenseValidationContext
{
    public LicenseValidationContext(int endpointCount = 0)
    {
        EndpointCount = endpointCount;
    }

    public int EndpointCount { get; }
}

public sealed class LicenseValidationResult
{
    private LicenseValidationResult(
        bool isValid,
        LicenseValidationStatus status,
        string errorCode,
        LicenseDetails details)
    {
        IsValid = isValid;
        Status = status;
        ErrorCode = errorCode;
        Details = details;
    }

    public bool IsValid { get; }

    public LicenseValidationStatus Status { get; }

    public string ErrorCode { get; }

    public LicenseDetails Details { get; }

    public static LicenseValidationResult Valid(LicenseDetails details)
        => new(true, LicenseValidationStatus.Valid, string.Empty, details);

    public static LicenseValidationResult Invalid(LicenseValidationStatus status, string errorCode)
        => new(false, status, errorCode, null);
}

public enum LicenseValidationStatus
{
    Valid = 0,
    MissingLicense = 1,
    MissingPublicKey = 2,
    InvalidFormat = 3,
    InvalidSignature = 4,
    Expired = 5,
    EndpointLimitExceeded = 6,
    EditionMismatch = 7
}

public sealed class LicenseDetails
{
    public LicenseDetails(
        string licenseId,
        string customer,
        string edition,
        DateTimeOffset expiresOn,
        int maxEndpoints)
    {
        LicenseId = licenseId;
        Customer = customer;
        Edition = edition;
        ExpiresOn = expiresOn;
        MaxEndpoints = maxEndpoints;
    }

    public string LicenseId { get; }

    public string Customer { get; }

    public string Edition { get; }

    public DateTimeOffset ExpiresOn { get; }

    public int MaxEndpoints { get; }
}

public sealed class OfflineLicenseEnvelope
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public sealed class OfflineLicensePayload
{
    [JsonPropertyName("license_id")]
    public string LicenseId { get; set; } = string.Empty;

    [JsonPropertyName("customer")]
    public string Customer { get; set; } = string.Empty;

    [JsonPropertyName("edition")]
    public string Edition { get; set; } = string.Empty;

    [JsonPropertyName("expires_on")]
    public DateTimeOffset ExpiresOn { get; set; }

    [JsonPropertyName("max_endpoints")]
    public int MaxEndpoints { get; set; }
}
