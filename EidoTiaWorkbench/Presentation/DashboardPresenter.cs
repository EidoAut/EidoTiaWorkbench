// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// DashboardPresenter.cs
// Wires dashboard actions to project, scan, export, and repository navigation.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Presentation {
    /// <summary>
    /// Wires dashboard actions to project, scan, export, and repository navigation.
    /// </summary>
    public sealed class DashboardPresenter : PresenterBase {
        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardPresenter"/> class.
        /// </summary>
        /// <param name="view">Dashboard view to wire.</param>
        /// <param name="controller">Controller that executes dashboard commands.</param>
        /// <param name="state">Application state used to resolve portal instance choices.</param>
        /// <param name="navigate">Navigation callback used to switch shell views.</param>
        public DashboardPresenter(
            DashboardView view,
            MainController controller,
            ApplicationState state,
            Action<string> navigate) {
            view.AttachRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    await controller.RefreshPortalInstancesAsync();
                    if (state.PortalInstances.Count > 1) {
                        navigate("Project");
                        MessageBox.Show(view, "Select the intended TIA Portal instance in Project view.", "Multiple instances", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    await controller.AttachToPortalAsync(null);
                });

            view.OpenProjectRequested += (s, e) => navigate("Project");
            view.ScanRequested += (s, e) => navigate("Export");
            view.ExportChangedRequested += (s, e) => navigate("Export");
            view.OpenRepositoryRequested += (s, e) => controller.OpenRepositoryFolder();
        }
    }
}
