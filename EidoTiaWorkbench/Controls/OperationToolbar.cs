// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides a themed horizontal toolbar container for operation buttons.
    /// </summary>
    public sealed class OperationToolbar : FlowLayoutPanel {
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationToolbar"/> class.
        /// </summary>
        public OperationToolbar() {
            this.Dock = DockStyle.Top;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.FlowDirection = FlowDirection.LeftToRight;
            this.WrapContents = true;
            this.BackColor = ThemeColors.Surface;
            this.Padding = new Padding(8, 6, 8, 6);
            this.Margin = new Padding(0, 0, 0, 10);
        }
    }
}
