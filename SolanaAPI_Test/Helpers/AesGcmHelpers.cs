using System.Security.Cryptography;

namespace SolanaAPI_Test.Helpers
{
    /// <summary>
    /// AES-GCM helper utilities for envelope encryption (DEK encrypting privateKey).
    /// Combined output format: [12-byte nonce][ciphertext][16-byte tag]
    /// Stored as Base64 string for convenience.
    /// </summary>
    public static class AesGcmHelpers
    {
        private const int NonceSize = 12; // recommended for GCM
        private const int TagSize = 16;   // 128-bit tag

        /// <summary>
        /// Generate cryptographically secure random bytes.
        /// </summary>
        public static byte[] GenerateRandomBytes(int length)
        {
            var b = new byte[length];
            RandomNumberGenerator.Fill(b);
            return b;
        }

        /// <summary>
        /// Encrypt plaintext using AES-GCM with given key (DEK).
        /// Returns Base64-encoded combined(bytes): nonce||ciphertext||tag.
        /// </summary>
        public static string EncryptToBase64(byte[] key, byte[] plaintext, byte[]? associatedData = null)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("Key must be 16/24/32 bytes (AES-128/192/256).", nameof(key));

            byte[] nonce = GenerateRandomBytes(NonceSize);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];

            try
            {
                using (var aesGcm = new AesGcm(key))
                {
                    if (associatedData == null)
                        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
                    else
                        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
                }

                // Pack into single byte[]: nonce || ciphertext || tag
                byte[] combined = new byte[NonceSize + ciphertext.Length + TagSize];
                Buffer.BlockCopy(nonce, 0, combined, 0, NonceSize);
                Buffer.BlockCopy(ciphertext, 0, combined, NonceSize, ciphertext.Length);
                Buffer.BlockCopy(tag, 0, combined, NonceSize + ciphertext.Length, TagSize);

                return Convert.ToBase64String(combined);
            }
            finally
            {
                // Clean sensitive buffers
                Array.Clear(nonce, 0, nonce.Length);
                Array.Clear(ciphertext, 0, ciphertext.Length);
                Array.Clear(tag, 0, tag.Length);
            }
        }

        /// <summary>
        /// Decrypt Base64 combined (nonce||ciphertext||tag) using key.
        /// Returns plaintext bytes (caller must clear when done).
        /// </summary>
        public static byte[] DecryptFromBase64(byte[] key, string combinedBase64, byte[]? associatedData = null)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (combinedBase64 == null) throw new ArgumentNullException(nameof(combinedBase64));

            var combined = Convert.FromBase64String(combinedBase64);

            if (combined.Length < NonceSize + TagSize)
                throw new ArgumentException("Invalid combined ciphertext.", nameof(combinedBase64));

            int ciphertextLength = combined.Length - NonceSize - TagSize;
            byte[] nonce = new byte[NonceSize];
            byte[] ciphertext = new byte[ciphertextLength];
            byte[] tag = new byte[TagSize];

            try
            {
                Buffer.BlockCopy(combined, 0, nonce, 0, NonceSize);
                Buffer.BlockCopy(combined, NonceSize, ciphertext, 0, ciphertextLength);
                Buffer.BlockCopy(combined, NonceSize + ciphertextLength, tag, 0, TagSize);

                byte[] plaintext = new byte[ciphertextLength];
                using (var aesGcm = new AesGcm(key))
                {
                    if (associatedData == null)
                        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                    else
                        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
                }
                return plaintext; // caller should clear when done
            }
            finally
            {
                Array.Clear(nonce, 0, nonce.Length);
                Array.Clear(ciphertext, 0, ciphertext.Length);
                Array.Clear(tag, 0, tag.Length);
                // Note: do NOT clear 'combined' here if you may still need it; it's local so fine to let GC handle.
            }
        }
    }
}
