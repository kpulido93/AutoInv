using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Webhook.Models;
using Webhook.Options;
using Webhook.Services;

namespace Webhook.Tests;

public class OfflineLicenseValidatorTests
{
    [Fact]
    public async Task Community_mode_runs_without_license_file()
    {
        var validator = CreateValidator(new LicenseOptions
        {
            Edition = ProductEditions.CommunityInternal,
            LicenseFilePath = string.Empty,
            DevelopmentMaxEndpoints = 5
        });

        var result = await validator.ValidateAsync(new LicenseValidationContext(endpointCount: 3));

        Assert.True(result.IsValid);
        Assert.Equal(ProductEditions.CommunityInternal, result.Details.Edition);
        Assert.Equal(5, result.Details.MaxEndpoints);
    }

    [Fact]
    public async Task Community_unsigned_development_license_file_is_valid()
    {
        using var temp = TemporaryLicenseFile.Create();
        var payload = CreatePayload(DateTimeOffset.UtcNow.AddDays(7), ProductEditions.CommunityInternal);
        temp.WriteUnsignedDevelopmentLicense(payload);

        var validator = CreateValidator(new LicenseOptions
        {
            Edition = ProductEditions.CommunityInternal,
            LicenseFilePath = temp.LicensePath,
            AllowUnsignedDevelopmentLicense = true
        });

        var result = await validator.ValidateAsync(new LicenseValidationContext(endpointCount: 10));

        Assert.True(result.IsValid);
        Assert.Equal(ProductEditions.CommunityInternal, result.Details.Edition);
    }

    [Fact]
    public async Task Enterprise_license_with_valid_signature_is_valid()
    {
        using var rsa = RSA.Create(2048);
        using var temp = TemporaryLicenseFile.Create();
        var payload = CreatePayload(DateTimeOffset.UtcNow.AddDays(30));
        temp.WriteSignedLicense(payload, rsa);

        var validator = CreateValidator(new LicenseOptions
        {
            Edition = ProductEditions.Enterprise,
            LicenseFilePath = temp.LicensePath,
            PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem()
        });

        var result = await validator.ValidateAsync(new LicenseValidationContext(endpointCount: 25));

        Assert.True(result.IsValid);
        Assert.Equal("Acme Corp", result.Details.Customer);
        Assert.Equal(ProductEditions.Enterprise, result.Details.Edition);
        Assert.Equal(100, result.Details.MaxEndpoints);
    }

    [Fact]
    public async Task Enterprise_license_with_expired_payload_is_invalid()
    {
        using var rsa = RSA.Create(2048);
        using var temp = TemporaryLicenseFile.Create();
        var payload = CreatePayload(DateTimeOffset.UtcNow.AddDays(-1));
        temp.WriteSignedLicense(payload, rsa);

        var validator = CreateValidator(new LicenseOptions
        {
            Edition = ProductEditions.Enterprise,
            LicenseFilePath = temp.LicensePath,
            PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem()
        });

        var result = await validator.ValidateAsync(new LicenseValidationContext(endpointCount: 1));

        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationStatus.Expired, result.Status);
        Assert.Equal("license_expired", result.ErrorCode);
    }

    [Fact]
    public async Task Enterprise_license_with_invalid_signature_is_invalid()
    {
        using var signingKey = RSA.Create(2048);
        using var verificationKey = RSA.Create(2048);
        using var temp = TemporaryLicenseFile.Create();
        var payload = CreatePayload(DateTimeOffset.UtcNow.AddDays(30));
        temp.WriteSignedLicense(payload, signingKey);

        var validator = CreateValidator(new LicenseOptions
        {
            Edition = ProductEditions.Enterprise,
            LicenseFilePath = temp.LicensePath,
            PublicKeyPem = verificationKey.ExportSubjectPublicKeyInfoPem()
        });

        var result = await validator.ValidateAsync(new LicenseValidationContext(endpointCount: 1));

        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationStatus.InvalidSignature, result.Status);
        Assert.Equal("license_signature_invalid", result.ErrorCode);
    }

    private static OfflineLicensePayload CreatePayload(DateTimeOffset expiresOn, string edition = ProductEditions.Enterprise)
        => new()
        {
            LicenseId = "lic-test-001",
            Customer = "Acme Corp",
            Edition = edition,
            ExpiresOn = expiresOn,
            MaxEndpoints = 100
        };

    private static OfflineLicenseValidator CreateValidator(LicenseOptions options)
        => new(
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<OfflineLicenseValidator>.Instance);

    private sealed class TemporaryLicenseFile : IDisposable
    {
        private TemporaryLicenseFile(string directory)
        {
            Directory = directory;
            LicensePath = Path.Combine(directory, "license.json");
        }

        public string Directory { get; }

        public string LicensePath { get; }

        public static TemporaryLicenseFile Create()
        {
            var directory = Path.Combine(Path.GetTempPath(), "autoinventario-license-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            return new TemporaryLicenseFile(directory);
        }

        public void WriteSignedLicense(OfflineLicensePayload payload, RSA rsa)
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var signature = rsa.SignData(
                Encoding.UTF8.GetBytes(encodedPayload),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            var envelope = new OfflineLicenseEnvelope
            {
                Algorithm = "RS256",
                Payload = encodedPayload,
                Signature = Base64UrlEncode(signature)
            };

            File.WriteAllText(LicensePath, JsonSerializer.Serialize(envelope), Encoding.UTF8);
        }

        public void WriteUnsignedDevelopmentLicense(OfflineLicensePayload payload)
        {
            var payloadJson = JsonSerializer.Serialize(payload);
            var envelope = new OfflineLicenseEnvelope
            {
                Algorithm = "none",
                Payload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson)),
                Signature = string.Empty
            };

            File.WriteAllText(LicensePath, JsonSerializer.Serialize(envelope), Encoding.UTF8);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
