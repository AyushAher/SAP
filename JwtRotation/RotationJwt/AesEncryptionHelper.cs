using System;
using System.IO;
using System.Security.Cryptography;

namespace RotatingJwt
{
    /// <summary>
    /// Provides helper methods for AES encryption and decryption.
    /// </summary>
    internal static class AesEncryptionHelper
    {
        private const int IvSize = 16; // 128 bits

        /// <summary>
        /// Encrypts the given plain text using AES encryption.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <param name="keyString">The encryption key as a Base64-encoded string (256-bit AES).</param>
        /// <returns>The encrypted text as a Base64-encoded string, including the IV.</returns>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        internal static string Encrypt(this string plainText, string keyString)
        {
            var key = ConvertKeyStringToBytes(keyString);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV(); // Generate a random IV

            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length); // Prepend IV to the output

            using (var cryptoStream = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        /// <summary>
        /// Decrypts the given AES-encrypted text.
        /// </summary>
        /// <param name="cipherTextWithIv">The encrypted text (Base64-encoded) containing the IV.</param>
        /// <param name="keyString">The encryption key as a Base64-encoded string (256-bit AES).</param>
        /// <returns>The decrypted plain text.</returns>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        internal static string Decrypt(this string cipherTextWithIv, string keyString)
        {
            var key = ConvertKeyStringToBytes(keyString);

            using var aes = Aes.Create();
            var byteCipherText = Convert.FromBase64String(cipherTextWithIv);
            aes.Key = key;
            var iv = new byte[IvSize];
            Array.Copy(byteCipherText, 0, iv, 0, IvSize);
            aes.IV = iv;

            using var ms = new MemoryStream(byteCipherText, IvSize, byteCipherText.Length - IvSize);
            using var cryptoStream = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Converts a Base64-encoded key string to a byte array.
        /// </summary>
        /// <param name="keyString">The Base64-encoded AES key.</param>
        /// <returns>A 32-byte key for AES encryption.</returns>
        /// <exception cref="ArgumentException">Thrown if the key is not a valid 32-byte Base64-encoded string.</exception>
        private static byte[] ConvertKeyStringToBytes(string keyString)
        {
            byte[] keyBytes;

            // Try decoding as Base64 first
            try
            {
                keyBytes = Convert.FromBase64String(keyString);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid AES key format. Ensure it's Base64 encoded.");
            }

            // Ensure the key is exactly 32 bytes
            if (keyBytes.Length != 32)
            {
                throw new ArgumentException("Key length must be exactly 32 bytes (256-bit AES).");
            }

            return keyBytes;
        }
    }
}
