// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Represents one non-fatal issue encountered while exporting an artifact.
    /// </summary>
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
        public ExportItem Item {
            get;
        }

        /// <summary>
        /// Gets the exception describing the warning condition.
        /// </summary>
        public Exception Exception {
            get;
        }

        /// <summary>
        /// Returns a compact text representation for logs and summary dialogs.
        /// </summary>
        /// <returns>A string containing the item and warning exception details.</returns>
        public override string ToString() =>
            this.Item + " -> " + this.Exception.GetType().Name + ": " + this.Exception.Message;
    }
}
