// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using EidoTiaWorkbench.Core.Inventory;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Describes item-level progress emitted by the import engine.
    /// </summary>
    public sealed class ImportProgressInfo {
        /// <summary>
        /// Gets or sets the number of artifacts completed so far.
        /// </summary>
        public int Done {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of artifacts planned for the import.
        /// </summary>
        public int Total {
            get; set;
        }

        /// <summary>
        /// Gets or sets the repository artifact currently being processed.
        /// </summary>
        public RepositoryArtifact Artifact {
            get; set;
        }

        /// <summary>
        /// Gets or sets the human-readable import phase.
        /// </summary>
        public string Phase {
            get; set;
        }

        /// <summary>
        /// Gets or sets the import strategy selected for the artifact.
        /// </summary>
        public string Strategy {
            get; set;
        }

        /// <summary>
        /// Gets or sets the elapsed time for the current artifact when known.
        /// </summary>
        public long ElapsedMilliseconds {
            get; set;
        }
    }
}
