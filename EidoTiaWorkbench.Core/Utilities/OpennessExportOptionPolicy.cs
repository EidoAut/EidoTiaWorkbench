// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using Siemens.Engineering;

using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Utilities {
    /// <summary>
    /// Selects Siemens Openness export options that are safe to import back into TIA Portal.
    /// </summary>
    internal static class OpennessExportOptionPolicy {
        /// <summary>
        /// Returns the export option to use for a repository artifact kind.
        /// </summary>
        /// <param name="kind">Artifact kind being exported.</param>
        /// <returns>The Openness export option for round-trippable repository XML.</returns>
        public static ExportOptions ForArtifact(ExportKind kind) {
            if (kind == ExportKind.HmiScreen) {
                return ExportOptions.None;
            }

            return ExportOptions.WithDefaults;
        }
    }
}
