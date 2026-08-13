// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Provides a UI-safe descriptor for one attachable TIA Portal instance.
    /// </summary>
    public sealed class TiaPortalInstanceInfo {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiaPortalInstanceInfo"/> class.
        /// </summary>
        /// <param name="instanceIndex">Index used by Openness to attach to the running process.</param>
        /// <param name="processId">Operating system process identifier, when Openness exposes it.</param>
        /// <param name="displayName">Display name shown in the selection UI.</param>
        /// <param name="projectPath">Currently open project path reported by the process, if available.</param>
        public TiaPortalInstanceInfo(int instanceIndex, int? processId, string displayName, string projectPath) {
            this.InstanceIndex = instanceIndex;
            this.ProcessId = processId;
            this.DisplayName = displayName;
            this.ProjectPath = projectPath;
        }

        /// <summary>
        /// Gets the index used by Openness to attach to the running process.
        /// </summary>
        public int InstanceIndex {
            get;
        }

        /// <summary>
        /// Gets the operating system process identifier, when Openness exposes it.
        /// </summary>
        public int? ProcessId {
            get;
        }

        /// <summary>
        /// Gets the display name shown in the selection UI.
        /// </summary>
        public string DisplayName {
            get;
        }

        /// <summary>
        /// Gets the currently open project path reported by the process, if available.
        /// </summary>
        public string ProjectPath {
            get;
        }

        /// <summary>
        /// Returns display text for selection controls and logs.
        /// </summary>
        /// <returns>A string containing process and project information.</returns>
        public override string ToString() {
            var pid = this.ProcessId.HasValue ? "PID " + this.ProcessId.Value + " - " : string.Empty;
            var project = string.IsNullOrWhiteSpace(this.ProjectPath) ? "No project path reported" : this.ProjectPath;
            return pid + this.DisplayName + " | " + project;
        }
    }
}
