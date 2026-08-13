// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Encapsulates queue-level decisions that follow an artifact export failure.
    /// </summary>
    /// <remarks>
    /// Artifact failures are isolated by default. The queue is aborted only when classification confirms
    /// session loss after any allowed controlled recovery has failed or already been consumed.
    /// </remarks>
    public static class ExportFailureIsolationPolicy {
        /// <summary>
        /// Determines whether the exporter should spend its single controlled recovery attempt.
        /// </summary>
        /// <param name="classification">Current artifact failure classification.</param>
        /// <param name="recoveryAlreadyAttempted">Whether this run has already attempted recovery.</param>
        /// <returns><see langword="true"/> when recovery is allowed and useful.</returns>
        public static bool ShouldAttemptControlledRecovery(
            ExportFailureClassification classification,
            bool recoveryAlreadyAttempted) {
            return classification != null &&
                   classification.Status == ArtifactExportStatus.AbortedSessionLost &&
                   !recoveryAlreadyAttempted;
        }

        /// <summary>
        /// Determines whether remaining artifacts must be marked as a session-loss cascade.
        /// </summary>
        /// <param name="classification">Final artifact failure classification after recovery, if any.</param>
        /// <returns><see langword="true"/> when the remaining queue cannot be safely attempted.</returns>
        public static bool ShouldAbortQueue(ExportFailureClassification classification) {
            return classification != null &&
                   classification.Status == ArtifactExportStatus.AbortedSessionLost;
        }
    }
}
