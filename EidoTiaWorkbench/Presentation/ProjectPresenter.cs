// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Presentation {
    /// <summary>
    /// Wires project view actions to portal attach, start, open, and disconnect workflows.
    /// </summary>
    public sealed class ProjectPresenter : PresenterBase {
        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectPresenter"/> class.
        /// </summary>
        /// <param name="view">Project view to wire.</param>
        /// <param name="controller">Controller that executes project commands.</param>
        /// <param name="state">Application state used to decide disconnect behavior.</param>
        public ProjectPresenter(ProjectView view, MainController controller, ApplicationState state) {
            view.RefreshInstancesRequested += (s, e) => FireAndReport(view, async () => {
                await controller.RefreshPortalInstancesAsync();
                view.ShowInstances(state.PortalInstances);
            });

            view.AttachSelectedRequested += (s, e) => FireAndReport(view, async () => {
                await controller.AttachToPortalAsync(view.SelectedInstanceIndex);
                view.Bind(state);
            });

            view.StartPortalRequested += (s, e) => FireAndReport(view, async () => {
                await controller.StartPortalAsync(view.StartWithUi);
                view.Bind(state);
            });

            view.OpenProjectRequested += (s, e) => FireAndReport(view, async () => {
                await controller.OpenProjectAsync(view.ProjectPath);
                view.Bind(state);
            });

            view.DisconnectRequested += (s, e) => FireAndReport(view, async () => {
                var closeOwned = false;
                if (state.PortalOwnership == PortalOwnership.Owned) {
                    var answer = MessageBox.Show(
                        view,
                        "This TIA Portal instance was started by " + ProductInfo.Name + ". Close it while disconnecting?",
                        "Disconnect",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
                    if (answer == DialogResult.Cancel) {
                        return;
                    }

                    closeOwned = answer == DialogResult.Yes;
                }

                await controller.DisconnectAsync(closeOwned);
                view.Bind(state);
            });
        }
    }
}
