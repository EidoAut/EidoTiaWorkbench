// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// WarningBanner.cs
// Displays a persistent warning message at the top of a view.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays a persistent warning message at the top of a view.
    /// </summary>
    public sealed class WarningBanner : Label {
        /// <summary>
        /// Initializes a new instance of the <see cref="WarningBanner"/> class.
        /// </summary>
        public WarningBanner() {
            this.AutoSize = false;
            this.Dock = DockStyle.Top;

            // Warning banners use one stable line of chrome above the view content.
            this.Height = 34;
            this.TextAlign = ContentAlignment.MiddleLeft;
            this.Padding = new Padding(12, 0, 12, 0);
            this.BackColor = ThemeColors.WarningSurface;
            this.ForeColor = ThemeColors.Warning;
            this.Font = ThemeFonts.Body;
            this.Visible = false;
        }
    }
}
