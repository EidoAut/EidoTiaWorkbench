// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Classifies the severity of one import preflight validation issue.
    /// </summary>
    public enum ImportValidationSeverity {
        /// <summary>
        /// The issue is informational and does not block import.
        /// </summary>
        Info,

        /// <summary>
        /// The issue may lead to an import failure but does not always prove the payload is invalid.
        /// </summary>
        Warning,

        /// <summary>
        /// The issue is expected to make the import unsafe or invalid.
        /// </summary>
        Error,
    }
}
