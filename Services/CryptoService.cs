using System.Security.Cryptography;
using System.Text;

namespace AutoInventario.Services
{
    public sealed record EncryptedInventoryPayload(
        string CryptoVersion,
        string Ciphertext,
        string EncryptedKey,
        string Nonce,
        string Tag);

    public static class CryptoService
    {
        public const string CurrentCryptoVersion = "2";

        public static string LoadPublicKey()
        {
            using Stream? stream = typeof(Program).Assembly.GetManifestResourceStream("AutoInventario.Resources.public.key");
            if (stream == null)
                throw new FileNotFoundException("No se encontró la clave pública embebida.");

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static EncryptedInventoryPayload EncryptAuthenticatedPayload(
            string plainText,
            string publicKey,
            string clientId)
        {
            var aesKey = RandomNumberGenerator.GetBytes(32);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
            var ciphertext = new byte[plaintextBytes.Length];
            var associatedData = BuildAssociatedData(CurrentCryptoVersion, clientId);

            using (var aesGcm = new AesGcm(aesKey, tag.Length))
            {
                aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag, associatedData);
            }

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKey.ToCharArray());
            var encryptedKey = rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);

            CryptographicOperations.ZeroMemory(aesKey);
            CryptographicOperations.ZeroMemory(plaintextBytes);

            return new EncryptedInventoryPayload(
                CurrentCryptoVersion,
                Convert.ToBase64String(ciphertext),
                Convert.ToBase64String(encryptedKey),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag));
        }

        public static (string EncryptedData, string EncryptedKey, string IV) EncryptData(string plainText, string publicKey)
        {
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                byte[] encryptedData;
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    using (StreamWriter writer = new StreamWriter(cs))
                    {
                        writer.Write(plainText);
                    }
                    encryptedData = ms.ToArray();
                }

                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportFromPem(publicKey.ToCharArray());
                    byte[] encryptedKey = rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA256);

                    return (
                        Convert.ToBase64String(encryptedData),
                        Convert.ToBase64String(encryptedKey),
                        Convert.ToBase64String(aes.IV)
                    );
                }
            }
        }

        public static byte[] BuildAssociatedData(string cryptoVersion, string clientId)
            => Encoding.UTF8.GetBytes($"AutoInventario|{cryptoVersion}|{clientId}");
    }
}
