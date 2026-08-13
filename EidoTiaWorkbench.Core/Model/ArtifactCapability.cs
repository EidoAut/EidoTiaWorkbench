// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Describes operation support for one artifact type in the installed Openness API.
    /// </summary>
    public enum ArtifactCapability {
        /// <summary>
        /// The operation is not exposed by the installed public Openness API.
        /// </summary>
        NotSupported,

        /// <summary>
        /// The artifact can be discovered, but this operation is not available.
        /// </summary>
        ScanOnly,

        /// <summary>
        /// The artifact can be exported from TIA Portal.
        /// </summary>
        Export,

        /// <summary>
        /// The artifact can be imported into TIA Portal.
        /// </summary>
        Import,

        /// <summary>
        /// The artifact supports both export and import.
        /// </summary>
        ExportAndImport,
    }
}
