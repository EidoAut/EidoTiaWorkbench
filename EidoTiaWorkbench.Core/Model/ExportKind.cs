// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportKind.cs
// Identifies the exportable object type. This enum is used across scanning, export, import, and path
// generation to determine how each artifact should be handled.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Identifies the exportable object type.
    ///
    /// This enum is used across scanning, export, import, and path generation
    /// to determine how each artifact should be handled.
    /// </summary>
    public enum ExportKind {
        /// <summary>
        /// User-defined data type (UDT / PLC data type).
        /// </summary>
        Udt,

        /// <summary>
        /// Function block (FB).
        /// Typically contains instance data and may reference UDTs.
        /// </summary>
        Fb,

        /// <summary>
        /// Function (FC).
        /// Stateless block without instance DB.
        /// </summary>
        Fc,

        /// <summary>
        /// Organization block (OB).
        /// Entry point blocks executed by the PLC runtime (e.g., OB1, OB100).
        /// </summary>
        Ob,

        /// <summary>
        /// Data block (DB).
        /// Can be global or instance DB.
        /// </summary>
        Db,

        /// <summary>
        /// PLC tag table.
        /// Contains symbolic tags and addresses.
        /// Availability depends on Openness API exposure.
        /// </summary>
        PlcTagTable,

        /// <summary>
        /// Classic WinCC HMI screen exported from an HMI target screen folder.
        /// </summary>
        HmiScreen,

        /// <summary>
        /// Logical HMI screen folder used by the UI to group selectable screens.
        /// Folder selection expands to the screens contained in the folder.
        /// </summary>
        HmiScreenFolder,

        /// <summary>
        /// PLC watch table exported from the watch and force table group.
        /// </summary>
        PlcWatchTable,

        /// <summary>
        /// PLC force table exported from the watch and force table group.
        /// </summary>
        PlcForceTable,

        /// <summary>
        /// PLC user constant exported from the PLC tag table root.
        /// </summary>
        PlcUserConstant,

        /// <summary>
        /// PLC technology object represented by a technological instance DB.
        /// </summary>
        TechnologicalObject,

        /// <summary>
        /// PLC external source discovered from the external source group.
        /// </summary>
        PlcExternalSource,

        /// <summary>
        /// PLC alarm text list discovered from the alarm text list group.
        /// </summary>
        PlcAlarmTextList,

        /// <summary>
        /// Classic WinCC screen template.
        /// </summary>
        HmiTemplate,

        /// <summary>
        /// Classic WinCC popup screen.
        /// </summary>
        HmiPopup,

        /// <summary>
        /// Classic WinCC slide-in screen.
        /// </summary>
        HmiSlideIn,

        /// <summary>
        /// Classic WinCC global screen elements.
        /// </summary>
        HmiGlobalElements,

        /// <summary>
        /// Classic WinCC screen overview.
        /// </summary>
        HmiScreenOverview,

        /// <summary>
        /// Classic WinCC HMI tag table.
        /// </summary>
        HmiTagTable,

        /// <summary>
        /// Classic WinCC HMI connection.
        /// </summary>
        HmiConnection,

        /// <summary>
        /// Classic WinCC HMI text list.
        /// </summary>
        HmiTextList,

        /// <summary>
        /// Classic WinCC HMI graphic list.
        /// </summary>
        HmiGraphicList,

        /// <summary>
        /// Classic WinCC HMI cycle.
        /// </summary>
        HmiCycle,

        /// <summary>
        /// Classic WinCC VB script.
        /// </summary>
        HmiVBScript,

        /// <summary>
        /// WinCC Unified tag exchange artifact.
        /// </summary>
        UnifiedTagTable,

        /// <summary>
        /// WinCC Unified script module exchange artifact.
        /// </summary>
        UnifiedScriptModule,

        /// <summary>
        /// WinCC Unified text list exchange artifact.
        /// </summary>
        UnifiedTextList,

        /// <summary>
        /// WinCC Unified graphic list exchange artifact.
        /// </summary>
        UnifiedGraphicList,

        /// <summary>
        /// Hardware CAx transfer artifact.
        /// </summary>
        HardwareCax,

        /// <summary>
        /// Repository artifact that cannot be mapped to a supported Openness operation.
        /// </summary>
        Unsupported,
    }
}
