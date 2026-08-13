// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Represents an explicit health check of the TIA Portal session and project reference.
    /// </summary>
    public sealed class TiaSessionHealth {
        /// <summary>
        /// Gets or sets the UTC time when the health check was performed.
        /// </summary>
        public DateTime CheckedUtc {
            get; set;
        }

        /// <summary>
        /// Gets or sets the health status observed on the Openness STA thread.
        /// </summary>
        public TiaSessionHealthStatus Status {
            get; set;
        }

        /// <summary>
        /// Gets or sets a short explanation of the health state.
        /// </summary>
        public string Message {
            get; set;
        }

        /// <summary>
        /// Gets or sets the project name read during validation when available.
        /// </summary>
        public string ProjectName {
            get; set;
        }

        /// <summary>
        /// Gets or sets the file-system path read during validation when available.
        /// </summary>
        public string ProjectPath {
            get; set;
        }

        /// <summary>
        /// Gets or sets the exception captured while validating the session.
        /// </summary>
        public Exception Exception {
            get; set;
        }

        /// <summary>
        /// Gets a value indicating whether Openness objects may be safely resolved.
        /// </summary>
        public bool IsHealthy {
            get {
                return this.Status == TiaSessionHealthStatus.Healthy;
            }
        }

        /// <summary>
        /// Creates a healthy session state.
        /// </summary>
        /// <param name="projectName">Project name observed during validation.</param>
        /// <returns>A healthy session health object.</returns>
        public static TiaSessionHealth Healthy(string projectName) {
            return Healthy(projectName, null);
        }

        /// <summary>
        /// Creates a healthy session state.
        /// </summary>
        /// <param name="projectName">Project name observed during validation.</param>
        /// <param name="projectPath">Project path observed during validation, when available.</param>
        /// <returns>A healthy session health object.</returns>
        public static TiaSessionHealth Healthy(string projectName, string projectPath) {
            return new TiaSessionHealth {
                CheckedUtc = DateTime.UtcNow,
                Status = TiaSessionHealthStatus.Healthy,
                ProjectName = projectName,
                ProjectPath = projectPath,
                Message = "TIA Portal project reference is valid.",
            };
        }

        /// <summary>
        /// Creates an unhealthy session state with optional exception context.
        /// </summary>
        /// <param name="status">Unhealthy status.</param>
        /// <param name="message">Short diagnostic message.</param>
        /// <param name="exception">Optional validation exception.</param>
        /// <returns>An unhealthy session health object.</returns>
        public static TiaSessionHealth Unhealthy(TiaSessionHealthStatus status, string message, Exception exception) {
            return new TiaSessionHealth {
                CheckedUtc = DateTime.UtcNow,
                Status = status,
                Message = message,
                Exception = exception,
            };
        }

        /// <summary>
        /// Formats the health state for reports and diagnostic logs.
        /// </summary>
        /// <returns>A single diagnostic string containing status, message, and exception details.</returns>
        public string ToDiagnosticString() {
            var text = this.Status + ": " + (this.Message ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(this.ProjectName)) {
                text += " Project=" + this.ProjectName + ".";
            }

            if (!string.IsNullOrWhiteSpace(this.ProjectPath)) {
                text += " Path=" + this.ProjectPath + ".";
            }

            if (this.Exception != null) {
                text += " Exception=" + this.Exception.GetType().FullName + ": " + this.Exception.Message;
                text += " HResult=0x" + this.Exception.HResult.ToString("X8") + ".";
            }

            return text;
        }
    }
}
