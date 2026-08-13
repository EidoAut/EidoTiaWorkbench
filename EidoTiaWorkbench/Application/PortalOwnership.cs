// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Identifies whether the active TIA Portal process is owned by this application.
    /// </summary>
    public enum PortalOwnership {
        /// <summary>
        /// No TIA Portal process is currently associated with the session.
        /// </summary>
        None,

        /// <summary>
        /// The session is attached to a process that was already running.
        /// </summary>
        Attached,

        /// <summary>
        /// The session started the process and may close it when requested.
        /// </summary>
        Owned,
    }
}
