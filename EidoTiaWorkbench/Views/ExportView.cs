// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportView.cs
// Displays scan results and export controls for PLC artifacts.
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
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>
    /// Displays scan results and export controls for PLC artifacts.
    /// </summary>
    public sealed class ExportView : UserControl {
        private readonly ArtifactFilterBar _filter;
        private readonly ArtifactTreeControl _tree;
        private readonly Label _selectionSummary;
        private readonly Label _inventorySummary;
        private readonly Label _detailsTitle;
        private readonly Label _details;
        private readonly ComboBox _profile;
        private readonly CheckBox _includeDbs;
        private readonly CheckBox _normalizeXml;
        private readonly CheckBox _preserveHmiXml;
        private readonly CheckBox _incremental;
        private readonly SecondaryButton _retryIncomplete;
        private readonly SecondaryButton _exportChanged;
        private readonly SecondaryButton _exportAll;
        private readonly PrimaryButton _exportSelected;
        private List<ExportItem> _items = new List<ExportItem>();
        private List<ExportItem> _visibleItems = new List<ExportItem>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportView"/> class.
        /// </summary>
        public ExportView() {
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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));

            var header = new PageHeader();
            header.SetText("Export", "Scan PLC and HMI artifacts, filter selection, and write structured XML payloads.");

            this._inventorySummary = new Label {
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextPrimary,
                BackColor = ThemeColors.SurfaceSecondary,
                Padding = new Padding(16, 12, 16, 12),
                Margin = new Padding(0, 8, 0, 12),
            };
            var toolbar = new OperationToolbar();
            AddButton(toolbar, "Refresh", (sender, e) => this.RefreshRequested?.Invoke(this, EventArgs.Empty));
            AddButton(
                toolbar,
                "Select all",
                (sender, e) => {
                    this._tree.SelectAll();
                    this.UpdateSelectionSummary();
                });
            AddButton(
                toolbar,
                "Clear selection",
                (sender, e) => {
                    this._tree.ClearSelection();
                    this.UpdateSelectionSummary();
                });

            // The export profile names are short; this preferred width keeps the wrapping toolbar compact.
            this._profile = new ThemedComboBox { Width = 126, Margin = new Padding(8, 4, 8, 0) };
            this._profile.DataSource = Enum.GetValues(typeof(ExportProfile));
            this._profile.SelectedItem = ExportProfile.Core;
            this._includeDbs = new ThemedCheckBox { Text = "Include DBs", AutoSize = true, Margin = new Padding(4, 10, 8, 0) };
            this._normalizeXml = new ThemedCheckBox { Text = "Normalize PLC XML", Checked = true, AutoSize = true, Margin = new Padding(4, 10, 8, 0) };
            this._preserveHmiXml = new ThemedCheckBox { Text = "Preserve HMI XML", Checked = true, AutoSize = true, Margin = new Padding(4, 10, 8, 0) };
            this._incremental = new ThemedCheckBox { Text = "Incremental", Checked = true, AutoSize = true, Margin = new Padding(4, 10, 8, 0) };
            toolbar.Controls.Add(this._profile);
            var options = new OperationToolbar { Visible = false, Margin = Padding.Empty };
            options.Controls.Add(this._includeDbs);
            options.Controls.Add(this._normalizeXml);
            options.Controls.Add(this._preserveHmiXml);
            options.Controls.Add(this._incremental);
            var optionsButton = new SecondaryButton { Text = "Export options", AccessibleDescription = "Show or hide XML, DB and incremental export settings." };
            optionsButton.Click += (sender, e) => {
                options.Visible = !options.Visible;
                optionsButton.Text = options.Visible ? "Hide options" : "Export options";
            };
            toolbar.Controls.Add(optionsButton);
            toolbar.SetFlowBreak(optionsButton, true);
            toolbar.Controls.Add(options);
            toolbar.SizeChanged += (sender, e) => options.MaximumSize = new Size(Math.Max(300, toolbar.ClientSize.Width - 24), 0);

            this._filter = new ArtifactFilterBar();
            this._filter.FilterChanged += (s, e) => this.ApplyFilter();

            var body = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
            };
            body.SizeChanged += (s, e) => {
                if (body.IsHandleCreated) {
                    body.BeginInvoke(new Action(() => {
                        if (!body.IsDisposed) {
                            TryInitializeBodySplitter(body);
                        }
                    }));
                }
            };
            body.HandleCreated += (s, e) => body.BeginInvoke(new Action(() => TryInitializeBodySplitter(body)));
            var left = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = ThemeColors.InputSurface,
            };
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this._tree = new ArtifactTreeControl();
            this._tree.SelectedItemChanged += (s, e) => this.UpdateDetails();
            this._tree.SelectionChanged += (s, e) => this.UpdateSelectionSummary();
            left.Controls.Add(this._filter, 0, 0);
            left.Controls.Add(this._tree, 0, 1);

            var detailsPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = ThemeColors.Surface,
                Padding = new Padding(14),
            };
            detailsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detailsPanel.AutoScroll = true;
            this._detailsTitle = new Label {
                Text = "Artifact details",
                Font = ThemeFonts.SectionTitle,
                ForeColor = ThemeColors.TextPrimary,
                Dock = DockStyle.Top,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8),
            };
            this._details = new Label {
                Dock = DockStyle.Top,
                AutoSize = true,
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextSecondary,
            };
            detailsPanel.SizeChanged += (s, e) => this._details.MaximumSize = new Size(Math.Max(180, detailsPanel.ClientSize.Width - 48), 0);
            detailsPanel.Controls.Add(this._detailsTitle, 0, 0);
            detailsPanel.Controls.Add(this._details, 0, 1);

            body.Panel1.Controls.Add(left);
            body.Panel2.Controls.Add(detailsPanel);

            var bottom = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = ThemeColors.Surface,
                Margin = new Padding(0, 10, 0, 0),
                Padding = new Padding(10, 8, 10, 8),
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, FlowDirection = FlowDirection.RightToLeft };
            var toolTip = new ToolTip();
            this.Disposed += (sender, e) => toolTip.Dispose();
            this._retryIncomplete = new SecondaryButton { Text = "Retry incomplete" };
            this._exportChanged = new SecondaryButton { Text = "Export changed" };
            this._exportAll = new SecondaryButton { Text = "Export all" };
            this._exportSelected = new PrimaryButton { Text = "Export selected" };
            toolTip.SetToolTip(this._retryIncomplete, "Retry failed or aborted artifacts from the previous manifest.");
            toolTip.SetToolTip(this._exportChanged, "Export only artifacts that differ from the manifest.");
            toolTip.SetToolTip(this._exportAll, "Export every scanned PLC and HMI artifact.");
            toolTip.SetToolTip(this._exportSelected, "Export only the checked artifacts.");
            this._retryIncomplete.Click += (s, e) => this.RetryIncompleteRequested?.Invoke(this, EventArgs.Empty);
            this._exportSelected.Click += (s, e) => this.ExportSelectedRequested?.Invoke(this, EventArgs.Empty);
            this._exportAll.Click += (s, e) => this.ExportAllRequested?.Invoke(this, EventArgs.Empty);
            this._exportChanged.Click += (s, e) => this.ExportChangedRequested?.Invoke(this, EventArgs.Empty);
            this._selectionSummary = new Label {
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ThemeColors.TextSecondary,
                Margin = new Padding(0, 0, 8, 0),
            };
            bottom.Controls.Add(this._selectionSummary, 0, 0);
            actions.Controls.Add(this._exportSelected);
            actions.Controls.Add(this._exportAll);
            actions.Controls.Add(this._exportChanged);
            actions.Controls.Add(this._retryIncomplete);
            bottom.Controls.Add(actions, 0, 1);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(this._inventorySummary, 0, 1);
            root.Controls.Add(toolbar, 0, 2);
            root.Controls.Add(body, 0, 3);
            root.Controls.Add(bottom, 0, 4);
            this.Controls.Add(root);
            AppTheme.ApplyThemeTree(this);
            this.UpdateSelectionSummary();
            this.UpdateDetails();
        }

        /// <summary>
        /// Occurs when the user requests a fresh scan.
        /// </summary>
        public event EventHandler RefreshRequested;

        /// <summary>
        /// Occurs when the user requests exporting checked artifacts.
        /// </summary>
        public event EventHandler ExportSelectedRequested;

        /// <summary>
        /// Occurs when the user requests exporting every scanned artifact.
        /// </summary>
        public event EventHandler ExportAllRequested;

        /// <summary>
        /// Occurs when the user requests incremental export of changed artifacts.
        /// </summary>
        public event EventHandler ExportChangedRequested;

        /// <summary>
        /// Occurs when the user requests retrying failed or aborted artifacts from the previous manifest.
        /// </summary>
        public event EventHandler RetryIncompleteRequested;

        /// <summary>
        /// Gets the selected export profile.
        /// </summary>
        public ExportProfile Profile {
            get {
                return (ExportProfile)this._profile.SelectedItem;
            }
        }

        /// <summary>
        /// Gets a value indicating whether DB artifacts should be included in scans.
        /// </summary>
        public bool IncludeDbs {
            get {
                return this._includeDbs.Checked;
            }
        }

        /// <summary>
        /// Gets a value indicating whether exported XML should be normalized.
        /// </summary>
        public bool NormalizeXml {
            get {
                return this._normalizeXml.Checked;
            }
        }

        /// <summary>
        /// Gets a value indicating whether HMI screen XML should be preserved exactly as Openness exports it.
        /// </summary>
        public bool PreserveHmiXml {
            get {
                return this._preserveHmiXml.Checked;
            }
        }

        /// <summary>
        /// Gets a value indicating whether exports should use manifest-based incremental mode.
        /// </summary>
        public bool Incremental {
            get {
                return this._incremental.Checked;
            }
        }

        /// <summary>
        /// Gets checked artifacts from the tree.
        /// </summary>
        public List<ExportItem> SelectedItems {
            get {
                return this._tree.GetCheckedItems();
            }
        }

        /// <summary>
        /// Gets all scanned artifacts currently held by the view.
        /// </summary>
        public List<ExportItem> AllItems {
            get {
                return this._items.ToList();
            }
        }

        /// <summary>
        /// Binds scan results from application state.
        /// </summary>
        /// <param name="state">Application state containing the latest scan.</param>
        public void Bind(ApplicationState state) {
            this.SetArtifacts(state.LastScan);
        }

        /// <summary>
        /// Replaces the artifacts displayed by the view.
        /// </summary>
        /// <param name="items">Artifacts to display.</param>
        public void SetArtifacts(IEnumerable<ExportItem> items) {
            var next = (items ?? Enumerable.Empty<ExportItem>()).ToList();
            if (this._items.SequenceEqual(next)) {
                this.UpdateSelectionSummary();
                return;
            }

            this._items = next;
            this.ApplyFilter();
        }

        /// <summary>
        /// Adds one export command button to the provided panel.
        /// </summary>
        /// <param name="panel">Panel that receives the button.</param>
        /// <param name="text">Button text.</param>
        /// <param name="handler">Click handler.</param>
        private static void AddButton(FlowLayoutPanel panel, string text, EventHandler handler) {
            var button = new SecondaryButton { Text = text };
            button.Click += handler;
            panel.Controls.Add(button);
        }

        /// <summary>
        /// Sets the initial export/details split only after WinForms has assigned a usable width.
        /// </summary>
        /// <param name="body">Split container that hosts the artifact tree and details inspector.</param>
        /// <returns><see langword="true"/> when the splitter was initialized.</returns>
        private static bool TryInitializeBodySplitter(SplitContainer body) {
            var treeMinimum = ThemeMetrics.Scale(body, 320);
            var inspectorMinimum = ThemeMetrics.Scale(body, 260);

            var width = body.Width;
            if (width <= treeMinimum + inspectorMinimum) {
                return false;
            }

            var inspectorWidth = Math.Min(ThemeMetrics.Scale(body, 360), Math.Max(ThemeMetrics.Scale(body, 300), width / 4));
            var maxDistance = width - inspectorMinimum;
            var distance = Math.Min(maxDistance, Math.Max(treeMinimum, width - inspectorWidth));
            if (distance < treeMinimum || distance > maxDistance) {
                return false;
            }

            try {
                body.Panel1MinSize = treeMinimum;
                body.Panel2MinSize = inspectorMinimum;
                body.SplitterDistance = Math.Min(distance, width - inspectorMinimum - body.SplitterWidth);
                return true;
            }
            catch (InvalidOperationException) {
                return false;
            }
        }

        /// <summary>
        /// Applies current scope, kind, target, and text filters to the artifact tree.
        /// </summary>
        private void ApplyFilter() {
            IEnumerable<ExportItem> filtered = this._items;
            if (this._filter.Scope == "HMI") {
                filtered = filtered.Where(i => i.IsHmi);
            }
            else if (this._filter.Scope == "PLC") {
                filtered = filtered.Where(i => i.IsPlc);
            }

            if (this._filter.Kind.HasValue) {
                filtered = filtered.Where(i => i.Kind == this._filter.Kind.Value);
            }

            if (!string.IsNullOrWhiteSpace(this._filter.DeviceText)) {
                filtered = filtered.Where(i => i.CpuPath.IndexOf(this._filter.DeviceText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrWhiteSpace(this._filter.SearchText)) {
                var search = this._filter.SearchText;
                filtered = filtered.Where(i =>
                    i.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    i.GroupPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    i.LogicalKey.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            this._visibleItems = filtered.ToList();
            this._tree.SetItems(this._visibleItems);
            this.UpdateSelectionSummary();
        }

        /// <summary>
        /// Refreshes the selected/scanned artifact count label.
        /// </summary>
        private void UpdateSelectionSummary() {
            var selected = this._tree.GetCheckedItems().Count;
            this._inventorySummary.Text = this._items.Count + " artifacts    ·    " + selected + " selected    ·    " + this._visibleItems.Count(i => i.IsPlc) + " PLC / " + this._visibleItems.Count(i => i.IsHmi) + " HMI";
            this._selectionSummary.Text = selected + " selected / " + this._items.Count + " scanned / " + this._visibleItems.Count + " visible";
            this.UpdateCommandStates(selected);
            if (this._tree.SelectedItem == null) {
                this.UpdateDetails();
            }
        }

        /// <summary>
        /// Updates export action availability from the current scan and selection.
        /// </summary>
        /// <param name="selectedCount">Number of checked artifacts.</param>
        private void UpdateCommandStates(int selectedCount) {
            var hasScan = this._items.Count > 0;
            this._retryIncomplete.Enabled = hasScan;
            this._exportChanged.Enabled = hasScan;
            this._exportAll.Enabled = hasScan;
            this._exportSelected.Enabled = selectedCount > 0;
        }

        /// <summary>
        /// Shows detailed information for the selected export artifact.
        /// </summary>
        private void UpdateDetails() {
            var item = this._tree.SelectedItem;
            if (item == null) {
                this._detailsTitle.Text = "Export overview";
                var selected = this._tree.GetCheckedItems().Count;
                var plc = this._visibleItems.Count(i => i.IsPlc);
                var hmi = this._visibleItems.Count(i => i.IsHmi);
                var exportable = this._visibleItems.Count(i => i.CanExport);
                var unavailable = this._visibleItems.Count - exportable;
                this._details.Text =
                    "Selection: " + selected + " checked artifact(s)" + Environment.NewLine +
                    "Visible inventory: " + this._visibleItems.Count + " artifact(s)" + Environment.NewLine +
                    "PLC / HMI: " + plc + " / " + hmi + Environment.NewLine +
                    "Exportable: " + exportable + Environment.NewLine +
                    "Restricted or unavailable: " + unavailable + Environment.NewLine +
                    Environment.NewLine +
                    "Workspace: configured from Settings" + Environment.NewLine +
                    "Output path: calculated when the export runs" + Environment.NewLine +
                    "Details: select an artifact in the tree.";
                return;
            }

            this._detailsTitle.Text = "Artifact details";
            this._details.Text =
                item.Name + Environment.NewLine + Environment.NewLine +
                "TYPE   " + item.Kind + Environment.NewLine +
                "Target: " + item.CpuPath + Environment.NewLine +
                (string.IsNullOrWhiteSpace(item.TargetType) ? string.Empty : "Target type: " + item.TargetType + Environment.NewLine) +
                Environment.NewLine + "LOCATION" + Environment.NewLine + item.GroupPath + Environment.NewLine +
                Environment.NewLine + "IDENTIFIER" + Environment.NewLine + item.LogicalKey + Environment.NewLine +
                Environment.NewLine + "AVAILABILITY" + Environment.NewLine +
                (item.CanExport ? "Ready for export" : "Export unavailable") + Environment.NewLine +
                (string.IsNullOrWhiteSpace(item.UnavailableReason) ? string.Empty : "Warning: " + item.UnavailableReason);
            if (item.Metadata.Count > 0) {
                this._details.Text += Environment.NewLine + Environment.NewLine + "METADATA" + Environment.NewLine + string.Join(Environment.NewLine, item.Metadata.Select(pair => pair.Key + ": " + pair.Value));
            }
        }
    }
}
