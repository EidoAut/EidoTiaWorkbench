// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportConflictPolicy.cs
// Defines how repository imports handle an artifact that already exists in the target TIA project.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Defines how repository imports handle an artifact that already exists in the target TIA project.
    /// </summary>
    public enum ImportConflictPolicy {
        /// <summary>
        /// Ask the user before replacing existing content.
        /// The service expects the caller to obtain confirmation before running the import.
        /// </summary>
        Ask,

        /// <summary>
        /// Keep existing TIA Portal content and skip the conflicting repository artifact.
        /// </summary>
        Skip,

        /// <summary>
        /// Replace existing TIA Portal content after creating a backup when backup mode is enabled.
        /// </summary>
        Replace,
    }
}
