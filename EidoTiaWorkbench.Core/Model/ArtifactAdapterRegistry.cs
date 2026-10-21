// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactAdapterRegistry.cs
// Central registry that binds catalog descriptors to concrete pipeline support.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Central registry that binds catalog descriptors to concrete pipeline support.
    /// </summary>
    public static class ArtifactAdapterRegistry {
        private static readonly IReadOnlyDictionary<ExportKind, IArtifactAdapter> AdaptersByKind =
            ArtifactCatalog.All.ToDictionary(d => d.Kind, d => (IArtifactAdapter)new CatalogArtifactAdapter(d));

        /// <summary>
        /// Gets all registered adapters.
        /// </summary>
        public static IReadOnlyCollection<IArtifactAdapter> All {
            get {
                return AdaptersByKind.Values.ToList().AsReadOnly();
            }
        }

        /// <summary>
        /// Gets the adapter for one artifact kind.
        /// </summary>
        /// <param name="kind">Artifact kind to resolve.</param>
        /// <returns>The registered adapter, or the unsupported adapter.</returns>
        public static IArtifactAdapter Get(ExportKind kind) {
            IArtifactAdapter adapter;
            return AdaptersByKind.TryGetValue(kind, out adapter)
                ? adapter
                : AdaptersByKind[ExportKind.Unsupported];
        }

        /// <summary>
        /// Validates that catalog support claims and registered adapters are internally consistent.
        /// </summary>
        /// <returns>Validation messages; an empty collection means the registry is consistent.</returns>
        public static IReadOnlyList<string> Validate() {
            var messages = new List<string>();
            foreach (ExportKind kind in Enum.GetValues(typeof(ExportKind))) {
                var descriptor = ArtifactCatalog.Get(kind);
                var adapter = Get(kind);
                if (descriptor.Kind != kind) {
                    messages.Add("Missing descriptor for " + kind + ".");
                }

                if (descriptor.CanExport && !adapter.CanExport) {
                    messages.Add("Descriptor declares export without adapter support: " + kind + ".");
                }

                if (descriptor.CanImport && !adapter.CanImport) {
                    messages.Add("Descriptor declares import without adapter support: " + kind + ".");
                }

                if (descriptor.CanScan && !adapter.CanScan) {
                    messages.Add("Descriptor declares scan without adapter support: " + kind + ".");
                }
            }

            var duplicates = ArtifactCatalog.All
                .Where(d => d.Kind != ExportKind.Unsupported)
                .Where(d => !(d.Domain == ArtifactDomain.Plc && string.Equals(d.Category, "Blocks", StringComparison.OrdinalIgnoreCase)))
                .GroupBy(d => d.Domain + "|" + d.Category, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);
            foreach (var duplicate in duplicates) {
                messages.Add("Repository category is not unique: " + duplicate + ".");
            }

            return messages.AsReadOnly();
        }

        /// <summary>
        /// Adapter implementation backed by the central descriptor.
        /// </summary>
        private sealed class CatalogArtifactAdapter : IArtifactAdapter {
            private readonly ArtifactDescriptor _descriptor;

            /// <summary>
            /// Initializes a new instance of the <see cref="CatalogArtifactAdapter"/> class.
            /// </summary>
            /// <param name="descriptor">Descriptor that supplies adapter capabilities.</param>
            public CatalogArtifactAdapter(ArtifactDescriptor descriptor) {
                this._descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            }

            /// <inheritdoc />
            public ExportKind Kind => this._descriptor.Kind;

            /// <inheritdoc />
            public bool CanScan => this._descriptor.CanScan;

            /// <inheritdoc />
            public bool CanExport => this._descriptor.CanExport;

            /// <inheritdoc />
            public bool CanImport => this._descriptor.CanImport;

            /// <inheritdoc />
            public ArtifactFormat Format => this._descriptor.Format;

            /// <inheritdoc />
            public string Extension => this._descriptor.Extension;

            /// <inheritdoc />
            public int ImportOrder => this._descriptor.ImportOrder;
        }
    }
}
