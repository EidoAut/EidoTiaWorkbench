// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// StatusBadge.cs
// Displays compact status text with a colored background.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays compact status text with a colored background.
    /// </summary>
    public sealed class StatusBadge : Label {
        /// <summary>
        /// Initializes a new instance of the <see cref="StatusBadge"/> class.
        /// </summary>
        public StatusBadge() {
            this.SetStyle(
                ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
                true);
            this.AutoSize = true;

            // Badges keep a small touch target while still auto-sizing for longer translated states.
            this.MinimumSize = new Size(92, 26);
            this.Padding = new Padding(10, 4, 10, 4);
            this.TextAlign = ContentAlignment.MiddleCenter;
            this.Font = ThemeFonts.Badge;
            this.Margin = new Padding(4);
            this.SetStatus("Ready", ThemeColors.Info);
        }

        /// <summary>
        /// Updates badge text and background color.
        /// </summary>
        /// <param name="text">Status text to display.</param>
        /// <param name="color">Background color representing the status.</param>
        public void SetStatus(string text, Color color) {
            this.Text = text;
            this.BackColor = RoundedDrawing.Blend(color, ThemeColors.Surface, 0.13F);
            this.ForeColor = color;
            this.Invalidate();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(this.Parent?.BackColor ?? ThemeColors.Surface);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = RoundedDrawing.Path(new RectangleF(0.5F, 0.5F, this.Width - 1, this.Height - 1), 5))
            using (var fill = new SolidBrush(RoundedDrawing.Blend(this.ForeColor, ThemeColors.Surface, 0.13F)))
            using (var border = new Pen(RoundedDrawing.Blend(this.ForeColor, ThemeColors.Surface, 0.40F))) {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
            }

            TextRenderer.DrawText(
                e.Graphics,
                this.Text,
                this.Font,
                this.ClientRectangle,
                this.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        /// <summary>
        /// Chooses a readable foreground color for the badge background.
        /// </summary>
        /// <param name="background">Badge background color.</param>
        /// <returns>A high-contrast foreground color.</returns>
        private static Color GetReadableTextColor(Color background) {
            var luminance = (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
            return luminance > 150 ? Color.FromArgb(0x00, 0x10, 0x20) : Color.White;
        }
    }
}
