// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportView.cs
// Displays workspace inventory and import controls for selected artifacts.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>
    /// Displays workspace inventory and import controls for selected artifacts.
    /// </summary>
    public sealed class ImportView : UserControl {
        private readonly PathSelectorControl _repository;
        private readonly Label _manifestStatus;
        private readonly ArtifactTreeControl _tree;
        private readonly DataGridView _comparison;
        private readonly TextBox _conflicts;
        private readonly CheckBox _dryRun;
        private readonly CheckBox _createBackup;
        private readonly ComboBox _conflictPolicy;
        private readonly Label _summary;
        private List<RepositoryArtifact> _artifacts = new List<RepositoryArtifact>();
        private string _artifactSignature = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportView"/> class.
        /// </summary>
        public ImportView() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Background;
            this.Padding = new Padding(ThemeMetrics.Xl);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new PageHeader();
            header.SetText("Import", "Load importable artifacts from the workspace and compare with the project.");

            var repoPanel = new TableLayoutPanel {
                Dock = DockStyle.Top,
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ThemeColors.Surface,
                Padding = new Padding(12),
            };
            repoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            repoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this._repository = new PathSelectorControl {
                FolderMode = true,
                DialogTitle = "Select workspace",
            };
            this._manifestStatus = new Label {
                Dock = DockStyle.Fill,
                Text = "Workspace not validated",
                ForeColor = ThemeColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            var load = new PrimaryButton {
                Text = "Load inventory",
                Anchor = AnchorStyles.None,
                AutoSize = false,
                Size = new Size(132, 36),
                Margin = Padding.Empty,
            };
            load.Click += (s, e) => this.LoadRepositoryRequested?.Invoke(this, EventArgs.Empty);
            repoPanel.Controls.Add(this._repository, 0, 0);
            repoPanel.Controls.Add(load, 1, 0);
            repoPanel.Controls.Add(this._manifestStatus, 0, 1);
            repoPanel.SetColumnSpan(this._manifestStatus, 2);

            var legend = BuildLegend();

            var body = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,

                // Initial distance keeps the workspace tree and comparison grid visible before user adjustment.
                SplitterDistance = 580,
            };

            body.SizeChanged += (s, e) => {
                if (body.ClientSize.Width > 500) {
                    body.SplitterDistance = Math.Max(240, (int)(body.ClientSize.Width * 0.58));
                }
            };
            this._tree = new ArtifactTreeControl();
            this._tree.ConfigureSelection(item => item != null && item.CanImport);
            this._tree.SelectionChanged += (s, e) => this.UpdateSummary();
            body.Panel1.Controls.Add(this._tree);

            var right = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                BackColor = ThemeColors.Surface,
                Padding = new Padding(12),
            };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            right.Dock = DockStyle.Top;
            body.Panel2.AutoScroll = true;
            body.Panel2.SizeChanged += (sender, e) => right.Height = Math.Max(body.Panel2.ClientSize.Height, ThemeMetrics.Scale(right, 320));
            right.Height = 320;
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
            right.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            this._comparison = new DataGridView {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
            };
            AppTheme.StyleGrid(this._comparison);

            // The first comparison columns are fixed for scanability; the path column fills remaining space.
            this._comparison.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Artifact", DataPropertyName = "Artifact", Width = 180 });
            this._comparison.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "State", DataPropertyName = "State", Width = 120 });
            this._comparison.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Workspace path", DataPropertyName = "Path", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            this._conflicts = new TextBox {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = ThemeColors.SurfaceSecondary,
                Text = "Conflict details will appear here after validation.",
            };

            this._summary = new Label {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ForeColor = ThemeColors.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
            };

            right.Controls.Add(new Label { Text = "Workspace comparison", AutoSize = true, Font = ThemeFonts.SectionTitle, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
            right.Controls.Add(this._comparison, 0, 1);
            right.Controls.Add(new Label { Text = "Prevalidation & conflicts", AutoSize = true, Font = ThemeFonts.SectionTitle, Margin = new Padding(0, 12, 0, 8) }, 0, 2);
            right.Controls.Add(this._conflicts, 0, 3);
            right.Controls.Add(this._summary, 0, 4);
            body.Panel2.Controls.Add(right);

            var bottom = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Padding = new Padding(0, 8, 0, 0),
            };
            var import = new PrimaryButton { Text = "Import selected" };
            var dryRun = new SecondaryButton { Text = "Dry run" };
            this._dryRun = new ThemedCheckBox {
                Text = "Dry run by default",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 10, 8, 0),
            };
            this._createBackup = new ThemedCheckBox {
                Text = "Create backup",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(8, 10, 8, 0),
            };
            this._conflictPolicy = new ThemedComboBox {
                // Conflict policy names are short and the bottom toolbar wraps when width is constrained.
                Width = 116,
                Margin = new Padding(8, 4, 8, 0),
            };
            this._conflictPolicy.DataSource = Enum.GetValues(typeof(ImportConflictPolicy));
            this._conflictPolicy.SelectedItem = ImportConflictPolicy.Ask;
            import.Click += (s, e) => {
                if (this._dryRun.Checked) {
                    this.DryRunRequested?.Invoke(this, EventArgs.Empty);
                }
                else {
                    this.ImportSelectedRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            this._dryRun.CheckedChanged += (s, e) => import.Text = this._dryRun.Checked ? "Preview selected" : "REAL IMPORT";
            import.Text = "Preview selected";
            dryRun.Click += (s, e) => this.DryRunRequested?.Invoke(this, EventArgs.Empty);
            bottom.Controls.Add(import);
            bottom.Controls.Add(dryRun);
            bottom.Controls.Add(this._conflictPolicy);
            bottom.Controls.Add(this._createBackup);
            bottom.Controls.Add(this._dryRun);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(repoPanel, 0, 1);
            root.Controls.Add(legend, 0, 2);
            root.Controls.Add(body, 0, 3);
            root.Controls.Add(bottom, 0, 4);
            this.Controls.Add(root);
        }

        /// <summary>
        /// Occurs when the user requests loading workspace inventory.
        /// </summary>
        public event EventHandler LoadRepositoryRequested;

        /// <summary>
        /// Occurs when the user requests a dry-run summary.
        /// </summary>
        public event EventHandler DryRunRequested;

        /// <summary>
        /// Occurs when the user requests importing checked workspace artifacts.
        /// </summary>
        public event EventHandler ImportSelectedRequested;

        /// <summary>
        /// Gets or sets the selected workspace path.
        /// </summary>
        public string RepositoryPath {
            get {
                return this._repository.SelectedPath;
            }

            set {
                this._repository.SelectedPath = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether dry run mode is selected by default.
        /// </summary>
        public bool DryRunByDefault {
            get {
                return this._dryRun.Checked;
            }
        }

        /// <summary>
        /// Gets a value indicating whether existing HMI screens should be backed up before replacement.
        /// </summary>
        public bool CreateBackupBeforeImport {
            get {
                return this._createBackup.Checked;
            }
        }

        /// <summary>
        /// Gets the selected conflict policy for HMI screen imports.
        /// </summary>
        public ImportConflictPolicy ConflictPolicy {
            get {
                return (ImportConflictPolicy)this._conflictPolicy.SelectedItem;
            }
        }

        /// <summary>
        /// Gets checked workspace artifacts mapped from the tree selection.
        /// </summary>
        public List<RepositoryArtifact> SelectedArtifacts {
            get {
                var keys = new HashSet<string>(this._tree.GetCheckedItems().Select(i => i.LogicalKey), StringComparer.OrdinalIgnoreCase);
                return this._artifacts.Where(a => keys.Contains(a.Item.LogicalKey)).ToList();
            }
        }

        /// <summary>
        /// Binds workspace path and inventory from application state.
        /// </summary>
        /// <param name="state">Application state containing workspace inventory.</param>
        public void Bind(ApplicationState state) {
            this.RepositoryPath = state.SelectedRepository;
            this.SetArtifacts(state.RepositoryInventory);
        }

        /// <summary>
        /// Replaces workspace artifacts displayed by the import view.
        /// </summary>
        /// <param name="artifacts">Workspace artifacts to display.</param>
        public void SetArtifacts(IEnumerable<RepositoryArtifact> artifacts) {
            this._artifacts = (artifacts ?? Enumerable.Empty<RepositoryArtifact>()).ToList();
            var signature = string.Join(
                "|",
                this._artifacts
                .Select(a => a.Item.LogicalKey + ":" + a.State)
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray());
            var defaultKeys = string.Equals(signature, this._artifactSignature, StringComparison.Ordinal)
                ? null
                : this._artifacts
                    .Where(IsDefaultImportSelection)
                    .Select(a => a.Item.LogicalKey)
                    .ToList();
            this._artifactSignature = signature;
            this._tree.SetItems(this._artifacts.Select(a => a.Item), defaultKeys);
            this._comparison.DataSource = this._artifacts.Select(a => new ImportRow(a)).ToList();

            var invalid = this._artifacts.Count(a => a.State == RepositoryArtifactState.InvalidXml || a.State == RepositoryArtifactState.Unsupported);
            this._manifestStatus.Text = this._artifacts.Count == 0
                ? "No workspace inventory loaded"
                : "Manifest/workspace validation loaded " + this._artifacts.Count + " artifact(s), " + invalid + " invalid or unsupported.";

            var conflictText = this._artifacts
                .Where(a => !string.IsNullOrWhiteSpace(a.Warning))
                .Select(a => a.Item.Name + ": " + a.Warning)
                .ToList();
            this._conflicts.Text = conflictText.Count == 0
                ? "No conflicts detected by workspace validation."
                : string.Join(Environment.NewLine, conflictText);
            this.UpdateSummary();
        }

        /// <summary>
        /// Builds the workspace comparison legend.
        /// </summary>
        /// <returns>The legend control.</returns>
        private static Control BuildLegend() {
            var panel = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = ThemeColors.Background,
            };
            AddLegend(panel, "New", ThemeColors.Info);
            AddLegend(panel, "Modified", ThemeColors.Warning);
            AddLegend(panel, "Identical", ThemeColors.Success);
            AddLegend(panel, "Missing dependency", ThemeColors.Warning);
            AddLegend(panel, "Conflict", ThemeColors.Error);
            AddLegend(panel, "Invalid XML", ThemeColors.Error);
            AddLegend(panel, "Unsupported", ThemeColors.Disabled);
            return panel;
        }

        /// <summary>
        /// Adds one state badge to the import legend.
        /// </summary>
        /// <param name="panel">Panel that receives the badge.</param>
        /// <param name="text">Badge text.</param>
        /// <param name="color">Badge accent color.</param>
        private static void AddLegend(FlowLayoutPanel panel, string text, Color color) {
            var badge = new StatusBadge();
            badge.SetStatus(text, color);
            panel.Controls.Add(badge);
        }

        /// <summary>
        /// Determines whether a workspace artifact should be checked by default for import.
        /// </summary>
        /// <param name="artifact">Repository artifact to inspect.</param>
        /// <returns><see langword="true"/> for new, modified, or conflicting import-capable artifacts.</returns>
        private static bool IsDefaultImportSelection(RepositoryArtifact artifact) {
            return artifact.Item.CanImport &&
                   (artifact.State == RepositoryArtifactState.New ||
                    artifact.State == RepositoryArtifactState.Modified ||
                    artifact.State == RepositoryArtifactState.Conflict);
        }

        /// <summary>
        /// Refreshes the selected/workspace artifact count label.
        /// </summary>
        private void UpdateSummary() {
            this._summary.Text = this.SelectedArtifacts.Count + " selected / " + this._artifacts.Count + " workspace artifacts";
        }

        /// <summary>
        /// Projection used by the comparison grid in the import view.
        /// </summary>
        private sealed class ImportRow {
            /// <summary>
            /// Initializes a new instance of the <see cref="ImportRow"/> class.
            /// </summary>
            /// <param name="artifact">Workspace artifact displayed by the row.</param>
            public ImportRow(RepositoryArtifact artifact) {
                this.Artifact = artifact.Item.Name;
                this.State = artifact.State.ToString();
                this.Path = artifact.RelativePath;
            }

            /// <summary>
            /// Gets the artifact name.
            /// </summary>
            public string Artifact {
                get; private set;
            }

            /// <summary>
            /// Gets the workspace comparison state.
            /// </summary>
            public string State {
                get; private set;
            }

            /// <summary>
            /// Gets the workspace-relative file path.
            /// </summary>
            public string Path {
                get; private set;
            }
        }
    }
}
