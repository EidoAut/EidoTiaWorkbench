// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// OperationOverlay.cs
// Displays the active operation name, current artifact, and progress bar above the current view.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays the active operation name, current artifact, and progress bar above the current view.
    /// </summary>
    public sealed class OperationOverlay : UserControl {
        private readonly Label _title;
        private readonly Label _artifact;
        private readonly ProgressBar _progress;
        private readonly SecondaryButton _cancel;

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationOverlay"/> class.
        /// </summary>
        public OperationOverlay() {
            this.Dock = DockStyle.Top;

            // The overlay has fixed chrome height so progress feedback does not resize the active view.
            this.Height = 88;
            this.BackColor = ThemeColors.OperationSurface;
            this.Padding = new Padding(14, 8, 14, 8);
            this.Visible = false;

            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 14F));

            this._title = new Label {
                Dock = DockStyle.Fill,
                Font = ThemeFonts.SectionTitle,
                ForeColor = ThemeColors.TextPrimary,
                AutoEllipsis = true,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this._artifact = new Label {
                Dock = DockStyle.Fill,
                Font = ThemeFonts.Secondary,
                ForeColor = ThemeColors.TextSecondary,
                AutoEllipsis = true,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this._progress = new ThemedProgressBar {
                Dock = DockStyle.Top,

                // A slim progress indicator is deliberate chrome rather than content layout.
                Height = 10,
                Style = ProgressBarStyle.Continuous,
                MarqueeAnimationSpeed = 24,
            };
            this._cancel = new SecondaryButton {
                Text = "Cancel after current artifact",
                AutoSize = true,
                Margin = new Padding(12, 8, 0, 0),
            };
            this._cancel.Click += (s, e) => this.CancelRequested?.Invoke(this, EventArgs.Empty);

            layout.Controls.Add(this._title, 0, 0);
            layout.Controls.Add(this._artifact, 0, 1);
            layout.Controls.Add(this._progress, 0, 2);
            layout.Controls.Add(this._cancel, 1, 0);
            layout.SetRowSpan(this._cancel, 3);
            this.Controls.Add(layout);
        }

        /// <summary>
        /// Occurs when the user requests cooperative cancellation after the current artifact.
        /// </summary>
        public event EventHandler CancelRequested;

        /// <summary>
        /// Updates overlay visibility and progress from the current operation.
        /// </summary>
        /// <param name="operation">Operation state to display.</param>
        public void Bind(CurrentOperationInfo operation) {
            operation = operation ?? CurrentOperationInfo.Idle;
            this.Visible = operation.IsBusy;
            this._title.Text = string.IsNullOrWhiteSpace(operation.StatusText) ? operation.Name : operation.StatusText;
            if (!operation.IsIndeterminate && operation.Maximum > 0) {
                this._title.Text += " (" + operation.Progress + " / " + operation.Maximum + ")";
            }

            this._artifact.Text = string.IsNullOrWhiteSpace(
                operation.CurrentArtifact)
                    ? "Waiting for the engineering operation to complete. You can review Activity for details."
                    : operation.CurrentArtifact;
            this._progress.Style = operation.IsIndeterminate ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            this._progress.Maximum = System.Math.Max(1, operation.Maximum);
            if (!operation.IsIndeterminate) {
                this._progress.Value = System.Math.Min(this._progress.Maximum, System.Math.Max(0, operation.Progress));
            }

            this._cancel.Visible = operation.CanCancel;
            this._cancel.Enabled = operation.CanCancel;
        }
    }
}
