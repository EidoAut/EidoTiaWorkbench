// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>
    /// Displays user-level settings, defaults, and diagnostic information.
    /// </summary>
    public sealed class SettingsView : UserControl {
        private readonly PathSelectorControl _repository;
        private readonly Label _diagnostics;
        private readonly TableLayoutPanel _sections;
        private readonly ToolTip _toolTip;
        private RadioButton _systemTheme;
        private RadioButton _lightTheme;
        private RadioButton _darkTheme;
        private bool _suppressThemeEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsView"/> class.
        /// </summary>
        public SettingsView() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Background;
            this.Padding = new Padding(18);

            this._toolTip = new ToolTip {
                AutoPopDelay = 12000,
                InitialDelay = 350,
                ReshowDelay = 100,
                ShowAlways = true,
            };

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new PageHeader();
            header.SetText("Settings", "User-level preferences stored outside the project workspace.");

            var scroll = new Panel {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = ThemeColors.Background,
            };

            this._sections = new TableLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                Padding = new Padding(0, 0, 10, 0),
            };
            this._sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this._sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            this._sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this._sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this._sections.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var general = this.CreateSection("General");
            this._repository = new PathSelectorControl {
                DialogTitle = "Select default workspace",
                Margin = new Padding(0, 2, 0, 10),

                // The workspace selector keeps a readable minimum and then adapts to the section width.
                MinimumSize = new Size(560, 34),
                Width = 620,
            };
            general.SizeChanged += (s, e) => {
                var available = Math.Max(this._repository.MinimumSize.Width, general.ClientSize.Width - general.Padding.Horizontal - 8);
                this._repository.Width = Math.Min(620, available);
            };
            this._repository.PathChanged += (s, e) => this.RepositoryChanged?.Invoke(this, EventArgs.Empty);
            general.Controls.Add(this.CreateText("Default workspace"));
            general.Controls.Add(this._repository);
            general.Controls.Add(this.CreateCheck("Attach to running TIA automatically", false, "Attach to an existing TIA Portal instance when one compatible instance is available."));
            general.Controls.Add(this.CreateCheck("Start TIA with UI", true, "Start owned TIA Portal instances with the Siemens user interface visible."));
            general.Controls.Add(this.CreateCheck("Remember last project", true, "Keep the last project path for convenience. The project itself is not copied."));
            general.Controls.Add(this.CreateCheck("Confirm destructive operations", true, "Ask before operations that can replace existing TIA Portal content."));

            var export = this.CreateSection("Export");
            export.Controls.Add(this.CreateCheck("Incremental export", true, "Use manifest hashes to skip unchanged PLC and HMI XML files."));
            export.Controls.Add(this.CreateCheck("Normalize PLC XML", true, "Apply deterministic XML formatting to PLC artifacts to reduce review noise."));
            export.Controls.Add(this.CreateCheck("Preserve HMI XML", true, "Write HMI screen XML exactly as Openness exports it to keep reimport safe."));
            export.Controls.Add(this.CreateCheck("Include DBs", false, "Include data blocks when scanning PLC software."));
            export.Controls.Add(this.CreateCheck("Generate source extracts", false, "Write best-effort readable source extracts next to exported PLC XML when supported."));
            export.Controls.Add(this.CreateCheck("Write manifest", true, "Store SHA-256 hashes and metadata for incremental export and import inventory."));
            export.Controls.Add(this.CreateText("Hash algorithm: SHA-256"));

            var import = this.CreateSection("Import");
            import.Controls.Add(this.CreateCheck("Dry run by default", true, "Preview workspace operations before applying them to TIA Portal."));
            import.Controls.Add(this.CreateCheck("Create archive before bulk import", true, "Export existing HMI screens to a backup folder before replacement."));
            import.Controls.Add(this.CreateCheck("Continue after individual errors", true, "Record item-level failures and continue with the remaining selection."));
            import.Controls.Add(this.CreateText("Conflict policy"));
            var policy = new ThemedComboBox {
                // The policy names are short and fit this preferred width at supported DPI levels.
                Width = 180,
                Margin = new Padding(0, 2, 0, 10),
            };
            policy.DataSource = Enum.GetValues(typeof(ImportConflictPolicy));
            policy.SelectedItem = ImportConflictPolicy.Ask;
            this._toolTip.SetToolTip(policy, "Default HMI conflict policy used by import previews and replacement confirmation.");
            import.Controls.Add(policy);

            var appearance = this.CreateSection("Appearance");
            appearance.Controls.Add(this.CreateText("Theme"));
            appearance.Controls.Add(this.CreateThemeSelector());
            appearance.Controls.Add(this.CreateCheck("Compact density", false, "Reduce vertical spacing for dense engineering workflows."));
            appearance.Controls.Add(this.CreateText("UI scale follows Windows DPI."));

            var diagnostics = this.CreateSection("Diagnostics");
            this._diagnostics = this.CreateText(string.Empty);
            diagnostics.Controls.Add(this._diagnostics);

            this._sections.Controls.Add(general, 0, 0);
            this._sections.Controls.Add(export, 0, 1);
            this._sections.Controls.Add(import, 1, 0);
            this._sections.Controls.Add(appearance, 1, 1);
            this._sections.Controls.Add(diagnostics, 0, 2);
            this._sections.SetColumnSpan(diagnostics, 2);

            scroll.Controls.Add(this._sections);
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(scroll, 0, 1);
            this.Controls.Add(root);
            this.Resize += (s, e) => this.UpdateResponsiveLayout();
            this.UpdateResponsiveLayout();
        }

        /// <summary>
        /// Occurs when the default workspace path changes.
        /// </summary>
        public event EventHandler RepositoryChanged;

        /// <summary>
        /// Occurs when the selected theme preference changes.
        /// </summary>
        public event EventHandler ThemePreferenceChanged;

        /// <summary>
        /// Gets or sets the default workspace path.
        /// </summary>
        public string RepositoryPath {
            get {
                return this._repository.SelectedPath;
            }

            set {
                this._repository.SelectedPath = value;
                this._toolTip.SetToolTip(this._repository, value ?? string.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the selected application theme preference.
        /// </summary>
        public ThemePreference ThemePreference {
            get {
                if (this._darkTheme != null && this._darkTheme.Checked) {
                    return ThemePreference.Dark;
                }

                if (this._lightTheme != null && this._lightTheme.Checked) {
                    return ThemePreference.Light;
                }

                return ThemePreference.System;
            }

            set {
                this._suppressThemeEvents = true;
                try {
                    if (this._darkTheme != null) {
                        this._darkTheme.Checked = value == ThemePreference.Dark;
                    }

                    if (this._lightTheme != null) {
                        this._lightTheme.Checked = value == ThemePreference.Light;
                    }

                    if (this._systemTheme != null) {
                        this._systemTheme.Checked = value == ThemePreference.System;
                    }
                } finally {
                    this._suppressThemeEvents = false;
                }
            }
        }

        /// <summary>
        /// Binds settings fields and diagnostic text from application state.
        /// </summary>
        /// <param name="state">Application state containing selected workspace path.</param>
        /// <param name="logFilePath">Full path of the application log file.</param>
        public void Bind(ApplicationState state, string logFilePath) {
            this.RepositoryPath = state.SelectedRepository;
            this.ThemePreference = AppTheme.CurrentPreference;
            this._diagnostics.Text =
                "Openness installation path: resolved at startup" + "\r\n" +
                "Detected Siemens assemblies: Step7 and WinCC are referenced with Copy Local disabled" + "\r\n" +
                "Windows user-group status: check Siemens Openness permissions" + "\r\n" +
                "Log file path: " + logFilePath + "\r\n" +
                "Environment check: .NET Framework 4.8 x64, TIA Portal V21";
        }

        /// <summary>
        /// Creates one settings section container.
        /// </summary>
        /// <param name="title">Section title.</param>
        /// <returns>The configured section panel.</returns>
        private FlowLayoutPanel CreateSection(string title) {
            var section = new FlowLayoutPanel {
                Dock = DockStyle.Fill,

                // Settings sections keep a readable minimum before the view switches to one column.
                MinimumSize = new Size(360, 0),
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,

                // Sections are vertical stacks; responsive wrapping is handled by the parent grid.
                WrapContents = false,
                BackColor = ThemeColors.Surface,
                Padding = new Padding(22, 20, 22, 20),
                Margin = new Padding(0, 0, 18, 18),
            };
            section.Controls.Add(new Label {
                Text = title,
                AutoSize = true,
                Font = ThemeFonts.SectionTitle,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 0, 0, 10),
            });
            return section;
        }

        /// <summary>
        /// Creates a settings checkbox with tooltip text.
        /// </summary>
        /// <param name="text">Checkbox text.</param>
        /// <param name="value">Initial checked value.</param>
        /// <param name="tooltip">Tooltip text.</param>
        /// <returns>The configured checkbox.</returns>
        private CheckBox CreateCheck(string text, bool value, string tooltip) {
            var check = new CheckBox {
                Text = text,
                Checked = value,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 6),
            };
            this._toolTip.SetToolTip(check, tooltip);
            return check;
        }

        /// <summary>
        /// Creates a body label for settings content.
        /// </summary>
        /// <param name="text">Label text.</param>
        /// <returns>The configured label.</returns>
        private Label CreateText(string text) {
            return new Label {
                Text = text,
                AutoSize = true,

                // Body text wraps at a readable line length instead of stretching across wide screens.
                MaximumSize = new Size(560, 0),
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 4, 0, 4),
            };
        }

        /// <summary>
        /// Creates the theme radio-button selector.
        /// </summary>
        /// <returns>The configured theme selector control.</returns>
        private Control CreateThemeSelector() {
            var panel = new FlowLayoutPanel {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 8),
            };
            this._systemTheme = new RadioButton { Text = "System", AutoSize = true, Checked = AppTheme.CurrentPreference == ThemePreference.System, Margin = new Padding(0, 4, 16, 4) };
            this._lightTheme = new RadioButton { Text = "Light", AutoSize = true, Checked = AppTheme.CurrentPreference == ThemePreference.Light, Margin = new Padding(0, 4, 16, 4) };
            this._darkTheme = new RadioButton { Text = "Dark", AutoSize = true, Checked = AppTheme.CurrentPreference == ThemePreference.Dark, Margin = new Padding(0, 4, 16, 4) };
            this._systemTheme.CheckedChanged += this.ThemeRadio_CheckedChanged;
            this._lightTheme.CheckedChanged += this.ThemeRadio_CheckedChanged;
            this._darkTheme.CheckedChanged += this.ThemeRadio_CheckedChanged;
            this._toolTip.SetToolTip(this._systemTheme, "Follow the current Windows app theme where supported.");
            this._toolTip.SetToolTip(this._lightTheme, "Use the light Eido workbench palette.");
            this._toolTip.SetToolTip(this._darkTheme, "Use the dark Eido workbench palette.");
            panel.Controls.Add(this._systemTheme);
            panel.Controls.Add(this._lightTheme);
            panel.Controls.Add(this._darkTheme);
            return panel;
        }

        /// <summary>
        /// Raises the theme preference change event when a theme radio button becomes checked.
        /// </summary>
        /// <param name="sender">Radio button that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        private void ThemeRadio_CheckedChanged(object sender, EventArgs e) {
            if (this._suppressThemeEvents) {
                return;
            }

            var radio = sender as RadioButton;
            if (radio == null || !radio.Checked) {
                return;
            }

            this.ThemePreferenceChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reflows settings sections between one and two columns based on available width.
        /// </summary>
        private void UpdateResponsiveLayout() {
            var twoColumns = this.ClientSize.Width >= 900;
            this._sections.SuspendLayout();
            try {
                this._sections.ColumnCount = twoColumns ? 2 : 1;
                var workspaceMinimumWidth = twoColumns ? 300 : 560;
                this._repository.MinimumSize = new Size(workspaceMinimumWidth, 34);
                this._repository.Width = Math.Max(this._repository.Width, workspaceMinimumWidth);
                this._sections.ColumnStyles.Clear();
                if (twoColumns) {
                    this._sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    this._sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                    this._sections.SetColumn(this._sections.Controls[0], 0);
                    this._sections.SetRow(this._sections.Controls[0], 0);
                    this._sections.SetColumnSpan(this._sections.Controls[0], 1);
                    this._sections.SetColumn(this._sections.Controls[1], 0);
                    this._sections.SetRow(this._sections.Controls[1], 1);
                    this._sections.SetColumnSpan(this._sections.Controls[1], 1);
                    this._sections.SetColumn(this._sections.Controls[2], 1);
                    this._sections.SetRow(this._sections.Controls[2], 0);
                    this._sections.SetColumnSpan(this._sections.Controls[2], 1);
                    this._sections.SetColumn(this._sections.Controls[3], 1);
                    this._sections.SetRow(this._sections.Controls[3], 1);
                    this._sections.SetColumnSpan(this._sections.Controls[3], 1);
                    this._sections.SetColumn(this._sections.Controls[4], 0);
                    this._sections.SetRow(this._sections.Controls[4], 2);
                    this._sections.SetColumnSpan(this._sections.Controls[4], 2);
                } else {
                    this._sections.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                    for (var i = 0; i < this._sections.Controls.Count; i++) {
                        this._sections.SetColumn(this._sections.Controls[i], 0);
                        this._sections.SetRow(this._sections.Controls[i], i);
                        this._sections.SetColumnSpan(this._sections.Controls[i], 1);
                    }
                }
            } finally {
                this._sections.ResumeLayout();
            }
        }
    }
}
