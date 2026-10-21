// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportProgressInfo.cs
// Describes item-level progress emitted by the import engine.
//
// Developed by EIDO Automation
// =========================================================================
using EidoTiaWorkbench.Core.Inventory;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Describes item-level progress emitted by the import engine.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class ImportProgressInfo {
        /// <summary>
        /// Gets or sets the number of artifacts completed so far.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Done {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of artifacts planned for the import.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Total {
            get; set;
        }

        /// <summary>
        /// Gets or sets the repository artifact currently being processed.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public RepositoryArtifact Artifact {
            get; set;
        }

        /// <summary>
        /// Gets or sets the human-readable import phase.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Phase {
            get; set;
        }

        /// <summary>
        /// Gets or sets the import strategy selected for the artifact.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string Strategy {
            get; set;
        }

        /// <summary>
        /// Gets or sets the elapsed time for the current artifact when known.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public long ElapsedMilliseconds {
            get; set;
        }
    }
}
