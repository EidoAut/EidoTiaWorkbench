// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportPresenter.cs
// Wires import view actions to repository inventory, dry-run, and import workflows.
//
// Developed by EIDO Automation
// =========================================================================
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Forms;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Presentation {
    /// <summary>
    /// Wires import view actions to repository inventory, dry-run, and import workflows.
    /// </summary>
    public sealed class ImportPresenter : PresenterBase {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImportPresenter"/> class.
        /// </summary>
        /// <param name="view">Import view to wire.</param>
        /// <param name="controller">Controller that executes repository and import commands.</param>
        /// <param name="state">Application state used to refresh repository bindings.</param>
        public ImportPresenter(ImportView view, MainController controller, ApplicationState state) {
            view.LoadRepositoryRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    await controller.LoadRepositoryInventoryAsync(view.RepositoryPath);
                    view.Bind(state);
                });

            view.DryRunRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    var summary = await controller.PreviewImportRepositoryArtifactsAsync(
                        view.SelectedArtifacts,
                        view.ConflictPolicy);
                    summary.Messages.Add("Dry run only. No objects will be imported.");
                    using (var form = new OperationSummaryForm(summary, confirmMode: false)) {
                        form.ShowDialog(view);
                    }
                });

            view.ImportSelectedRequested += (s, e) => FireAndReport(
                view,
                async () => {
                    var selected = view.SelectedArtifacts;
                    var summary = await controller.PreviewImportRepositoryArtifactsAsync(selected, view.ConflictPolicy);
                    summary.Operation = "Import selected";
                    summary.BackupPath = view.CreateBackupBeforeImport
                        ? "A backup will be created before replacing HMI screens."
                        : "Backup disabled by user.";
                    using (var form = new OperationSummaryForm(summary, confirmMode: true)) {
                        if (form.ShowDialog(view) != DialogResult.OK) {
                            return;
                        }
                    }

                    await controller.ImportRepositoryArtifactsAsync(
                        selected,
                        view.ConflictPolicy,
                        view.CreateBackupBeforeImport);
                });
        }

        /// <summary>
        /// Builds a basic summary from the selected repository artifacts.
        /// </summary>
        /// <param name="view">Import view containing the current selection.</param>
        /// <param name="operation">Display name of the operation.</param>
        /// <returns>A summary describing the selected import artifacts.</returns>
        private static OperationSummary BuildSummary(ImportView view, string operation) {
            var selected = view.SelectedArtifacts;
            var summary = new OperationSummary {
                Operation = operation,
                Ok = selected.Count,
                Skipped = 0,
                Warnings = selected.Count(a => !string.IsNullOrWhiteSpace(a.Warning)),
                Errors = selected.Count(a => a.State == RepositoryArtifactState.InvalidXml ||
                                             a.State == RepositoryArtifactState.Unsupported),
                ConflictPolicy = "Default Openness import options",
                BackupPath = "Not selected",
            };

            summary.Messages.Add("New objects: " + selected.Count(a => a.State == RepositoryArtifactState.New));
            summary.Messages.Add(
                "Objects that will be replaced: " + selected.Count(a => a.State == RepositoryArtifactState.Modified));
            summary.Messages.Add(
                "Objects omitted: " + selected.Count(a => a.State == RepositoryArtifactState.Unsupported));
            foreach (var warning in selected.Where(a => !string.IsNullOrWhiteSpace(a.Warning))) {
                summary.Messages.Add("Warning: " + warning.Item.Name + " - " + warning.Warning);
            }

            return summary;
        }
    }
}
