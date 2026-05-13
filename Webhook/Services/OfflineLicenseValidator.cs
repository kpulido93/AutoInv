using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Webhook.Models;
using Webhook.Options;

namespace Webhook.Services;

public sealed class OfflineLicenseValidator : ILicenseValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOptions<LicenseOptions> _options;
    private readonly ILogger<OfflineLicenseValidator> _logger;

    public OfflineLicenseValidator(
        IOptions<LicenseOptions> options,
        ILogger<OfflineLicenseValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public ValueTask<LicenseValidationResult> ValidateAsync(
        LicenseValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var configuredEdition = NormalizeEdition(options.Edition);
        if (!ProductEditions.IsKnown(configuredEdition))
        {
            return ValueTask.FromResult(LicenseValidationResult.Invalid(
                LicenseValidationStatus.InvalidFormat,
                "license_edition_invalid"));
        }

        if (!ProductEditions.RequiresSignedLicense(configuredEdition) &&
            string.IsNullOrWhiteSpace(options.LicenseFilePath))
        {
            return ValueTask.FromResult(CreateDevelopmentResult(options, configuredEdition, context.EndpointCount));
        }

        if (string.IsNullOrWhiteSpace(options.LicenseFilePath) ||
            !File.Exists(options.LicenseFilePath))
        {
            return ValueTask.FromResult(LicenseValidationResult.Invalid(
                LicenseValidationStatus.MissingLicense,
                "license_file_missing"));
        }

        try
        {
            var licenseJson = File.ReadAllText(options.LicenseFilePath, Encoding.UTF8);
            var envelope = JsonSerializer.Deserialize<OfflineLicenseEnvelope>(licenseJson, JsonOptions);
            if (envelope == null || string.IsNullOrWhiteSpace(envelope.Payload))
            {
                return ValueTask.FromResult(LicenseValidationResult.Invalid(
                    LicenseValidationStatus.InvalidFormat,
                    "license_format_invalid"));
            }

            if (IsUnsignedDevelopmentLicense(options, configuredEdition, envelope))
            {
                var developmentPayload = DecodePayload(envelope.Payload);
                return ValueTask.FromResult(ValidatePayload(developmentPayload, configuredEdition, context.EndpointCount));
            }

            if (!string.Equals(envelope.Algorithm, "RS256", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(envelope.Signature))
            {
                return ValueTask.FromResult(LicenseValidationResult.Invalid(
                    LicenseValidationStatus.InvalidFormat,
                    "license_algorithm_invalid"));
            }

            var publicKeyPem = LoadPublicKeyPem(options);
            if (string.IsNullOrWhiteSpace(publicKeyPem))
            {
                return ValueTask.FromResult(LicenseValidationResult.Invalid(
                    LicenseValidationStatus.MissingPublicKey,
                    "license_public_key_missing"));
            }

            if (!VerifySignature(publicKeyPem, envelope.Payload, envelope.Signature))
            {
                return ValueTask.FromResult(LicenseValidationResult.Invalid(
                    LicenseValidationStatus.InvalidSignature,
                    "license_signature_invalid"));
            }

            var payload = DecodePayload(envelope.Payload);
            return ValueTask.FromResult(ValidatePayload(payload, configuredEdition, context.EndpointCount));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Offline license file has invalid JSON.");
            return ValueTask.FromResult(LicenseValidationResult.Invalid(
                LicenseValidationStatus.InvalidFormat,
                "license_json_invalid"));
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Offline license public key or signature is invalid.");
            return ValueTask.FromResult(LicenseValidationResult.Invalid(
                LicenseValidationStatus.InvalidSignature,
                "license_signature_invalid"));
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Offline license payload is not valid base64url.");
            return ValueTask.FromResult(LicenseValidationResult.Invalid(
                LicenseValidationStatus.InvalidFormat,
                "license_payload_invalid"));
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Offline license file could not be read.");
            return ValueTask.FromResult(LicenseValidationResult.Invalid(
                LicenseValidationStatus.MissingLicense,
                "license_file_unreadable"));
        }
    }

    private static LicenseValidationResult CreateDevelopmentResult(
        LicenseOptions options,
        string configuredEdition,
        int endpointCount)
    {
        var maxEndpoints = Math.Max(1, options.DevelopmentMaxEndpoints);
        if (endpointCount > maxEndpoints)
        {
            return LicenseValidationResult.Invalid(
                LicenseValidationStatus.EndpointLimitExceeded,
                "license_endpoint_limit_exceeded");
        }

        var details = new LicenseDetails(
            "development",
            string.IsNullOrWhiteSpace(options.DevelopmentCustomer) ? "Development" : options.DevelopmentCustomer,
            configuredEdition,
            DateTimeOffset.MaxValue,
            maxEndpoints);

        return LicenseValidationResult.Valid(details);
    }

    private static bool IsUnsignedDevelopmentLicense(
        LicenseOptions options,
        string configuredEdition,
        OfflineLicenseEnvelope envelope)
        => options.AllowUnsignedDevelopmentLicense &&
           !ProductEditions.RequiresSignedLicense(configuredEdition) &&
           string.Equals(envelope.Algorithm, "none", StringComparison.OrdinalIgnoreCase);

    private static LicenseValidationResult ValidatePayload(
        OfflineLicensePayload payload,
        string configuredEdition,
        int endpointCount)
    {
        if (payload == null ||
            string.IsNullOrWhiteSpace(payload.Customer) ||
            string.IsNullOrWhiteSpace(payload.Edition) ||
            payload.MaxEndpoints <= 0)
        {
            return LicenseValidationResult.Invalid(
                LicenseValidationStatus.InvalidFormat,
                "license_payload_invalid");
        }

        if (!string.Equals(NormalizeEdition(payload.Edition), configuredEdition, StringComparison.OrdinalIgnoreCase))
        {
            return LicenseValidationResult.Invalid(
                LicenseValidationStatus.EditionMismatch,
                "license_edition_mismatch");
        }

        if (payload.ExpiresOn < DateTimeOffset.UtcNow)
        {
            return LicenseValidationResult.Invalid(
                LicenseValidationStatus.Expired,
                "license_expired");
        }

        if (endpointCount > payload.MaxEndpoints)
        {
            return LicenseValidationResult.Invalid(
                LicenseValidationStatus.EndpointLimitExceeded,
                "license_endpoint_limit_exceeded");
        }

        var details = new LicenseDetails(
            payload.LicenseId,
            payload.Customer,
            NormalizeEdition(payload.Edition),
            payload.ExpiresOn,
            payload.MaxEndpoints);

        return LicenseValidationResult.Valid(details);
    }

    private static OfflineLicensePayload DecodePayload(string encodedPayload)
    {
        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(encodedPayload));
        var payload = JsonSerializer.Deserialize<OfflineLicensePayload>(payloadJson, JsonOptions);
        if (payload == null)
        {
            throw new JsonException("License payload was empty.");
        }

        return payload;
    }

    private static string LoadPublicKeyPem(LicenseOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PublicKeyPem))
        {
            return options.PublicKeyPem;
        }

        if (!string.IsNullOrWhiteSpace(options.PublicKeyPath) &&
            File.Exists(options.PublicKeyPath))
        {
            return File.ReadAllText(options.PublicKeyPath, Encoding.UTF8);
        }

        return string.Empty;
    }

    private static bool VerifySignature(string publicKeyPem, string payload, string signature)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem.AsSpan());

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signatureBytes = Base64UrlDecode(signature);

        return rsa.VerifyData(
            payloadBytes,
            signatureBytes,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
    }

    private static string NormalizeEdition(string edition)
    {
        if (string.Equals(edition, ProductEditions.Professional, StringComparison.OrdinalIgnoreCase))
        {
            return ProductEditions.Professional;
        }

        if (string.Equals(edition, ProductEditions.Enterprise, StringComparison.OrdinalIgnoreCase))
        {
            return ProductEditions.Enterprise;
        }

        return string.IsNullOrWhiteSpace(edition) ? ProductEditions.CommunityInternal : edition.Trim();
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }
}
