// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ActivityLogControl.cs
// Displays, filters, copies, and exports activity log entries.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays, filters, copies, and exports activity log entries.
    /// </summary>
    public sealed class ActivityLogControl : UserControl {
        private readonly DataGridView _grid;
        private readonly TextBox _search;
        private readonly ComboBox _severity;
        private readonly ComboBox _operation;
        private readonly TextBox _details;
        private readonly List<ActivityEntry> _entries = new List<ActivityEntry>();
        private string _logFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityLogControl"/> class.
        /// </summary>
        public ActivityLogControl() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Surface;
            this.Padding = new Padding(10);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 70F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

            var toolbar = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, 0, 0, 6),
            };

            // Filter controls use preferred widths and wrap with the toolbar on narrow screens.
            this._severity = new ThemedComboBox { Width = 128 };
            this._severity.Items.AddRange(new object[] { "All severity", "Info", "Success", "Warning", "Error" });
            this._severity.SelectedIndex = 0;
            this._operation = new ThemedComboBox { Width = 152 };
            this._operation.Items.Add("All operations");
            this._operation.SelectedIndex = 0;
            this._search = new TextBox { Width = 220, Margin = new Padding(0, 3, 8, 0) };
            var copy = new SecondaryButton { Text = "Copy" };
            var open = new SecondaryButton { Text = "Open log" };
            var clear = new SecondaryButton { Text = "Clear view" };
            var export = new SecondaryButton { Text = "Export report" };

            this._severity.SelectedIndexChanged += (s, e) => this.RefreshGrid();
            this._operation.SelectedIndexChanged += (s, e) => this.RefreshGrid();
            this._search.TextChanged += (s, e) => this.RefreshGrid();
            copy.Click += (s, e) => this.CopySelectedMessage();
            open.Click += (s, e) => this.OpenLogRequested?.Invoke(this, EventArgs.Empty);
            clear.Click += (s, e) => this.ClearViewRequested?.Invoke(this, EventArgs.Empty);
            export.Click += (s, e) => this.ExportReport();

            toolbar.Controls.Add(this._severity);
            toolbar.Controls.Add(this._operation);
            toolbar.Controls.Add(this._search);
            toolbar.Controls.Add(copy);
            toolbar.Controls.Add(open);
            toolbar.Controls.Add(clear);
            toolbar.Controls.Add(export);

            this._grid = new DataGridView {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
            };
            AppTheme.StyleGrid(this._grid);
            this._grid.CellFormatting += (sender, e) => {
                if (e.RowIndex >= 0 && this._grid.Columns[e.ColumnIndex].DataPropertyName == "Severity") {
                    var severity = Convert.ToString(e.Value);
                    e.CellStyle.ForeColor = severity == "Error" ? ThemeColors.Error : severity == "Warning" ? ThemeColors.Warning : ThemeColors.TextPrimary;
                }
            };

            // Fixed grid column widths preserve scanability; the message column absorbs remaining space.
            this._grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = "TimeText", Width = 100 });
            this._grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Severity", DataPropertyName = "Severity", Width = 112 });
            this._grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Operation", DataPropertyName = "Operation", Width = 120 });
            this._grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Artifact", DataPropertyName = "Artifact", Width = 160 });
            this._grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Target", DataPropertyName = "Target", Width = 160 });
            this._grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Message", DataPropertyName = "Message", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            this._grid.SelectionChanged += (s, e) => this.ShowDetails();

            this._details = new TextBox {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = ThemeColors.SurfaceSecondary,
            };

            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(this._grid, 0, 1);
            root.Controls.Add(this._details, 0, 2);
            this.Controls.Add(root);
        }

        /// <summary>
        /// Occurs when the user requests opening the backing log file.
        /// </summary>
        public event EventHandler OpenLogRequested;

        /// <summary>
        /// Occurs when the user requests clearing the visible activity entries.
        /// </summary>
        public event EventHandler ClearViewRequested;

        /// <summary>
        /// Sets the path of the backing log file opened by the toolbar.
        /// </summary>
        /// <param name="path">Full log file path.</param>
        public void SetLogFilePath(string path) {
            this._logFilePath = path;
        }

        /// <summary>
        /// Replaces visible activity entries and refreshes filters.
        /// </summary>
        /// <param name="entries">Entries to show.</param>
        public void Bind(IEnumerable<ActivityEntry> entries) {
            this._entries.Clear();
            if (entries != null) {
                this._entries.AddRange(entries);
            }

            this.RefreshOperations();
            this.RefreshGrid();
        }

        /// <summary>
        /// Adds one activity entry and refreshes the grid.
        /// </summary>
        /// <param name="entry">Entry to append.</param>
        public void AddEntry(ActivityEntry entry) {
            if (this.InvokeRequired) {
                this.BeginInvoke(new Action<ActivityEntry>(this.AddEntry), entry);
                return;
            }

            this._entries.Add(entry);
            this.RefreshOperations();
            this.RefreshGrid();
        }

        /// <summary>
        /// Determines whether text contains a search token using case-insensitive comparison.
        /// </summary>
        /// <param name="value">Text to inspect.</param>
        /// <param name="search">Search token.</param>
        /// <returns><see langword="true"/> when the token is present; otherwise <see langword="false"/>.</returns>
        private static bool Contains(string value, string search) {
            return (value ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Refreshes the operation filter options from the current activity entries.
        /// </summary>
        private void RefreshOperations() {
            var current = this._operation.SelectedItem as string ?? "All operations";
            this._operation.Items.Clear();
            this._operation.Items.Add("All operations");
            foreach (var op in this._entries.Select(e => e.Operation).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s)) {
                this._operation.Items.Add(op);
            }

            this._operation.SelectedItem = this._operation.Items.Contains(current) ? current : "All operations";
        }

        /// <summary>
        /// Applies current filters and refreshes the activity grid rows.
        /// </summary>
        private void RefreshGrid() {
            var rows = this._entries.AsEnumerable();
            var sev = this._severity.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(sev) && sev != "All severity") {
                rows = rows.Where(e => string.Equals(e.Severity.ToString(), sev, StringComparison.OrdinalIgnoreCase));
            }

            var op = this._operation.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(op) && op != "All operations") {
                rows = rows.Where(e => string.Equals(e.Operation, op, StringComparison.OrdinalIgnoreCase));
            }

            var search = (this._search.Text ?? string.Empty).Trim();
            if (search.Length > 0) {
                rows = rows.Where(e =>
                    Contains(e.Message, search) ||
                    Contains(e.Artifact, search) ||
                    Contains(e.Target, search) ||
                    Contains(e.Detail, search));
            }

            this._grid.DataSource = rows
                .OrderByDescending(e => e.Time)
                .Select(e => new ActivityRow(e))
                .ToList();
        }

        /// <summary>
        /// Shows diagnostic details for the selected activity row.
        /// </summary>
        private void ShowDetails() {
            if (this._grid.CurrentRow == null) {
                this._details.Text = string.Empty;
                return;
            }

            var row = this._grid.CurrentRow.DataBoundItem as ActivityRow;
            this._details.Text = row == null ? string.Empty : row.Entry.Detail ?? row.Entry.Message;
        }

        /// <summary>
        /// Copies the selected activity message to the clipboard.
        /// </summary>
        private void CopySelectedMessage() {
            if (this._grid.CurrentRow == null) {
                return;
            }

            var row = this._grid.CurrentRow.DataBoundItem as ActivityRow;
            if (row != null) {
                Clipboard.SetText(row.Entry.Message ?? string.Empty);
            }
        }

        /// <summary>
        /// Exports the visible activity list to a text report chosen by the user.
        /// </summary>
        private void ExportReport() {
            using (var dlg = new SaveFileDialog()) {
                dlg.Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*";
                dlg.FileName = ProductInfo.OperationReportFileName;
                if (dlg.ShowDialog(this) != DialogResult.OK) {
                    return;
                }

                File.WriteAllLines(
                    dlg.FileName,
                    this._entries.Select(e =>
                        string.Format(
                            "[{0:yyyy-MM-dd HH:mm:ss}] {1} {2} {3} {4} {5}",
                            e.Time,
                            e.Severity,
                            e.Operation,
                            e.Artifact,
                            e.Target,
                            e.Message)));
                Process.Start(dlg.FileName);
            }
        }

        /// <summary>
        /// Projection used by the grid to display activity entries.
        /// </summary>
        private sealed class ActivityRow {
            /// <summary>
            /// Initializes a new instance of the <see cref="ActivityRow"/> class.
            /// </summary>
            /// <param name="entry">Activity entry displayed by the row.</param>
            public ActivityRow(ActivityEntry entry) {
                this.Entry = entry;
            }

            /// <summary>
            /// Gets the underlying activity entry.
            /// </summary>
            public ActivityEntry Entry {
                get;
            }

            /// <summary>
            /// Gets the formatted activity time.
            /// </summary>
            public string TimeText {
                get {
                    return this.Entry.Time.ToString("HH:mm:ss");
                }
            }

            /// <summary>
            /// Gets the activity severity text.
            /// </summary>
            public string Severity {
                get {
                    return this.Entry.Severity.ToString();
                }
            }

            /// <summary>
            /// Gets the operation name.
            /// </summary>
            public string Operation {
                get {
                    return this.Entry.Operation;
                }
            }

            /// <summary>
            /// Gets the artifact name.
            /// </summary>
            public string Artifact {
                get {
                    return this.Entry.Artifact;
                }
            }

            /// <summary>
            /// Gets the target path.
            /// </summary>
            public string Target {
                get {
                    return this.Entry.Target;
                }
            }

            /// <summary>
            /// Gets the activity message.
            /// </summary>
            public string Message {
                get {
                    return this.Entry.Message;
                }
            }
        }
    }
}
