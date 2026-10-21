// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportFailureClassification.cs
// Describes how an Openness or local exception should affect artifact and run state.
//
// Developed by EIDO Automation
// =========================================================================
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Describes how an Openness or local exception should affect artifact and run state.
    /// </summary>
    public sealed class ExportFailureClassification {
        /// <summary>
        /// Gets or sets the artifact status produced by the classification.
        /// </summary>
        public ArtifactExportStatus Status {
            get; set;
        }

        /// <summary>
        /// Gets or sets the short message shown in activity, summary, and manifest views.
        /// </summary>
        public string Message {
            get; set;
        }

        /// <summary>
        /// Gets or sets detailed diagnostic text preserved for reports.
        /// </summary>
        public string TechnicalDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the condition may clear on a later attempt.
        /// </summary>
        public bool IsTransient {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether retrying the artifact is safe and useful.
        /// </summary>
        public bool IsRetryable {
            get; set;
        }
    }
}
