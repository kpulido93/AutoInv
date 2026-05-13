using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Webhook.Services;

namespace Webhook.Tests;

public class DecryptionServiceTests
{
    [Fact]
    public void Decrypt_authenticated_payload_roundtrips()
    {
        using var rsa = RSA.Create(2048);
        using var privateKeyFile = new TemporaryPrivateKeyFile(rsa);
        var service = CreateService(privateKeyFile.Path);
        var payload = CreateAuthenticatedPayload(rsa, "42", "{\"workstation\":{\"name\":\"PC-001\"}}");

        var result = service.Decrypt(payload);

        Assert.Equal(DecryptionService.CurrentCryptoVersion, result.CryptoVersion);
        Assert.Equal("{\"workstation\":{\"name\":\"PC-001\"}}", result.Plaintext);
    }

    [Fact]
    public void Decrypt_authenticated_payload_rejects_tampered_ciphertext()
    {
        using var rsa = RSA.Create(2048);
        using var privateKeyFile = new TemporaryPrivateKeyFile(rsa);
        var service = CreateService(privateKeyFile.Path);
        var payload = CreateAuthenticatedPayload(rsa, "42", "{\"workstation\":{\"name\":\"PC-001\"}}");
        var ciphertext = Convert.FromBase64String(payload.Ciphertext);
        ciphertext[0] ^= 0x01;
        payload.Ciphertext = Convert.ToBase64String(ciphertext);

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(payload));
    }

    [Fact]
    public void Decrypt_authenticated_payload_rejects_tampered_tag()
    {
        using var rsa = RSA.Create(2048);
        using var privateKeyFile = new TemporaryPrivateKeyFile(rsa);
        var service = CreateService(privateKeyFile.Path);
        var payload = CreateAuthenticatedPayload(rsa, "42", "{\"workstation\":{\"name\":\"PC-001\"}}");
        var tag = Convert.FromBase64String(payload.Tag);
        tag[0] ^= 0x01;
        payload.Tag = Convert.ToBase64String(tag);

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(payload));
    }

    [Fact]
    public void Decrypt_authenticated_payload_rejects_tampered_client_id()
    {
        using var rsa = RSA.Create(2048);
        using var privateKeyFile = new TemporaryPrivateKeyFile(rsa);
        var service = CreateService(privateKeyFile.Path);
        var payload = CreateAuthenticatedPayload(rsa, "42", "{\"workstation\":{\"name\":\"PC-001\"}}");
        payload.ClientID = "43";

        Assert.ThrowsAny<CryptographicException>(() => service.Decrypt(payload));
    }

    [Fact]
    public void Decrypt_rejects_unknown_crypto_version()
    {
        using var rsa = RSA.Create(2048);
        using var privateKeyFile = new TemporaryPrivateKeyFile(rsa);
        var service = CreateService(privateKeyFile.Path);
        var payload = CreateLegacyPayload(rsa, "{\"workstation\":{\"name\":\"PC-001\"}}");
        payload.CryptoVersion = "3";

        Assert.Throws<InvalidOperationException>(() => service.Decrypt(payload));
    }

    [Fact]
    public void Decrypt_legacy_payload_remains_supported_during_transition()
    {
        using var rsa = RSA.Create(2048);
        using var privateKeyFile = new TemporaryPrivateKeyFile(rsa);
        var service = CreateService(privateKeyFile.Path);
        var payload = CreateLegacyPayload(rsa, "{\"workstation\":{\"name\":\"PC-001\"}}");

        var result = service.Decrypt(payload);

        Assert.Equal("legacy", result.CryptoVersion);
        Assert.Equal("{\"workstation\":{\"name\":\"PC-001\"}}", result.Plaintext);
    }

    private static DecryptionService CreateService(string privateKeyPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:PrivateKeyPath"] = privateKeyPath
            })
            .Build();

        return new DecryptionService(configuration, NullLogger<DecryptionService>.Instance);
    }

    private static WebhookEvent CreateAuthenticatedPayload(RSA rsa, string clientId, string plaintext)
    {
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var aad = DecryptionService.BuildAssociatedData(DecryptionService.CurrentCryptoVersion, clientId);

        using (var aesGcm = new AesGcm(aesKey, tag.Length))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag, aad);
        }

        return new WebhookEvent
        {
            ClientID = clientId,
            CryptoVersion = DecryptionService.CurrentCryptoVersion,
            Ciphertext = Convert.ToBase64String(ciphertext),
            EncryptedKey = Convert.ToBase64String(rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256)),
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag)
        };
    }

    private static WebhookEvent CreateLegacyPayload(RSA rsa, string plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();

        byte[] ciphertext;
        using (var ms = new MemoryStream())
        {
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cs))
            {
                writer.Write(plaintext);
            }

            ciphertext = ms.ToArray();
        }

        return new WebhookEvent
        {
            ClientID = "42",
            Data = Convert.ToBase64String(ciphertext),
            Key = Convert.ToBase64String(rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256)),
            IV = Convert.ToBase64String(aes.IV)
        };
    }

    private sealed class TemporaryPrivateKeyFile : IDisposable
    {
        public TemporaryPrivateKeyFile(RSA rsa)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.pem");
            File.WriteAllText(Path, ExportPrivateKeyPem(rsa));
        }

        public string Path { get; }

        public void Dispose()
        {
            File.Delete(Path);
        }

        private static string ExportPrivateKeyPem(RSA rsa)
        {
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN PRIVATE KEY-----");
            builder.AppendLine(Convert.ToBase64String(rsa.ExportPkcs8PrivateKey(), Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END PRIVATE KEY-----");
            return builder.ToString();
        }
    }
}
