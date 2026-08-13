// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Forms {
    /// <summary>
    /// Displays conflict resolution options for future import validation results.
    /// </summary>
    public sealed class ConflictResolutionForm : Form {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictResolutionForm"/> class.
        /// </summary>
        public ConflictResolutionForm() {
            AppTheme.ApplyTo(this);
            this.Text = "Conflict resolution";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(520, 320);

            var label = new Label {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                Text = "Conflict resolution policies will be listed here when the import validator detects conflicts.",
                ForeColor = ThemeColors.TextSecondary,
            };
            this.Controls.Add(label);
        }
    }
}
