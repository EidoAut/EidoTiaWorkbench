// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections.Generic;

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Central descriptor for one artifact type supported or explicitly classified by the tool.
    /// </summary>
    public sealed class ArtifactDescriptor {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactDescriptor"/> class.
        /// </summary>
        /// <param name="kind">Stable artifact kind.</param>
        /// <param name="domain">Domain that owns the artifact.</param>
        /// <param name="displayName">Human-readable name shown in the UI.</param>
        /// <param name="category">Repository category folder.</param>
        /// <param name="extension">Repository file extension including the leading dot.</param>
        /// <param name="format">Repository payload format.</param>
        /// <param name="canScan">Whether project inventory can discover this artifact type.</param>
        /// <param name="canExport">Whether export is supported by the installed public API.</param>
        /// <param name="canImport">Whether import is supported by the installed public API.</param>
        /// <param name="canCreateGroups">Whether missing destination folders or groups can be created.</param>
        /// <param name="importOrder">Import ordering bucket; lower values import first.</param>
        /// <param name="apiUsed">Public Openness API member used for the operation.</param>
        /// <param name="limitations">Known limitations confirmed from the installed API.</param>
        public ArtifactDescriptor(
            ExportKind kind,
            ArtifactDomain domain,
            string displayName,
            string category,
            string extension,
            ArtifactFormat format,
            bool canScan,
            bool canExport,
            bool canImport,
            bool canCreateGroups,
            int importOrder,
            string apiUsed,
            string limitations) {
            this.Kind = kind;
            this.Domain = domain;
            this.DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            this.Category = category ?? throw new ArgumentNullException(nameof(category));
            this.Extension = extension ?? ".xml";
            this.Format = format;
            this.CanScan = canScan;
            this.CanExport = canExport;
            this.CanImport = canImport;
            this.CanCreateGroups = canCreateGroups;
            this.ImportOrder = importOrder;
            this.ApiUsed = apiUsed ?? string.Empty;
            this.Limitations = limitations ?? string.Empty;
            this.Dependencies = new List<ExportKind>();
        }

        /// <summary>
        /// Gets the stable artifact kind.
        /// </summary>
        public ExportKind Kind {
            get;
        }

        /// <summary>
        /// Gets the domain that owns the artifact.
        /// </summary>
        public ArtifactDomain Domain {
            get;
        }

        /// <summary>
        /// Gets the human-readable artifact name.
        /// </summary>
        public string DisplayName {
            get;
        }

        /// <summary>
        /// Gets the repository category folder.
        /// </summary>
        public string Category {
            get;
        }

        /// <summary>
        /// Gets the repository file extension.
        /// </summary>
        public string Extension {
            get;
        }

        /// <summary>
        /// Gets the repository payload format.
        /// </summary>
        public ArtifactFormat Format {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this artifact can be scanned from a project.
        /// </summary>
        public bool CanScan {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this artifact can be exported.
        /// </summary>
        public bool CanExport {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this artifact can be imported.
        /// </summary>
        public bool CanImport {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether missing destination groups can be created.
        /// </summary>
        public bool CanCreateGroups {
            get;
        }

        /// <summary>
        /// Gets the import ordering bucket.
        /// </summary>
        public int ImportOrder {
            get;
        }

        /// <summary>
        /// Gets known artifact dependencies used for planning imports.
        /// </summary>
        public List<ExportKind> Dependencies {
            get;
        }

        /// <summary>
        /// Gets the public Openness API member used by this descriptor.
        /// </summary>
        public string ApiUsed {
            get;
        }

        /// <summary>
        /// Gets limitations confirmed from the installed public Openness API.
        /// </summary>
        public string Limitations {
            get;
        }
    }
}
