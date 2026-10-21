// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// TiaPortalInstanceInfo.cs
// Provides a UI-safe descriptor for one attachable TIA Portal instance.
//
// Developed by EIDO Automation
// =========================================================================
using System;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Provides a UI-safe descriptor for one attachable TIA Portal instance.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class TiaPortalInstanceInfo {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiaPortalInstanceInfo"/> class.
        /// </summary>
        /// <param name="instanceIndex">Stable process ID used to re-resolve the selected running process.</param>
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
        /// Gets the stable process ID (legacy property name retained for compatibility).
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int InstanceIndex {
            get; private set;
        }

        /// <summary>
        /// Gets the operating system process identifier, when Openness exposes it.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int? ProcessId {
            get; private set;
        }

        /// <summary>
        /// Gets the display name shown in the selection UI.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string DisplayName {
            get; private set;
        }

        /// <summary>
        /// Gets the currently open project path reported by the process, if available.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string ProjectPath {
            get; private set;
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
