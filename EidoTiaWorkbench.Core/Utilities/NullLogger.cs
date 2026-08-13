// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Provides an <see cref="ILogger"/> implementation that intentionally discards every message.
    /// </summary>
    public sealed class NullLogger : ILogger {
        /// <summary>
        /// A shared no-op logger instance for callers that do not need a dedicated logger.
        /// </summary>
        public static readonly NullLogger Instance = new NullLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="NullLogger"/> class.
        /// </summary>
        private NullLogger() {
        }

        /// <inheritdoc />
        public void Info(string message) {
        }

        /// <inheritdoc />
        public void Warn(string message) {
        }

        /// <inheritdoc />
        public void Error(string message, Exception ex = null) {
        }
    }
}
