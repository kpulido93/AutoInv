using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace AutoInventario.Helpers
{
    public static class EncryptionHelper
    {
        public static string EncryptData(string jsonData, SecureString privateKey)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = DeriveKey(privateKey);
                    aes.GenerateIV();

                    using (var memoryStream = new MemoryStream())
                    using (var cryptoStream = new CryptoStream(memoryStream, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        var jsonBytes = Encoding.UTF8.GetBytes(jsonData);
                        cryptoStream.Write(jsonBytes, 0, jsonBytes.Length);
                        cryptoStream.FlushFinalBlock();

                        var encryptedData = memoryStream.ToArray();
                        return Convert.ToBase64String(aes.IV) + ":" + Convert.ToBase64String(encryptedData);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"Error en cifrado: {ex.Message}";
            }
        }

        private static byte[] DeriveKey(SecureString secureString)
        {
            var unsecureKey = SecureStringHelper.ConvertToUnsecureString(secureString);
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(unsecureKey));
            }
        }
    }
}
