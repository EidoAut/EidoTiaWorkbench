// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// OperationSummaryForm.cs
// Shows operation counters, messages, and optional confirmation controls.
//
// Developed by EIDO Automation
// =========================================================================
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Forms {
    /// <summary>
    /// Shows operation counters, messages, and optional confirmation controls.
    /// </summary>
    public sealed class OperationSummaryForm : Form {
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationSummaryForm"/> class.
        /// </summary>
        /// <param name="summary">Operation result to display.</param>
        /// <param name="confirmMode">Whether the dialog should ask for explicit confirmation.</param>
        public OperationSummaryForm(OperationSummary summary, bool confirmMode) {
            summary = summary ?? new OperationSummary();
            AppTheme.ApplyTo(this);
            this.Text = summary.Operation + " summary";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.ClientSize = new Size(720, 560);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(20),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label {
                Text = summary.Operation,
                Dock = DockStyle.Top,
                Font = ThemeFonts.PageTitle,
                ForeColor = ThemeColors.TextPrimary,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4),
            };

            var subtitle = new Label {
                Text = BuildSubtitle(summary),
                Dock = DockStyle.Top,
                Font = ThemeFonts.Secondary,
                ForeColor = ThemeColors.TextSecondary,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 12),
            };

            var counts = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 12),
            };
            AddBadge(
                counts,
                string.IsNullOrWhiteSpace(summary.Status) ? "Completed" : summary.Status,
                GetStatusColor(summary));
            AddBadge(
                counts,
                (string.IsNullOrWhiteSpace(summary.SuccessLabel) ? "OK" : summary.SuccessLabel) + " " + summary.Ok,
                ThemeColors.Success);
            AddBadgeIfNonZero(counts, "Skipped", summary.Skipped, ThemeColors.Disabled);
            AddBadgeIfNonZero(counts, "Restricted", summary.Restricted, ThemeColors.Warning);
            AddBadgeIfNonZero(counts, "Unavailable", summary.Unavailable, ThemeColors.Disabled);
            AddBadgeIfNonZero(counts, "Unsupported", summary.Unsupported, ThemeColors.Disabled);
            AddBadgeIfNonZero(counts, "Aborted", summary.AbortedSessionLost, ThemeColors.Error);
            AddBadgeIfNonZero(counts, "Cancelled", summary.Cancelled, ThemeColors.Warning);
            AddBadgeIfNonZero(counts, "Warnings", summary.Warnings, ThemeColors.Warning);
            AddBadgeIfNonZero(counts, "Errors", summary.Errors, ThemeColors.Error);

            var details = new TextBox {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = ThemeColors.SurfaceSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextPrimary,
                Text = BuildDetails(summary),
            };

            var buttons = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(0, 12, 0, 0),
            };
            var cancel = new SecondaryButton {
                Text = confirmMode ? "Cancel" : "Close",
                DialogResult = DialogResult.Cancel,
            };
            buttons.Controls.Add(cancel);

            if (confirmMode) {
                var confirm = new DangerButton {
                    Text = "Confirm import",
                    DialogResult = DialogResult.OK,
                };
                buttons.Controls.Add(confirm);
                this.AcceptButton = cancel;
            }
            else {
                this.AcceptButton = cancel;
            }

            this.CancelButton = cancel;
            this.Shown += (sender, e) => {
                details.Select(0, 0);
                cancel.Focus();
            };
            root.Controls.Add(title, 0, 0);
            root.Controls.Add(subtitle, 0, 1);
            root.Controls.Add(counts, 0, 2);
            root.Controls.Add(details, 0, 3);
            root.Controls.Add(buttons, 0, 4);
            this.Controls.Add(root);
            AppTheme.ApplyThemeTree(this);
        }

        /// <summary>
        /// Adds one summary badge to the operation summary dialog.
        /// </summary>
        /// <param name="panel">Panel that receives the badge.</param>
        /// <param name="text">Badge text.</param>
        /// <param name="color">Badge accent color.</param>
        private static void AddBadge(FlowLayoutPanel panel, string text, Color color) {
            var badge = new StatusBadge();
            badge.SetStatus(text, color);
            panel.Controls.Add(badge);
        }

        /// <summary>
        /// Adds one summary badge only when its value is non-zero.
        /// </summary>
        /// <param name="panel">Panel that receives the badge.</param>
        /// <param name="label">Badge label.</param>
        /// <param name="value">Counter value.</param>
        /// <param name="color">Badge accent color.</param>
        private static void AddBadgeIfNonZero(FlowLayoutPanel panel, string label, int value, Color color) {
            if (value <= 0) {
                return;
            }

            AddBadge(panel, label + " " + value, color);
        }

        /// <summary>
        /// Selects the status color used by the leading badge.
        /// </summary>
        /// <param name="summary">Operation summary to inspect.</param>
        /// <returns>Theme color matching the global operation state.</returns>
        private static Color GetStatusColor(OperationSummary summary) {
            var status = summary == null ? string.Empty : summary.Status ?? string.Empty;
            if (summary?.Errors > 0 || status.IndexOf("Failed", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Aborted", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Error", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return ThemeColors.Error;
            }

            if (summary?.Warnings > 0 || status.IndexOf(
                "Review",
                System.StringComparison.OrdinalIgnoreCase) >= 0 || status.IndexOf(
                    "Issue",
                    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Warning", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Cancelled", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                status.IndexOf("Preflight", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return ThemeColors.Warning;
            }

            return ThemeColors.Success;
        }

        /// <summary>
        /// Builds the secondary line shown below the operation title.
        /// </summary>
        /// <param name="summary">Operation summary to describe.</param>
        /// <returns>Short human-readable summary.</returns>
        private static string BuildSubtitle(OperationSummary summary) {
            var issues = summary.Errors + summary.Warnings + summary.Restricted + summary.Unavailable +
                         summary.Unsupported + summary.AbortedSessionLost + summary.Cancelled;
            if (issues == 0) {
                return "Operation completed without reported issues.";
            }

            return "Review the issue counters and detailed messages before continuing.";
        }

        /// <summary>
        /// Builds the detailed text shown in the summary dialog.
        /// </summary>
        /// <param name="summary">Operation summary to format.</param>
        /// <returns>Detailed multi-line summary text.</returns>
        private static string BuildDetails(OperationSummary summary) {
            var lines = new List<string>();
            lines.Add("Overview");
            lines.Add("Status: " + (summary.Status ?? "-"));
            lines.Add(
                "Successful: " + summary.Ok + "; skipped: " + summary.Skipped + "; warnings: " + summary.Warnings +
                    "; errors: " + summary.Errors + ".");
            lines.Add(
                "Restricted: " + summary.Restricted + "; unavailable: " + summary.Unavailable + "; unsupported: " +
                    summary.Unsupported + "; aborted: " + summary.AbortedSessionLost + "; cancelled: " +
                    summary.Cancelled + ".");

            if (!string.IsNullOrWhiteSpace(summary.ConflictPolicy) || !string.IsNullOrWhiteSpace(summary.BackupPath)) {
                lines.Add(string.Empty);
                lines.Add("Import options");
                lines.Add("Conflict policy: " + (summary.ConflictPolicy ?? "-"));
                lines.Add("Backup: " + (summary.BackupPath ?? "-"));
            }

            var hasPaths = !string.IsNullOrWhiteSpace(summary.OutputPath) ||
                           !string.IsNullOrWhiteSpace(summary.ManifestPath) ||
                           !string.IsNullOrWhiteSpace(summary.OperationReportPath);
            if (hasPaths) {
                lines.Add(string.Empty);
                lines.Add("Artifacts");
            }

            if (!string.IsNullOrWhiteSpace(summary.OutputPath)) {
                lines.Add("Output folder: " + summary.OutputPath);
            }

            if (!string.IsNullOrWhiteSpace(summary.ManifestPath)) {
                lines.Add("Manifest: " + summary.ManifestPath);
            }

            if (!string.IsNullOrWhiteSpace(summary.OperationReportPath)) {
                lines.Add("Operation report: " + summary.OperationReportPath);
            }

            lines.Add(string.Empty);
            lines.Add("Messages");
            if (summary.Messages.Any()) {
                lines.AddRange(summary.Messages);
            }
            else {
                lines.Add("No additional messages.");
            }

            return string.Join("\r\n", lines.ToArray());
        }
    }
}
