// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ILogger.cs
// Defines the minimal logging contract used by core export, import, and inventory services.
//
// Developed by EIDO Automation
// =========================================================================
using System;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Defines the minimal logging contract used by core export, import, and inventory services.
    /// </summary>
    public interface ILogger {
        /// <summary>
        /// Writes an informational message about normal progress.
        /// </summary>
        /// <param name="message">Message to write.</param>
        void Info(string message);

        /// <summary>
        /// Writes a warning message for a non-fatal condition.
        /// </summary>
        /// <param name="message">Warning text to write.</param>
        void Warn(string message);

        /// <summary>
        /// Writes an error message and optional exception details.
        /// </summary>
        /// <param name="message">Human-readable failure description.</param>
        /// <param name="ex">Optional exception associated with the failure.</param>
        void Error(string message, Exception ex = null);
    }
}
