// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

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
    public sealed class ArtifactExportRecord {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactExportRecord"/> class.
        /// </summary>
        /// <param name="item">Scanned artifact identity.</param>
        /// <param name="status">Final status assigned to this artifact.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
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
        public string LogicalKey {
            get; set;
        }

        /// <summary>
        /// Gets or sets the TIA object name shown to the user.
        /// </summary>
        public string Name {
            get; set;
        }

        /// <summary>
        /// Gets or sets the artifact kind that selected the export strategy.
        /// </summary>
        public ExportKind Kind {
            get; set;
        }

        /// <summary>
        /// Gets or sets the catalog domain such as PLC or classic HMI.
        /// </summary>
        public string Domain {
            get; set;
        }

        /// <summary>
        /// Gets or sets the repository category declared by the catalog.
        /// </summary>
        public string Category {
            get; set;
        }

        /// <summary>
        /// Gets or sets the CPU, HMI target, or other owning target path.
        /// </summary>
        public string Target {
            get; set;
        }

        /// <summary>
        /// Gets or sets optional target type metadata captured during scan.
        /// </summary>
        public string TargetType {
            get; set;
        }

        /// <summary>
        /// Gets or sets the group or folder path inside the owning target.
        /// </summary>
        public string GroupPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the final artifact-level status.
        /// </summary>
        public ArtifactExportStatus Status {
            get; set;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp at which this artifact attempt started.
        /// </summary>
        public DateTime AttemptedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets the UTC timestamp at which this artifact result was finalized.
        /// </summary>
        public DateTime CompletedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets a short user-facing message explaining the result.
        /// </summary>
        public string Message {
            get; set;
        }

        /// <summary>
        /// Gets or sets detailed diagnostic text, including exception and session health information.
        /// </summary>
        public string TechnicalDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets the full exception type name when an exception caused the result.
        /// </summary>
        public string ExceptionType {
            get; set;
        }

        /// <summary>
        /// Gets or sets the HRESULT in hexadecimal form when the exception exposes one.
        /// </summary>
        public string HResult {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this result appears transient.
        /// </summary>
        public bool IsTransient {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether retrying this artifact is safe and meaningful.
        /// </summary>
        public bool IsRetryable {
            get; set;
        }

        /// <summary>
        /// Gets or sets the repository-relative primary payload path, if a valid payload exists.
        /// </summary>
        public string PrimaryPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the SHA-256 hash of the valid payload associated with this result.
        /// </summary>
        public string Hash {
            get; set;
        }

        /// <summary>
        /// Gets or sets the Openness limitation declared by the central catalog.
        /// </summary>
        public string OpennessLimitation {
            get; set;
        }

        /// <summary>
        /// Gets or sets the precise reason an item was unavailable.
        /// </summary>
        public string UnavailableReason {
            get; set;
        }

        /// <summary>
        /// Gets or sets the failed capability check, such as item.CanExport or descriptor.CanExport.
        /// </summary>
        public string UnavailableCondition {
            get; set;
        }

        /// <summary>
        /// Gets or sets the logical key of the first artifact that detected session loss.
        /// </summary>
        public string SessionFailureKey {
            get; set;
        }

        /// <summary>
        /// Gets or sets the session health status observed after the attempt.
        /// </summary>
        public string SessionHealthStatus {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether controlled session recovery was attempted for this result.
        /// </summary>
        public bool SessionRecoveryAttempted {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether controlled session recovery restored a usable project reference.
        /// </summary>
        public bool SessionRecoverySucceeded {
            get; set;
        }

        /// <summary>
        /// Gets or sets diagnostic text describing the controlled session recovery attempt.
        /// </summary>
        public string SessionRecoveryDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets the catalog format used for the primary payload.
        /// </summary>
        public string Format {
            get; set;
        }

        /// <summary>
        /// Gets or sets the export strategy used for diagnostics.
        /// </summary>
        public string Strategy {
            get; set;
        }

        /// <summary>
        /// Gets or sets the concrete export handler selected by preflight.
        /// </summary>
        public string ExporterName {
            get; set;
        }

        /// <summary>
        /// Gets or sets a compact trace of export capability checks performed before Openness was invoked.
        /// </summary>
        public string CapabilityDetail {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this concrete scanned item allowed export.
        /// </summary>
        public bool CanExport {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the artifact can be imported.
        /// </summary>
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
