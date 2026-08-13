// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Identifies the current connection state of the TIA Portal session.
    /// </summary>
    public enum PortalConnectionState {
        /// <summary>
        /// No TIA Portal instance is attached or open.
        /// </summary>
        Disconnected,

        /// <summary>
        /// A TIA Portal instance is attached or open and can receive commands.
        /// </summary>
        Connected,

        /// <summary>
        /// The last portal operation failed or left the connection in an uncertain state.
        /// </summary>
        Error,
    }
}
