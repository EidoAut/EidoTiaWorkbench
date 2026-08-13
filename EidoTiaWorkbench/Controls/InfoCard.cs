// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays a compact title/value card with an optional status badge.
    /// </summary>
    public class InfoCard : UserControl {
        private readonly Label _title;
        private readonly Label _value;
        private readonly StatusBadge _badge;
        private readonly ToolTip _toolTip;

        /// <summary>
        /// Initializes a new instance of the <see cref="InfoCard"/> class.
        /// </summary>
        public InfoCard() {
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // Cards keep a compact readable minimum and grow with the responsive dashboard grid.
            this.MinimumSize = new Size(180, 92);
            this.BackColor = ThemeColors.Surface;
            this.Padding = new Padding(14, 12, 14, 12);
            this.Margin = new Padding(0, 0, 12, 12);
            this._toolTip = new ToolTip();

            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 2,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this._title = new Label {
                AutoSize = true,
                Font = ThemeFonts.Secondary,
                ForeColor = ThemeColors.TextSecondary,
            };
            this._value = new Label {
                Dock = DockStyle.Fill,
                Font = ThemeFonts.SectionTitle,
                ForeColor = ThemeColors.TextPrimary,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,

                // Two-line value space avoids clipping longer project and repository values at high DPI.
                MinimumSize = new Size(0, 38),
            };
            this._badge = new StatusBadge { Visible = false };

            layout.Controls.Add(this._title, 0, 0);
            layout.Controls.Add(this._badge, 1, 0);
            layout.Controls.Add(this._value, 0, 1);
            layout.SetColumnSpan(this._value, 2);
            this.Controls.Add(layout);
        }

        /// <summary>
        /// Sets title and value text and hides the optional badge.
        /// </summary>
        /// <param name="title">Card title.</param>
        /// <param name="value">Card value.</param>
        public void SetContent(string title, string value) {
            this._title.Text = title;
            this._value.Text = string.IsNullOrWhiteSpace(value) ? "-" : value;
            this._toolTip.SetToolTip(this._value, this._value.Text);
            this._toolTip.SetToolTip(this, this._value.Text);
            this._badge.Visible = false;
        }

        /// <summary>
        /// Sets title, value, and badge status for the card.
        /// </summary>
        /// <param name="title">Card title.</param>
        /// <param name="value">Card value.</param>
        /// <param name="badge">Badge text.</param>
        /// <param name="badgeColor">Badge background color.</param>
        public void SetContent(string title, string value, string badge, Color badgeColor) {
            this.SetContent(title, value);
            this._badge.SetStatus(badge, badgeColor);
            this._badge.Visible = true;
        }
    }
}
