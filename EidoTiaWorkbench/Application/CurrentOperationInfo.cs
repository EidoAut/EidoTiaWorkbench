// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Describes the operation currently running in the UI.
    /// </summary>
    public sealed class CurrentOperationInfo {
        /// <summary>
        /// Gets an idle operation descriptor used when no command is running.
        /// </summary>
        public static CurrentOperationInfo Idle {
            get {
                return new CurrentOperationInfo {
                    IsBusy = false,
                    State = OperationState.Idle,
                    Name = "Ready",
                    StatusText = "Ready",
                    CurrentArtifact = string.Empty,
                    Progress = 0,
                    Maximum = 1,
                    IsIndeterminate = false,
                };
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether an operation is currently running.
        /// </summary>
        public bool IsBusy {
            get; set;
        }

        /// <summary>
        /// Gets or sets the operation state that drives shell feedback and command availability.
        /// </summary>
        public OperationState State {
            get; set;
        }

        /// <summary>
        /// Gets or sets the operation name shown to the user.
        /// </summary>
        public string Name {
            get; set;
        }

        /// <summary>
        /// Gets or sets the current phase text shown in status chrome.
        /// </summary>
        public string StatusText {
            get; set;
        }

        /// <summary>
        /// Gets or sets the artifact currently being processed.
        /// </summary>
        public string CurrentArtifact {
            get; set;
        }

        /// <summary>
        /// Gets or sets the current progress value.
        /// </summary>
        public int Progress {
            get; set;
        }

        /// <summary>
        /// Gets or sets the maximum progress value.
        /// </summary>
        public int Maximum {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the current operation has unknown total work.
        /// </summary>
        public bool IsIndeterminate {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the current operation can be cancelled.
        /// </summary>
        public bool CanCancel {
            get; set;
        }
    }
}
