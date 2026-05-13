using System.Security.Cryptography;
using System.Text;
using AutoInventario.Services;

namespace AutoInventario.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void EncryptAuthenticatedPayload_roundtrips_with_aes_gcm()
    {
        using var rsa = RSA.Create(2048);
        var payload = CryptoService.EncryptAuthenticatedPayload(
            "{\"workstation\":{\"name\":\"PC-001\"}}",
            ExportPublicKeyPem(rsa),
            "42");

        var plaintext = Decrypt(payload, rsa, "42");

        Assert.Equal(CryptoService.CurrentCryptoVersion, payload.CryptoVersion);
        Assert.Equal("{\"workstation\":{\"name\":\"PC-001\"}}", plaintext);
        Assert.Equal(12, Convert.FromBase64String(payload.Nonce).Length);
        Assert.Equal(16, Convert.FromBase64String(payload.Tag).Length);
    }

    [Fact]
    public void EncryptAuthenticatedPayload_rejects_tampered_tag()
    {
        using var rsa = RSA.Create(2048);
        var payload = CryptoService.EncryptAuthenticatedPayload(
            "{\"workstation\":{\"name\":\"PC-001\"}}",
            ExportPublicKeyPem(rsa),
            "42");

        var tag = Convert.FromBase64String(payload.Tag);
        tag[0] ^= 0x01;
        var tampered = payload with { Tag = Convert.ToBase64String(tag) };

        Assert.ThrowsAny<CryptographicException>(() => Decrypt(tampered, rsa, "42"));
    }

    private static string Decrypt(EncryptedInventoryPayload payload, RSA rsa, string clientId)
    {
        var encryptedKey = Convert.FromBase64String(payload.EncryptedKey);
        var aesKey = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
        var nonce = Convert.FromBase64String(payload.Nonce);
        var ciphertext = Convert.FromBase64String(payload.Ciphertext);
        var tag = Convert.FromBase64String(payload.Tag);
        var plaintext = new byte[ciphertext.Length];
        var aad = CryptoService.BuildAssociatedData(payload.CryptoVersion, clientId);

        using var aesGcm = new AesGcm(aesKey, tag.Length);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, aad);
        return Encoding.UTF8.GetString(plaintext);
    }

    private static string ExportPublicKeyPem(RSA rsa)
    {
        var builder = new StringBuilder();
        builder.AppendLine("-----BEGIN PUBLIC KEY-----");
        builder.AppendLine(Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo(), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END PUBLIC KEY-----");
        return builder.ToString();
    }
}
