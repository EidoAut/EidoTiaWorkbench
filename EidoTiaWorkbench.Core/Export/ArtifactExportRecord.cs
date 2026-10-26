// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactExportRecord.cs
// Captures the final export outcome for one scanned artifact.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Captures the final export outcome for one scanned artifact.
    /// </summary>
    /// <remarks>
    /// This record is the authoritative artifact-level result used by the UI summary,
    /// operation report, retry selection, and manifest. It deliberately does not hold
    /// live Openness objects, so it can survive a lost TIA session and later retries.
    /// </remarks>
    [System.Runtime.Serialization.DataContract]
    public sealed class ArtifactExportRecord {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactExportRecord"/> class.
        /// </summary>
        /// <param name="item">Scanned artifact identity.</param>
        /// <param name="status">Final status assigned to this artifact.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="item"/> is <see langword="null"/>.
        /// </exception>
        public ArtifactExportRecord(ExportItem item, ArtifactExportStatus status) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            var descriptor = ArtifactCatalog.Get(item.Kind);
            var now = DateTime.UtcNow;
            this.LogicalKey = item.LogicalKey;
            this.Name = item.Name;
            this.Kind = item.Kind;
            this.Domain = item.Domain.ToString();
            this.Category = item.Category;
            this.Target = item.CpuPath;
            this.TargetType = item.TargetType;
            this.GroupPath = item.GroupPath;
            this.Status = status;
            this.AttemptedUtc = now;
            this.CompletedUtc = now;
            this.CanExport = item.CanExport;
            this.CanImport = item.CanImport;
            this.OpennessLimitation = descriptor.Limitations;
            this.Strategy = descriptor.ApiUsed;
            this.ExporterName = ArtifactExportPreflight.GetExporterName(item.Kind);
            this.Format = descriptor.Format.ToString();
        }

        /// <summary>
        /// Gets or sets the stable logical identity used to retry or correlate this artifact.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string LogicalKey {
            get; set;
        }

        /// <summary>
        /// Gets or sets the TIA object name shown to the user.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Name {
            get; set;
        }

        /// <summary>
        /// Gets or sets the artifact kind that selected the export strategy.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ExportKind Kind {
            get; set;
        }

        /// <summary>
        /// Gets or sets the catalog domain such as PLC or classic HMI.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Domain {
            get; set;
        }

        /// <summary>
        /// Gets or sets the repository category declared by the catalog.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Category {
            get; set;
        }

        /// <summary>
        /// Gets or sets the CPU, HMI target, or other owning target path.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Target {
            get; set;
        }

        /// <summary>
        /// Gets or sets optional target type metadata captured during scan.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string TargetType {
            get; set;
        }

        /// <summary>
        /// Gets or sets the group or folder path inside the owning target.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string GroupPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the final artifact-level status.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ArtifactExportStatus Status {
            get; set;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp at which this artifact attempt started.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public DateTime AttemptedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp at which this artifact result was finalized.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public DateTime CompletedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets a short user-facing message explaining the result.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Message {
            get; set;
        }

        /// <summary>
        /// Gets or sets detailed diagnostic text, including exception and session health information.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string TechnicalDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets the full exception type name when an exception caused the result.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string ExceptionType {
            get; set;
        }

        /// <summary>
        /// Gets or sets the HRESULT in hexadecimal form when the exception exposes one.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string HResult {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this result appears transient.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool IsTransient {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether retrying this artifact is safe and meaningful.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool IsRetryable {
            get; set;
        }

        /// <summary>
        /// Gets or sets the repository-relative primary payload path, if a valid payload exists.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string PrimaryPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the valid payload associated with this result.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Hash {
            get; set;
        }

        /// <summary>
        /// Gets or sets the Openness limitation declared by the central catalog.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string OpennessLimitation {
            get; set;
        }

        /// <summary>
        /// Gets or sets the precise reason an item was unavailable.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string UnavailableReason {
            get; set;
        }

        /// <summary>
        /// Gets or sets the failed capability check, such as item.CanExport or descriptor.CanExport.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string UnavailableCondition {
            get; set;
        }

        /// <summary>
        /// Gets or sets the logical key of the first artifact that detected session loss.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string SessionFailureKey {
            get; set;
        }

        /// <summary>
        /// Gets or sets the session health status observed after the attempt.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string SessionHealthStatus {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether controlled session recovery was attempted for this result.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool SessionRecoveryAttempted {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether controlled session recovery restored a usable project reference.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool SessionRecoverySucceeded {
            get; set;
        }

        /// <summary>
        /// Gets or sets diagnostic text describing the controlled session recovery attempt.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string SessionRecoveryDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets the catalog format used for the primary payload.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Format {
            get; set;
        }

        /// <summary>
        /// Gets or sets the export strategy used for diagnostics.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Strategy {
            get; set;
        }

        /// <summary>
        /// Gets or sets the concrete export handler selected by preflight.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string ExporterName {
            get; set;
        }

        /// <summary>
        /// Gets or sets a compact trace of export capability checks performed before Openness was invoked.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string CapabilityDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this concrete scanned item allowed export.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool CanExport {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the artifact can be imported.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool CanImport {
            get; set;
        }

        /// <summary>
        /// Gets the duration of this artifact attempt.
        /// </summary>
        public TimeSpan Duration {
            get {
                return this.CompletedUtc >= this.AttemptedUtc
                    ? this.CompletedUtc - this.AttemptedUtc
                    : TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Marks the record as completed at the current UTC time.
        /// </summary>
        public void CompleteNow() {
            this.CompletedUtc = DateTime.UtcNow;
        }
    }
}
