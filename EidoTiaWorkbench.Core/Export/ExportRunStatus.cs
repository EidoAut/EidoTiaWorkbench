// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportRunStatus.cs
// Classifies the overall result of an export run after all artifact-level outcomes are known.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Classifies the overall result of an export run after all artifact-level outcomes are known.
    /// </summary>
    public enum ExportRunStatus {
        /// <summary>
        /// Every scanned artifact either exported successfully or was skipped because it was unchanged.
        /// </summary>
        Completed = 0,

        /// <summary>
        /// The run finished its queue but contains restricted, unavailable, unsupported, or failed artifacts.
        /// </summary>
        CompletedWithIssues = 1,

        /// <summary>
        /// The TIA session or project was lost and the remaining artifacts were not attempted.
        /// </summary>
        Aborted = 2,

        /// <summary>
        /// The user requested cooperative cancellation between artifacts.
        /// </summary>
        Cancelled = 3,

        /// <summary>
        /// The export operation failed before a complete artifact-level result set could be produced.
        /// </summary>
        Failed = 4,

        /// <summary>
        /// Legacy alias kept for callers that used the pre-issue export status model.
        /// </summary>
        Succeeded = Completed,

        /// <summary>
        /// Legacy alias kept for callers that used the pre-issue export status model.
        /// </summary>
        SucceededWithLimitations = CompletedWithIssues,

        /// <summary>
        /// Legacy alias kept for callers that used the pre-issue export status model.
        /// </summary>
        PartiallySucceeded = CompletedWithIssues,
    }
}
