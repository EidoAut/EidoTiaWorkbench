// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Core.Model {
    /// <summary>
    /// Declares the pipeline responsibilities owned by one artifact kind.
    /// </summary>
    /// <remarks>
    /// The concrete Openness calls still run inside the STA-bound export and import services. This
    /// interface centralizes support claims so the catalog, UI, repository scanner, and services agree.
    /// </remarks>
    public interface IArtifactAdapter {
        /// <summary>
        /// Gets the artifact kind handled by the adapter.
        /// </summary>
        ExportKind Kind {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this adapter can scan project inventory for the kind.
        /// </summary>
        bool CanScan {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this adapter can export the kind.
        /// </summary>
        bool CanExport {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether this adapter can import the kind.
        /// </summary>
        bool CanImport {
            get;
        }

        /// <summary>
        /// Gets the payload format handled by the adapter.
        /// </summary>
        ArtifactFormat Format {
            get;
        }

        /// <summary>
        /// Gets the primary file extension written to the repository.
        /// </summary>
        string Extension {
            get;
        }

        /// <summary>
        /// Gets the import ordering bucket used by export and import workflows.
        /// </summary>
        int ImportOrder {
            get;
        }
    }
}
