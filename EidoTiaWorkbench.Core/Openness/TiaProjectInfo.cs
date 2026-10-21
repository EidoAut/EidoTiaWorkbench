// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// TiaProjectInfo.cs
// Provides a UI-safe snapshot of the active TIA Portal session.
//
// Developed by EIDO Automation
// =========================================================================
using System.Collections.Generic;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Provides a UI-safe snapshot of the active TIA Portal session.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class TiaProjectInfo {
        /// <summary>
        /// Gets or sets a value indicating whether a TIA Portal instance is attached or open.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool IsPortalOpen {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether a project is currently open.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool IsProjectOpen {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this tool owns the active TIA Portal process.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool OwnsPortal {
            get; set;
        }

        /// <summary>
        /// Gets or sets the current project name.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string ProjectName {
            get; set;
        }

        /// <summary>
        /// Gets or sets the current project path.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string ProjectPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the TIA Portal version associated with the current session.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string TiaVersion {
            get; set;
        }

        /// <summary>
        /// Gets the PLC targets discovered in the current project.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<string> PlcTargets { get; private set; } = new List<string>();

        /// <summary>
        /// Gets the HMI targets discovered in the current project.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<string> HmiTargets { get; private set; } = new List<string>();
    }
}
