// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportResult.cs
// Summarizes an export run for presentation and follow-up actions.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Summarizes an export run for presentation and follow-up actions.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class ExportResult {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportResult"/> class.
        /// </summary>
        public ExportResult() {
            this.StartedUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets or sets the physical folder where this export run was written.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string OutputRoot {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items written to disk.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Exported {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items skipped due to unchanged content in incremental mode.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Skipped {
            get; set;
        }

        /// <summary>
        /// Gets or sets the UTC time when the export run started.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public DateTime StartedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets the UTC time when the export run finished.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public DateTime CompletedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets the overall export status derived from artifact outcomes.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ExportRunStatus Status {
            get; set;
        }

        /// <summary>
        /// Gets or sets the manifest path written for this run.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string ManifestPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the operation report path written for this run.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string OperationReportPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the first artifact result that detected a lost session.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ArtifactExportRecord FirstSessionFailure {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items blocked by TIA Portal protection or permissions.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Restricted {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items unavailable through the public API.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Unavailable {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items unsupported by this export pipeline.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Unsupported {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of artifacts aborted because the TIA session was lost.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int AbortedSessionLost {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of artifacts cancelled before an export attempt.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Cancelled {
            get; set;
        }

        /// <summary>
        /// Gets the authoritative per-artifact results for this run.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<ArtifactExportRecord> Artifacts { get; private set; } = new List<ArtifactExportRecord>();

        /// <summary>
        /// Gets the fatal errors that prevented individual items from exporting.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<ExportError> Errors { get; private set; } = new List<ExportError>();

        /// <summary>
        /// Gets the non-fatal issues encountered during export.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<ExportWarning> Warnings { get; private set; } = new List<ExportWarning>();

        /// <summary>
        /// Gets a value indicating whether at least one fatal export error occurred.
        /// </summary>
        public bool HasErrors => this.Errors.Count > 0;

        /// <summary>
        /// Gets a value indicating whether at least one non-fatal export warning occurred.
        /// </summary>
        public bool HasWarnings => this.Warnings.Count > 0;

        /// <summary>
        /// Recomputes counters and the overall status from artifact-level results.
        /// </summary>
        public void Recalculate() {
            this.Exported = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.Succeeded);
            this.Skipped = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.SkippedUnchanged);
            this.Restricted = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.Restricted);
            this.Unavailable = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.Unavailable);
            this.Unsupported = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.Unsupported);
            this.AbortedSessionLost = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.AbortedSessionLost);
            this.Cancelled = this.Artifacts.Count(r => r.Status == ArtifactExportStatus.Cancelled);

            if (this.Cancelled > 0) {
                this.Status = ExportRunStatus.Cancelled;
                return;
            }

            if (this.AbortedSessionLost > 0 &&
                !ExportOrderPolicy.HasOnlyFinalIsolatedSessionLoss(this.Artifacts)) {
                this.Status = ExportRunStatus.Aborted;
                return;
            }

            if (this.Artifacts.Count == 0 && this.Errors.Count > 0) {
                this.Status = ExportRunStatus.Failed;
                return;
            }

            if (this.Errors.Count > 0 ||
                this.Artifacts.Any(r => r.Status == ArtifactExportStatus.Failed) ||
                this.AbortedSessionLost > 0 ||
                this.Restricted > 0 ||
                this.Unavailable > 0 ||
                this.Unsupported > 0 ||
                this.Warnings.Count > 0) {
                this.Status = ExportRunStatus.CompletedWithIssues;
                return;
            }

            this.Status = ExportRunStatus.Completed;
        }
    }
}
