// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactExportPreflight.cs
// Describes the artifact-level export capability decision made before Openness is invoked.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Describes the artifact-level export capability decision made before Openness is invoked.
    /// </summary>
    /// <remarks>
    /// This object is intentionally independent from Siemens runtime objects. It captures whether the
    /// scanned item, central catalog, and registered adapter all agree that an export attempt is valid.
    /// </remarks>
    public sealed class ArtifactExportPreflight {
        /// <summary>
        /// Gets or sets a value indicating whether the exporter may call Openness for this artifact.
        /// </summary>
        public bool CanAttemptExport {
            get; set;
        }

        /// <summary>
        /// Gets or sets the artifact status to record when the export cannot be attempted.
        /// </summary>
        public ArtifactExportStatus BlockedStatus {
            get; set;
        }

        /// <summary>
        /// Gets or sets a short message explaining the capability decision.
        /// </summary>
        public string Message {
            get; set;
        }

        /// <summary>
        /// Gets or sets the precise reason for an unavailable or unsupported artifact.
        /// </summary>
        public string Reason {
            get; set;
        }

        /// <summary>
        /// Gets or sets the condition that prevented export, such as item.CanExport.
        /// </summary>
        public string Condition {
            get; set;
        }

        /// <summary>
        /// Gets or sets the handler selected for this artifact kind.
        /// </summary>
        public string ExporterName {
            get; set;
        }

        /// <summary>
        /// Gets or sets the public Openness API advertised for the handler.
        /// </summary>
        public string ApiUsed {
            get; set;
        }

        /// <summary>
        /// Gets or sets detailed capability flags for manifest and operation report diagnostics.
        /// </summary>
        public string CapabilityDetail {
            get; set;
        }

        /// <summary>
        /// Evaluates whether one scanned artifact can be exported before resolving any live Openness object.
        /// </summary>
        /// <param name="item">Scanned artifact identity and item-level capabilities.</param>
        /// <param name="descriptor">Catalog descriptor for the artifact kind.</param>
        /// <param name="adapter">Registered pipeline adapter for the artifact kind.</param>
        /// <returns>A preflight result used by export, manifest, and report generation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="item"/>, <paramref name="descriptor"/>, or <paramref name="adapter"/>
        /// is <see langword="null"/>.
        /// </exception>
        public static ArtifactExportPreflight Evaluate(ExportItem item, ArtifactDescriptor descriptor, IArtifactAdapter adapter) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            if (descriptor == null) {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (adapter == null) {
                throw new ArgumentNullException(nameof(adapter));
            }

            var preflight = new ArtifactExportPreflight {
                CanAttemptExport = true,
                BlockedStatus = ArtifactExportStatus.Succeeded,
                ExporterName = GetExporterName(item.Kind),
                ApiUsed = descriptor.ApiUsed,
                CapabilityDetail = BuildCapabilityDetail(item, descriptor, adapter),
            };

            if (item.Kind == ExportKind.Unsupported || descriptor.Format == ArtifactFormat.Unsupported) {
                preflight.CanAttemptExport = false;
                preflight.BlockedStatus = ArtifactExportStatus.Unsupported;
                preflight.Reason = BuildReason(item, descriptor);
                preflight.Condition = item.Kind == ExportKind.Unsupported
                    ? "item.Kind"
                    : "descriptor.Format";
                preflight.Message = descriptor.DisplayName + " is unsupported by this export pipeline: " + preflight.Reason;
                return preflight;
            }

            if (!item.CanExport || !descriptor.CanExport || !adapter.CanExport) {
                preflight.CanAttemptExport = false;
                preflight.BlockedStatus = ArtifactExportStatus.Unavailable;
                preflight.Reason = BuildReason(item, descriptor);
                preflight.Condition = BuildUnavailableCondition(item, descriptor, adapter);
                preflight.Message = descriptor.DisplayName + " is not exportable: " + preflight.Reason;
                return preflight;
            }

            preflight.Message = descriptor.DisplayName + " export capability confirmed.";
            return preflight;
        }

        /// <summary>
        /// Returns the concrete export handler name selected for an artifact kind.
        /// </summary>
        /// <param name="kind">Artifact kind.</param>
        /// <returns>Stable handler name for diagnostics.</returns>
        public static string GetExporterName(ExportKind kind) {
            switch (kind) {
                case ExportKind.HmiGraphicList:
                    return "HmiGraphicListExporter";
                case ExportKind.HmiTextList:
                    return "HmiTextListExporter";
                case ExportKind.HmiConnection:
                    return "HmiConnectionExporter";
                case ExportKind.HmiCycle:
                    return "HmiCycleExporter";
                case ExportKind.HmiScreen:
                    return "HmiScreenExporter";
                case ExportKind.HmiTemplate:
                case ExportKind.HmiPopup:
                case ExportKind.HmiSlideIn:
                case ExportKind.HmiVBScript:
                    return "HmiFolderExporter";
                case ExportKind.HmiTagTable:
                    return "HmiTagTableExporter";
                case ExportKind.HmiGlobalElements:
                case ExportKind.HmiScreenOverview:
                    return "HmiSingletonExporter";
                case ExportKind.Udt:
                    return "PlcTypeExporter";
                case ExportKind.Ob:
                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Db:
                    return "PlcBlockExporter";
                case ExportKind.PlcTagTable:
                    return "PlcTagTableExporter";
                case ExportKind.PlcUserConstant:
                    return "PlcUserConstantExporter";
                case ExportKind.PlcWatchTable:
                case ExportKind.PlcForceTable:
                case ExportKind.TechnologicalObject:
                    return "PlcGenericArtifactExporter";
                default:
                    return "UnsupportedExporter";
            }
        }

        /// <summary>
        /// Builds a compact capability trace for reports.
        /// </summary>
        /// <param name="item">Scanned artifact identity and item-level capabilities.</param>
        /// <param name="descriptor">Catalog descriptor for the artifact kind.</param>
        /// <param name="adapter">Registered pipeline adapter for the artifact kind.</param>
        /// <returns>Single-line capability diagnostic text.</returns>
        private static string BuildCapabilityDetail(ExportItem item, ArtifactDescriptor descriptor, IArtifactAdapter adapter) {
            return "item.CanExport=" + item.CanExport +
                   "; descriptor.CanExport=" + descriptor.CanExport +
                   "; adapter.CanExport=" + adapter.CanExport +
                   "; handler=" + GetExporterName(item.Kind) +
                   "; api=" + (descriptor.ApiUsed ?? string.Empty);
        }

        /// <summary>
        /// Builds the human-readable reason for a blocked artifact.
        /// </summary>
        /// <param name="item">Scanned artifact identity.</param>
        /// <param name="descriptor">Catalog descriptor for the artifact kind.</param>
        /// <returns>Human-readable reason text.</returns>
        private static string BuildReason(ExportItem item, ArtifactDescriptor descriptor) {
            if (!string.IsNullOrWhiteSpace(item.UnavailableReason)) {
                return item.UnavailableReason;
            }

            if (!string.IsNullOrWhiteSpace(descriptor.Limitations)) {
                return descriptor.Limitations;
            }

            return "No public Openness export operation is enabled for this artifact in this pipeline.";
        }

        /// <summary>
        /// Identifies the capability flag that blocked an export attempt.
        /// </summary>
        /// <param name="item">Scanned artifact identity and item-level capabilities.</param>
        /// <param name="descriptor">Catalog descriptor for the artifact kind.</param>
        /// <param name="adapter">Registered pipeline adapter for the artifact kind.</param>
        /// <returns>Capability condition name, or an empty string when no condition failed.</returns>
        private static string BuildUnavailableCondition(ExportItem item, ArtifactDescriptor descriptor, IArtifactAdapter adapter) {
            if (!item.CanExport) {
                return "item.CanExport";
            }

            if (!descriptor.CanExport) {
                return "descriptor.CanExport";
            }

            if (!adapter.CanExport) {
                return "adapter.CanExport";
            }

            return string.Empty;
        }
    }
}
