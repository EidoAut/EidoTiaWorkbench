// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// DashboardView.cs
// Displays high-level portal, project, workspace, and last-operation status.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>
    /// Displays high-level portal, project, workspace, and last-operation status.
    /// </summary>
    public sealed class DashboardView : UserControl {
        private readonly ConnectionStatusCard _connection;
        private readonly InfoCard _session;
        private readonly InfoCard _project;
        private readonly InfoCard _tiaVersion;
        private readonly InfoCard _plcTargets;
        private readonly InfoCard _hmiTargets;
        private readonly InfoCard _repository;
        private readonly InfoCard _lastExport;
        private readonly InfoCard _lastImport;
        private readonly InfoCard _lastResult;
        private readonly EmptyStateControl _emptyState;
        private readonly TableLayoutPanel _emptyStateHost;
        private readonly Panel _contentPanel;
        private readonly TableLayoutPanel _cardsGrid;
        private readonly Control[] _cards;

        /// <summary>
        /// Initializes a new instance of the <see cref="DashboardView"/> class.
        /// </summary>
        public DashboardView() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Background;
            this.Padding = new Padding(ThemeMetrics.Xl);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var header = new PageHeader();
            header.SetText("Dashboard", "Connection, project and workspace status.");

            var actions = new OperationToolbar();
            AddAction(actions, "Attach to TIA Portal", "project", (s, e) => this.AttachRequested?.Invoke(this, EventArgs.Empty));
            AddAction(actions, "Open project", "project", (s, e) => this.OpenProjectRequested?.Invoke(this, EventArgs.Empty));
            AddAction(actions, "Scan project", "activity", (s, e) => this.ScanRequested?.Invoke(this, EventArgs.Empty));
            AddAction(actions, "Export changed artifacts", "export", (s, e) => this.ExportChangedRequested?.Invoke(this, EventArgs.Empty));
            AddAction(actions, "Open workspace", "project", (s, e) => this.OpenRepositoryRequested?.Invoke(this, EventArgs.Empty));

            this._contentPanel = new Panel {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ThemeColors.Background,
            };
            this._cardsGrid = new TableLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
            };

            this._connection = new ConnectionStatusCard { Dock = DockStyle.Fill };
            this._session = NewCard();
            this._project = NewCard();
            this._tiaVersion = NewCard();
            this._plcTargets = NewCard();
            this._hmiTargets = NewCard();
            this._repository = NewCard();
            this._lastExport = NewCard();
            this._lastImport = NewCard();
            this._lastResult = NewCard();

            this._cards = new Control[]
            {
                this._connection, this._session, this._project,
                this._tiaVersion, this._plcTargets, this._hmiTargets,
                this._repository, this._lastExport, this._lastImport,
                this._lastResult,
            };

            this._emptyStateHost = new TableLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 1,
                BackColor = ThemeColors.Background,
                Padding = new Padding(0, 18, 0, 0),
            };
            this._emptyStateHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this._emptyStateHost.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this._emptyStateHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            this._emptyState = new EmptyStateControl { Anchor = AnchorStyles.Top };
            this._emptyState.SetMessage(
                "No TIA Portal connection",
                "Attach to a running TIA Portal instance or open a project to scan PLC and HMI artifacts.");
            this._emptyState.ActionRequested += (s, e) => this.AttachRequested?.Invoke(this, EventArgs.Empty);
            this._emptyStateHost.Controls.Add(this._emptyState, 1, 0);

            this._contentPanel.Controls.Add(this._emptyStateHost);
            this._contentPanel.Controls.Add(this._cardsGrid);
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(actions, 0, 1);
            root.Controls.Add(this._contentPanel, 0, 2);
            this.Controls.Add(root);
            this.SizeChanged += (s, e) => this.UpdateCardLayout();
            this.UpdateCardLayout();
        }

        /// <summary>
        /// Occurs when the user requests attachment to a running TIA Portal instance.
        /// </summary>
        public event EventHandler AttachRequested;

        /// <summary>
        /// Occurs when the user requests navigation to project opening.
        /// </summary>
        public event EventHandler OpenProjectRequested;

        /// <summary>
        /// Occurs when the user requests a project scan.
        /// </summary>
        public event EventHandler ScanRequested;

        /// <summary>
        /// Occurs when the user requests exporting changed artifacts.
        /// </summary>
        public event EventHandler ExportChangedRequested;

        /// <summary>
        /// Occurs when the user requests opening the selected workspace folder.
        /// </summary>
        public event EventHandler OpenRepositoryRequested;

        /// <summary>
        /// Updates dashboard cards from application state.
        /// </summary>
        /// <param name="state">Application state to display.</param>
        public void Bind(ApplicationState state) {
            var connected = state.PortalConnectionState == PortalConnectionState.Connected;
            this._connection.SetConnected(connected, connected ? state.IntegrationVersion + " session available" : "No active Openness session");
            this._session.SetContent("Session", state.PortalOwnership.ToString());

            var project = state.ActiveProject;
            this._project.SetContent("Active project", project == null ? "-" : project.Name);
            this._tiaVersion.SetContent("TIA Portal version", project == null ? state.IntegrationVersion : project.Version);
            this._plcTargets.SetContent("Detected PLC targets", project == null ? "0" : project.PlcTargets.Count.ToString());
            this._hmiTargets.SetContent("Detected HMI targets", project == null ? "0" : project.HmiTargets.Count.ToString());
            this._repository.SetContent("Workspace folder", state.SelectedRepository);
            this._lastExport.SetContent("Last export", state.LastExport);
            this._lastImport.SetContent("Last import", state.LastImport);
            this._lastResult.SetContent("Last operation result", state.LastOperationResult);

            this._cardsGrid.Visible = true;
            this._emptyStateHost.Visible = !connected;
        }

        /// <inheritdoc />
        protected override void OnFontChanged(EventArgs e) {
            base.OnFontChanged(e);
            if (this._cards != null) {
                this.UpdateCardLayout();
            }
        }

        /// <summary>
        /// Creates a dashboard information card with shared docking.
        /// </summary>
        /// <returns>The configured information card.</returns>
        private static InfoCard NewCard() {
            return new InfoCard { Dock = DockStyle.Fill };
        }

        /// <summary>
        /// Adds one dashboard action button.
        /// </summary>
        /// <param name="panel">Panel that receives the button.</param>
        /// <param name="text">Button text.</param>
        /// <param name="icon">Icon provider key.</param>
        /// <param name="handler">Click handler.</param>
        private static void AddAction(FlowLayoutPanel panel, string text, string icon, EventHandler handler) {
            var button = new SecondaryButton {
                Text = text,
                Image = IconProvider.Get(icon, ThemeColors.TextPrimary, 16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
            };
            button.Click += handler;
            panel.Controls.Add(button);
        }

        /// <summary>
        /// Reflows dashboard cards across clear width thresholds to avoid empty cells and clipped text.
        /// </summary>
        private void UpdateCardLayout() {
            var availableWidth = this._contentPanel.ClientSize.Width;
            var columns = availableWidth >= ThemeMetrics.Scale(this, 1100) ? 3 : availableWidth >= ThemeMetrics.Scale(this, 760) ? 2 : 1;

            this._cardsGrid.SuspendLayout();
            try {
                this._cardsGrid.Controls.Clear();
                this._cardsGrid.ColumnStyles.Clear();
                this._cardsGrid.RowStyles.Clear();
                this._cardsGrid.ColumnCount = columns;
                for (var column = 0; column < columns; column++) {
                    this._cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
                }

                for (var i = 0; i < this._cards.Length; i++) {
                    var row = i / columns;
                    var column = i % columns;
                    if (row >= this._cardsGrid.RowStyles.Count) {
                        this._cardsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, ThemeMetrics.Scale(this, 112)));
                    }

                    this._cardsGrid.Controls.Add(this._cards[i], column, row);
                    this._cardsGrid.SetColumnSpan(this._cards[i], 1);
                }

                var remainder = this._cards.Length % columns;
                if (remainder == 1 && columns > 1) {
                    this._cardsGrid.SetColumnSpan(this._lastResult, columns);
                }
            }
            finally {
                this._cardsGrid.ResumeLayout();
            }
        }
    }
}
