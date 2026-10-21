// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ProjectView.cs
// Displays TIA Portal instances, project selection, and session controls.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>
    /// Displays TIA Portal instances, project selection, and session controls.
    /// </summary>
    public sealed class ProjectView : UserControl {
        private readonly ListView _instances;
        private readonly ImageList _rowImages;
        private readonly Bitmap _rowSpacer;
        private readonly Label _emptySessions;
        private readonly CheckBox _startWithUi;
        private readonly TextBox _projectPath;
        private readonly InfoCard _projectInfo;
        private readonly WarningBanner _warning;
        private readonly ToolTip _toolTip;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectView"/> class.
        /// </summary>
        public ProjectView() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Background;
            this.Padding = new Padding(ThemeMetrics.Xl);
            this.AutoScroll = true;
            this._toolTip = new ToolTip();

            var root = new TableLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 5,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new PageHeader();
            header.SetText("Project", "Attach to TIA Portal, start an owned session, and choose the active project.");

            this._warning = new WarningBanner();

            var toolbar = new OperationToolbar();
            AddButton(toolbar, "Refresh instances", (s, e) => this.RefreshInstancesRequested?.Invoke(this, EventArgs.Empty));
            AddButton(toolbar, "Attach selected", (s, e) => this.AttachSelectedRequested?.Invoke(this, EventArgs.Empty), true);
            AddButton(toolbar, "Start new instance", (s, e) => this.StartPortalRequested?.Invoke(this, EventArgs.Empty));
            AddButton(toolbar, "Disconnect", (s, e) => this.DisconnectRequested?.Invoke(this, EventArgs.Empty));
            this._startWithUi = new ThemedCheckBox {
                Text = "Start TIA with UI",
                Checked = true,
                AutoSize = true,
                Margin = new Padding(10, 10, 0, 0),
            };
            toolbar.Controls.Add(this._startWithUi);

            this._instances = new ListView {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                HideSelection = false,
                BorderStyle = BorderStyle.None,
            };
            this._rowImages = new ImageList { ImageSize = new Size(1, 32), ColorDepth = ColorDepth.Depth32Bit };
            this._rowSpacer = new Bitmap(1, 32);
            this._rowImages.Images.Add(this._rowSpacer);

            this._instances.SmallImageList = this._rowImages;
            this._instances.Height = 176;
            this._instances.Dock = DockStyle.Top;
            this._instances.MultiSelect = false;
            this._instances.Columns.Add("TIA Portal session", 220);
            this._instances.Columns.Add("Project path", 620);
            this._instances.SizeChanged += (s, e) =>
                this._instances.Columns[1].Width = Math.Max(
                    200,
                    this._instances.ClientSize.Width - this._instances.Columns[0].Width);

            var sessions = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(16), BackColor = ThemeColors.Surface, Margin = new Padding(0, 12, 0, 16) };
            sessions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sessions.Controls.Add(new Label { Text = "Available sessions", Font = ThemeFonts.SectionTitle, AutoSize = true, Margin = new Padding(0, 0, 0, 12) });
            this._emptySessions = new Label {
                Text = "No TIA Portal sessions found.\r\nRefresh the list, or start a new instance using the controls above.",
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(16),
                BackColor = ThemeColors.SurfaceSecondary,
                ForeColor = ThemeColors.TextSecondary,
            };
            sessions.Controls.Add(this._emptySessions);
            sessions.Controls.Add(this._instances);
            this._instances.Visible = false;

            var bottomPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                AutoSize = true,
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottomPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var openProjectPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = ThemeColors.Surface,
                Padding = new Padding(12),
            };
            openProjectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            openProjectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            openProjectPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this._projectPath = new TextBox {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 7, 10, 0),
            };
            AppTheme.StyleInput(this._projectPath);
            this._projectPath.TextChanged += (s, e) => this._toolTip.SetToolTip(this._projectPath, this._projectPath.Text);
            var browseButton = new SecondaryButton {
                Text = "Browse",
                Anchor = AnchorStyles.None,
                AutoSize = false,
                Size = new Size(104, 40),
                Margin = new Padding(0, 0, 8, 0),
            };
            browseButton.Click += (s, e) => this.BrowseProject();
            var openButton = new PrimaryButton {
                Text = "Open project",
                Anchor = AnchorStyles.None,
                AutoSize = false,
                Size = new Size(148, 40),
                Margin = Padding.Empty,
            };
            openButton.Click += (s, e) => this.OpenProjectRequested?.Invoke(this, EventArgs.Empty);
            openProjectPanel.Controls.Add(this._projectPath, 0, 0);
            openProjectPanel.Controls.Add(browseButton, 1, 0);
            openProjectPanel.Controls.Add(openButton, 2, 0);

            this._projectInfo = new InfoCard { Dock = DockStyle.Fill };
            bottomPanel.Controls.Add(openProjectPanel, 0, 0);
            bottomPanel.Controls.Add(this._projectInfo, 0, 1);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(this._warning, 0, 1);
            root.Controls.Add(toolbar, 0, 2);
            root.Controls.Add(sessions, 0, 3);
            root.Controls.Add(bottomPanel, 0, 4);
            this.Controls.Add(root);
            AppTheme.ApplyThemeTree(this);
        }

        /// <summary>
        /// Occurs when the user requests refreshing running TIA Portal instances.
        /// </summary>
        public event EventHandler RefreshInstancesRequested;

        /// <summary>
        /// Occurs when the user requests attachment to the selected TIA Portal instance.
        /// </summary>
        public event EventHandler AttachSelectedRequested;

        /// <summary>
        /// Occurs when the user requests starting a new TIA Portal instance.
        /// </summary>
        public event EventHandler StartPortalRequested;

        /// <summary>
        /// Occurs when the user requests opening the selected project path.
        /// </summary>
        public event EventHandler OpenProjectRequested;

        /// <summary>
        /// Occurs when the user requests disconnecting from TIA Portal.
        /// </summary>
        public event EventHandler DisconnectRequested;

        /// <summary>
        /// Gets a value indicating whether a new TIA Portal instance should start with UI.
        /// </summary>
        public bool StartWithUi {
            get {
                return this._startWithUi.Checked;
            }
        }

        /// <summary>
        /// Gets the project path entered in the path selector.
        /// </summary>
        public string ProjectPath {
            get {
                return this._projectPath.Text;
            }
        }

        /// <summary>
        /// Gets the selected running TIA Portal instance index.
        /// </summary>
        public int? SelectedInstanceIndex {
            get {
                if (this._instances.SelectedItems.Count == 0) {
                    return null;
                }

                return this._instances.SelectedItems[0].Tag as int?;
            }
        }

        /// <summary>
        /// Updates project details and warnings from application state.
        /// </summary>
        /// <param name="state">Application state to display.</param>
        public void Bind(ApplicationState state) {
            var project = state.ActiveProject;
            if (project == null) {
                this._projectInfo.SetContent("Project", "No project adopted or opened");
            }
            else {
                this._projectInfo.SetContent(
                    "Project",
                    project.Name + "  ·  " + state.IntegrationVersion + "  ·  PLC: " + project.PlcTargets.Count + "  ·  HMI: " + project.HmiTargets.Count + "\r\n" + project.Path,
                    state.PortalOwnership.ToString(),
                    state.PortalOwnership == PortalOwnership.Owned ? ThemeColors.Info : ThemeColors.Success);
                if (string.IsNullOrWhiteSpace(this._projectPath.Text)) {
                    this._projectPath.Text = project.Path;
                }
            }

            this._warning.Visible = state.PortalInstances.Count > 1;
            this._warning.Text = state.PortalInstances.Count > 1
                ? "Multiple TIA Portal instances detected. Select the intended instance before attaching."
                : string.Empty;
        }

        /// <summary>
        /// Replaces the running TIA Portal instances shown in the list.
        /// </summary>
        /// <param name="instances">Instances to show.</param>
        public void ShowInstances(IEnumerable<TiaPortalInstanceInfo> instances) {
            this._instances.BeginUpdate();
            this._instances.Items.Clear();
            foreach (var instance in instances) {
                var item = new ListViewItem(instance.ProcessId.HasValue
                    ? "PID " + instance.ProcessId.Value
                    : instance.DisplayName);
                item.SubItems.Add(instance.ProjectPath ?? string.Empty);
                item.Tag = instance.InstanceIndex;
                this._instances.Items.Add(item);
            }

            this._instances.EndUpdate();
            this._emptySessions.Visible = this._instances.Items.Count == 0;
            this._instances.Visible = this._instances.Items.Count > 0;
            this._instances.Height = ThemeMetrics.Scale(this, Math.Min(240, 44 + (this._instances.Items.Count * 36)));
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) {
            if (disposing) {
                this._toolTip.Dispose();
                this._rowImages.Dispose();
                this._rowSpacer.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Adds one project command button to the provided panel.
        /// </summary>
        /// <param name="panel">Panel that receives the button.</param>
        /// <param name="text">Button text.</param>
        /// <param name="handler">Click handler.</param>
        /// <param name="primary">Whether the primary action style should be used.</param>
        private static void AddButton(FlowLayoutPanel panel, string text, EventHandler handler, bool primary = false) {
            ThemedButton button = primary ? (ThemedButton)new PrimaryButton() : new SecondaryButton();
            button.Text = text;
            button.Click += handler;
            panel.Controls.Add(button);
        }

        /// <summary>
        /// Opens a file picker for TIA project files and stores the selected path.
        /// </summary>
        private void BrowseProject() {
            using (var dlg = new OpenFileDialog()) {
                dlg.Title = "Select TIA project";
                dlg.Filter = "TIA Project (*.ap*)|*.ap*|All files (*.*)|*.*";
                if (System.IO.File.Exists(this._projectPath.Text)) {
                    dlg.FileName = this._projectPath.Text;
                }

                if (dlg.ShowDialog(this) == DialogResult.OK) {
                    this._projectPath.Text = dlg.FileName;
                }
            }
        }
    }
}
