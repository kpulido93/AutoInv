using System.Security.Cryptography;
using System.Text;

namespace AutoInventario.Services
{
    public static class CryptoService
    {
        public static string LoadPublicKey()
        {
            using Stream? stream = typeof(Program).Assembly.GetManifestResourceStream("AutoInventario.Resources.public.key");
            if (stream == null)
                throw new FileNotFoundException("No se encontró la clave pública embebida.");

            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
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
    }
}
