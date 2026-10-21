// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportItem.cs
// Represents one exportable artifact discovered in a TIA project. This is a lightweight model object used
// by the UI and services to avoid holding onto live Openness objects across operations.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Represents one exportable artifact discovered in a TIA project.
    /// This is a lightweight model object used by the UI and services to avoid
    /// holding onto live Openness objects across operations.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class ExportItem {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportItem"/> class.
        /// </summary>
        /// <param name="cpuPath">CPU path, typically "&lt;DeviceName&gt;/&lt;CpuName&gt;".</param>
        /// <param name="groupPath">Group path inside PLC software, or an empty value for the root group.</param>
        /// <param name="name">TIA object name shown in the engineering project.</param>
        /// <param name="kind">Artifact kind used to select export and import handling.</param>
        /// <param name="logicalKey">Stable unique key used for selection persistence and later object resolution.</param>
        /// <param name="targetType">Optional human-readable target type discovered from TIA Portal.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="cpuPath"/>, <paramref name="name"/>, or
        /// <paramref name="logicalKey"/> is <see langword="null"/>.
        /// </exception>
        public ExportItem(string cpuPath, string groupPath, string name, ExportKind kind, string logicalKey, string targetType = null)
            : this(
                cpuPath,
                groupPath,
                name,
                kind,
                logicalKey,
                targetType,
                ArtifactCatalog.Get(kind).CanExport,
                ArtifactCatalog.Get(kind).CanImport,
                null,
                null,
                null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportItem"/> class with explicit capabilities and metadata.
        /// </summary>
        /// <param name="cpuPath">Device, CPU, or target path used to resolve the owning object.</param>
        /// <param name="groupPath">Group or folder path inside the owning target.</param>
        /// <param name="name">TIA object name shown in the engineering project.</param>
        /// <param name="kind">Artifact kind used to select catalog behavior.</param>
        /// <param name="logicalKey">Stable unique key used for repository and selection identity.</param>
        /// <param name="targetType">Optional human-readable target type discovered from TIA Portal.</param>
        /// <param name="canExport">Whether export is available for this concrete item.</param>
        /// <param name="canImport">Whether import is available for this concrete item.</param>
        /// <param name="unavailableReason">Reason export/import is unavailable for this item, if any.</param>
        /// <param name="dependencies">Known logical dependencies for this item.</param>
        /// <param name="metadata">Serializable metadata used for future resolver improvements.</param>
        public ExportItem(
            string cpuPath,
            string groupPath,
            string name,
            ExportKind kind,
            string logicalKey,
            string targetType,
            bool canExport,
            bool canImport,
            string unavailableReason,
            IEnumerable<string> dependencies,
            IDictionary<string, string> metadata) {
            this.CpuPath = cpuPath ?? throw new ArgumentNullException(nameof(cpuPath));
            this.GroupPath = groupPath ?? string.Empty;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Kind = kind;
            this.LogicalKey = logicalKey ?? throw new ArgumentNullException(nameof(logicalKey));
            this.TargetType = targetType ?? string.Empty;
            var descriptor = ArtifactCatalog.Get(kind);
            this.Domain = descriptor.Domain;
            this.Category = descriptor.Category;
            this.CanExport = canExport;
            this.CanImport = canImport;
            this.UnavailableReason = unavailableReason ?? string.Empty;
            this.Dependencies = (dependencies ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            this.Metadata = new Dictionary<string, string>(metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the human-friendly path identifying the CPU, e.g. "Device1/CPU_1516".
        /// This is used to resolve the target PlcSoftware by device + CPU name.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string CpuPath {
            get; private set;
        }

        /// <summary>
        /// Gets the logical group path inside PLC software, e.g. "Blocks/Drives/Motion".
        /// Used to build deterministic repo paths and to locate the destination group on import.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string GroupPath {
            get; private set;
        }

        /// <summary>
        /// Gets the object name as shown in TIA, e.g. "FB10", "OB1", "UDT3", or "Signals".
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Name {
            get; private set;
        }

        /// <summary>
        /// Gets the kind of artifact, such as FB, FC, OB, UDT, DB, or tag table.
        /// Determines export/import handling and output naming.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ExportKind Kind {
            get; private set;
        }

        /// <summary>
        /// Gets the stable logical key used to re-resolve the object later.
        /// This should remain stable across sessions and scans, unlike in-memory object references.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string LogicalKey {
            get; private set;
        }

        /// <summary>
        /// Gets the optional target type discovered from the engineering project.
        /// For HMI screens this normally contains the device or runtime family.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string TargetType {
            get; private set;
        }

        /// <summary>
        /// Gets the artifact domain from the central capability catalog.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ArtifactDomain Domain {
            get; private set;
        }

        /// <summary>
        /// Gets the repository category from the central capability catalog.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Category {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether this concrete item can be exported.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool CanExport {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether this concrete item can be imported.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public bool CanImport {
            get; private set;
        }

        /// <summary>
        /// Gets the reason an operation is unavailable for this item.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string UnavailableReason {
            get; private set;
        }

        /// <summary>
        /// Gets known logical dependencies for this item.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public IReadOnlyList<string> Dependencies {
            get; private set;
        }

        /// <summary>
        /// Gets serializable metadata used by resolvers and repository inventory.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public IDictionary<string, string> Metadata {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether the artifact belongs to a classic HMI target.
        /// </summary>
        public bool IsHmi => this.Domain == ArtifactDomain.HmiClassic || this.Domain == ArtifactDomain.HmiUnified;

        /// <summary>
        /// Gets a value indicating whether the artifact belongs to PLC software.
        /// </summary>
        public bool IsPlc => this.Domain == ArtifactDomain.Plc;

        /// <summary>
        /// Returns a compact human-readable representation for logs and UI.
        /// </summary>
        /// <returns>A string containing CPU path, group path, artifact kind, and object name.</returns>
        public override string ToString() => $"{this.CpuPath} | {this.GroupPath} | {this.Kind} | {this.Name}";
    }
}
