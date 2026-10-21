// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ValidationView.cs
// Validates repository payloads without requiring or modifying a TIA project.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>Validates repository payloads without requiring or modifying a TIA project.</summary>
    public sealed class ValidationView : UserControl {
        private readonly PathSelectorControl _path = new PathSelectorControl { Dock = DockStyle.Top };
        private readonly TextBox _report = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Font = new Font("Consolas", 10) };
        private readonly DataGridView _issues = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = false };
        private readonly SecondaryButton _validate = new SecondaryButton { Text = "Validate workspace (offline)" };

        /// <summary>Initializes a new instance of the <see cref="ValidationView"/> class. Creates an offline workspace validator; validation never attaches to TIA.</summary>
        public ValidationView() {
            this.Dock = DockStyle.Fill;
            this.Padding = new Padding(ThemeMetrics.Xl);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            var header = new PageHeader();
            header.SetText("Validation", "Local XML, manifest and dependency checks. Siemens import compatibility requires a live test.");
            layout.Controls.Add(header);
            layout.Controls.Add(this._path);
            layout.Controls.Add(this._validate);
            AppTheme.StyleGrid(this._issues);
            this._issues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Severity", DataPropertyName = "Severity", Width = 110 });
            this._issues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Check", DataPropertyName = "Phase", Width = 150 });
            this._issues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", DataPropertyName = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            this._issues.SelectionChanged += (sender, e) => {
                var issue = this._issues.CurrentRow?.DataBoundItem as ImportValidationIssue;
                if (issue != null) {
                    this._report.Text = issue.ToString();
                }
            };
            layout.Controls.Add(this._issues);
            layout.Controls.Add(this._report);
            this.Controls.Add(layout);
            this._validate.Click += async (s, e) => {
                var path = this._path.SelectedPath;
                this._validate.Enabled = false;
                this._report.Text = "Validating workspace...";
                try {
                    var validation = await Task.Run(() => {
                        if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path)) {
                            throw new System.IO.DirectoryNotFoundException("Select an existing workspace folder.");
                        }

                        var inventory = new RepositoryInventoryService(new ManifestService()).Scan(path);
                        var plan = new ImportDependencyPlanner().Build(inventory);
                        var result = new ImportPreflightValidator().Validate(inventory, plan);
                        return Tuple.Create(inventory.Count, result);
                    });
                    this._issues.DataSource = validation.Item2.Issues.ToList();
                    this._report.Text = "Offline validation · No engineering changes\r\n" + validation.Item1 + " artifacts · " + validation.Item2.ErrorCount + " errors · " + validation.Item2.WarningCount + " warnings\r\nSelect a result to inspect its artifact and full diagnostic details.";
                }
                catch (Exception ex) {
                    this._report.Text = "Validation failed: " + ex.Message;
                }
                finally {
                    if (!this.IsDisposed) {
                        this._validate.Enabled = true;
                    }
                }
            };
        }

        /// <summary>Updates the workspace path used for the next offline validation.</summary>
        /// <param name="state">Current workspace selection.</param>
        public void Bind(ApplicationState state) {
            this._path.SelectedPath = state.SelectedRepository;
        }
    }
}
