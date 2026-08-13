// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Collections.Generic;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Summarizes an import run for presentation and follow-up actions.
    /// </summary>
    public sealed class ImportResult {
        /// <summary>
        /// Gets or sets the total number of items successfully imported.
        /// </summary>
        public int Imported {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items intentionally skipped by policy or dry-run preview.
        /// </summary>
        public int Skipped {
            get; set;
        }

        /// <summary>
        /// Gets or sets the total number of items skipped because cancellation was requested.
        /// </summary>
        public int Cancelled {
            get; set;
        }

        /// <summary>
        /// Gets or sets the folder where pre-replacement backups were written.
        /// </summary>
        public string BackupRoot {
            get; set;
        }

        /// <summary>
        /// Gets the non-fatal issues encountered during import.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>
        /// Gets the fatal errors that prevented one or more items from being imported.
        /// </summary>
        public List<ImportError> Errors { get; } = new List<ImportError>();

        /// <summary>
        /// Gets a value indicating whether at least one import error occurred.
        /// </summary>
        public bool HasErrors => this.Errors.Count > 0;
    }
}
