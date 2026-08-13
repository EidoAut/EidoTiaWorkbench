// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Represents one failed import operation.
    /// </summary>
    public sealed class ImportError {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportError"/> class.
        /// </summary>
        /// <param name="item">Artifact that failed to import.</param>
        /// <param name="exception">Exception describing the import failure.</param>
        public ImportError(ExportItem item, Exception exception) {
            this.Item = item;
            this.Exception = exception;
        }

        /// <summary>
        /// Gets the artifact that failed to import.
        /// </summary>
        public ExportItem Item {
            get;
        }

        /// <summary>
        /// Gets the exception describing the import failure.
        /// </summary>
        public Exception Exception {
            get;
        }

        /// <summary>
        /// Returns a compact text representation for logs and summary dialogs.
        /// </summary>
        /// <returns>A string containing the item and import exception details.</returns>
        public override string ToString() =>
            this.Item + " -> " + this.Exception.GetType().Name + ": " + this.Exception.Message;
    }
}
