// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Identifies the single long-running operation phase currently owned by the shell.
    /// </summary>
    public enum OperationState {
        /// <summary>
        /// No operation is currently running.
        /// </summary>
        Idle,

        /// <summary>
        /// The application is connecting to, starting, or disconnecting from TIA Portal.
        /// </summary>
        Connecting,

        /// <summary>
        /// The application is opening a TIA Portal project.
        /// </summary>
        OpeningProject,

        /// <summary>
        /// The application is loading repository inventory from disk.
        /// </summary>
        LoadingRepository,

        /// <summary>
        /// The application is reading project inventory.
        /// </summary>
        LoadingInventory,

        /// <summary>
        /// The application is comparing project and repository artifacts.
        /// </summary>
        Comparing,

        /// <summary>
        /// The application is validating repository artifacts before import.
        /// </summary>
        Validating,

        /// <summary>
        /// The application is exporting artifacts from TIA Portal.
        /// </summary>
        Exporting,

        /// <summary>
        /// The application is importing artifacts into TIA Portal.
        /// </summary>
        Importing,

        /// <summary>
        /// The application is backing up existing TIA Portal artifacts before replacement.
        /// </summary>
        BackingUp,

        /// <summary>
        /// The last operation completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// The last operation failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The last operation was cancelled cooperatively.
        /// </summary>
        Cancelled,
    }
}
