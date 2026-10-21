// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// TransactionalFileWriter.cs
// Writes export payloads through a same-directory temporary file and promotes them atomically.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Writes export payloads through a same-directory temporary file and promotes them atomically.
    /// </summary>
    /// <remarks>
    /// The writer protects incremental repositories from partial or empty payloads. When validation
    /// fails, the previous destination file is left untouched and the caller receives an exception.
    /// </remarks>
    public static class TransactionalFileWriter {
        /// <summary>
        /// Writes UTF-8 text to a temporary file, validates it, and atomically promotes it to destination.
        /// </summary>
        /// <param name="destinationPath">Final payload path in the repository.</param>
        /// <param name="content">Payload content to write without a UTF-8 BOM.</param>
        /// <param name="format">Catalog payload format used for minimal validation.</param>
        /// <returns>The SHA-256 hash of the exact bytes promoted to the final path.</returns>
        /// <exception cref="ArgumentException">Thrown when the destination or content is empty.</exception>
        /// <exception cref="InvalidDataException">Thrown when the payload does not pass validation.</exception>
        public static string PromoteUtf8Text(string destinationPath, string content, ArtifactFormat format) {
            if (string.IsNullOrWhiteSpace(destinationPath)) {
                throw new ArgumentException("Destination path is required.", nameof(destinationPath));
            }

            if (string.IsNullOrEmpty(content)) {
                throw new ArgumentException("Export payload is empty.", nameof(content));
            }

            var directory = Path.GetDirectoryName(destinationPath) ?? ".";
            Directory.CreateDirectory(directory);

            var tempPath = Path.Combine(
                directory,
                "." + Path.GetFileName(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try {
                File.WriteAllText(tempPath, content, new UTF8Encoding(false));
                ValidatePayload(tempPath, format);
                var bytes = File.ReadAllBytes(tempPath);
                var hash = HashHelper.Sha256Hex(bytes);

                if (File.Exists(destinationPath)) {
                    File.Replace(tempPath, destinationPath, null, true);
                }
                else {
                    File.Move(tempPath, destinationPath);
                }

                return hash;
            }
            finally {
                TryDelete(tempPath);
            }
        }

        /// <summary>
        /// Performs minimal payload validation before a temporary file can replace the final payload.
        /// </summary>
        /// <param name="path">Temporary payload path to validate.</param>
        /// <param name="format">Expected payload format.</param>
        private static void ValidatePayload(string path, ArtifactFormat format) {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0) {
                throw new InvalidDataException("Openness produced an empty payload.");
            }

            if (format == ArtifactFormat.Xml) {
                XDocument.Load(path);
            }
        }

        /// <summary>
        /// Deletes a temporary file without masking the original export failure.
        /// </summary>
        /// <param name="path">Temporary file path.</param>
        private static void TryDelete(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
            catch {
                // Temporary cleanup is best effort; a stale temp file must not hide the export result.
            }
        }
    }
}
