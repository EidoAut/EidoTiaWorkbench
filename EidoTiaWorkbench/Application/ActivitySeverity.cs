// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ActivitySeverity.cs
// Identifies the severity used to present an activity log entry.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Identifies the severity used to present an activity log entry.
    /// </summary>
    public enum ActivitySeverity {
        /// <summary>
        /// Informational progress or status message.
        /// </summary>
        Info,

        /// <summary>
        /// Successful completion message.
        /// </summary>
        Success,

        /// <summary>
        /// Non-fatal condition that may require user attention.
        /// </summary>
        Warning,

        /// <summary>
        /// Failure condition that prevented an operation or item from completing.
        /// </summary>
        Error,
    }
}
