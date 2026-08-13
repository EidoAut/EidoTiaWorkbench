// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

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
            this.BackColor = color;
            this.ForeColor = GetReadableTextColor(color);
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
