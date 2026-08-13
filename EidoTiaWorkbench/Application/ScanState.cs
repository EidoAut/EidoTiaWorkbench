// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Identifies the lifecycle state of the project inventory scan.
    /// </summary>
    public enum ScanState {
        /// <summary>
        /// No scan result is available.
        /// </summary>
        NotScanned,

        /// <summary>
        /// A scan is currently running.
        /// </summary>
        Scanning,

        /// <summary>
        /// A scan completed successfully and results are available.
        /// </summary>
        Ready,

        /// <summary>
        /// The latest scan failed.
        /// </summary>
        Error,
    }
}
