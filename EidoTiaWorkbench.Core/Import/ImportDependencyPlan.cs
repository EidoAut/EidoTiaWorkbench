// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportDependencyPlan.cs
// Contains the dependency-ordered import list plus planner diagnostics.
//
// Developed by EIDO Automation
// =========================================================================
using System.Collections.Generic;
using EidoTiaWorkbench.Core.Inventory;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Contains the dependency-ordered import list plus planner diagnostics.
    /// </summary>
    public sealed class ImportDependencyPlan {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportDependencyPlan"/> class.
        /// </summary>
        /// <param name="orderedArtifacts">Artifacts ordered so dependencies are imported first.</param>
        /// <param name="issues">Dependency issues detected while building the plan.</param>
        public ImportDependencyPlan(
            IReadOnlyList<RepositoryArtifact> orderedArtifacts,
            IReadOnlyList<ImportDependencyIssue> issues) {
            this.OrderedArtifacts = orderedArtifacts ?? new List<RepositoryArtifact>().AsReadOnly();
            this.Issues = issues ?? new List<ImportDependencyIssue>().AsReadOnly();
        }

        /// <summary>
        /// Gets artifacts ordered so dependencies are imported first.
        /// </summary>
        public IReadOnlyList<RepositoryArtifact> OrderedArtifacts {
            get;
        }

        /// <summary>
        /// Gets dependency issues detected while building the plan.
        /// </summary>
        public IReadOnlyList<ImportDependencyIssue> Issues {
            get;
        }
    }
}
