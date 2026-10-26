// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportPresenter.cs
// Wires export view actions to scan and export controller commands.
//
// Developed by EIDO Automation
// =========================================================================
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Presentation {
    /// <summary>
    /// Wires export view actions to scan and export controller commands.
    /// </summary>
    public sealed class ExportPresenter : PresenterBase {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportPresenter"/> class.
        /// </summary>
        /// <param name="view">Export view to wire.</param>
        /// <param name="controller">Controller that executes scan and export commands.</param>
        /// <param name="state">Application state used to refresh view bindings.</param>
        public ExportPresenter(ExportView view, MainController controller, ApplicationState state) {
            view.RefreshRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    await controller.ScanProjectAsync(view.Profile, view.IncludeDbs);
                    view.Bind(state);
                });

            view.ExportSelectedRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    await controller.ExportAsync(
                        view.SelectedItems,
                        view.NormalizeXml,
                        view.PreserveHmiXml,
                        view.Incremental,
                        "Export selected");
                });

            view.ExportAllRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    await controller.ExportAsync(
                        view.AllItems,
                        view.NormalizeXml,
                        view.PreserveHmiXml,
                        view.Incremental,
                        "Export all");
                });

            view.ExportChangedRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    var selected = view.SelectedItems.Count > 0 ? view.SelectedItems : view.AllItems;
                    await controller.ExportAsync(
                        selected,
                        view.NormalizeXml,
                        view.PreserveHmiXml,
                        true,
                        "Export changed");
                });

            view.RetryIncompleteRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    await controller.RetryIncompleteExportAsync(view.AllItems, view.NormalizeXml, view.PreserveHmiXml);
                });
        }
    }
}
