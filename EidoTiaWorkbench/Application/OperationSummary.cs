// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Collections.Generic;

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Summarizes the outcome of an export, import, project, or session operation.
    /// </summary>
    public sealed class OperationSummary {
        /// <summary>
        /// Gets or sets the operation name.
        /// </summary>
        public string Operation {
            get; set;
        }

        /// <summary>
        /// Gets or sets the label used for the primary success counter.
        /// </summary>
        public string SuccessLabel {
            get; set;
        } = "OK";

        /// <summary>
        /// Gets or sets the number of successful items.
        /// </summary>
        public int Ok {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of skipped items.
        /// </summary>
        public int Skipped {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of items blocked by TIA Portal restrictions.
        /// </summary>
        public int Restricted {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of items unavailable through the public API.
        /// </summary>
        public int Unavailable {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of items unsupported by this export/import pipeline.
        /// </summary>
        public int Unsupported {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of artifacts aborted because the TIA session was lost.
        /// </summary>
        public int AbortedSessionLost {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of artifacts cancelled before they were attempted.
        /// </summary>
        public int Cancelled {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of non-fatal warnings.
        /// </summary>
        public int Warnings {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of errors.
        /// </summary>
        public int Errors {
            get; set;
        }

        /// <summary>
        /// Gets or sets the global operation status.
        /// </summary>
        public string Status {
            get; set;
        }

        /// <summary>
        /// Gets or sets the output folder produced by an export operation.
        /// </summary>
        public string OutputPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the manifest path produced by an export operation.
        /// </summary>
        public string ManifestPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the operation report path produced by an export operation.
        /// </summary>
        public string OperationReportPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the conflict policy reported for an import operation.
        /// </summary>
        public string ConflictPolicy {
            get; set;
        }

        /// <summary>
        /// Gets or sets the backup path reported for an import operation.
        /// </summary>
        public string BackupPath {
            get; set;
        }

        /// <summary>
        /// Gets detailed warning and error messages associated with the operation.
        /// </summary>
        public List<string> Messages { get; } = new List<string>();
    }
}
