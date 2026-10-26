// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// CommandStateService.cs
// Centralizes command availability so views do not duplicate state rules.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Centralizes command availability so views do not duplicate state rules.
    /// </summary>
    public sealed class CommandStateService {
        private readonly ApplicationState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandStateService"/> class.
        /// </summary>
        /// <param name="state">Application state used to derive command availability.</param>
        public CommandStateService(ApplicationState state) {
            this._state = state;
        }

        /// <summary>
        /// Gets a value indicating whether a new long-running operation can start.
        /// </summary>
        public bool CanStartOperation {
            get {
                return !this._state.CurrentOperation.IsBusy;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the user can attach to a TIA Portal instance.
        /// </summary>
        public bool CanAttachPortal {
            get {
                return this.CanStartOperation && this._state.HasOpennessAdapter;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the user can open a TIA Portal project.
        /// </summary>
        public bool CanOpenProject {
            get {
                return this.CanStartOperation && this._state.PortalConnectionState == PortalConnectionState.Connected;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current project can be scanned.
        /// </summary>
        public bool CanScanProject {
            get {
                return this.CanStartOperation && this._state.ActiveProject != null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether selected scan results can be exported.
        /// </summary>
        public bool CanExport {
            get {
                return this.CanStartOperation && this._state.ScanState == ScanState.Ready &&
                    this._state.ActiveProject != null &&
                    this._state.PortalConnectionState == PortalConnectionState.Connected &&
                    this._state.LastScan.Count > 0 && !string.IsNullOrWhiteSpace(
                    this._state.SelectedRepository);
            }
        }

        /// <summary>
        /// Gets a value indicating whether repository artifacts can be imported.
        /// </summary>
        public bool CanImport {
            get {
                return this.CanStartOperation
                    && this._state.RepositoryInventory.Count > 0
                    && this._state.ActiveProject != null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the selected repository can be opened in Explorer.
        /// </summary>
        public bool CanOpenRepository {
            get {
                return !string.IsNullOrWhiteSpace(this._state.SelectedRepository);
            }
        }
    }
}
