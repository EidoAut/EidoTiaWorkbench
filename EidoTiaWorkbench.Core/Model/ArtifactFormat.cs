// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Describes the repository payload format used by one artifact.
    /// </summary>
    public enum ArtifactFormat {
        /// <summary>
        /// Simatic ML XML exported by the public Openness API.
        /// </summary>
        Xml,

        /// <summary>
        /// Structured Control Language source file.
        /// </summary>
        Scl,

        /// <summary>
        /// Excel workbook used by Openness provider-level exchanges.
        /// </summary>
        Xlsx,

        /// <summary>
        /// AutomationML hardware exchange payload.
        /// </summary>
        Aml,

        /// <summary>
        /// Directory payload containing more than one file.
        /// </summary>
        Directory,

        /// <summary>
        /// Format is not supported by this application version.
        /// </summary>
        Unsupported,
    }
}
