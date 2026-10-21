// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactExportStatus.cs
// Classifies the outcome of an artifact-level export attempt.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Classifies the outcome of an artifact-level export attempt.
    /// </summary>
    public enum ArtifactExportStatus {
        /// <summary>
        /// The artifact was written and verified.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The artifact was intentionally skipped because the current payload hash matched the manifest.
        /// </summary>
        SkippedUnchanged,

        /// <summary>
        /// Legacy alias kept for older callers that used "Skipped" before artifact states were formalized.
        /// </summary>
        Skipped = SkippedUnchanged,

        /// <summary>
        /// The public API does not expose the requested operation for this artifact.
        /// </summary>
        Unavailable,

        /// <summary>
        /// TIA Portal rejected the operation because the artifact is protected or restricted.
        /// </summary>
        Restricted,

        /// <summary>
        /// The operation was expected to work but failed while the TIA session remained valid.
        /// </summary>
        Failed,

        /// <summary>
        /// The TIA session or project was lost and this artifact was not completed.
        /// </summary>
        AbortedSessionLost,

        /// <summary>
        /// The user requested cancellation before this artifact was attempted.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The application does not implement this artifact kind.
        /// </summary>
        Unsupported,
    }
}
