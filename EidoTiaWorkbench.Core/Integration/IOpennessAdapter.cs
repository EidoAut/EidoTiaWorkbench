// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// IOpennessAdapter.cs
// Owns one version-specific engineering worker. No Siemens objects escape this boundary.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EidoTiaWorkbench.Core.Openness;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>Owns one version-specific engineering worker. No Siemens objects escape this boundary.</summary>
    public interface IOpennessAdapter : IDisposable {
        /// <summary>Gets a value indicating whether the last acknowledged session has an open portal.</summary>
        bool IsPortalOpen {
            get;
        }

        /// <summary>Enumerates processes for the selected API without attaching.</summary>
        /// <returns>Stable process identifiers and UI-safe descriptions.</returns>
        Task<List<TiaPortalInstanceInfo>> GetRunningPortalInstancesAsync();

        /// <summary>Attaches after revalidating process identity and version.</summary>
        /// <param name="instanceIndex">Stable process ID; legacy parameter name retained.</param>
        /// <returns>Completion after attachment is acknowledged.</returns>
        Task AttachToRunningPortalAsync(int instanceIndex);

        /// <summary>Starts a new portal owned by this worker.</summary>
        /// <param name="withUserInterface">Whether Siemens should show its UI.</param>
        /// <returns>Completion after startup and any access prompt.</returns>
        Task StartPortalAsync(bool withUserInterface);

        /// <summary>Opens a project in the active session.</summary>
        /// <param name="projectPath">Local Siemens project file.</param>
        /// <returns>Completion after the project is adopted.</returns>
        Task OpenProjectAsync(string projectPath);

        /// <summary>Releases the session; external portal processes retain their owner.</summary>
        /// <param name="closeOwnedPortal">Whether an owned portal should close.</param>
        /// <returns>Completion after cleanup is acknowledged.</returns>
        Task DisconnectAsync(bool closeOwnedPortal);

        /// <summary>Reads a snapshot without leaking Siemens references.</summary>
        /// <returns>Current portal, project and target descriptions.</returns>
        Task<TiaProjectInfo> GetProjectInfoAsync();

        /// <summary>
        /// Executes a serialized command off the UI thread; cancellation waits for safe worker completion.
        /// </summary>
        /// <param name="request">Versioned operation and artifact identities.</param>
        /// <param name="token">Cancellation between items, never forced termination.</param>
        /// <param name="progress">Callback on the calling background thread.</param>
        /// <returns>Acknowledged result with per-artifact failures.</returns>
        /// <exception cref="System.IO.IOException">Worker disconnected; mutation outcome may be uncertain.</exception>
        /// <exception cref="NotSupportedException">No usable adapter selected.</exception>
        WorkerMessage Execute(WorkerMessage request, CancellationToken token, Action<WorkerMessage> progress = null);
    }
}
