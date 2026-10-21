// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportOrderPolicy.cs
// Defines export queue ordering independently from import dependency ordering.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Defines export queue ordering independently from import dependency ordering.
    /// </summary>
    /// <remarks>
    /// Import order is dependency-first. Export order is preservation-first: payloads that
    /// are known to round-trip or fail locally are written before historically volatile
    /// artifacts that can invalidate the Openness session.
    /// </remarks>
    internal static class ExportOrderPolicy {
        private const int SafePhase = 0;
        private const int FinalIsolatedPhase = 1000;
        private const int PreviousSessionFailurePhase = 2000;

        /// <summary>
        /// Orders artifacts for export without using import dependency order.
        /// </summary>
        /// <param name="items">Artifacts selected for export.</param>
        /// <param name="previousSessionFailureKey">Logical key that lost the previous session, if any.</param>
        /// <returns>A deterministic export queue.</returns>
        public static IReadOnlyList<ExportItem> OrderForExport(
            IEnumerable<ExportItem> items,
            string previousSessionFailureKey) {
            if (items == null) {
                throw new ArgumentNullException(nameof(items));
            }

            return items
                .OrderBy(i => GetPhase(i, previousSessionFailureKey))
                .ThenBy(i => GetOrder(i.Kind))
                .ThenBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns the export order bucket for an artifact kind.
        /// </summary>
        /// <param name="kind">Artifact kind being ordered.</param>
        /// <returns>Lower values export earlier within the same phase.</returns>
        public static int GetOrder(ExportKind kind) {
            switch (kind) {
                case ExportKind.Udt:
                    return 0;
                case ExportKind.PlcTagTable:
                    return 10;
                case ExportKind.PlcUserConstant:
                    return 11;
                case ExportKind.Ob:
                    return 20;
                case ExportKind.Fb:
                    return 21;
                case ExportKind.Fc:
                    return 22;
                case ExportKind.Db:
                    return 23;
                case ExportKind.TechnologicalObject:
                    return 70;
                case ExportKind.HmiTagTable:
                    return 100;
                case ExportKind.HmiConnection:
                    return 101;
                case ExportKind.HmiTextList:
                    return 102;
                case ExportKind.HmiCycle:
                    return 104;
                case ExportKind.HmiVBScript:
                    return 105;
                case ExportKind.HmiTemplate:
                    return 110;
                case ExportKind.HmiPopup:
                    return 111;
                case ExportKind.HmiSlideIn:
                    return 112;
                case ExportKind.HmiGlobalElements:
                    return 113;
                case ExportKind.HmiScreenOverview:
                    return 114;
                case ExportKind.HmiScreen:
                    return 120;
                case ExportKind.PlcWatchTable:
                    return 160;
                case ExportKind.PlcForceTable:
                    return 161;
                case ExportKind.PlcExternalSource:
                    return 180;
                case ExportKind.PlcAlarmTextList:
                    return 181;
                case ExportKind.HmiScreenFolder:
                    return 190;
                case ExportKind.UnifiedTagTable:
                    return 200;
                case ExportKind.UnifiedScriptModule:
                    return 210;
                case ExportKind.UnifiedTextList:
                    return 220;
                case ExportKind.UnifiedGraphicList:
                    return 221;
                case ExportKind.HardwareCax:
                    return 300;
                case ExportKind.HmiGraphicList:
                    return 900;
                case ExportKind.Unsupported:
                default:
                    return 999;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the artifact belongs to the final isolated export phase.
        /// </summary>
        /// <param name="kind">Artifact kind to inspect.</param>
        /// <returns><see langword="true"/> when the artifact is intentionally attempted after safe payloads.</returns>
        public static bool IsFinalIsolated(ExportKind kind) {
            return kind == ExportKind.HmiGraphicList;
        }

        /// <summary>
        /// Determines whether all session-loss records belong to the final isolated phase.
        /// </summary>
        /// <param name="records">Artifact records from one export result.</param>
        /// <returns><see langword="true"/> when final isolated artifacts are the only session-loss records.</returns>
        public static bool HasOnlyFinalIsolatedSessionLoss(IEnumerable<ArtifactExportRecord> records) {
            if (records == null) {
                return false;
            }

            var sessionLosses = records
                .Where(r => r != null && r.Status == ArtifactExportStatus.AbortedSessionLost)
                .ToList();
            return sessionLosses.Count > 0 && sessionLosses.All(r => IsFinalIsolated(r.Kind));
        }

        private static int GetPhase(ExportItem item, string previousSessionFailureKey) {
            if (IsPreviousSessionFailure(item, previousSessionFailureKey)) {
                return PreviousSessionFailurePhase;
            }

            return IsFinalIsolated(item.Kind) ? FinalIsolatedPhase : SafePhase;
        }

        private static bool IsPreviousSessionFailure(ExportItem item, string previousSessionFailureKey) {
            return item != null &&
                   !string.IsNullOrWhiteSpace(previousSessionFailureKey) &&
                   string.Equals(item.LogicalKey, previousSessionFailureKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
