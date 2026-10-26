// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportFailureClassifier.cs
// Centralizes export exception classification for TIA Portal Openness.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Centralizes export exception classification for TIA Portal Openness.
    /// </summary>
    /// <remarks>
    /// Siemens Openness exceptions do not always expose a stable domain-specific exception type.
    /// This classifier therefore prefers session health and HRESULT data, and uses documented
    /// text heuristics only as a final layer for known protection messages.
    /// </remarks>
    public static class ExportFailureClassifier {
        /// <summary>
        /// Classifies an exception raised while exporting one artifact.
        /// </summary>
        /// <param name="exception">Exception raised by the export attempt.</param>
        /// <param name="healthAfterFailure">Session health observed immediately after the exception.</param>
        /// <returns>A classification used to produce artifact result, manifest, and report entries.</returns>
        public static ExportFailureClassification Classify(Exception exception, TiaSessionHealth healthAfterFailure) {
            return Classify(exception, healthAfterFailure, null);
        }

        /// <summary>
        /// Classifies an exception raised while exporting one artifact.
        /// </summary>
        /// <param name="exception">Exception raised by the export attempt.</param>
        /// <param name="healthAfterFailure">Session health observed or recovered after the exception.</param>
        /// <param name="item">Artifact being exported, when available.</param>
        /// <returns>A classification used to produce artifact result, manifest, and report entries.</returns>
        public static ExportFailureClassification Classify(
            Exception exception,
            TiaSessionHealth healthAfterFailure,
            ExportItem item) {
            if (exception != null && IsRestrictedExport(exception)) {
                return new ExportFailureClassification {
                    Status = ArtifactExportStatus.Restricted,
                    Message = "TIA Portal rejected the export because the artifact is protected or restricted.",
                    TechnicalDetail = BuildExceptionDetail(exception),
                    IsTransient = false,
                    IsRetryable = false,
                };
            }

            if (exception is ExportWarningException) {
                return new ExportFailureClassification {
                    Status = ArtifactExportStatus.Unavailable,
                    Message = exception.Message,
                    TechnicalDetail = BuildExceptionDetail(exception),
                    IsTransient = false,
                    IsRetryable = false,
                };
            }

            if (exception is MissingMethodException) {
                return new ExportFailureClassification {
                    Status = ArtifactExportStatus.Unsupported,
                    Message = "No compatible public Openness export method was found for this artifact.",
                    TechnicalDetail = BuildExceptionDetail(exception),
                    IsTransient = false,
                    IsRetryable = false,
                };
            }

            if (healthAfterFailure != null && !healthAfterFailure.IsHealthy) {
                return new ExportFailureClassification {
                    Status = ArtifactExportStatus.AbortedSessionLost,
                    Message = "The TIA Portal session or project was lost while exporting this artifact.",
                    TechnicalDetail = BuildSessionFailureDetail(exception, healthAfterFailure),
                    IsTransient = true,
                    IsRetryable = true,
                };
            }

            if (item != null && item.Kind == ExportKind.HmiGraphicList) {
                return new ExportFailureClassification {
                    Status = ArtifactExportStatus.Failed,
                    Message = "HMI graphic list export failed in the GraphicList exporter while the TIA session remained valid.",
                    TechnicalDetail = exception == null ? string.Empty : BuildExceptionDetail(exception),
                    IsTransient = false,
                    IsRetryable = true,
                };
            }

            return new ExportFailureClassification {
                Status = ArtifactExportStatus.Failed,
                Message = exception == null ? "Export failed." : exception.Message,
                TechnicalDetail = exception == null ? string.Empty : BuildExceptionDetail(exception),
                IsTransient = false,
                IsRetryable = true,
            };
        }

        /// <summary>
        /// Builds a compact hexadecimal HRESULT string when available.
        /// </summary>
        /// <param name="exception">Exception to inspect.</param>
        /// <returns>HRESULT as 0xXXXXXXXX, or an empty value when no exception is present.</returns>
        public static string GetHResult(Exception exception) {
            if (exception == null) {
                return string.Empty;
            }

            return "0x" + exception.HResult.ToString("X8");
        }

        /// <summary>
        /// Flattens nested exception messages into one searchable diagnostic value.
        /// </summary>
        /// <param name="exception">Root exception to inspect.</param>
        /// <returns>Concatenated exception messages.</returns>
        public static string FlattenExceptionText(Exception exception) {
            var parts = new List<string>();
            var current = exception;
            while (current != null) {
                if (!string.IsNullOrWhiteSpace(current.Message)) {
                    parts.Add(current.GetType().FullName + ": " + current.Message);
                }

                current = current.InnerException;
            }

            return string.Join(" | ", parts.ToArray());
        }

        /// <summary>
        /// Determines whether an exception represents a confirmed protection or permission restriction.
        /// </summary>
        /// <param name="exception">Exception raised by Openness.</param>
        /// <returns><see langword="true"/> when known protection text is present.</returns>
        private static bool IsRestrictedExport(Exception exception) {
            var text = FlattenExceptionText(exception);
            return text.IndexOf("not permitted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("not allowed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Combines the original exception and the independent session health check.
        /// </summary>
        /// <param name="exception">Original export exception.</param>
        /// <param name="health">Session health observed after the exception.</param>
        /// <returns>Detailed text for diagnostics and manual TIA validation.</returns>
        private static string BuildSessionFailureDetail(Exception exception, TiaSessionHealth health) {
            var original = exception == null ? "No original exception was captured." : BuildExceptionDetail(exception);
            var session = health == null ? "Session health was not checked." : health.ToDiagnosticString();
            return original + Environment.NewLine + "Session health after failure: " + session;
        }

        /// <summary>
        /// Builds detailed exception text including reflection-attached Openness diagnostics.
        /// </summary>
        /// <param name="exception">Exception to format.</param>
        /// <returns>Detailed exception text.</returns>
        private static string BuildExceptionDetail(Exception exception) {
            var text = exception.ToString();
            if (exception.Data != null && exception.Data.Count > 0) {
                text += Environment.NewLine + "Diagnostic data:";
                foreach (var key in exception.Data.Keys) {
                    text += Environment.NewLine + "  " + key + "=" + exception.Data[key];
                }
            }

            return text;
        }
    }
}
