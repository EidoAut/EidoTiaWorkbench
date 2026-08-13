// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Core.Inventory {
    /// <summary>
    /// Identifies the comparison state of an XML artifact found in the repository.
    /// </summary>
    public enum RepositoryArtifactState {
        /// <summary>
        /// The repository artifact does not have a matching manifest entry and can be imported as new content.
        /// </summary>
        New,

        /// <summary>
        /// The repository artifact exists in the manifest but its current hash differs from the last export.
        /// </summary>
        Modified,

        /// <summary>
        /// The repository artifact hash matches the last recorded export hash.
        /// </summary>
        Identical,

        /// <summary>
        /// The repository artifact references content that is not currently available for import.
        /// </summary>
        MissingDependency,

        /// <summary>
        /// The repository artifact would conflict with an existing TIA Portal object.
        /// </summary>
        Conflict,

        /// <summary>
        /// The repository file cannot be parsed as valid XML.
        /// </summary>
        InvalidXml,

        /// <summary>
        /// The repository path or artifact category is not supported by the importer.
        /// </summary>
        Unsupported,
    }
}
