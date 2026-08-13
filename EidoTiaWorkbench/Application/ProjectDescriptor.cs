// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Collections.Generic;

namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Describes the project currently shown by the presentation layer.
    /// </summary>
    public sealed class ProjectDescriptor {
        /// <summary>
        /// Gets or sets the project name reported by TIA Portal.
        /// </summary>
        public string Name {
            get; set;
        }

        /// <summary>
        /// Gets or sets the project path reported by TIA Portal.
        /// </summary>
        public string Path {
            get; set;
        }

        /// <summary>
        /// Gets or sets the TIA Portal version associated with the project.
        /// </summary>
        public string Version {
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
