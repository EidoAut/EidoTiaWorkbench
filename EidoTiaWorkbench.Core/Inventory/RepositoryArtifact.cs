// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// RepositoryArtifact.cs
// Describes a repository XML file and the logical TIA artifact it can import.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Inventory {
    /// <summary>
    /// Describes a repository XML file and the logical TIA artifact it can import.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class RepositoryArtifact {
        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryArtifact"/> class.
        /// </summary>
        /// <param name="item">Logical TIA artifact represented by the repository file.</param>
        /// <param name="repositoryPath">Absolute path to the XML file in the repository.</param>
        /// <param name="relativePath">Repository-relative path used as the manifest key.</param>
        /// <param name="state">Comparison state calculated for the artifact.</param>
        /// <param name="sha256">
        /// Current SHA-256 hash of the XML file, or <see langword="null"/> if hashing failed.
        /// </param>
        /// <param name="lastExportUtc">UTC timestamp recorded in the manifest for the last export.</param>
        /// <param name="warning">Optional warning explaining unsupported or invalid repository content.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="item"/>, <paramref name="repositoryPath"/>, or
        /// <paramref name="relativePath"/> is <see langword="null"/>.
        /// </exception>
        public RepositoryArtifact(
            ExportItem item,
            string repositoryPath,
            string relativePath,
            RepositoryArtifactState state,
            string sha256,
            string lastExportUtc,
            string warning) {
            this.Item = item ?? throw new ArgumentNullException(nameof(item));
            this.RepositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
            this.RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            this.State = state;
            this.Sha256 = sha256;
            this.LastExportUtc = lastExportUtc;
            this.Warning = warning;
        }

        /// <summary>
        /// Gets the logical TIA artifact represented by the repository file.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public ExportItem Item {
            get; private set;
        }

        /// <summary>
        /// Gets the absolute path to the XML file in the repository.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string RepositoryPath {
            get; private set;
        }

        /// <summary>
        /// Gets the repository-relative path used as the manifest key.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string RelativePath {
            get; private set;
        }

        /// <summary>
        /// Gets the comparison state calculated for the artifact.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public RepositoryArtifactState State {
            get; private set;
        }

        /// <summary>
        /// Gets the current SHA-256 hash of the XML file, or <see langword="null"/> if hashing failed.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Sha256 {
            get; private set;
        }

        /// <summary>
        /// Gets the UTC timestamp recorded in the manifest for the last export.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string LastExportUtc {
            get; private set;
        }

        /// <summary>
        /// Gets an optional warning explaining unsupported or invalid repository content.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Warning {
            get; private set;
        }
    }
}
