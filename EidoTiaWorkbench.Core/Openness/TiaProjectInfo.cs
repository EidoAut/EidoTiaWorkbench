// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Collections.Generic;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Provides a UI-safe snapshot of the active TIA Portal session.
    /// </summary>
    public sealed class TiaProjectInfo {
        /// <summary>
        /// Gets or sets a value indicating whether a TIA Portal instance is attached or open.
        /// </summary>
        public bool IsPortalOpen {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether a project is currently open.
        /// </summary>
        public bool IsProjectOpen {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this tool owns the active TIA Portal process.
        /// </summary>
        public bool OwnsPortal {
            get; set;
        }

        /// <summary>
        /// Gets or sets the current project name.
        /// </summary>
        public string ProjectName {
            get; set;
        }

        /// <summary>
        /// Gets or sets the current project path.
        /// </summary>
        public string ProjectPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the TIA Portal version associated with the current session.
        /// </summary>
        public string TiaVersion {
            get; set;
        }

        /// <summary>
        /// Gets the PLC targets discovered in the current project.
        /// </summary>
        public List<string> PlcTargets { get; } = new List<string>();

        /// <summary>
        /// Gets the HMI targets discovered in the current project.
        /// </summary>
        public List<string> HmiTargets { get; } = new List<string>();
    }
}
