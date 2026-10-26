// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportDependencyPlanner.cs
// Builds a dependency graph for repository imports and returns a stable dependency-first order.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Builds a dependency graph for repository imports and returns a stable dependency-first order.
    /// </summary>
    public sealed class ImportDependencyPlanner {
        /// <summary>
        /// Builds an import dependency plan for the selected repository artifacts.
        /// </summary>
        /// <param name="selection">Repository artifacts selected by the user.</param>
        /// <returns>Ordered artifacts plus dependency warnings and errors.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <see langword="null"/>.
        /// </exception>
        public ImportDependencyPlan Build(IReadOnlyCollection<RepositoryArtifact> selection) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            var artifacts = selection
                .Where(a => a != null && a.Item != null)
                .Distinct(new RepositoryArtifactLogicalKeyComparer())
                .ToList();
            var byKey = artifacts.ToDictionary(a => a.Item.LogicalKey, StringComparer.OrdinalIgnoreCase);
            var edges = artifacts.ToDictionary(
                a => a.Item.LogicalKey,
                a => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
            var issues = new List<ImportDependencyIssue>();

            foreach (var artifact in artifacts) {
                AddExplicitDependencies(artifact, byKey, edges, issues);
                AddConservativeDependencies(artifact, artifacts, edges);
            }

            var ordered = TopologicalSort(artifacts, byKey, edges, issues);
            return new ImportDependencyPlan(ordered.AsReadOnly(), issues.AsReadOnly());
        }

        /// <summary>
        /// Adds manifest-declared logical dependencies to the graph.
        /// </summary>
        /// <param name="artifact">Artifact whose dependencies are inspected.</param>
        /// <param name="byKey">Selected artifacts by logical key.</param>
        /// <param name="edges">Graph edges keyed by dependent logical key.</param>
        /// <param name="issues">Mutable dependency issues collection.</param>
        private static void AddExplicitDependencies(
            RepositoryArtifact artifact,
            IReadOnlyDictionary<string, RepositoryArtifact> byKey,
            IDictionary<string, HashSet<string>> edges,
            ICollection<ImportDependencyIssue> issues) {
            foreach (var dependencyKey in artifact.Item.Dependencies ?? new List<string>()) {
                if (string.IsNullOrWhiteSpace(dependencyKey)) {
                    continue;
                }

                if (byKey.ContainsKey(dependencyKey)) {
                    edges[artifact.Item.LogicalKey].Add(dependencyKey);
                    continue;
                }

                issues.Add(new ImportDependencyIssue(
                    artifact,
                    dependencyKey,
                    "Manifest dependency is not selected for import: " + dependencyKey + "."));
            }
        }

        /// <summary>
        /// Adds known domain ordering dependencies where Openness commonly rejects child artifacts first.
        /// </summary>
        /// <param name="artifact">Dependent artifact being planned.</param>
        /// <param name="artifacts">All selected artifacts.</param>
        /// <param name="edges">Graph edges keyed by dependent logical key.</param>
        private static void AddConservativeDependencies(
            RepositoryArtifact artifact,
            IEnumerable<RepositoryArtifact> artifacts,
            IDictionary<string, HashSet<string>> edges) {
            foreach (var candidate in artifacts) {
                if (candidate == artifact ||
                    !SameTarget(artifact, candidate) ||
                    !ShouldImportBefore(candidate.Item.Kind, artifact.Item.Kind)) {
                    continue;
                }

                edges[artifact.Item.LogicalKey].Add(candidate.Item.LogicalKey);
            }
        }

        /// <summary>
        /// Determines whether two artifacts belong to the same PLC CPU or HMI target.
        /// </summary>
        /// <param name="left">First artifact.</param>
        /// <param name="right">Second artifact.</param>
        /// <returns><see langword="true"/> when both artifacts share a target path.</returns>
        private static bool SameTarget(RepositoryArtifact left, RepositoryArtifact right) {
            return string.Equals(left.Item.CpuPath, right.Item.CpuPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a candidate kind should be treated as a dependency of another kind.
        /// </summary>
        /// <param name="candidate">Potential dependency kind.</param>
        /// <param name="dependent">Dependent artifact kind.</param>
        /// <returns><see langword="true"/> when the candidate should be imported first.</returns>
        private static bool ShouldImportBefore(ExportKind candidate, ExportKind dependent) {
            if (dependent == ExportKind.HmiScreen) {
                return candidate == ExportKind.HmiTagTable ||
                       candidate == ExportKind.HmiTemplate ||
                       candidate == ExportKind.HmiTextList ||
                       candidate == ExportKind.HmiGraphicList ||
                       candidate == ExportKind.HmiConnection ||
                       candidate == ExportKind.HmiVBScript;
            }

            if (dependent == ExportKind.HmiTemplate ||
                dependent == ExportKind.HmiPopup ||
                dependent == ExportKind.HmiSlideIn) {
                return candidate == ExportKind.HmiTagTable ||
                       candidate == ExportKind.HmiTextList ||
                       candidate == ExportKind.HmiGraphicList ||
                       candidate == ExportKind.HmiConnection ||
                       candidate == ExportKind.HmiVBScript;
            }

            if (dependent == ExportKind.Ob ||
                dependent == ExportKind.Fb ||
                dependent == ExportKind.Fc ||
                dependent == ExportKind.Db ||
                dependent == ExportKind.TechnologicalObject) {
                return candidate == ExportKind.Udt ||
                       candidate == ExportKind.PlcTagTable ||
                       candidate == ExportKind.PlcUserConstant;
            }

            if (dependent == ExportKind.PlcUserConstant) {
                return candidate == ExportKind.PlcTagTable;
            }

            return ArtifactAdapterRegistry.Get(
                candidate).ImportOrder < ArtifactAdapterRegistry.Get(dependent).ImportOrder &&
                   candidate != ExportKind.Unsupported;
        }

        /// <summary>
        /// Performs a stable topological sort, falling back to catalog import order when no edge exists.
        /// </summary>
        /// <param name="artifacts">Selected artifacts.</param>
        /// <param name="byKey">Selected artifacts by key.</param>
        /// <param name="edges">Dependency edges keyed by dependent artifact key.</param>
        /// <param name="issues">Mutable dependency issues collection.</param>
        /// <returns>Dependency-ordered artifacts.</returns>
        private static List<RepositoryArtifact> TopologicalSort(
            IReadOnlyCollection<RepositoryArtifact> artifacts,
            IReadOnlyDictionary<string, RepositoryArtifact> byKey,
            IDictionary<string, HashSet<string>> edges,
            ICollection<ImportDependencyIssue> issues) {
            var ordered = new List<RepositoryArtifact>();
            var permanent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var temporary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stable = artifacts
                .OrderBy(a => ArtifactAdapterRegistry.Get(a.Item.Kind).ImportOrder)
                .ThenBy(a => a.Item.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Item.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(a => a.Item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var artifact in stable) {
                Visit(artifact, byKey, edges, permanent, temporary, ordered, issues);
            }

            return ordered;
        }

        /// <summary>
        /// Visits one graph node during topological sorting.
        /// </summary>
        /// <param name="artifact">Artifact to visit.</param>
        /// <param name="byKey">Artifacts by logical key.</param>
        /// <param name="edges">Dependency edges keyed by dependent artifact key.</param>
        /// <param name="permanent">Permanently visited node keys.</param>
        /// <param name="temporary">Temporary recursion stack keys.</param>
        /// <param name="ordered">Mutable ordered output.</param>
        /// <param name="issues">Mutable dependency issues collection.</param>
        private static void Visit(
            RepositoryArtifact artifact,
            IReadOnlyDictionary<string, RepositoryArtifact> byKey,
            IDictionary<string, HashSet<string>> edges,
            ISet<string> permanent,
            ISet<string> temporary,
            ICollection<RepositoryArtifact> ordered,
            ICollection<ImportDependencyIssue> issues) {
            var key = artifact.Item.LogicalKey;
            if (permanent.Contains(key)) {
                return;
            }

            if (temporary.Contains(key)) {
                issues.Add(new ImportDependencyIssue(artifact, key, "Cyclic import dependency detected."));
                return;
            }

            temporary.Add(key);
            foreach (var dependencyKey in edges[key]
                .OrderBy(
                    k => byKey.ContainsKey(
                        k) ? ArtifactAdapterRegistry.Get(byKey[k].Item.Kind).ImportOrder : int.MaxValue)
                .ThenBy(k => k, StringComparer.OrdinalIgnoreCase)) {
                RepositoryArtifact dependency;
                if (byKey.TryGetValue(dependencyKey, out dependency)) {
                    Visit(dependency, byKey, edges, permanent, temporary, ordered, issues);
                }
            }

            temporary.Remove(key);
            permanent.Add(key);
            if (!ordered.Contains(artifact)) {
                ordered.Add(artifact);
            }
        }

        /// <summary>
        /// Compares repository artifacts by logical key to remove accidental duplicate selections.
        /// </summary>
        private sealed class RepositoryArtifactLogicalKeyComparer : IEqualityComparer<RepositoryArtifact> {
            /// <inheritdoc />
            public bool Equals(RepositoryArtifact x, RepositoryArtifact y) {
                if (ReferenceEquals(x, y)) {
                    return true;
                }

                if (x == null || y == null || x.Item == null || y.Item == null) {
                    return false;
                }

                return string.Equals(x.Item.LogicalKey, y.Item.LogicalKey, StringComparison.OrdinalIgnoreCase);
            }

            /// <inheritdoc />
            public int GetHashCode(RepositoryArtifact obj) {
                return obj == null || obj.Item == null || obj.Item.LogicalKey == null
                    ? 0
                    : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item.LogicalKey);
            }
        }
    }
}
