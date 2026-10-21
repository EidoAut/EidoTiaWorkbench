// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportWarning.cs
// Represents one non-fatal issue encountered while exporting an artifact.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Represents one non-fatal issue encountered while exporting an artifact.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class ExportWarning {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportWarning"/> class.
        /// </summary>
        /// <param name="item">Artifact that triggered the warning.</param>
        /// <param name="exception">Exception describing the warning condition.</param>
        public ExportWarning(ExportItem item, Exception exception) {
            this.Item = item;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets the artifact that triggered the warning.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ExportItem Item {
            get; private set;
        }

        /// <summary>
        /// Gets the exception describing the warning condition.
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
        /// <returns>A string containing the item and warning exception details.</returns>
        public override string ToString() =>
            this.Item + " -> " + this.Exception.GetType().Name + ": " + this.Exception.Message;
    }
}
