// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Signals a non-fatal export condition that should be recorded as a warning.
    /// </summary>
    internal sealed class ExportWarningException : Exception {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportWarningException"/> class.
        /// </summary>
        /// <param name="message">Warning message describing why the item could not be fully exported.</param>
        /// <param name="item">Artifact that caused the warning.</param>
        public ExportWarningException(string message, ExportItem item)
            : base(message) {
            this.Item = item;
        }

        /// <summary>
        /// Gets the export item that caused this warning.
        /// </summary>
        public ExportItem Item {
            get;
        }
    }
}
