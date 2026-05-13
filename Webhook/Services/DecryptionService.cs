using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Models;

namespace Webhook.Services;

public interface IDecryptionService
{
    DecryptionResult Decrypt(WebhookEvent webhookEvent);
}

public sealed record DecryptionResult(string Plaintext, string CryptoVersion);

public sealed class DecryptionService : IDecryptionService
{
    public const string CurrentCryptoVersion = "2";

    private readonly IConfiguration _configuration;
    private readonly ILogger<DecryptionService> _logger;
    private string _cachedPrivateKey = string.Empty;

    public DecryptionService(IConfiguration configuration, ILogger<DecryptionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public DecryptionResult Decrypt(WebhookEvent webhookEvent)
    {
        if (webhookEvent is null)
        {
            throw new InvalidOperationException("Missing webhook payload.");
        }

        if (string.Equals(webhookEvent.CryptoVersion, CurrentCryptoVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new DecryptionResult(DecryptAuthenticatedPayload(webhookEvent), CurrentCryptoVersion);
        }

        if (!string.IsNullOrWhiteSpace(webhookEvent.CryptoVersion))
        {
            throw new InvalidOperationException("Unsupported crypto_version.");
        }

        return new DecryptionResult(DecryptLegacyPayload(webhookEvent), "legacy");
    }

    private string DecryptAuthenticatedPayload(WebhookEvent webhookEvent)
    {
        if (string.IsNullOrWhiteSpace(webhookEvent.ClientID) ||
            string.IsNullOrWhiteSpace(webhookEvent.Ciphertext) ||
            string.IsNullOrWhiteSpace(webhookEvent.EncryptedKey) ||
            string.IsNullOrWhiteSpace(webhookEvent.Nonce) ||
            string.IsNullOrWhiteSpace(webhookEvent.Tag))
        {
            throw new InvalidOperationException("Invalid authenticated payload shape.");
        }

        var privateKey = LoadPrivateKey();
        var encryptedAesKey = Convert.FromBase64String(webhookEvent.EncryptedKey);
        var nonce = Convert.FromBase64String(webhookEvent.Nonce);
        var ciphertext = Convert.FromBase64String(webhookEvent.Ciphertext);
        var tag = Convert.FromBase64String(webhookEvent.Tag);
        if (nonce.Length != 12 || tag.Length != 16)
        {
            throw new InvalidOperationException("Invalid authenticated payload cryptographic parameter sizes.");
        }

        var plaintextBytes = new byte[ciphertext.Length];
        var associatedData = BuildAssociatedData(webhookEvent.CryptoVersion, webhookEvent.ClientID);

        byte[] aesKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(privateKey.ToCharArray());
            aesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
        }

        try
        {
            using var aesGcm = new AesGcm(aesKey, tag.Length);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintextBytes, associatedData);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    private string DecryptLegacyPayload(WebhookEvent webhookEvent)
    {
        if (string.IsNullOrWhiteSpace(webhookEvent.Data) ||
            string.IsNullOrWhiteSpace(webhookEvent.Key) ||
            string.IsNullOrWhiteSpace(webhookEvent.IV))
        {
            throw new InvalidOperationException("Invalid legacy payload shape.");
        }

        var privateKey = LoadPrivateKey();
        var encryptedAesKey = Convert.FromBase64String(webhookEvent.Key);
        var iv = Convert.FromBase64String(webhookEvent.IV);
        var cipherText = Convert.FromBase64String(webhookEvent.Data);

        byte[] aesKey;
        using (var rsa = RSA.Create())
        {
            rsa.ImportFromPem(privateKey.ToCharArray());
            aesKey = rsa.Decrypt(encryptedAesKey, RSAEncryptionPadding.OaepSHA256);
        }

        try
        {
            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = iv;
            using var ms = new MemoryStream(cipherText);
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cs);
            return reader.ReadToEnd();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aesKey);
        }
    }

    private string ResolvePrivateKeyPath()
    {
        var path = Environment.GetEnvironmentVariable("WEBHOOK_PRIVATE_KEY_PATH");

        if (string.IsNullOrWhiteSpace(path))
        {
            path = _configuration["Security:PrivateKeyPath"];
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "mrbyte",
                "Webhook",
                "private.key");
        }

        return path;
    }

    private string LoadPrivateKey()
    {
        if (!string.IsNullOrWhiteSpace(_cachedPrivateKey))
        {
            return _cachedPrivateKey;
        }

        var path = ResolvePrivateKeyPath();
        _logger.LogInformation("Loading private key for webhook payload decryption from configured path.");

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("No private key path is configured.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Configured private key file was not found.", path);
        }

        _cachedPrivateKey = File.ReadAllText(path);
        _logger.LogInformation("Private key loaded for webhook payload decryption.");

        return _cachedPrivateKey;
    }

    public static byte[] BuildAssociatedData(string cryptoVersion, string clientId)
        => Encoding.UTF8.GetBytes($"AutoInventario|{cryptoVersion}|{clientId}");
}
