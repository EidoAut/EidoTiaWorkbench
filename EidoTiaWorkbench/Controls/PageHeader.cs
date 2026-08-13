// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays a page title and subtitle at the top of a view.
    /// </summary>
    public sealed class PageHeader : UserControl {
        private readonly Label _title;
        private readonly Label _subtitle;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageHeader"/> class.
        /// </summary>
        public PageHeader() {
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.Dock = DockStyle.Top;
            this.BackColor = ThemeColors.Background;

            var layout = new TableLayoutPanel {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            this._title = new Label {
                AutoSize = true,
                Font = ThemeFonts.PageTitle,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 0, 0, 2),
            };
            this._subtitle = new Label {
                AutoSize = true,
                Font = ThemeFonts.Secondary,
                ForeColor = ThemeColors.TextSecondary,
                Margin = new Padding(0, 0, 0, 10),
            };

            layout.Controls.Add(this._title, 0, 0);
            layout.Controls.Add(this._subtitle, 0, 1);
            this.Controls.Add(layout);
        }

        /// <summary>
        /// Updates the page title and subtitle text.
        /// </summary>
        /// <param name="title">Title text.</param>
        /// <param name="subtitle">Subtitle text.</param>
        public void SetText(string title, string subtitle) {
            this._title.Text = title;
            this._subtitle.Text = subtitle;
        }
    }
}
