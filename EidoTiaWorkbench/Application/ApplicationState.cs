// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections.Generic;
using System.Linq;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Observable application state shared by presenters and shell.
    /// </summary>
    public sealed class ApplicationState {
        private readonly List<ExportItem> _lastScan = new List<ExportItem>();
        private readonly List<RepositoryArtifact> _repositoryInventory = new List<RepositoryArtifact>();
        private readonly List<ActivityEntry> _activity = new List<ActivityEntry>();
        private readonly List<TiaPortalInstanceInfo> _portalInstances = new List<TiaPortalInstanceInfo>();

        private PortalConnectionState _portalConnectionState = PortalConnectionState.Disconnected;
        private PortalOwnership _portalOwnership = PortalOwnership.None;
        private ProjectDescriptor _activeProject;
        private string _selectedRepository;
        private ScanState _scanState = ScanState.NotScanned;
        private CurrentOperationInfo _currentOperation = CurrentOperationInfo.Idle;
        private OperationSummary _lastOperationSummary;
        private string _lastExport;
        private string _lastImport;
        private string _lastOperationResult;

        /// <summary>
        /// Occurs when any observable state value changes.
        /// </summary>
        public event EventHandler StateChanged;

        /// <summary>
        /// Occurs when a new activity entry is appended to the activity log.
        /// </summary>
        public event EventHandler<ActivityEntryEventArgs> ActivityAdded;

        /// <summary>
        /// Gets or sets the current connection state of the TIA Portal session.
        /// </summary>
        public PortalConnectionState PortalConnectionState {
            get {
                return this._portalConnectionState;
            }

            set {
                this.SetField(ref this._portalConnectionState, value);
            }
        }

        /// <summary>
        /// Gets or sets whether the active TIA Portal process is owned by this application.
        /// </summary>
        public PortalOwnership PortalOwnership {
            get {
                return this._portalOwnership;
            }

            set {
                this.SetField(ref this._portalOwnership, value);
            }
        }

        /// <summary>
        /// Gets or sets the descriptor of the current project shown in the UI.
        /// </summary>
        public ProjectDescriptor ActiveProject {
            get {
                return this._activeProject;
            }

            set {
                this.SetField(ref this._activeProject, value);
            }
        }

        /// <summary>
        /// Gets or sets the repository folder selected by the user.
        /// </summary>
        public string SelectedRepository {
            get {
                return this._selectedRepository;
            }

            set {
                this.SetField(ref this._selectedRepository, value);
            }
        }

        /// <summary>
        /// Gets or sets the state of the latest project scan.
        /// </summary>
        public ScanState ScanState {
            get {
                return this._scanState;
            }

            set {
                this.SetField(ref this._scanState, value);
            }
        }

        /// <summary>
        /// Gets or sets information about the operation currently running in the UI.
        /// </summary>
        public CurrentOperationInfo CurrentOperation {
            get {
                return this._currentOperation;
            }

            set {
                this.SetField(ref this._currentOperation, value ?? CurrentOperationInfo.Idle);
            }
        }

        /// <summary>
        /// Gets or sets the summary of the last completed operation.
        /// </summary>
        public OperationSummary LastOperationSummary {
            get {
                return this._lastOperationSummary;
            }

            set {
                this.SetField(ref this._lastOperationSummary, value);
            }
        }

        /// <summary>
        /// Gets or sets the display timestamp of the last export operation.
        /// </summary>
        public string LastExport {
            get {
                return this._lastExport;
            }

            set {
                this.SetField(ref this._lastExport, value);
            }
        }

        /// <summary>
        /// Gets or sets the display timestamp of the last import operation.
        /// </summary>
        public string LastImport {
            get {
                return this._lastImport;
            }

            set {
                this.SetField(ref this._lastImport, value);
            }
        }

        /// <summary>
        /// Gets or sets the last operation result text shown in dashboard summaries.
        /// </summary>
        public string LastOperationResult {
            get {
                return this._lastOperationResult;
            }

            set {
                this.SetField(ref this._lastOperationResult, value);
            }
        }

        /// <summary>
        /// Gets a snapshot of the most recent project scan result.
        /// </summary>
        public IReadOnlyList<ExportItem> LastScan {
            get {
                return this._lastScan.ToList();
            }
        }

        /// <summary>
        /// Gets a snapshot of repository artifacts discovered for import.
        /// </summary>
        public IReadOnlyList<RepositoryArtifact> RepositoryInventory {
            get {
                return this._repositoryInventory.ToList();
            }
        }

        /// <summary>
        /// Gets a snapshot of activity entries displayed by the activity view.
        /// </summary>
        public IReadOnlyList<ActivityEntry> Activity {
            get {
                return this._activity.ToList();
            }
        }

        /// <summary>
        /// Gets a snapshot of running TIA Portal instances available for attachment.
        /// </summary>
        public IReadOnlyList<TiaPortalInstanceInfo> PortalInstances {
            get {
                return this._portalInstances.ToList();
            }
        }

        /// <summary>
        /// Replaces the last scan result and notifies observers.
        /// </summary>
        /// <param name="items">New exportable items, or <see langword="null"/> to clear the scan.</param>
        public void SetLastScan(IEnumerable<ExportItem> items) {
            this._lastScan.Clear();
            if (items != null) {
                this._lastScan.AddRange(items);
            }

            this.OnStateChanged();
        }

        /// <summary>
        /// Replaces the repository inventory and notifies observers.
        /// </summary>
        /// <param name="artifacts">Repository artifacts, or <see langword="null"/> to clear the inventory.</param>
        public void SetRepositoryInventory(IEnumerable<RepositoryArtifact> artifacts) {
            this._repositoryInventory.Clear();
            if (artifacts != null) {
                this._repositoryInventory.AddRange(artifacts);
            }

            this.OnStateChanged();
        }

        /// <summary>
        /// Replaces the list of attachable TIA Portal instances and notifies observers.
        /// </summary>
        /// <param name="instances">Portal instance descriptors, or <see langword="null"/> to clear the list.</param>
        public void SetPortalInstances(IEnumerable<TiaPortalInstanceInfo> instances) {
            this._portalInstances.Clear();
            if (instances != null) {
                this._portalInstances.AddRange(instances);
            }

            this.OnStateChanged();
        }

        /// <summary>
        /// Adds an activity entry, raises the activity event, and notifies observers.
        /// </summary>
        /// <param name="entry">Activity entry to append.</param>
        public void AddActivity(ActivityEntry entry) {
            if (entry == null) {
                return;
            }

            this._activity.Add(entry);
            this.ActivityAdded?.Invoke(this, new ActivityEntryEventArgs(entry));
            this.OnStateChanged();
        }

        /// <summary>
        /// Clears entries currently shown in the activity view and notifies observers.
        /// </summary>
        public void ClearActivityView() {
            this._activity.Clear();
            this.OnStateChanged();
        }

        /// <summary>
        /// Updates a backing field and raises a state notification only when the value changes.
        /// </summary>
        /// <typeparam name="T">Type of the stored value.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value to assign.</param>
        private void SetField<T>(ref T field, T value) {
            if (EqualityComparer<T>.Default.Equals(field, value)) {
                return;
            }

            field = value;
            this.OnStateChanged();
        }

        /// <summary>
        /// Raises the state changed event for observers.
        /// </summary>
        private void OnStateChanged() {
            this.StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
