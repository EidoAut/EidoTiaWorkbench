// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// EmptyStateControl.cs
// Shows an empty-state title and message when a view has no content.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Shows an empty-state title and message when a view has no content.
    /// </summary>
    public sealed class EmptyStateControl : UserControl {
        private readonly Label _title;
        private readonly Label _message;
        private readonly PrimaryButton _action;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmptyStateControl"/> class.
        /// </summary>
        public EmptyStateControl() {
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = ThemeColors.SurfaceSecondary;
            this.Padding = new Padding(24, 22, 24, 22);
            this.Margin = new Padding(0, 0, 0, 12);

            var panel = new TableLayoutPanel {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 3,
            };
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            this._title = new Label {
                AutoSize = true,
                Font = ThemeFonts.SectionTitle,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 0, 0, 8),
            };
            this._message = new Label {
                AutoSize = true,

                // Empty-state copy wraps at a readable line length inside the scrollable dashboard.
                MaximumSize = new Size(720, 0),
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextSecondary,
            };
            this._action = new PrimaryButton {
                Text = "Attach to TIA Portal",
                Margin = new Padding(0, 16, 0, 0),
            };
            this._action.Click += (s, e) => this.ActionRequested?.Invoke(this, System.EventArgs.Empty);

            panel.Controls.Add(this._title, 0, 0);
            panel.Controls.Add(this._message, 0, 1);
            panel.Controls.Add(this._action, 0, 2);
            this.Controls.Add(panel);
        }

        /// <summary>
        /// Occurs when the user activates the empty-state primary action.
        /// </summary>
        public event System.EventHandler ActionRequested;

        /// <summary>
        /// Updates the empty-state title and explanatory message.
        /// </summary>
        /// <param name="title">Title shown at the top of the empty state.</param>
        /// <param name="message">Supporting message shown below the title.</param>
        public void SetMessage(string title, string message) {
            this._title.Text = title;
            this._message.Text = message;
        }
    }
}
