// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactDomain.cs
// Identifies the engineering domain that owns an exportable or importable artifact.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Identifies the engineering domain that owns an exportable or importable artifact.
    /// </summary>
    public enum ArtifactDomain {
        /// <summary>
        /// STEP 7 PLC software artifacts.
        /// </summary>
        Plc,

        /// <summary>
        /// Classic WinCC HMI artifacts.
        /// </summary>
        HmiClassic,

        /// <summary>
        /// WinCC Unified artifacts.
        /// </summary>
        HmiUnified,

        /// <summary>
        /// Hardware or device-level artifacts.
        /// </summary>
        Hardware,

        /// <summary>
        /// Project-level artifacts that are not owned by a single device.
        /// </summary>
        Project,
    }
}
