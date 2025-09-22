using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace ThoughtGarden.Api.Data
{
    public class EncryptionHelper
    {
        private readonly byte[] _key;

        public EncryptionHelper(IConfiguration config)
        {
            // Pull from appsettings.json (Encryption:JournalEncryptionKey)
            var keyBase64 = config["Encryption:JournalEncryptionKey"];
            if (string.IsNullOrEmpty(keyBase64))
                throw new InvalidOperationException("Encryption key not configured in appsettings.json.");

            _key = Convert.FromBase64String(keyBase64);
        }

        public (string CipherText, string IV) Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var sw = new StreamWriter(cs))
            {
                sw.Write(plainText);
            }

            return (
                Convert.ToBase64String(ms.ToArray()),
                Convert.ToBase64String(aes.IV)
            );
        }

        public string Decrypt(string cipherText, string ivBase64)
        {
            try
            {
                var iv = Convert.FromBase64String(ivBase64);
                var buffer = Convert.FromBase64String(cipherText);

                using var aes = Aes.Create();
                aes.Key = _key;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(buffer);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);

                return sr.ReadToEnd();
            }
            catch (Exception ex)
            {
                throw new DecryptionFailedException("Unable to decrypt journal entry.", ex);
            }
        }
    }
}
