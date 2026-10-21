// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportDependencyIssue.cs
// Describes a dependency problem discovered while planning import order.
//
// Developed by EIDO Automation
// =========================================================================
using EidoTiaWorkbench.Core.Inventory;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Describes a dependency problem discovered while planning import order.
    /// </summary>
    public sealed class ImportDependencyIssue {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDependencyIssue"/> class.
        /// </summary>
        /// <param name="artifact">Artifact that declared or implied the dependency.</param>
        /// <param name="dependencyKey">Missing or cyclic dependency key.</param>
        /// <param name="message">Human-readable dependency message.</param>
        public ImportDependencyIssue(RepositoryArtifact artifact, string dependencyKey, string message) {
            this.Artifact = artifact;
            this.DependencyKey = dependencyKey ?? string.Empty;
            this.Message = message ?? string.Empty;
        }

        /// <summary>
        /// Gets the artifact that declared or implied the dependency.
        /// </summary>
        public RepositoryArtifact Artifact {
            get;
        }

        /// <summary>
        /// Gets the missing or cyclic dependency key.
        /// </summary>
        public string DependencyKey {
            get;
        }

        /// <summary>
        /// Gets the human-readable dependency message.
        /// </summary>
        public string Message {
            get;
        }
    }
}
