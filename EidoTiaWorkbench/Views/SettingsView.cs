// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// SettingsView.cs
// Edits working persisted preferences with a scrollable single-column layout.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>Edits working persisted preferences with a scrollable single-column layout.</summary>
    public sealed class SettingsView : UserControl {
        private readonly PathSelectorControl _repository = new PathSelectorControl { Dock = DockStyle.Top, Margin = Padding.Empty };
        private readonly IntegrationSettingsControl _integration = new IntegrationSettingsControl();
        private readonly ComboBox _theme = new ThemedComboBox { Width = 220, Margin = Padding.Empty };
        private readonly Label _diagnostics = new Label { AutoSize = true, Dock = DockStyle.Top, Margin = Padding.Empty };
        private bool _binding;

        /// <summary>Initializes a new instance of the <see cref="SettingsView"/> class. Creates the persisted workspace, appearance and integration editors.</summary>
        public SettingsView() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Background;
            this.ForeColor = ThemeColors.TextPrimary;
            this.Padding = new Padding(ThemeMetrics.Xl);
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var stack = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, Padding = new Padding(10) };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var header = new PageHeader { Margin = Padding.Empty };
            header.SetText("Settings", "Persisted workspace, appearance and engineering integration preferences.");
            stack.Controls.Add(header);
            stack.Controls.Add(this._integration);
            stack.Controls.Add(Section("Default workspace", this._repository));

            this._theme.DataSource = Enum.GetValues(typeof(ThemePreference));
            stack.Controls.Add(Section("Appearance", this._theme));
            stack.Controls.Add(new Label { AutoSize = true, Text = "Import and export options are configured in their operation views.", Margin = new Padding(0, 14, 0, 14) });
            stack.Controls.Add(Section("Diagnostics", this._diagnostics));
            scroll.Controls.Add(stack);
            this.Controls.Add(scroll);
            this._repository.PathChanged += (s, e) => {
                if (!this._binding) {
                    this.RepositoryChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            this._theme.SelectedIndexChanged += (s, e) => {
                if (!this._binding) {
                    this.ThemePreferenceChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            this.Resize += (s, e) => this._diagnostics.MaximumSize = new Size(Math.Max(100, this.ClientSize.Width - 80), 0);
        }

        /// <summary>Occurs when the user changes the default workspace path.</summary>
        public event EventHandler RepositoryChanged;

        /// <summary>Occurs when the user chooses a different appearance preference.</summary>
        public event EventHandler ThemePreferenceChanged;

        /// <summary>Gets or sets the default engineering workspace shown in the editor.</summary>
        public string RepositoryPath {
            get => this._repository.SelectedPath; set => this._repository.SelectedPath = value;
        }

        /// <summary>Gets or sets the appearance preference shown in the editor.</summary>
        public ThemePreference ThemePreference {
            get => (ThemePreference)this._theme.SelectedItem; set => this._theme.SelectedItem = value;
        }

        /// <summary>Binds runtime selection to the controller without opening a TIA session.</summary>
        /// <param name="controller">Owner of discovery results and persisted integration settings.</param>
        public void Configure(MainController controller) {
            this._integration.Configure(controller);
        }

        /// <summary>Refreshes settings and diagnostics while suppressing user-change notifications.</summary>
        /// <param name="state">Current workspace and runtime state.</param>
        /// <param name="logFilePath">Active diagnostic log path shown to the user.</param>
        public void Bind(ApplicationState state, string logFilePath) {
            this._binding = true;
            try {
                this.RepositoryPath = state.SelectedRepository;
                this.ThemePreference = AppTheme.CurrentPreference;
                this._diagnostics.Text = "Active: " + state.IntegrationVersion + "\r\nDesktop: .NET Framework 4.8 x64, Siemens-free Core.\r\n" +
                    "Engineering: isolated worker, single STA, cooperative cancellation.\r\nLog: " + logFilePath;
            }
            finally {
                this._binding = false;
            }
        }

        private static Control Section(string title, Control content) {
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, BackColor = ThemeColors.Surface, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 16) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.Controls.Add(Heading(title));
            panel.Controls.Add(content);
            return panel;
        }

        private static Label Heading(string text) => new Label {
            Text = text,
            Font = ThemeFonts.SectionTitle,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };
    }
}
