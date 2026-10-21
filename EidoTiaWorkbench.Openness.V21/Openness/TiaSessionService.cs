// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// TiaSessionService.cs
// Manages the lifecycle of a TIA Portal session and project using the Openness API.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EidoTiaWorkbench.Core.Utilities;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Manages the lifecycle of a TIA Portal session and project using the
    /// Openness API.
    /// </summary>
    /// <remarks>
    /// Responsibilities:
    /// - Start or attach to a running TIA Portal instance
    /// - Open / close projects
    /// - Archive projects
    /// - Ensure all calls are executed on the STA thread required by Openness
    ///
    /// The service can either own the TIA Portal instance (created by this
    /// process) or attach to an existing one.
    /// </remarks>
    public sealed class TiaSessionService : IDisposable {
        /// <summary>
        /// STA worker used to marshal all Openness calls onto the required
        /// Single-Threaded Apartment thread.
        /// </summary>
        private readonly StaWorker _sta;

        /// <summary>
        /// Logger instance used for diagnostic and operational messages.
        /// </summary>
        private readonly ILogger _log;

        /// <summary>
        /// Active TIA Portal instance.
        /// </summary>
        private TiaPortal _tiaPortal;

        /// <summary>
        /// Currently opened project within the TIA Portal session.
        /// </summary>
        private Project _project;

        /// <summary>
        /// Indicates whether this service created (owns) the TIA Portal instance.
        /// If false, disposal will not close the external instance.
        /// </summary>
        private bool _ownsPortal;

        /// <summary>
        /// Disposal state flag.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TiaSessionService"/> class.
        /// </summary>
        /// <param name="staWorker">STA worker used to execute all Openness operations.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="staWorker"/> is <see langword="null"/>.
        /// </exception>
        public TiaSessionService(StaWorker staWorker)
            : this(staWorker, NullLogger.Instance) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TiaSessionService"/> class.
        /// </summary>
        /// <param name="staWorker">STA worker used to execute all Openness operations.</param>
        /// <param name="logger">Logger that receives session lifecycle messages.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="staWorker"/> is <see langword="null"/>.
        /// </exception>
        public TiaSessionService(StaWorker staWorker, ILogger logger) {
            this._sta = staWorker ?? throw new ArgumentNullException(nameof(staWorker));
            this._log = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Gets a value indicating whether a TIA Portal instance is currently attached or open.
        /// </summary>
        public bool IsPortalOpen => this._tiaPortal != null;

        /// <summary>
        /// Gets a value indicating whether a project is currently open.
        /// </summary>
        public bool IsProjectOpen => this._project != null;

        /// <summary>
        /// Gets a value indicating whether this service owns the active TIA Portal process.
        /// </summary>
        public bool OwnsPortal => this._ownsPortal;

        /// <summary>
        /// Returns the running TIA Portal processes that can be presented to the user.
        /// </summary>
        /// <returns>A task that returns UI-safe descriptors for attachable TIA Portal instances.</returns>
        public Task<List<TiaPortalInstanceInfo>> GetRunningPortalInstancesAsync() {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => {
                var result = new List<TiaPortalInstanceInfo>();
                var processes = TiaPortal.GetProcesses();

                for (var i = 0; i < processes.Count; i++) {
                    var process = processes[i];
                    result.Add(new TiaPortalInstanceInfo(
                        process.Id,
                        process.Id,
                        GetObjectString(process, "Name") ?? "TIA Portal instance " + (i + 1),
                        GetObjectString(process, "ProjectPath") ?? GetObjectString(process, "ProjectFile")));
                }

                return result;
            });
        }

        // ------------------------------------------------------------------
        // OPEN / ATTACH PORTAL
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens a TIA Portal instance or attaches to an existing running one.
        /// </summary>
        /// <remarks>
        /// Strategy:
        /// 1) Attempt to attach to a running instance (preferred).
        /// 2) If none available, start a new instance.
        ///
        /// When attaching, the service will try to adopt an already open
        /// project if found.
        /// </remarks>
        /// <param name="withUserInterface">Whether a newly created TIA Portal instance should display its UI.</param>
        /// <returns>A task that completes after a portal instance is attached or started.</returns>
        public Task OpenPortalAsync(bool withUserInterface) {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => {
                if (this._tiaPortal != null) {
                    return true;
                }

                // -----------------------------
                // 1) Try attach to running TIA
                // -----------------------------
                try {
                    var processes = TiaPortal.GetProcesses();

                    if (processes != null && processes.Count > 0) {
                        if (processes.Count > 1) {
                            throw new InvalidOperationException(
                                "Multiple TIA Portal instances are running. Select the intended instance in Project view.");
                        }

                        this._tiaPortal = processes[0].Attach();
                        this._project = this._tiaPortal.Projects.FirstOrDefault();
                        this._ownsPortal = false;

                        this._log.Info("Attached to existing TIA Portal instance.");

                        if (this._project != null) {
                            this._log.Info("Adopted open project: " + this._project.Name);
                        }

                        return true;
                    }
                }
                catch (Exception ex) {
                    if (ex is InvalidOperationException &&
                        ex.Message.IndexOf("Multiple TIA Portal instances", StringComparison.OrdinalIgnoreCase) >= 0) {
                        throw;
                    }

                    this._log.Warn("Attach failed. Starting new TIA. " + ex.Message);
                }

                // -----------------------------
                // 2) Start new instance
                // -----------------------------
                this._tiaPortal = new TiaPortal(
                    withUserInterface
                        ? TiaPortalMode.WithUserInterface
                        : TiaPortalMode.WithoutUserInterface);

                this._ownsPortal = true;

                this._log.Info("Started new TIA Portal instance.");
                return true;
            });
        }

        /// <summary>
        /// Attaches to one explicit running TIA Portal process selected by the UI.
        /// </summary>
        /// <param name="instanceIndex">Index of the running TIA Portal process returned by Openness.</param>
        /// <returns>A task that completes after the selected instance is attached.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the selected TIA Portal instance is no longer available.
        /// </exception>
        public Task AttachToRunningPortalAsync(int instanceIndex) {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => {
                if (this._tiaPortal != null) {
                    this.DisconnectInternal(closeOwnedPortal: true);
                }

                var processes = TiaPortal.GetProcesses();
                var selected = processes.FirstOrDefault(process => process.Id == instanceIndex);
                if (selected == null) {
                    throw new ArgumentOutOfRangeException(nameof(instanceIndex), "Selected TIA Portal instance is no longer available.");
                }

                using (var operatingSystemProcess = System.Diagnostics.Process.GetProcessById(selected.Id)) {
                    var executable = operatingSystemProcess.MainModule.FileName;
                    var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(executable);
                    if (version.FileMajorPart != 2100) {
                        throw new NotSupportedException("Selected process is not TIA Portal V21: " + executable);
                    }
                }

                this._tiaPortal = selected.Attach();
                this._project = this._tiaPortal.Projects.FirstOrDefault();
                this._ownsPortal = false;

                this._log.Info("Attached to selected TIA Portal instance.");
                if (this._project != null) {
                    this._log.Info("Adopted open project: " + this._project.Name);
                }

                return true;
            });
        }

        /// <summary>
        /// Starts a new TIA Portal process. The resulting process is owned by this tool.
        /// </summary>
        /// <param name="withUserInterface">Whether the new TIA Portal process should display its UI.</param>
        /// <returns>A task that completes after the new process is created.</returns>
        public Task StartPortalAsync(bool withUserInterface) {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => {
                if (this._tiaPortal != null) {
                    this.DisconnectInternal(closeOwnedPortal: true);
                }

                this._tiaPortal = new TiaPortal(
                    withUserInterface
                        ? TiaPortalMode.WithUserInterface
                        : TiaPortalMode.WithoutUserInterface);

                this._project = null;
                this._ownsPortal = true;
                this._log.Info("Started new TIA Portal instance.");

                return true;
            });
        }

        // ------------------------------------------------------------------
        // PROJECT MANAGEMENT
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens a TIA Portal project from disk.
        /// </summary>
        /// <remarks>
        /// If another project is already open, it will be closed first.
        /// </remarks>
        /// <param name="projectPath">Path to the TIA Portal project file.</param>
        /// <returns>A task that completes after the project has opened.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="projectPath"/> is empty.</exception>
        public Task OpenProjectAsync(string projectPath) {
            this.EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path is required.", nameof(projectPath));
            }

            var fi = new FileInfo(projectPath);

            return this._sta.InvokeAsync(() => {
                this.EnsurePortalOpenOnSta();

                this.CloseProjectInternal();
                this._project = this._tiaPortal.Projects.Open(fi);

                this._log.Info("Opened project: " + this._project.Name);
                return true;
            });
        }

        /// <summary>
        /// Closes the currently open project, if any.
        /// </summary>
        /// <returns>A task that completes after the current project has been closed or confirmed absent.</returns>
        public Task CloseProjectAsync() {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => {
                this.CloseProjectInternal();
                return true;
            });
        }

        /// <summary>
        /// Disconnects this tool from TIA Portal. External instances are never closed.
        /// Owned instances are only closed when requested by the caller after confirmation.
        /// </summary>
        /// <param name="closeOwnedPortal">Whether an owned TIA Portal process should be disposed.</param>
        /// <returns>A task that completes after the session is detached or closed.</returns>
        public Task DisconnectAsync(bool closeOwnedPortal) {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => {
                this.DisconnectInternal(closeOwnedPortal);
                return true;
            });
        }

        /// <summary>
        /// Returns a UI-safe snapshot of the current session and project.
        /// </summary>
        /// <returns>A task that returns current portal, project, and target information.</returns>
        public Task<TiaProjectInfo> GetProjectInfoAsync() {
            this.EnsureNotDisposed();

            return this._sta.InvokeAsync(() => this.BuildProjectInfoOnSta());
        }

        // ------------------------------------------------------------------
        // ARCHIVE
        // ------------------------------------------------------------------

        /// <summary>
        /// Archives the currently open project to a .zap archive.
        /// </summary>
        /// <remarks>
        /// The archive directory will be created if it does not exist.
        /// </remarks>
        /// <param name="archivePath">Destination archive path requested by the user.</param>
        /// <returns>A task that completes after the current project has been archived.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="archivePath"/> is empty.</exception>
        public Task ArchiveProjectAsync(string archivePath) {
            this.EnsureNotDisposed();

            if (string.IsNullOrWhiteSpace(archivePath)) {
                throw new ArgumentException("Archive path is required.", nameof(archivePath));
            }

            var fi = new FileInfo(archivePath);

            return this._sta.InvokeAsync(() => {
                this.EnsureProjectOpenOnSta();

                var dir = new DirectoryInfo(fi.DirectoryName ?? ".");
                Directory.CreateDirectory(dir.FullName);

                var targetName = Path.GetFileNameWithoutExtension(fi.Name);
                if (string.IsNullOrWhiteSpace(targetName)) {
                    targetName = this._project.Name;
                }

                // ProjectArchivationMode = 0 → default archive behavior
                this._project.Archive(dir, targetName, (ProjectArchivationMode)0);

                this._log.Info("Project archived: " + targetName);
                return true;
            });
        }

        /// <summary>
        /// Disposes the TIA session safely.
        /// </summary>
        /// <remarks>
        /// The service closes any open project, disposes a TIA Portal instance only when it owns that
        /// instance, and otherwise detaches from external instances.
        /// </remarks>
        public void Dispose() {
            if (this._disposed) {
                return;
            }

            this._disposed = true;

            try {
                this._sta.InvokeAsync(() => {
                    this.DisconnectInternal(closeOwnedPortal: true);
                    return true;
                }).GetAwaiter().GetResult();
            }
            catch (Exception ex) {
                this._log.Error("Failed to dispose TIA session cleanly.", ex);
            }
        }

        // ------------------------------------------------------------------
        // INTERNAL ACCESS
        // ------------------------------------------------------------------

        /// <summary>
        /// Returns the currently open project (STA thread only).
        /// </summary>
        /// <remarks>
        /// Revalidates that the project is still open in case the user
        /// manually closed it from the TIA UI.
        /// </remarks>
        /// <returns>The currently open project after validating the Openness handle.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no project is currently open.</exception>
        internal Project GetOpenProjectOnSta() {
            var health = this.CheckHealthOnSta();
            if (!health.IsHealthy) {
                throw new InvalidOperationException(health.Message, health.Exception);
            }

            return this._project;
        }

        /// <summary>
        /// Checks whether the current TIA Portal and project references can still be used.
        /// </summary>
        /// <remarks>
        /// This method must be called on the Openness STA thread. It validates more than
        /// a local null check by querying both the portal project collection and the project
        /// name. Any exception is preserved so the export pipeline can distinguish a lost
        /// session from an artifact-specific Openness failure.
        /// </remarks>
        /// <returns>A health object describing the current session state.</returns>
        internal TiaSessionHealth CheckHealthOnSta() {
            if (this._disposed) {
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.Disposed,
                    "The TIA session service has been disposed.",
                    null);
            }

            if (this._tiaPortal == null) {
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.NoPortal,
                    "TIA Portal is not open or attached.",
                    null);
            }

            try {
                GC.KeepAlive(this._tiaPortal.Projects.Count);
            }
            catch (Exception ex) {
                this._tiaPortal = null;
                this._project = null;
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.PortalReferenceInvalid,
                    "TIA Portal could not be queried. The instance may have disconnected or terminated.",
                    ex);
            }

            if (this._project == null) {
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.NoProject,
                    "No project is open.",
                    null);
            }

            try {
                var projectName = this._project.Name;
                var projectPath = ResolveProjectPath(this._project);
                GC.KeepAlive(projectName);
                return TiaSessionHealth.Healthy(projectName, projectPath);
            }
            catch (Exception ex) {
                this._project = null;
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.ProjectReferenceInvalid,
                    "The project reference could not be validated and will not be reused.",
                    ex);
            }
        }

        /// <summary>
        /// Attempts one controlled recovery of the active portal/project references.
        /// </summary>
        /// <remarks>
        /// Recovery never reuses a stale project object. It first revalidates or reattaches a portal,
        /// then adopts the currently open project from that portal so subsequent artifact exports resolve
        /// fresh Siemens objects from stable logical keys.
        /// </remarks>
        /// <returns>The health observed after the recovery attempt.</returns>
        internal TiaSessionHealth TryRecoverProjectOnSta() {
            return this.TryRecoverProjectOnSta(null, null);
        }

        /// <summary>
        /// Attempts one controlled recovery of the active portal/project references.
        /// </summary>
        /// <param name="expectedProjectName">Project name captured before the export run began.</param>
        /// <param name="expectedProjectPath">Project path captured before the export run began, when available.</param>
        /// <returns>The health observed after the recovery attempt.</returns>
        internal TiaSessionHealth TryRecoverProjectOnSta(string expectedProjectName, string expectedProjectPath) {
            if (this._disposed) {
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.Disposed,
                    "The TIA session service has been disposed.",
                    null);
            }

            Exception originalFailure = null;
            if (this._tiaPortal != null) {
                try {
                    GC.KeepAlive(this._tiaPortal.Projects.Count);
                }
                catch (Exception ex) {
                    originalFailure = ex;
                    this._tiaPortal = null;
                    this._project = null;
                }
            }

            if (this._tiaPortal == null) {
                try {
                    var processes = TiaPortal.GetProcesses();
                    if (processes == null || processes.Count != 1) {
                        return TiaSessionHealth.Unhealthy(
                            TiaSessionHealthStatus.NoPortal,
                            "Controlled recovery could not identify exactly one running TIA Portal instance.",
                            originalFailure);
                    }

                    this._tiaPortal = processes[0].Attach();
                    this._ownsPortal = false;
                    this._log.Warn("Recovered by reattaching to the single running TIA Portal instance.");
                }
                catch (Exception ex) {
                    this._tiaPortal = null;
                    this._project = null;
                    return TiaSessionHealth.Unhealthy(
                        TiaSessionHealthStatus.PortalReferenceInvalid,
                        "Controlled recovery could not reattach to TIA Portal.",
                        ex);
                }
            }

            try {
                this._project = this.ResolveRecoveryProject(expectedProjectName, expectedProjectPath);
                if (this._project == null) {
                    return TiaSessionHealth.Unhealthy(
                        TiaSessionHealthStatus.NoProject,
                        "Controlled recovery found TIA Portal but no matching open project.",
                        originalFailure);
                }

                var health = this.CheckHealthOnSta();
                if (health.IsHealthy) {
                    this._log.Warn("Recovered the open TIA project reference: " + health.ProjectName);
                }

                return health;
            }
            catch (Exception ex) {
                this._project = null;
                return TiaSessionHealth.Unhealthy(
                    TiaSessionHealthStatus.ProjectReferenceInvalid,
                    "Controlled recovery could not adopt a valid open project.",
                    ex);
            }
        }

        /// <summary>
        /// Resolves the best available file-system path for a TIA project.
        /// </summary>
        /// <param name="project">Project whose path should be resolved.</param>
        /// <returns>The project path when available; otherwise <see langword="null"/>.</returns>
        private static string ResolveProjectPath(Project project) {
            if (project == null) {
                return null;
            }

            var direct = GetObjectString(project, "Path") ??
                         GetObjectString(project, "ProjectPath") ??
                         GetObjectString(project, "ProjectFile");
            if (!string.IsNullOrWhiteSpace(direct)) {
                return direct;
            }

            var fileInfo = GetObjectValue(project, "ProjectFile") as FileInfo;
            return fileInfo != null ? fileInfo.FullName : null;
        }

        /// <summary>
        /// Collects PLC and HMI target names for the project information snapshot.
        /// </summary>
        /// <param name="project">Project whose device tree should be inspected.</param>
        /// <param name="info">Project information snapshot to update.</param>
        private static void CollectTargets(Project project, TiaProjectInfo info) {
            foreach (var device in project.Devices) {
                foreach (var item in FindAllDeviceItems(device)) {
                    var container = item.GetService<SoftwareContainer>();
                    var software = container != null ? container.Software : null;
                    if (software == null) {
                        continue;
                    }

                    var typeName = software.GetType().Name;
                    var targetName = device.Name + "/" + item.Name;

                    if (typeName.IndexOf("PlcSoftware", StringComparison.OrdinalIgnoreCase) >= 0) {
                        info.PlcTargets.Add(targetName);
                    }
                    else if (typeName.IndexOf("Hmi", StringComparison.OrdinalIgnoreCase) >= 0) {
                        info.HmiTargets.Add(targetName);
                    }
                }
            }
        }

        /// <summary>
        /// Walks every device item below a TIA device.
        /// </summary>
        /// <param name="device">Device whose items should be traversed.</param>
        /// <returns>All nested device items.</returns>
        private static IEnumerable<DeviceItem> FindAllDeviceItems(Device device) {
            var stack = new Stack<DeviceItem>(device.DeviceItems);
            while (stack.Count > 0) {
                var item = stack.Pop();
                yield return item;

                foreach (var child in item.DeviceItems) {
                    stack.Push(child);
                }
            }
        }

        /// <summary>
        /// Reads a property value through reflection when the property is available.
        /// </summary>
        /// <param name="obj">Object to inspect.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The property value, or <see langword="null"/> when unavailable.</returns>
        private static object GetObjectValue(object obj, string propertyName) {
            if (obj == null) {
                return null;
            }

            var prop = obj.GetType().GetProperty(propertyName);
            return prop == null ? null : prop.GetValue(obj, null);
        }

        /// <summary>
        /// Reads an optional string property through reflection.
        /// </summary>
        /// <param name="obj">Object to inspect.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The string property value, or <see langword="null"/> when unavailable.</returns>
        private static string GetObjectString(object obj, string propertyName) {
            return GetObjectValue(obj, propertyName) as string;
        }

        /// <summary>
        /// Reads an optional integer property through reflection.
        /// </summary>
        /// <param name="obj">Object to inspect.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The integer property value, or <see langword="null"/> when unavailable.</returns>
        private static int? GetObjectInt(object obj, string propertyName) {
            var value = GetObjectValue(obj, propertyName);
            if (value is int) {
                return (int)value;
            }

            return null;
        }

        /// <summary>
        /// Resolves the recovered project while preserving the identity captured before the operation started.
        /// </summary>
        /// <param name="expectedProjectName">Expected project name, when available.</param>
        /// <param name="expectedProjectPath">Expected project path, when available.</param>
        /// <returns>The matching project, or <see langword="null"/> when no unambiguous match exists.</returns>
        private Project ResolveRecoveryProject(string expectedProjectName, string expectedProjectPath) {
            if (this._tiaPortal == null) {
                return null;
            }

            var projects = this._tiaPortal.Projects.ToList();
            if (projects.Count == 0) {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(expectedProjectPath)) {
                var byPath = projects
                    .Where(p => string.Equals(ResolveProjectPath(p), expectedProjectPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byPath.Count == 1) {
                    return byPath[0];
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(expectedProjectName)) {
                var byName = projects
                    .Where(p => string.Equals(p.Name, expectedProjectName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (byName.Count == 1) {
                    return byName[0];
                }

                return null;
            }

            return projects.Count == 1 ? projects[0] : null;
        }

        /// <summary>
        /// Closes the project without thread marshaling.
        /// Must be called from STA context.
        /// </summary>
        private void CloseProjectInternal() {
            if (this._project != null) {
                try {
                    this._project.Close();
                }
                finally {
                    this._project = null;
                }
            }
        }

        /// <summary>
        /// Disconnects or closes the active portal without crossing thread boundaries.
        /// Must be called from STA context.
        /// </summary>
        /// <param name="closeOwnedPortal">True to dispose an owned portal instance; false to leave it running.</param>
        private void DisconnectInternal(bool closeOwnedPortal) {
            if (this._tiaPortal == null) {
                this._project = null;
                this._ownsPortal = false;
                return;
            }

            if (this._ownsPortal && closeOwnedPortal) {
                this.CloseProjectInternal();
                this._log.Info("Disposing owned TIA Portal instance.");
                this._tiaPortal.Dispose();
            }
            else {
                this._log.Info(this._ownsPortal
                    ? "Kept owned TIA Portal instance running."
                    : "Detached from external TIA Portal instance.");
            }

            this._tiaPortal = null;
            this._project = null;
            this._ownsPortal = false;
        }

        /// <summary>
        /// Builds a project/session snapshot for the presentation layer.
        /// Must be called from STA context.
        /// </summary>
        /// <returns>The current project information snapshot.</returns>
        private TiaProjectInfo BuildProjectInfoOnSta() {
            var info = new TiaProjectInfo {
                IsPortalOpen = this._tiaPortal != null,
                IsProjectOpen = this._project != null,
                OwnsPortal = this._ownsPortal,
                TiaVersion = "V21",
            };

            var health = this.CheckHealthOnSta();
            if (!health.IsHealthy) {
                info.IsPortalOpen = this._tiaPortal != null;
                info.IsProjectOpen = false;
                return info;
            }

            try {
                info.ProjectName = this._project.Name;
                info.ProjectPath = ResolveProjectPath(this._project);
                CollectTargets(this._project, info);
            }
            catch (Exception ex) {
                this._project = null;
                info.IsProjectOpen = false;
                info.ProjectName = null;
                info.ProjectPath = null;
                this._log.Warn("Project information refresh failed: " + ex.Message);
            }

            return info;
        }

        /// <summary>
        /// Ensures a TIA Portal instance is open.
        /// </summary>
        private void EnsurePortalOpenOnSta() {
            if (this._tiaPortal == null) {
                throw new InvalidOperationException(
                    "TIA Portal is not open. Call OpenPortalAsync() first.");
            }
        }

        /// <summary>
        /// Ensures a project is open.
        /// </summary>
        private void EnsureProjectOpenOnSta() {
            this.EnsurePortalOpenOnSta();

            if (this._project == null) {
                throw new InvalidOperationException(
                    "No project is open.");
            }
        }

        /// <summary>
        /// Throws if the service has been disposed.
        /// </summary>
        private void EnsureNotDisposed() {
            if (this._disposed) {
                throw new ObjectDisposedException(nameof(TiaSessionService));
            }
        }
    }
}
