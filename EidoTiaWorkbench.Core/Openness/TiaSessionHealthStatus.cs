// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// TiaSessionHealthStatus.cs
// Classifies the current TIA Portal session and project availability.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Classifies the current TIA Portal session and project availability.
    /// </summary>
    public enum TiaSessionHealthStatus {
        /// <summary>
        /// A portal and project reference were validated successfully.
        /// </summary>
        Healthy,

        /// <summary>
        /// No portal object is currently attached to the session service.
        /// </summary>
        NoPortal,

        /// <summary>
        /// A portal exists but the service has no open project reference.
        /// </summary>
        NoProject,

        /// <summary>
        /// The portal reference could not be queried and may have disconnected or terminated.
        /// </summary>
        PortalReferenceInvalid,

        /// <summary>
        /// The project reference could not be queried and must not be reused.
        /// </summary>
        ProjectReferenceInvalid,

        /// <summary>
        /// The session service was disposed.
        /// </summary>
        Disposed,
    }
}
