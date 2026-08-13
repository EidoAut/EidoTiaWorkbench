// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Presentation {
    /// <summary>
    /// Wires activity log view events to controller commands and state notifications.
    /// </summary>
    public sealed class ActivityPresenter {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityPresenter"/> class.
        /// </summary>
        /// <param name="view">Activity view to wire.</param>
        /// <param name="controller">Controller that handles log commands.</param>
        /// <param name="state">Application state that publishes activity events.</param>
        public ActivityPresenter(ActivityView view, MainController controller, ApplicationState state) {
            view.ActivityLog.SetLogFilePath(controller.LogFilePath);
            view.ActivityLog.OpenLogRequested += (s, e) => controller.OpenLogFile();
            view.ActivityLog.ClearViewRequested += (s, e) => controller.ClearActivityView();
            state.ActivityAdded += (s, e) => view.ActivityLog.AddEntry(e.Entry);
        }
    }
}
