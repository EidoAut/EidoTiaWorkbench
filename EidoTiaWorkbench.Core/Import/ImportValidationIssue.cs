// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using EidoTiaWorkbench.Core.Inventory;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Describes one human-readable problem found before calling Siemens Openness import.
    /// </summary>
    public sealed class ImportValidationIssue {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportValidationIssue"/> class.
        /// </summary>
        /// <param name="severity">Severity assigned to the issue.</param>
        /// <param name="phase">Validation phase that produced the issue.</param>
        /// <param name="artifact">Artifact associated with the issue, when available.</param>
        /// <param name="message">Human-readable validation message.</param>
        public ImportValidationIssue(
            ImportValidationSeverity severity,
            string phase,
            RepositoryArtifact artifact,
            string message) {
            this.Severity = severity;
            this.Phase = phase ?? string.Empty;
            this.Artifact = artifact;
            this.Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the severity assigned to the issue.
        /// </summary>
        public ImportValidationSeverity Severity {
            get;
        }

        /// <summary>
        /// Gets the validation phase that produced the issue.
        /// </summary>
        public string Phase {
            get;
        }

        /// <summary>
        /// Gets the artifact associated with the issue, when available.
        /// </summary>
        public RepositoryArtifact Artifact {
            get;
        }

        /// <summary>
        /// Gets the human-readable validation message.
        /// </summary>
        public string Message {
            get;
        }

        /// <summary>
        /// Returns the issue formatted for logs and summary dialogs.
        /// </summary>
        /// <returns>Readable validation issue text.</returns>
        public override string ToString() {
            var artifact = this.Artifact == null || this.Artifact.Item == null
                ? string.Empty
                : " | " + this.Artifact.Item;
            return this.Severity + " | " + this.Phase + artifact + " | " + this.Message;
        }
    }
}
