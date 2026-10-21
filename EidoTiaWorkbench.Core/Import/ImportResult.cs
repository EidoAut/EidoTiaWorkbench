// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportResult.cs
// Summarizes an import run for presentation and follow-up actions.
//
// Developed by EIDO Automation
// =========================================================================
using System.Collections.Generic;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Summarizes an import run for presentation and follow-up actions.
    /// </summary>
    [System.Runtime.Serialization.DataContract]
    public sealed class ImportResult {
        /// <summary>
        /// Gets or sets the total number of items successfully imported.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Imported {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items intentionally skipped by policy or dry-run preview.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Skipped {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items skipped because cancellation was requested.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public int Cancelled {
            get; set;
        }

        /// <summary>
        /// Gets or sets the folder where pre-replacement backups were written.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public string BackupRoot {
            get; set;
        }

        /// <summary>
        /// Gets the non-fatal issues encountered during import.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<string> Warnings { get; private set; } = new List<string>();

        /// <summary>
        /// Gets the fatal errors that prevented one or more items from being imported.
        /// </summary>
        [System.Runtime.Serialization.DataMember]
        public List<ImportError> Errors { get; private set; } = new List<ImportError>();

        /// <summary>
        /// Gets a value indicating whether at least one import error occurred.
        /// </summary>
        public bool HasErrors => this.Errors.Count > 0;
    }
}
