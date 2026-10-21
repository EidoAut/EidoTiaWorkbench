// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// FileLogger.cs
// Writes timestamped log entries to a single UTF-8 file.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.IO;
using System.Text;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Writes timestamped log entries to a single UTF-8 file.
    /// </summary>
    public sealed class FileLogger : ILogger {
        private readonly object _gate = new object();
        private readonly string _path;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLogger"/> class.
        /// </summary>
        /// <param name="path">Full path of the log file to append.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
        public FileLogger(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            this._path = path;
            Directory.CreateDirectory(Path.GetDirectoryName(this._path) ?? ".");
        }

        /// <inheritdoc />
        public void Info(string message) => this.Write("INFO", message, null);

        /// <inheritdoc />
        public void Warn(string message) => this.Write("WARN", message, null);

        /// <inheritdoc />
        public void Error(string message, Exception ex = null) => this.Write("ERROR", message, ex);

        /// <summary>
        /// Writes one timestamped log entry to disk.
        /// </summary>
        /// <param name="level">Log severity label.</param>
        /// <param name="message">Message text to write.</param>
        /// <param name="ex">Optional exception details.</param>
        private void Write(string level, string message, Exception ex) {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}";

            if (ex != null) {
                line += $" | {ex.GetType().Name}: {ex.Message}\n{ExceptionDiagnosticFormatter.Format(ex)}";
            }

            lock (this._gate) {
                File.AppendAllText(
                    this._path,
                    line + Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }
    }
}
