// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Represents one failed export operation.
    /// </summary>
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
        public ExportItem Item {
            get;
        }

        /// <summary>
        /// Gets the exception describing the export failure.
        /// </summary>
        public Exception Exception {
            get;
        }

        /// <summary>
        /// Returns a compact text representation for logs and summary dialogs.
        /// </summary>
        /// <returns>A string containing the item and export exception details.</returns>
        public override string ToString() =>
            this.Item + " -> " + this.Exception.GetType().Name + ": " + this.Exception.Message;
    }
}
