// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Coordinates UI commands, Core services, state and operational logging.
    /// </summary>
    public sealed class MainController : IDisposable {
        private readonly ApplicationState _state;
        private readonly SynchronizationContext _uiContext;
        private readonly StaWorker _sta;
        private readonly TiaSessionService _session;
        private readonly PlcInventoryService _inventory;
        private readonly HmiInventoryService _hmiInventory;
        private readonly ManifestService _manifest;
        private readonly RepositoryInventoryService _repositoryInventory;
        private readonly ExportService _export;
        private readonly ImportService _import;
        private readonly ImportDependencyPlanner _importPlanner;
        private readonly ImportPreflightValidator _importValidator;
        private readonly ILogger _logger;
        private CancellationTokenSource _currentCancellation;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainController"/> class.
        /// </summary>
        /// <param name="state">Shared application state updated by controller operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is <see langword="null"/>.</exception>
        public MainController(ApplicationState state) {
            this._state = state ?? throw new ArgumentNullException(nameof(state));
            this._uiContext = SynchronizationContext.Current;

            this.LogFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductInfo.DataFolderName,
                "logs",
                "app.log");

            this._logger = new UiLogger(this._state, new FileLogger(this.LogFilePath), this._uiContext);
            this._sta = new StaWorker();
            this._session = new TiaSessionService(this._sta, this._logger);
            this._inventory = new PlcInventoryService(this._session);
            this._hmiInventory = new HmiInventoryService(this._session);
            var xmlNormalize = new XmlNormalizeService();
            this._manifest = new ManifestService();
            this._repositoryInventory = new RepositoryInventoryService(this._manifest);
            this._export = new ExportService(this._session, xmlNormalize, this._manifest, this._logger);
            this._import = new ImportService(this._session, this._logger);
            this._importPlanner = new ImportDependencyPlanner();
            this._importValidator = new ImportPreflightValidator();

            this._state.SelectedRepository = GetDefaultRepositoryPath();
            this._state.CurrentOperation = CurrentOperationInfo.Idle;
            this.Log(ActivitySeverity.Info, "Startup", string.Empty, string.Empty, "Application ready.");
        }

        /// <summary>
        /// Gets the full path of the application log file.
        /// </summary>
        public string LogFilePath {
            get;
        }

        /// <summary>
        /// Refreshes the list of running TIA Portal instances available for attachment.
        /// </summary>
        /// <returns>A task that completes when the state has been updated.</returns>
        public async Task RefreshPortalInstancesAsync() {
            var instances = await this._session.GetRunningPortalInstancesAsync();
            this._state.SetPortalInstances(instances);

            var message = instances.Count == 0
                ? "No running TIA Portal instances detected."
                : "Detected " + instances.Count + " running TIA Portal instance(s).";

            this.Log(
                ActivitySeverity.Info,
                "Project",
                string.Empty,
                string.Empty,
                message);
        }

        /// <summary>
        /// Attaches to a running TIA Portal instance selected by the user or by the current state.
        /// </summary>
        /// <param name="instanceIndex">Optional index of the running TIA Portal instance to attach.</param>
        /// <returns>A task that completes when the session state has been refreshed.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no instance is available or multiple instances require an explicit selection.
        /// </exception>
        public async Task AttachToPortalAsync(int? instanceIndex) {
            await this.RunOperationAsync(OperationState.Connecting, "Attach to TIA Portal", async () => {
                this.UpdateOperationProgress(OperationState.Connecting, "Connecting to TIA Portal...", 0, 0, isIndeterminate: true);
                if (!instanceIndex.HasValue) {
                    await this.RefreshPortalInstancesAsync();
                    if (this._state.PortalInstances.Count > 1) {
                        throw new InvalidOperationException(
                            "Multiple TIA Portal instances are running. Select one explicitly in Project view.");
                    }

                    if (this._state.PortalInstances.Count == 0) {
                        throw new InvalidOperationException("No running TIA Portal instance was detected.");
                    }

                    instanceIndex = this._state.PortalInstances[0].InstanceIndex;
                }

                await this._session.AttachToRunningPortalAsync(instanceIndex.Value);
                await this.RefreshSessionStateAsync();
                this.Log(ActivitySeverity.Success, "Project", string.Empty, string.Empty, "Attached to TIA Portal.");
                return new OperationSummary { Operation = "Attach to TIA Portal", Ok = 1 };
            });
        }

        /// <summary>
        /// Starts a new TIA Portal instance owned by this application.
        /// </summary>
        /// <param name="withUserInterface">Whether the new TIA Portal process should display its UI.</param>
        /// <returns>A task that completes when the session state has been refreshed.</returns>
        public async Task StartPortalAsync(bool withUserInterface) {
            await this.RunOperationAsync(OperationState.Connecting, "Start TIA Portal", async () => {
                this.UpdateOperationProgress(OperationState.Connecting, "Starting TIA Portal...", 0, 0, isIndeterminate: true);
                await this._session.StartPortalAsync(withUserInterface);
                await this.RefreshSessionStateAsync();
                this.Log(ActivitySeverity.Success, "Project", string.Empty, string.Empty, "Started a new TIA Portal instance.");
                return new OperationSummary { Operation = "Start TIA Portal", Ok = 1 };
            });
        }

        /// <summary>
        /// Opens a TIA Portal project, starting TIA Portal first when needed.
        /// </summary>
        /// <param name="projectPath">Path to the project file selected by the user.</param>
        /// <returns>A task that completes when the project has opened and state has refreshed.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="projectPath"/> is empty.</exception>
        public async Task OpenProjectAsync(string projectPath) {
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path is required.", nameof(projectPath));
            }

            await this.RunOperationAsync(OperationState.OpeningProject, "Open project", async () => {
                this.UpdateOperationProgress(OperationState.OpeningProject, "Opening project...", 0, 0, isIndeterminate: true);
                if (!this._session.IsPortalOpen) {
                    await this._session.StartPortalAsync(withUserInterface: true);
                }

                await this._session.OpenProjectAsync(projectPath);
                await this.RefreshSessionStateAsync();
                this.Log(ActivitySeverity.Success, "Project", string.Empty, projectPath, "Project opened.");
                return new OperationSummary { Operation = "Open project", Ok = 1 };
            });
        }

        /// <summary>
        /// Disconnects from TIA Portal and optionally closes an owned process.
        /// </summary>
        /// <param name="closeOwnedPortal">Whether an owned TIA Portal process should be closed.</param>
        /// <returns>A task that completes when the session state has been refreshed.</returns>
        public async Task DisconnectAsync(bool closeOwnedPortal) {
            await this.RunOperationAsync(OperationState.Connecting, "Disconnect", async () => {
                this.UpdateOperationProgress(OperationState.Connecting, "Disconnecting from TIA Portal...", 0, 0, isIndeterminate: true);
                await this._session.DisconnectAsync(closeOwnedPortal);
                await this.RefreshSessionStateAsync();
                this.Log(ActivitySeverity.Info, "Project", string.Empty, string.Empty, "Disconnected from TIA Portal.");
                return new OperationSummary { Operation = "Disconnect", Ok = 1 };
            });
        }

        /// <summary>
        /// Scans the open project for exportable PLC and HMI artifacts.
        /// </summary>
        /// <param name="profile">Export profile controlling the artifact categories to include.</param>
        /// <param name="includeDbs">Whether data blocks should be included in the scan result.</param>
        /// <returns>A task that completes when scan state and project target lists have been updated.</returns>
        public async Task ScanProjectAsync(ExportProfile profile, bool includeDbs) {
            await this.RunOperationAsync(OperationState.LoadingInventory, "Scan project", async () => {
                this.UpdateOperationProgress(OperationState.LoadingInventory, "Reading inventory...", 0, 0, isIndeterminate: true);
                this._state.ScanState = ScanState.Scanning;
                var items = await this._sta.InvokeAsync(() => {
                    var scanned = new List<ExportItem>();
                    scanned.AddRange(this._inventory.ScanExportables(profile, includeDbs));
                    scanned.AddRange(this._hmiInventory.ScanScreens());
                    return scanned;
                });
                this._state.SetLastScan(items);
                this._state.ScanState = ScanState.Ready;

                var project = this._state.ActiveProject;
                if (project != null) {
                    project.PlcTargets.Clear();
                    project.HmiTargets.Clear();
                    project.PlcTargets.AddRange(items.Where(i => i.IsPlc).Select(i => i.CpuPath).Distinct(StringComparer.OrdinalIgnoreCase));
                    project.HmiTargets.AddRange(items.Where(i => i.IsHmi).Select(i => i.CpuPath).Distinct(StringComparer.OrdinalIgnoreCase));
                    this._state.ActiveProject = project;
                }

                this.Log(ActivitySeverity.Success, "Scan", string.Empty, string.Empty, "Scan complete: " + items.Count + " artifact(s).");
                return new OperationSummary { Operation = "Scan project", Ok = items.Count };
            });
        }

        /// <summary>
        /// Exports selected PLC artifacts to the selected repository.
        /// </summary>
        /// <param name="items">Artifacts selected by the user.</param>
        /// <param name="normalizePlcXml">Whether exported PLC XML should be normalized for source control.</param>
        /// <param name="preserveHmiXml">Whether exported HMI XML should be preserved exactly as Openness writes it.</param>
        /// <param name="incremental">Whether unchanged files should be skipped through manifest comparison.</param>
        /// <param name="operationName">Operation name shown in progress and activity messages.</param>
        /// <returns>A task that completes when the export operation has finished.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no artifacts are selected or no repository path is configured.
        /// </exception>
        public async Task ExportAsync(
            IReadOnlyCollection<ExportItem> items,
            bool normalizePlcXml,
            bool preserveHmiXml,
            bool incremental,
            string operationName) {
            if (items == null || items.Count == 0) {
                throw new InvalidOperationException("Select at least one artifact to export.");
            }

            if (string.IsNullOrWhiteSpace(this._state.SelectedRepository)) {
                throw new InvalidOperationException("Select a workspace folder first.");
            }

            Directory.CreateDirectory(this._state.SelectedRepository);

            await this.ExportWithCancellationAsync(items, normalizePlcXml, preserveHmiXml, incremental, operationName);
        }

        /// <summary>
        /// Requests cooperative cancellation of the current operation after the current artifact completes.
        /// </summary>
        public void CancelCurrentOperation() {
            var cts = this._currentCancellation;
            if (cts != null && !cts.IsCancellationRequested) {
                cts.Cancel();
                this.Log(ActivitySeverity.Warning, "Cancel", string.Empty, string.Empty, "Cancel after current artifact requested.");
            }
        }

        /// <summary>
        /// Retries artifacts whose last manifest state was failed, aborted, or explicitly retryable.
        /// </summary>
        /// <param name="items">Current scan items used to resolve artifacts again in the open project.</param>
        /// <param name="normalizePlcXml">Whether PLC XML should be normalized for source control.</param>
        /// <param name="preserveHmiXml">Whether HMI XML should be preserved exactly as exported by Openness.</param>
        /// <returns>A task that completes when retry has finished.</returns>
        public async Task RetryIncompleteExportAsync(
            IReadOnlyCollection<ExportItem> items,
            bool normalizePlcXml,
            bool preserveHmiXml) {
            if (items == null || items.Count == 0) {
                throw new InvalidOperationException("Scan the project before retrying incomplete artifacts.");
            }

            var outputRoot = this._state.LastOperationSummary == null
                ? null
                : this._state.LastOperationSummary.OutputPath;
            if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot)) {
                throw new InvalidOperationException("No previous export manifest was found for retry.");
            }

            var manifest = this._manifest.Load(outputRoot);
            var retryKeys = new HashSet<string>(
                manifest.Entries.Values
                    .Where(ManifestService.IsRetryableEntry)
                    .Select(e => e.LogicalKey)
                    .Where(k => !string.IsNullOrWhiteSpace(k)),
                StringComparer.OrdinalIgnoreCase);
            var retryItems = items.Where(i => retryKeys.Contains(i.LogicalKey)).ToList();
            if (retryItems.Count == 0) {
                throw new InvalidOperationException("The previous manifest does not contain retryable artifacts from the current scan.");
            }

            await this.RunCancellableOperationAsync(OperationState.Exporting, "Retry incomplete export", async token => {
                return await this.ExportInternalAsync(
                    retryItems,
                    normalizePlcXml,
                    preserveHmiXml,
                    incremental: true,
                    operationName: "Retry incomplete export",
                    cancellationToken: token);
            });
        }

        /// <summary>
        /// Updates the repository path selected by the user.
        /// </summary>
        /// <param name="repositoryPath">Repository folder path to store in application state.</param>
        public void SetSelectedRepository(string repositoryPath) {
            this._state.SelectedRepository = repositoryPath;
        }

        /// <summary>
        /// Loads import inventory from a repository folder without blocking the UI thread.
        /// </summary>
        /// <param name="repositoryPath">Repository folder to scan for XML artifacts.</param>
        /// <returns>A task that completes when repository state has been updated.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repositoryPath"/> is empty.</exception>
        public async Task LoadRepositoryInventoryAsync(string repositoryPath) {
            if (string.IsNullOrWhiteSpace(repositoryPath)) {
                throw new ArgumentException("Workspace path is required.", nameof(repositoryPath));
            }

            await this.RunOperationAsync(OperationState.LoadingRepository, "Load workspace", async () => {
                this.UpdateOperationProgress(OperationState.LoadingRepository, "Loading workspace...", 0, 0, isIndeterminate: true);
                this._state.SelectedRepository = repositoryPath;
                var artifacts = await Task.Run(() => this._repositoryInventory.Scan(repositoryPath));
                this._state.SetRepositoryInventory(artifacts);
                this.Log(
                    ActivitySeverity.Info,
                    "Import",
                    string.Empty,
                    repositoryPath,
                    "Workspace inventory loaded: " + artifacts.Count + " artifact(s).");
                return new OperationSummary {
                    Operation = "Load workspace",
                    Ok = artifacts.Count,
                    Status = "Completed",
                };
            });
        }

        /// <summary>
        /// Imports selected repository artifacts into the open TIA Portal project.
        /// </summary>
        /// <param name="artifacts">Repository artifacts selected by the user.</param>
        /// <returns>A task that completes when the import operation has finished.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no repository artifacts are selected.</exception>
        public async Task ImportRepositoryArtifactsAsync(IReadOnlyCollection<RepositoryArtifact> artifacts) {
            await this.ImportRepositoryArtifactsAsync(artifacts, ImportConflictPolicy.Ask, createBackup: true);
        }

        /// <summary>
        /// Imports selected repository artifacts into the open TIA Portal project with explicit HMI conflict handling.
        /// </summary>
        /// <param name="artifacts">Repository artifacts selected by the user.</param>
        /// <param name="conflictPolicy">Policy used when an HMI screen already exists.</param>
        /// <param name="createBackup">Whether existing HMI screens should be backed up before replacement.</param>
        /// <returns>A task that completes when the import operation has finished.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no repository artifacts are selected.</exception>
        public async Task ImportRepositoryArtifactsAsync(
            IReadOnlyCollection<RepositoryArtifact> artifacts,
            ImportConflictPolicy conflictPolicy,
            bool createBackup) {
            if (artifacts == null || artifacts.Count == 0) {
                throw new InvalidOperationException("Select at least one workspace artifact to import.");
            }

            await this.RunCancellableOperationAsync(OperationState.Importing, "Import selected", async token => {
                var selection = artifacts.ToList();
                var backupRoot = Path.Combine(
                    this._state.SelectedRepository,
                    "meta",
                    "import-backups",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                this.UpdateOperationProgress(OperationState.Validating, "Validating import selection...", 0, selection.Count, isIndeterminate: false);
                var plan = await Task.Run(() => this._importPlanner.Build(selection), token);
                var preflight = await Task.Run(() => this._importValidator.Validate(selection, plan), token);
                if (preflight.HasBlockingErrors) {
                    var failedSummary = new OperationSummary {
                        Operation = "Import selected",
                        Status = "Preflight failed",
                        Warnings = preflight.WarningCount,
                        Errors = preflight.ErrorCount,
                        ConflictPolicy = conflictPolicy.ToString(),
                        BackupPath = "No backup created because import did not start.",
                    };
                    AddPreflightMessages(failedSummary, preflight);
                    AddImportPlanMessages(failedSummary, plan);
                    this.Log(ActivitySeverity.Error, "Import", string.Empty, this._state.SelectedRepository, BuildResultText(failedSummary));
                    return failedSummary;
                }

                this.UpdateOperationProgress(OperationState.Importing, "Importing artifacts...", 0, plan.OrderedArtifacts.Count, isIndeterminate: false);
                var result = await this._sta.InvokeAsync(() => this._import.ImportFiles(
                    plan.OrderedArtifacts,
                    conflictPolicy,
                    createBackup,
                    backupRoot,
                    token,
                    this.UpdateImportProgress), token);

                var summary = new OperationSummary {
                    Operation = "Import selected",
                    Ok = result.Imported,
                    Skipped = result.Skipped,
                    Cancelled = result.Cancelled,
                    Warnings = result.Warnings.Count + preflight.WarningCount,
                    Errors = result.Errors.Count + preflight.ErrorCount,
                    ConflictPolicy = conflictPolicy.ToString(),
                    BackupPath = result.BackupRoot ?? "Not used",
                };

                AddPreflightMessages(summary, preflight);
                AddImportPlanMessages(summary, plan);

                foreach (var warning in result.Warnings) {
                    summary.Messages.Add("Warning: " + warning);
                }

                foreach (var error in result.Errors) {
                    summary.Messages.Add("Error: " + error);
                }

                this._state.LastImport = DateTime.Now.ToString("g");
                this._state.LastOperationResult = BuildResultText(summary);

                var severity = result.Errors.Count > 0
                    ? ActivitySeverity.Error
                    : result.Warnings.Count > 0 ? ActivitySeverity.Warning : ActivitySeverity.Success;
                this.Log(severity, "Import", string.Empty, this._state.SelectedRepository, BuildResultText(summary));
                return summary;
            });
        }

        /// <summary>
        /// Previews repository import actions against the open TIA Portal project without modifying it.
        /// </summary>
        /// <param name="artifacts">Repository artifacts selected by the user.</param>
        /// <param name="conflictPolicy">Policy used to classify HMI screen conflicts.</param>
        /// <returns>A task returning an operation summary for the preview dialog.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no repository artifacts are selected.</exception>
        public async Task<OperationSummary> PreviewImportRepositoryArtifactsAsync(
            IReadOnlyCollection<RepositoryArtifact> artifacts,
            ImportConflictPolicy conflictPolicy) {
            if (artifacts == null || artifacts.Count == 0) {
                throw new InvalidOperationException("Select at least one workspace artifact to import.");
            }

            return await this.RunOperationAsync(OperationState.Validating, "Dry run", async () => {
                var selection = artifacts.ToList();
                this.UpdateOperationProgress(OperationState.Validating, "Validating workspace artifacts...", 0, selection.Count, isIndeterminate: false);
                var plan = await Task.Run(() => this._importPlanner.Build(selection));
                var preflight = await Task.Run(() => this._importValidator.Validate(selection, plan));
                ImportResult result;
                if (preflight.HasBlockingErrors) {
                    result = new ImportResult();
                } else {
                    this.UpdateOperationProgress(OperationState.Comparing, "Simulating import actions...", 0, plan.OrderedArtifacts.Count, isIndeterminate: false);
                    result = await this._sta.InvokeAsync(() => this._import.PreviewFiles(plan.OrderedArtifacts, conflictPolicy));
                }

                var summary = new OperationSummary {
                    Operation = "Dry run",
                    SuccessLabel = "Simulated",
                    Ok = result.Imported,
                    Skipped = result.Skipped,
                    Warnings = result.Warnings.Count + preflight.WarningCount,
                    Errors = result.Errors.Count + preflight.ErrorCount,
                    Status = preflight.HasBlockingErrors ? "Preflight failed" : "Dry run only",
                    ConflictPolicy = conflictPolicy.ToString(),
                    BackupPath = "No files are written during dry run.",
                };

                summary.Messages.Add("Workspace validation: completed.");
                summary.Messages.Add("Manifest validation: completed.");
                summary.Messages.Add("XML validation: completed.");
                summary.Messages.Add("Dependency validation: completed.");
                summary.Messages.Add("Import simulation: Openness target lookup only; Siemens XML import compatibility is not verified until a real import runs.");
                AddPreflightMessages(summary, preflight);
                AddImportPlanMessages(summary, plan);

                foreach (var warning in result.Warnings) {
                    summary.Messages.Add(warning);
                }

                foreach (var error in result.Errors) {
                    summary.Messages.Add("Error: " + error);
                }

                return summary;
            });
        }

        /// <summary>
        /// Opens the selected repository folder in Windows Explorer.
        /// </summary>
        public void OpenRepositoryFolder() {
            if (string.IsNullOrWhiteSpace(this._state.SelectedRepository)) {
                return;
            }

            Directory.CreateDirectory(this._state.SelectedRepository);
            Process.Start("explorer.exe", this._state.SelectedRepository);
        }

        /// <summary>
        /// Opens the application log file with the default shell association.
        /// </summary>
        public void OpenLogFile() {
            Directory.CreateDirectory(Path.GetDirectoryName(this.LogFilePath) ?? ".");
            if (!File.Exists(this.LogFilePath)) {
                File.WriteAllText(this.LogFilePath, string.Empty, System.Text.Encoding.UTF8);
            }

            Process.Start(this.LogFilePath);
        }

        /// <summary>
        /// Clears activity entries currently shown in the activity view.
        /// </summary>
        public void ClearActivityView() {
            this._state.ClearActivityView();
        }

        /// <summary>
        /// Refreshes portal/project state from the active TIA Portal session.
        /// </summary>
        /// <returns>A task that completes when state has been synchronized.</returns>
        public async Task RefreshSessionStateAsync() {
            var info = await this._session.GetProjectInfoAsync();

            this._state.PortalConnectionState = info.IsPortalOpen
                ? PortalConnectionState.Connected
                : PortalConnectionState.Disconnected;
            this._state.PortalOwnership = info.IsPortalOpen
                ? (info.OwnsPortal ? PortalOwnership.Owned : PortalOwnership.Attached)
                : PortalOwnership.None;

            if (info.IsProjectOpen) {
                var project = new ProjectDescriptor {
                    Name = info.ProjectName,
                    Path = info.ProjectPath,
                    Version = info.TiaVersion,
                };
                project.PlcTargets.AddRange(info.PlcTargets);
                project.HmiTargets.AddRange(info.HmiTargets);
                this._state.ActiveProject = project;
            } else {
                this._state.ActiveProject = null;
            }
        }

        /// <summary>
        /// Releases the TIA session and STA worker owned by this controller.
        /// </summary>
        public void Dispose() {
            if (this._disposed) {
                return;
            }

            this._disposed = true;
            this._session.Dispose();
            this._sta.Dispose();
        }

        /// <summary>
        /// Builds the compact result text shown in shell status areas.
        /// </summary>
        /// <param name="summary">Operation summary to format.</param>
        /// <returns>The formatted result text.</returns>
        private static string BuildResultText(OperationSummary summary) {
            if (summary == null) {
                return string.Empty;
            }

            return string.Format(
                "{0} - {10}: {1}, skipped: {2}, restricted: {3}, unavailable: {4}, unsupported: {5}, aborted: {6}, cancelled: {7}, warnings: {8}, errors: {9}",
                string.IsNullOrWhiteSpace(summary.Status) ? "Completed" : summary.Status,
                summary.Ok,
                summary.Skipped,
                summary.Restricted,
                summary.Unavailable,
                summary.Unsupported,
                summary.AbortedSessionLost,
                summary.Cancelled,
                summary.Warnings,
                summary.Errors,
                string.IsNullOrWhiteSpace(summary.SuccessLabel) ? "OK" : summary.SuccessLabel);
        }

        /// <summary>
        /// Adds preflight validation messages to an operation summary.
        /// </summary>
        /// <param name="summary">Summary that receives messages.</param>
        /// <param name="preflight">Preflight result to append.</param>
        private static void AddPreflightMessages(OperationSummary summary, ImportPreflightResult preflight) {
            if (summary == null || preflight == null) {
                return;
            }

            foreach (var issue in preflight.Issues) {
                summary.Messages.Add(issue.ToString());
            }
        }

        /// <summary>
        /// Adds dependency plan messages to an operation summary.
        /// </summary>
        /// <param name="summary">Summary that receives messages.</param>
        /// <param name="plan">Dependency plan to describe.</param>
        private static void AddImportPlanMessages(OperationSummary summary, ImportDependencyPlan plan) {
            if (summary == null || plan == null) {
                return;
            }

            summary.Messages.Add("Dependency order:");
            foreach (var artifact in plan.OrderedArtifacts) {
                summary.Messages.Add("  " + artifact.Item.Kind + " | " + artifact.Item.CpuPath + " | " + artifact.Item.GroupPath + " | " + artifact.Item.Name);
            }
        }

        /// <summary>
        /// Classifies an operation summary for final activity logging.
        /// </summary>
        /// <param name="summary">Completed operation summary.</param>
        /// <returns>Activity severity that reflects errors, restrictions, warnings, and clean success.</returns>
        private static ActivitySeverity GetSummarySeverity(OperationSummary summary) {
            if (summary == null) {
                return ActivitySeverity.Warning;
            }

            if (summary.Errors > 0 || summary.AbortedSessionLost > 0) {
                return ActivitySeverity.Error;
            }

            if (summary.Warnings > 0 || summary.Restricted > 0 || summary.Unavailable > 0 || summary.Unsupported > 0 || summary.Skipped > 0 || summary.Cancelled > 0) {
                return ActivitySeverity.Warning;
            }

            return ActivitySeverity.Success;
        }

        /// <summary>
        /// Gets the default export repository path for first launch.
        /// </summary>
        /// <returns>The default repository path under the user's documents folder.</returns>
        private static string GetDefaultRepositoryPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "EidoTiaWorkbenchExports");
        }

        /// <summary>
        /// Executes the shared export flow and converts Core results into an application summary.
        /// </summary>
        /// <param name="items">Artifacts to export.</param>
        /// <param name="normalizePlcXml">Whether PLC XML should be normalized.</param>
        /// <param name="preserveHmiXml">Whether HMI XML should be preserved exactly.</param>
        /// <param name="incremental">Whether manifest comparison is enabled.</param>
        /// <param name="operationName">Operation name shown to the user.</param>
        /// <param name="cancellationToken">Cooperative cancellation token.</param>
        /// <returns>Application operation summary.</returns>
        private async Task<OperationSummary> ExportInternalAsync(
            IReadOnlyCollection<ExportItem> items,
            bool normalizePlcXml,
            bool preserveHmiXml,
            bool incremental,
            string operationName,
            CancellationToken cancellationToken) {
            var selection = items.ToList();
            var result = await this._sta.InvokeAsync(() => this._export.Export(
                this._state.SelectedRepository,
                selection,
                normalizePlcXml,
                preserveHmiXml,
                incremental,
                (done, total, current, ex) => this.UpdateProgress(operationName, done, total, current, ex),
                cancellationToken));

            var summary = new OperationSummary {
                Operation = operationName,
                Status = result.Status.ToString(),
                Ok = result.Exported,
                Skipped = result.Skipped,
                Restricted = result.Restricted,
                Unavailable = result.Unavailable,
                Unsupported = result.Unsupported,
                AbortedSessionLost = result.AbortedSessionLost,
                Cancelled = result.Cancelled,
                Warnings = result.Warnings.Count,
                Errors = result.Errors.Count,
                OutputPath = result.OutputRoot,
                ManifestPath = result.ManifestPath,
                OperationReportPath = result.OperationReportPath,
            };

            foreach (var artifact in result.Artifacts.Where(r =>
                         r.Status != ArtifactExportStatus.Succeeded &&
                         r.Status != ArtifactExportStatus.SkippedUnchanged)) {
                summary.Messages.Add(
                    artifact.Status + ": " + artifact.Kind + " | " + artifact.Target + " | " +
                    artifact.GroupPath + " | " + artifact.Name + " -> " + artifact.Message);
            }

            this._state.LastExport = DateTime.Now.ToString("g");
            this._state.LastOperationResult = BuildResultText(summary);
            this.Log(GetSummarySeverity(summary), "Export", string.Empty, result.OutputRoot, BuildResultText(summary));
            return summary;
        }

        /// <summary>
        /// Exports selected artifacts with cooperative cancellation enabled.
        /// </summary>
        /// <param name="items">Artifacts selected by the user.</param>
        /// <param name="normalizePlcXml">Whether PLC XML should be normalized.</param>
        /// <param name="preserveHmiXml">Whether HMI XML should be preserved exactly.</param>
        /// <param name="incremental">Whether manifest comparison is enabled.</param>
        /// <param name="operationName">Operation name shown to the user.</param>
        /// <returns>A task that completes when the export has finished.</returns>
        private async Task ExportWithCancellationAsync(
            IReadOnlyCollection<ExportItem> items,
            bool normalizePlcXml,
            bool preserveHmiXml,
            bool incremental,
            string operationName) {
            await this.RunCancellableOperationAsync(OperationState.Exporting, operationName, async token => {
                return await this.ExportInternalAsync(
                    items,
                    normalizePlcXml,
                    preserveHmiXml,
                    incremental,
                    operationName,
                    token);
            });
        }

        /// <summary>
        /// Runs a controller operation with common busy-state, logging, and error handling.
        /// </summary>
        /// <param name="state">Operation phase shown by the shell.</param>
        /// <param name="operationName">Display name of the operation.</param>
        /// <param name="body">Asynchronous operation body.</param>
        /// <returns>A task that completes when the operation finishes.</returns>
        private async Task<OperationSummary> RunOperationAsync(OperationState state, string operationName, Func<Task<OperationSummary>> body) {
            return await this.RunOperationAsync(state, operationName, body, canCancel: false);
        }

        /// <summary>
        /// Runs an operation that can be cooperatively cancelled between artifacts.
        /// </summary>
        /// <param name="state">Operation phase shown by the shell.</param>
        /// <param name="operationName">Display name of the operation.</param>
        /// <param name="body">Asynchronous operation body that receives the cancellation token.</param>
        /// <returns>A task that completes when the operation finishes.</returns>
        private async Task RunCancellableOperationAsync(
            OperationState state,
            string operationName,
            Func<CancellationToken, Task<OperationSummary>> body) {
            if (body == null) {
                throw new ArgumentNullException(nameof(body));
            }

            await this.RunOperationAsync(state, operationName, () => body(this._currentCancellation.Token), canCancel: true);
        }

        /// <summary>
        /// Runs a controller operation with common busy-state, logging, cancellation, and error handling.
        /// </summary>
        /// <param name="state">Operation phase shown by the shell.</param>
        /// <param name="operationName">Display name of the operation.</param>
        /// <param name="body">Asynchronous operation body.</param>
        /// <param name="canCancel">Whether the UI can request cooperative cancellation.</param>
        /// <returns>The operation summary produced by the body.</returns>
        private async Task<OperationSummary> RunOperationAsync(
            OperationState state,
            string operationName,
            Func<Task<OperationSummary>> body,
            bool canCancel) {
            if (this._state.CurrentOperation.IsBusy) {
                throw new InvalidOperationException("Another operation is already running.");
            }

            if (canCancel) {
                this._currentCancellation = new CancellationTokenSource();
            }

            this._state.CurrentOperation = new CurrentOperationInfo {
                IsBusy = true,
                State = state,
                Name = operationName,
                StatusText = GetDefaultStatusText(state),
                CurrentArtifact = string.Empty,
                Progress = 0,
                Maximum = 1,
                IsIndeterminate = true,
                CanCancel = canCancel,
            };
            await Task.Yield();

            var stopwatch = Stopwatch.StartNew();
            try {
                this.Log(ActivitySeverity.Info, operationName, string.Empty, string.Empty, "Started.");
                var summary = await body();
                stopwatch.Stop();
                this._state.LastOperationSummary = summary;
                this._state.LastOperationResult = BuildResultText(summary);
                this.Log(GetSummarySeverity(summary), operationName, string.Empty, string.Empty, "Finished in " + stopwatch.Elapsed + ". " + BuildResultText(summary));
                return summary;
            } catch (OperationCanceledException ex) {
                stopwatch.Stop();
                var summary = new OperationSummary {
                    Operation = operationName,
                    Status = "Cancelled",
                    Cancelled = 1,
                };
                this._state.LastOperationSummary = summary;
                this._state.LastOperationResult = BuildResultText(summary);
                this.Log(ActivitySeverity.Warning, operationName, string.Empty, string.Empty, "Cancelled after " + stopwatch.Elapsed + ".", ex.ToString());
                return summary;
            } catch (Exception ex) {
                stopwatch.Stop();
                this._state.LastOperationSummary = new OperationSummary {
                    Operation = operationName,
                    Errors = 1,
                };
                this._state.LastOperationResult = "Error: " + ex.Message;
                this._state.PortalConnectionState = this._session.IsPortalOpen ? PortalConnectionState.Connected : PortalConnectionState.Error;
                this.Log(ActivitySeverity.Error, operationName, string.Empty, string.Empty, ex.Message, ExceptionDiagnosticFormatter.Format(ex));
                throw;
            } finally {
                if (this._currentCancellation != null) {
                    this._currentCancellation.Dispose();
                    this._currentCancellation = null;
                }

                this._state.CurrentOperation = CurrentOperationInfo.Idle;
            }
        }

        /// <summary>
        /// Updates the UI-visible progress model from a worker-thread progress callback.
        /// </summary>
        /// <param name="operationName">Display name of the current operation.</param>
        /// <param name="done">Number of processed artifacts.</param>
        /// <param name="total">Total number of artifacts.</param>
        /// <param name="current">Current artifact, if available.</param>
        /// <param name="exception">Item-level warning or error, if available.</param>
        private void UpdateProgress(string operationName, int done, int total, ExportItem current, Exception exception) {
            this.PostToUi(() => {
                this._state.CurrentOperation = new CurrentOperationInfo {
                    IsBusy = true,
                    State = OperationState.Exporting,
                    Name = operationName,
                    StatusText = total > 0 ? "Exporting artifacts..." : "Exporting...",
                    CurrentArtifact = current != null ? current.ToString() : string.Empty,
                    Progress = done,
                    Maximum = Math.Max(1, total),
                    IsIndeterminate = total <= 0,
                    CanCancel = this._currentCancellation != null && !this._currentCancellation.IsCancellationRequested,
                };

                if (exception != null) {
                    this.Log(
                        exception.GetType().Name == "ExportWarningException"
                            ? ActivitySeverity.Warning
                            : ActivitySeverity.Error,
                        operationName,
                        current != null ? current.Name : string.Empty,
                        current != null ? current.CpuPath : string.Empty,
                        exception.Message,
                        exception.ToString());
                }
            });
        }

        /// <summary>
        /// Updates UI-visible progress for controller-owned operation phases.
        /// </summary>
        /// <param name="state">Current operation phase.</param>
        /// <param name="statusText">Human-readable phase text.</param>
        /// <param name="progress">Completed work count.</param>
        /// <param name="maximum">Total work count when known.</param>
        /// <param name="isIndeterminate">Whether total work is unknown.</param>
        private void UpdateOperationProgress(
            OperationState state,
            string statusText,
            int progress,
            int maximum,
            bool isIndeterminate) {
            this.PostToUi(() => {
                var current = this._state.CurrentOperation ?? CurrentOperationInfo.Idle;
                this._state.CurrentOperation = new CurrentOperationInfo {
                    IsBusy = true,
                    State = state,
                    Name = current.Name,
                    StatusText = statusText,
                    CurrentArtifact = current.CurrentArtifact ?? string.Empty,
                    Progress = Math.Max(0, progress),
                    Maximum = Math.Max(1, maximum),
                    IsIndeterminate = isIndeterminate,
                    CanCancel = current.CanCancel,
                };
            });
        }

        /// <summary>
        /// Updates UI-visible import progress from the STA-bound import service callback.
        /// </summary>
        /// <param name="progress">Import progress information.</param>
        private void UpdateImportProgress(ImportProgressInfo progress) {
            if (progress == null) {
                return;
            }

            this.PostToUi(() => {
                var artifactText = progress.Artifact == null || progress.Artifact.Item == null
                    ? string.Empty
                    : progress.Artifact.Item.ToString();
                this._state.CurrentOperation = new CurrentOperationInfo {
                    IsBusy = true,
                    State = OperationState.Importing,
                    Name = "Import selected",
                    StatusText = string.IsNullOrWhiteSpace(progress.Phase) ? "Importing artifacts..." : progress.Phase,
                    CurrentArtifact = artifactText,
                    Progress = progress.Done,
                    Maximum = Math.Max(1, progress.Total),
                    IsIndeterminate = progress.Total <= 0,
                    CanCancel = this._currentCancellation != null && !this._currentCancellation.IsCancellationRequested,
                };
            });
        }

        /// <summary>
        /// Gets the first status text shown before an operation-specific phase is available.
        /// </summary>
        /// <param name="state">Operation phase.</param>
        /// <returns>Human-readable status text.</returns>
        private static string GetDefaultStatusText(OperationState state) {
            switch (state) {
                case OperationState.Connecting:
                    return "Connecting to TIA Portal...";
                case OperationState.OpeningProject:
                    return "Opening project...";
                case OperationState.LoadingRepository:
                    return "Loading workspace...";
                case OperationState.LoadingInventory:
                    return "Reading inventory...";
                case OperationState.Comparing:
                    return "Comparing artifacts...";
                case OperationState.Validating:
                    return "Validating...";
                case OperationState.Exporting:
                    return "Exporting artifacts...";
                case OperationState.Importing:
                    return "Importing artifacts...";
                case OperationState.BackingUp:
                    return "Creating backup...";
                default:
                    return "Working...";
            }
        }

        /// <summary>
        /// Appends an activity entry to the application state.
        /// </summary>
        /// <param name="severity">Activity severity.</param>
        /// <param name="operation">Operation name.</param>
        /// <param name="artifact">Artifact name.</param>
        /// <param name="target">Target path.</param>
        /// <param name="message">User-visible message.</param>
        /// <param name="detail">Optional diagnostic detail.</param>
        private void Log(
            ActivitySeverity severity,
            string operation,
            string artifact,
            string target,
            string message,
            string detail = null) {
            this._state.AddActivity(new ActivityEntry {
                Time = DateTime.Now,
                Severity = severity,
                Operation = operation,
                Artifact = artifact,
                Target = target,
                Message = message,
                Detail = detail,
            });
        }

        /// <summary>
        /// Executes an action on the captured UI synchronization context when one is available.
        /// </summary>
        /// <param name="action">Action to execute.</param>
        private void PostToUi(Action action) {
            if (this._uiContext != null) {
                this._uiContext.Post(_ => action(), null);
            } else {
                action();
            }
        }

        /// <summary>
        /// Bridges core log messages into the application activity stream.
        /// </summary>
        private sealed class UiLogger : ILogger {
            private readonly ApplicationState _state;
            private readonly ILogger _inner;
            private readonly SynchronizationContext _context;

            /// <summary>
            /// Initializes a new instance of the <see cref="UiLogger"/> class.
            /// </summary>
            /// <param name="state">Application state that receives activity entries.</param>
            /// <param name="inner">Inner logger that writes durable diagnostics.</param>
            /// <param name="context">UI synchronization context used for state updates.</param>
            public UiLogger(ApplicationState state, ILogger inner, SynchronizationContext context) {
                this._state = state;
                this._inner = inner;
                this._context = context;
            }

            /// <inheritdoc />
            public void Info(string message) {
                this._inner.Info(message);
                this.Add(ActivitySeverity.Info, message, null);
            }

            /// <inheritdoc />
            public void Warn(string message) {
                this._inner.Warn(message);
                this.Add(ActivitySeverity.Warning, message, null);
            }

            /// <inheritdoc />
            public void Error(string message, Exception ex = null) {
                this._inner.Error(message, ex);
                this.Add(ActivitySeverity.Error, message, ex);
            }

            /// <summary>
            /// Adds a log entry to application state on the UI context.
            /// </summary>
            /// <param name="severity">Activity severity.</param>
            /// <param name="message">Log message.</param>
            /// <param name="ex">Optional exception detail.</param>
            private void Add(ActivitySeverity severity, string message, Exception ex) {
                Action add = () => this._state.AddActivity(new ActivityEntry {
                    Time = DateTime.Now,
                    Severity = severity,
                    Operation = "Core",
                    Message = message,
                    Detail = ex != null ? ExceptionDiagnosticFormatter.Format(ex) : null,
                });

                if (this._context != null) {
                    this._context.Post(_ => add(), null);
                } else {
                    add();
                }
            }
        }
    }
}
