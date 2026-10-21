// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactPayload.cs
// Describes the files that belong to a single repository artifact.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Linq;

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Describes the files that belong to a single repository artifact.
    /// </summary>
    public sealed class ArtifactPayload {
        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactPayload"/> class.
        /// </summary>
        /// <param name="primaryPath">Primary payload path used for repository identity.</param>
        /// <param name="files">All files that belong to the artifact.</param>
        /// <param name="format">Repository payload format.</param>
        /// <param name="contentHash">Aggregate content hash.</param>
        public ArtifactPayload(string primaryPath, IEnumerable<string> files, ArtifactFormat format, string contentHash) {
            this.PrimaryPath = primaryPath ?? throw new ArgumentNullException(nameof(primaryPath));
            this.Files = (files ?? new[] { primaryPath }).ToList().AsReadOnly();
            this.Format = format;
            this.ContentHash = contentHash ?? string.Empty;
        }

        /// <summary>
        /// Gets the primary path used by old repository layouts and UI previews.
        /// </summary>
        public string PrimaryPath {
            get;
        }

        /// <summary>
        /// Gets all files that belong to the artifact payload.
        /// </summary>
        public IReadOnlyList<string> Files {
            get;
        }

        /// <summary>
        /// Gets the repository payload format.
        /// </summary>
        public ArtifactFormat Format {
            get;
        }

        /// <summary>
        /// Gets the aggregate content hash for the payload.
        /// </summary>
        public string ContentHash {
            get;
        }
    }
}
