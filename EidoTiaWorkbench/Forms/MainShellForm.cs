// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// MainShellForm.cs
// Main desktop shell that hosts navigation, views, status, activity drawer, and global operation chrome.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Presentation;
using EidoTiaWorkbench.Theme;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.Forms {
    /// <summary>
    /// Main desktop shell that hosts navigation, views, status, activity drawer, and global operation chrome.
    /// </summary>
    public sealed class MainShellForm : Form {
        private readonly ApplicationState _state;
        private readonly MainController _controller;
        private readonly CommandStateService _commands;
        private readonly ApplicationHeader _header;
        private readonly SidebarNavigation _sidebar;
        private readonly Panel _viewHost;
        private readonly OperationOverlay _overlay;
        private readonly ActivityLogControl _activityDrawer;
        private readonly SplitContainer _contentSplit;
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly ToolStripStatusLabel _repositoryLabel;
        private readonly Timer _refreshTimer = new Timer { Interval = 100 };
        private readonly ToastNotification _toast;
        private readonly Dictionary<string, UserControl> _views = new Dictionary<string, UserControl>(
            StringComparer.OrdinalIgnoreCase);

        private readonly List<object> _presenters = new List<object>();
        private readonly Dictionary<Control, bool> _busyControls = new Dictionary<Control, bool>();
        private readonly bool _discoverOnLoad;
        private bool _refreshPending;
        private string _currentViewKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainShellForm"/> class.
        /// </summary>
        /// <param name="discoverOnLoad">
        /// Whether to enumerate TIA instances on startup; false keeps visual probes offline.
        /// </param>
        public MainShellForm(bool discoverOnLoad = true) {
            this._discoverOnLoad = discoverOnLoad;
            AppTheme.ApplyTo(this);
            this.Text = ProductInfo.Name;
            this.ClientSize = new Size(1280, 800);

            // The shell minimum protects the fixed sidebar plus usable central content width.
            this.MinimumSize = new Size(800, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);

            this._state = new ApplicationState();
            this._controller = new MainController(this._state);
            this._commands = new CommandStateService(this._state);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            // Fixed shell chrome rows reserve enough logical height for DPI-scaled header/status content.
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));

            this._header = new ApplicationHeader();
            this._header.ActivityToggleRequested += (s, e) => this.ToggleActivityDrawer();
            this._header.ThemeToggleRequested += (s, e) => this.ApplyThemePreference(
                ThemeColors.UseDarkPalette ? ThemePreference.Light : ThemePreference.Dark);

            var body = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
            };

            // The sidebar keeps a stable navigation target width; the form minimum protects content width.
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this._sidebar = new SidebarNavigation();
            this._sidebar.AddMainItem("Dashboard", "Dashboard", "dashboard");
            this._sidebar.AddMainItem("Project", "Project", "project");
            this._sidebar.AddMainItem("Export", "Export", "export");
            this._sidebar.AddMainItem("Import", "Import", "import");
            this._sidebar.AddMainItem("Validation", "Validation", "success");
            this._sidebar.AddMainItem("Activity", "Activity", "activity");
            this._sidebar.AddMainItem("Settings", "Settings", "settings");
            this._sidebar.AddBottomItem("About", "About", "success");
            this._sidebar.AddBottomItem("Exit", "Exit", "error");
            this._sidebar.NavigationRequested += this.Sidebar_NavigationRequested;

            this._contentSplit = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Panel2Collapsed = true,
                SplitterDistance = 520,
            };

            var viewSurface = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            viewSurface.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            viewSurface.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            viewSurface.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            this._overlay = new OperationOverlay();
            this._overlay.CancelRequested += (s, e) => this._controller.CancelCurrentOperation();
            this._viewHost = new Panel {
                Dock = DockStyle.Fill,
                BackColor = ThemeColors.Background,
            };
            this._toast = new ToastNotification();
            viewSurface.Controls.Add(this._overlay, 0, 0);
            viewSurface.Controls.Add(this._viewHost, 0, 1);
            this._contentSplit.Panel1.Controls.Add(viewSurface);

            this._activityDrawer = new ActivityLogControl();
            this._activityDrawer.SetLogFilePath(this._controller.LogFilePath);
            this._activityDrawer.OpenLogRequested += (s, e) => this._controller.OpenLogFile();
            this._activityDrawer.ClearViewRequested += (s, e) => this._controller.ClearActivityView();
            this._contentSplit.Panel2.Controls.Add(this._activityDrawer);

            body.Controls.Add(this._sidebar, 0, 0);
            body.Controls.Add(this._contentSplit, 1, 0);

            this._statusStrip = new StatusStrip {
                Dock = DockStyle.Fill,
                SizingGrip = false,
                Font = ThemeFonts.Secondary,
                BackColor = ThemeColors.Surface,
            };
            this._statusLabel = new ToolStripStatusLabel(
                "Ready") {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this._repositoryLabel = new ToolStripStatusLabel(string.Empty);
            this._statusStrip.Items.Add(this._statusLabel);
            this._statusStrip.Items.Add(this._repositoryLabel);
            var diagnostics = new ToolStripStatusLabel(
                "Activity") {
                IsLink = true,
                LinkColor = ThemeColors.Primary,
                ActiveLinkColor = ThemeColors.PrimaryHover,
                Margin = new Padding(16, 0, 8, 0),
            };
            diagnostics.Click += (sender, e) => this.ToggleActivityDrawer();
            this._statusStrip.Items.Add(diagnostics);

            root.Controls.Add(this._header, 0, 0);
            root.Controls.Add(body, 0, 1);
            root.Controls.Add(this._statusStrip, 0, 2);
            this.Controls.Add(root);

            this.CreateViews();
            this.WireState();
            this.Navigate("Dashboard");
            this.UpdateShellFromState();
            AppTheme.ApplyThemeTree(this);
        }

        /// <summary>
        /// Gets the local file used to persist shell window placement.
        /// </summary>
        private string PlacementFilePath {
            get {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ProductInfo.DataFolderName,
                    "window.txt");
            }
        }

        /// <inheritdoc />
        protected override void OnDpiChanged(DpiChangedEventArgs e) {
            base.OnDpiChanged(e);
            AppTheme.ApplyThemeTree(this);
        }

        /// <summary>
        /// Restores persisted window placement and starts initial portal discovery.
        /// </summary>
        /// <param name="e">Event data for the load event.</param>
        protected override void OnLoad(EventArgs e) {
            base.OnLoad(e);
            if (this._discoverOnLoad) {
                this.RestoreWindowPlacement();
            }

            this._activityDrawer.Bind(this._state.Activity);
            if (this._discoverOnLoad) {
                this.FireStartupRefresh();
            }
        }

        /// <summary>
        /// Persists window placement before the form closes.
        /// </summary>
        /// <param name="e">Event data for the form closing event.</param>
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if (this._state.CurrentOperation.IsBusy) {
                this._controller.CancelCurrentOperation();
                e.Cancel = true;
                this._statusLabel.Text = "Cancellation requested. Wait for the current Siemens call to finish before closing.";
            }
            else {
                this.SaveWindowPlacement();
            }

            base.OnFormClosing(e);
        }

        /// <summary>
        /// Releases controller resources owned by the shell.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be released.</param>
        protected override void Dispose(bool disposing) {
            if (disposing) {
                this._refreshTimer.Dispose();
                this._controller.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Determines whether restored bounds overlap any currently available screen.
        /// </summary>
        /// <param name="bounds">Window bounds to validate.</param>
        /// <returns>
        /// <see langword="true"/> when the bounds are visible on a screen; otherwise <see langword="false"/>.
        /// </returns>
        private static bool IntersectsAnyScreen(Rectangle bounds) {
            foreach (var screen in Screen.AllScreens) {
                if (screen.WorkingArea.IntersectsWith(bounds)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Clamps restored bounds to the working area while preserving the minimum shell size.
        /// </summary>
        /// <param name="bounds">Saved or scaled window bounds.</param>
        /// <param name="minimumSize">Minimum allowed window size.</param>
        /// <returns>Bounds guaranteed to fit the corresponding screen working area.</returns>
        private static Rectangle ClampToWorkingArea(Rectangle bounds, Size minimumSize) {
            var screen = Screen.FromRectangle(bounds);
            var area = screen.WorkingArea;
            var width = Math.Max(minimumSize.Width, Math.Min(bounds.Width, area.Width));
            var height = Math.Max(minimumSize.Height, Math.Min(bounds.Height, area.Height));
            var x = width >= area.Width ? area.Left : Math.Min(Math.Max(bounds.X, area.Left), area.Right - width);
            var y = height >= area.Height ? area.Top : Math.Min(Math.Max(bounds.Y, area.Top), area.Bottom - height);
            return new Rectangle(x, y, width, height);
        }

        /// <summary>Locates the hosted page heading so its context can move into the shell.</summary>
        /// <param name="root">Page subtree to inspect.</param>
        /// <returns>The first page header, or null for pages without a heading.</returns>
        private static PageHeader FindPageHeader(Control root) {
            foreach (Control child in root.Controls) {
                var header = child as PageHeader ?? FindPageHeader(child);
                if (header != null) {
                    return header;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the current form DPI used to scale legacy restored bounds.
        /// </summary>
        /// <returns>The current horizontal DPI for the form.</returns>
        private float GetCurrentDpi() {
            using (var g = this.CreateGraphics()) {
                return g.DpiX;
            }
        }

        /// <summary>
        /// Creates view instances and presenters owned by the shell.
        /// </summary>
        private void CreateViews() {
            var dashboard = new DashboardView();
            var project = new ProjectView();
            var export = new ExportView();
            var import = new ImportView();
            var activity = new ActivityView();
            var settings = new SettingsView();
            settings.Configure(this._controller);
            settings.RepositoryChanged += (s, e) => this._controller.SetSelectedRepository(settings.RepositoryPath);
            settings.ThemePreferenceChanged += (s, e) => this.ApplyThemePreference(settings.ThemePreference);

            this._views["Dashboard"] = dashboard;
            this._views["Project"] = project;
            this._views["Export"] = export;
            this._views["Import"] = import;
            this._views["Activity"] = activity;
            this._views["Settings"] = settings;
            this._views["Validation"] = new ValidationView();

            this._presenters.Add(new DashboardPresenter(dashboard, this._controller, this._state, this.Navigate));
            this._presenters.Add(new ProjectPresenter(project, this._controller, this._state));
            this._presenters.Add(new ExportPresenter(export, this._controller, this._state));
            this._presenters.Add(new ImportPresenter(import, this._controller, this._state));
            this._presenters.Add(new ActivityPresenter(activity, this._controller, this._state));

            this._state.ActivityAdded += (s, e) => this._activityDrawer.AddEntry(e.Entry);
        }

        /// <summary>
        /// Starts best-effort portal discovery after the shell loads.
        /// </summary>
        private async void FireStartupRefresh() {
            try {
                await this._controller.RefreshPortalInstancesAsync();
                this.RefreshCurrentView();
            }
            catch (Exception ex) {
                this._statusLabel.Text = "Openness discovery: " + ex.Message;
            }
        }

        /// <summary>
        /// Connects application state changes to shell refreshes.
        /// </summary>
        private void WireState() {
            this._refreshTimer.Tick += (s, e) => {
                if (this._refreshPending) {
                    this._refreshPending = false;
                    this.UpdateShellFromState();
                }
            };
            this._refreshTimer.Start();
            this._state.StateChanged += (s, e) => {
                if (this.InvokeRequired) {
                    this.BeginInvoke(new Action(this.UpdateShellFromState));
                    return;
                }

                if (this._state.CurrentOperation.IsBusy) {
                    this._refreshPending = true;
                }
                else {
                    this._refreshPending = false;
                    this.UpdateShellFromState();
                }
            };
        }

        /// <summary>
        /// Refreshes shell chrome, status bar, overlay, and current view from state.
        /// </summary>
        private void UpdateShellFromState() {
            this._header.Bind(this._state);
            this.Text = ProductInfo.Name + " | " + (this._controller.SelectedInstallation == null
                ? "Offline"
                : "TIA V" + this._controller.SelectedInstallation.MajorVersion);
            this._overlay.Bind(this._state.CurrentOperation);
            if (!this._contentSplit.Panel2Collapsed) {
                this._activityDrawer.Bind(this._state.Activity);
            }

            this.UseWaitCursor = false;
            if (!this._state.CurrentOperation.IsBusy) {
                foreach (var pair in this._busyControls) {
                    if (!pair.Key.IsDisposed) {
                        pair.Key.Enabled = pair.Value;
                    }
                }

                this._busyControls.Clear();
            }

            this._statusLabel.Text = this._state.CurrentOperation.IsBusy
                ? (string.IsNullOrWhiteSpace(
                    this._state.CurrentOperation.StatusText)
                        ? this._state.CurrentOperation.Name
                        : this._state.CurrentOperation.StatusText) + " " + this._state.CurrentOperation.CurrentArtifact
                : (this._state.LastOperationResult ?? "Ready");
            this._statusLabel.ToolTipText = this._statusLabel.Text;
            if (this._statusLabel.Text.Length > 120) {
                this._statusLabel.Text = this._statusLabel.Text.Substring(0, 117) + "…";
            }

            this._repositoryLabel.Text = string.IsNullOrWhiteSpace(this._state.SelectedRepository)
                ? this._state.IntegrationVersion
                : this._state.IntegrationVersion + " | " + this._state.PortalConnectionState;
            this.RefreshCurrentView();
        }

        /// <summary>
        /// Rebinds the currently active view from application state.
        /// </summary>
        private void RefreshCurrentView() {
            if (string.IsNullOrWhiteSpace(this._currentViewKey)) {
                return;
            }

            if (this._currentViewKey == "Validation") {
                ((ValidationView)this._views[this._currentViewKey]).Bind(this._state);
            }
            else if (this._currentViewKey == "Dashboard") {
                ((DashboardView)this._views[this._currentViewKey]).Bind(this._state);
            }
            else if (this._currentViewKey == "Project") {
                var view = (ProjectView)this._views[this._currentViewKey];
                view.Bind(this._state);
                view.ShowInstances(this._state.PortalInstances);
            }
            else if (this._currentViewKey == "Export") {
                ((ExportView)this._views[this._currentViewKey]).Bind(this._state);
            }
            else if (this._currentViewKey == "Import") {
                ((ImportView)this._views[this._currentViewKey]).Bind(this._state);
            }
            else if (this._currentViewKey == "Activity") {
                ((ActivityView)this._views[this._currentViewKey]).Bind(this._state);
            }
            else if (this._currentViewKey == "Settings") {
                ((SettingsView)this._views[this._currentViewKey]).Bind(this._state, this._controller.LogFilePath);
            }

            this.ApplyCommandAvailability(this._views[this._currentViewKey]);
        }

        /// <summary>Applies current command availability to all buttons in a view.</summary>
        /// <param name="root">Visible control subtree to update on the UI thread.</param>
        private void ApplyCommandAvailability(Control root) {
            if (root is ActivityView || root is ActivityLogControl) {
                return;
            }

            foreach (Control child in root.Controls) {
                var button = child as Button;
                if (button != null) {
                    switch (button.Text) {
                        case "Attach to TIA Portal":
                        case "Attach selected":
                        case "Start new instance":
                        case "Refresh instances":
                            button.Enabled = this._commands.CanAttachPortal;
                            break;
                        case "Scan project":
                        case "Refresh":
                            button.Enabled = this._commands.CanScanProject;
                            break;
                        case "Open project":
                            button.Enabled = this._state.HasOpennessAdapter && !this._state.CurrentOperation.IsBusy;
                            break;
                        case "Export changed artifacts":
                            button.Enabled = this._commands.CanExport;
                            break;
                        case "REAL IMPORT":
                            button.Enabled = this._commands.CanImport;
                            break;
                    }
                }

                if (this._state.CurrentOperation.IsBusy && (child is ButtonBase || child is ComboBox)) {
                    if (!this._busyControls.ContainsKey(child)) {
                        this._busyControls.Add(child, child.Enabled);
                    }

                    child.Enabled = false;
                }

                this.ApplyCommandAvailability(child);
            }
        }

        private void Sidebar_NavigationRequested(object sender, NavigationEventArgs e) {
            if (e.Key == "About") {
                using (var about = new AboutForm()) {
                    about.ShowDialog(this);
                }

                return;
            }

            if (e.Key == "Exit") {
                this.Close();
                return;
            }

            this.Navigate(e.Key);
        }

        /// <summary>
        /// Shows the view registered for the requested navigation key.
        /// </summary>
        /// <param name="key">Navigation key to activate.</param>
        private void Navigate(string key) {
            if (!this._views.ContainsKey(key)) {
                return;
            }

            this._currentViewKey = key;

            this._sidebar.SetActive(key);
            this._viewHost.Controls.Clear();
            var view = this._views[key];
            var pageHeader = FindPageHeader(view);
            this._header.SetSection(key, pageHeader?.DescriptionText ?? string.Empty);
            if (pageHeader != null) {
                pageHeader.Visible = false;
            }

            this._header.Bind(this._state);
            view.Dock = DockStyle.Fill;
            this._viewHost.Controls.Add(view);
            AppTheme.ApplyThemeTree(view);
            this.RefreshCurrentView();
        }

        /// <summary>
        /// Opens or closes the right-hand activity drawer.
        /// </summary>
        private void ToggleActivityDrawer() {
            this._contentSplit.Panel2Collapsed = !this._contentSplit.Panel2Collapsed;
            if (!this._contentSplit.Panel2Collapsed) {
                var distance = Math.Max(280, this._contentSplit.Height - 260);
                if (distance < this._contentSplit.Height) {
                    this._contentSplit.SplitterDistance = distance;
                }
            }
        }

        /// <summary>
        /// Applies a newly selected theme preference to the existing shell.
        /// </summary>
        /// <param name="preference">Theme preference selected by the user.</param>
        private void ApplyThemePreference(ThemePreference preference) {
            var previous = ThemeColors.Snapshot();
            AppTheme.ChangePreference(this, preference);

            // Cached pages outside the visible tree must switch palettes too.
            foreach (var view in this._views.Values) {
                if (view.Parent == null) {
                    AppTheme.ApplyThemeTree(view, previous);
                }
            }

            this._viewHost.BackColor = ThemeColors.Background;
            this._statusStrip.BackColor = ThemeColors.Surface;
            this._statusLabel.ForeColor = ThemeColors.TextSecondary;
            this._repositoryLabel.ForeColor = ThemeColors.TextSecondary;
            this._header.Bind(this._state);
            this.Text = ProductInfo.Name + " | " + (this._controller.SelectedInstallation == null
                ? "Offline"
                : "TIA V" + this._controller.SelectedInstallation.MajorVersion);
            this._sidebar.SetActive(this._currentViewKey);
            this.RefreshCurrentView();
        }

        /// <summary>
        /// Saves the current window placement for the next application launch.
        /// </summary>
        private void SaveWindowPlacement() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(this.PlacementFilePath) ?? ".");
                var bounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;
                if (bounds.Width < this.MinimumSize.Width ||
                    bounds.Height < this.MinimumSize.Height ||
                    !IntersectsAnyScreen(bounds)) {
                    return;
                }

                var values = new[]
                {
                    bounds.X.ToString(CultureInfo.InvariantCulture),
                    bounds.Y.ToString(CultureInfo.InvariantCulture),
                    bounds.Width.ToString(CultureInfo.InvariantCulture),
                    bounds.Height.ToString(CultureInfo.InvariantCulture),
                    (this.WindowState == FormWindowState.Maximized).ToString(CultureInfo.InvariantCulture),
                    this.GetCurrentDpi().ToString(CultureInfo.InvariantCulture),
                };
                var text = string.Join("|", values);
                File.WriteAllText(this.PlacementFilePath, text);
            }
            catch {
                // Window placement should never block shutdown.
            }
        }

        /// <summary>
        /// Restores the persisted window placement when it still fits the available screens.
        /// </summary>
        private void RestoreWindowPlacement() {
            try {
                if (!File.Exists(this.PlacementFilePath)) {
                    return;
                }

                var parts = File.ReadAllText(this.PlacementFilePath).Split('|');
                if (parts.Length < 5) {
                    return;
                }

                var bounds = new Rectangle(
                    int.Parse(parts[0], CultureInfo.InvariantCulture),
                    int.Parse(parts[1], CultureInfo.InvariantCulture),
                    int.Parse(parts[2], CultureInfo.InvariantCulture),
                    int.Parse(parts[3], CultureInfo.InvariantCulture));

                if (parts.Length >= 6) {
                    float savedDpi;
                    if (float.TryParse(
                        parts[5],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out savedDpi) && savedDpi > 0) {
                        var scale = this.GetCurrentDpi() / savedDpi;
                        bounds = new Rectangle(
                            bounds.X,
                            bounds.Y,
                            (int)Math.Round(bounds.Width * scale),
                            (int)Math.Round(bounds.Height * scale));
                    }
                }

                if (!IntersectsAnyScreen(bounds)) {
                    return;
                }

                bounds = ClampToWorkingArea(bounds, this.MinimumSize);
                this.StartPosition = FormStartPosition.Manual;
                this.Bounds = bounds;
                this.Width = Math.Max(this.Width, this.MinimumSize.Width);
                this.Height = Math.Max(this.Height, this.MinimumSize.Height);
                bool maximized;
                if (bool.TryParse(parts[4], out maximized) && maximized) {
                    this.WindowState = FormWindowState.Maximized;
                }
            }
            catch {
                this.StartPosition = FormStartPosition.CenterScreen;
            }
        }
    }
}
