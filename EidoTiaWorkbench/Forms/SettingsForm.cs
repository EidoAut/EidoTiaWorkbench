// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// SettingsForm.cs
// Hosts the settings view in a modal form.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Theme;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Forms {
    /// <summary>
    /// Hosts the settings view in a modal form.
    /// </summary>
    public sealed class SettingsForm : Form {
        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsForm"/> class.
        /// </summary>
        /// <param name="state">Application state to bind to the settings view.</param>
        /// <param name="logFilePath">Full path of the application log file.</param>
        public SettingsForm(ApplicationState state, string logFilePath) {
            AppTheme.ApplyTo(this);
            this.Text = "Settings";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(980, 680);

            // The modal settings window keeps enough space for one-column responsive sections.
            this.MinimumSize = new Size(760, 520);

            var view = new SettingsView { Dock = DockStyle.Fill };
            view.Bind(state, logFilePath);
            this.Controls.Add(view);
            AppTheme.ApplyThemeTree(this);
        }
    }
}
