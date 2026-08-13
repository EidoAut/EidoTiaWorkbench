// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Security.Cryptography;
using System.Text;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Cryptographic hashing utilities used for manifest/incremental exports.
    /// </summary>
    public static class HashHelper {
        /// <summary>
        /// Computes a SHA-256 hash for a string (UTF-8) and returns it as a lowercase hex string.
        /// </summary>
        /// <param name="content">Input text to hash. Must not be null.</param>
        /// <returns>Lowercase hex-encoded SHA-256 digest (64 characters).</returns>
        public static string Sha256Hex(string content) {
            if (content == null) {
                throw new ArgumentNullException(nameof(content));
            }

            // Hash the UTF-8 bytes of the provided string.
            return Sha256Hex(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>
        /// Computes a SHA-256 hash for raw bytes and returns it as a lowercase hex string.
        /// </summary>
        /// <param name="bytes">Input bytes to hash. Must not be null.</param>
        /// <returns>Lowercase hex-encoded SHA-256 digest (64 characters).</returns>
        public static string Sha256Hex(byte[] bytes) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            // SHA256 is disposed via using to release any underlying crypto resources.
            using (var sha = SHA256.Create()) {
                var hash = sha.ComputeHash(bytes);

                // Convert hash bytes to hex (two hex chars per byte).
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
