// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Predefined export sets to simplify UI and CLI usage.
    ///
    /// Profiles allow users to quickly select common export scopes
    /// without manually choosing artifact types.
    /// </summary>
    public enum ExportProfile {
        /// <summary>
        /// Core "code-only" export:
        /// - UDTs
        /// - FBs
        /// - FCs
        /// - OBs
        ///
        /// Excludes DBs and tag tables to keep the repository focused on logic.
        /// </summary>
        Core,

        /// <summary>
        /// Core plus PLC tag tables.
        ///
        /// Useful when tag tables act as an IO contract or interface
        /// between PLC logic and external systems.
        /// </summary>
        CorePlusTags,

        /// <summary>
        /// Full export set.
        ///
        /// Includes all typically exportable artifacts:
        /// - UDTs
        /// - FB/FC/OB
        /// - DBs
        /// - PLC tag tables
        ///
        /// Provides the most complete project snapshot for backup or migration.
        /// </summary>
        Full,
    }
}
