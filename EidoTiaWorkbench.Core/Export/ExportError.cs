// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportError.cs
// Represents one failed export operation.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Represents one failed export operation.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class ExportError {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportError"/> class.
        /// </summary>
        /// <param name="item">Artifact that failed to export.</param>
        /// <param name="exception">Exception describing the export failure.</param>
        public ExportError(ExportItem item, Exception exception) {
            this.Item = item;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets the artifact that failed to export.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ExportItem Item {
            get; private set;
        }

        /// <summary>
        /// Gets the exception describing the export failure.
        /// </summary>
        public Exception Exception {
            get; private set;
        }

        /// <summary>Gets diagnostics for the worker boundary without serializing exception types.</summary>
        [System.Runtime.Serialization.DataMember]
        public string Diagnostic {
            get {
                return this.Exception == null ? string.Empty : this.Exception.ToString();
            }

            private set {
                this.Exception = new InvalidOperationException(value);
            }
        }

        /// <summary>
        /// Returns a compact text representation for logs and summary dialogs.
        /// </summary>
        /// <returns>A string containing the item and export exception details.</returns>
        public override string ToString() =>
            this.Item + " -> " + this.Exception.GetType().Name + ": " + this.Exception.Message;
    }
}
