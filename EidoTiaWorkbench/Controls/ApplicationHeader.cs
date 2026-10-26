// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ApplicationHeader.cs
// Displays global application title, active project, connection state, operation state, and activity
// toggle.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays global application title, active project, connection state, operation state, and activity toggle.
    /// </summary>
    public sealed class ApplicationHeader : UserControl {
        private readonly Label _title;
        private readonly Label _project;
        private readonly StatusBadge _connectionBadge;
        private readonly StatusBadge _operationBadge;
        private readonly SecondaryButton _activityButton;
        private readonly SecondaryButton _themeButton;
        private readonly ToolTip _toolTip;
        private string _sectionDescription;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationHeader"/> class.
        /// </summary>
        public ApplicationHeader() {
            this.Dock = DockStyle.Fill;

            // The shell row is 72 logical pixels, so this keeps the header readable at high DPI.
            this.MinimumSize = new Size(0, 92);
            this.BackColor = ThemeColors.Surface;
            this.Padding = new Padding(14, 10, 14, 10);
            this._toolTip = new ToolTip();

            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1,
            };

            // The header icon column is intentionally fixed so DPI scaling cannot collapse the logo.
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var logo = new PictureBox {
                Image = LoadHeaderImage(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(40, 40),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 0, 10, 0),
            };

            var text = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2,
            };
            text.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            text.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            this._title = new Label {
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Text = ProductInfo.Name,
                Font = ThemeFonts.SectionTitle,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 0, 0, 2),
            };
            this._project = new Label {
                Dock = DockStyle.Fill,
                Text = "No active project",
                AutoSize = true,
                Font = ThemeFonts.Secondary,
                ForeColor = ThemeColors.TextSecondary,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft,
            };
            text.Controls.Add(this._title, 0, 0);
            text.Controls.Add(this._project, 0, 1);

            this._connectionBadge = new StatusBadge { Anchor = AnchorStyles.None };
            this._operationBadge = new StatusBadge { Anchor = AnchorStyles.None };
            this._activityButton = new SecondaryButton {
                Text = "Activity",
                Image = IconProvider.Get("activity", ThemeColors.TextPrimary, 16),
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Anchor = AnchorStyles.None,
            };
            this._activityButton.Click += (s, e) => this.ActivityToggleRequested?.Invoke(this, EventArgs.Empty);
            this._themeButton = new SecondaryButton {
                Text = ThemeColors.UseDarkPalette ? "Light mode" : "Dark mode",
                AccessibleName = "Switch color theme",
                Anchor = AnchorStyles.None,
                AutoSize = true,
            };
            this._themeButton.Click += (s, e) => this.ThemeToggleRequested?.Invoke(this, EventArgs.Empty);

            layout.Controls.Add(logo, 0, 0);
            layout.Controls.Add(text, 1, 0);
            layout.Controls.Add(this._connectionBadge, 2, 0);
            layout.Controls.Add(this._operationBadge, 3, 0);
            layout.Controls.Add(this._activityButton, 4, 0);
            layout.Controls.Add(this._themeButton, 5, 0);
            this.Controls.Add(layout);
        }

        /// <summary>
        /// Occurs when the user requests the activity drawer to be shown or hidden.
        /// </summary>
        public event EventHandler ActivityToggleRequested;

        /// <summary>Occurs when the user requests the opposite color theme.</summary>
        public event EventHandler ThemeToggleRequested;

        /// <summary>Sets the active workspace context in the shell header.</summary>
        /// <param name="section">Selected navigation title.</param>
        /// <param name="description">Short description of the selected workspace.</param>
        public void SetSection(string section, string description) {
            this._title.Text = section;
            this._sectionDescription = description;
        }

        /// <summary>
        /// Updates header labels and badges from application state.
        /// </summary>
        /// <param name="state">Current application state to display.</param>
        public void Bind(ApplicationState state) {
            this._themeButton.Text = ThemeColors.UseDarkPalette ? "Light mode" : "Dark mode";
            var project = state.ActiveProject;
            this._project.Text = project == null ? "No active project" : project.Name + " | " + project.Path;
            this._project.Text = this._sectionDescription + "\r\n" + state.IntegrationVersion + " · " +
                this._project.Text;
            this._toolTip.SetToolTip(this._project, this._project.Text);
            var previousImage = this._activityButton.Image;
            this._activityButton.Image = IconProvider.Get("activity", ThemeColors.TextPrimary, 16);
            previousImage?.Dispose();

            if (state.PortalConnectionState == PortalConnectionState.Connected) {
                this._connectionBadge.SetStatus(
                    state.PortalOwnership == PortalOwnership.Owned ? "Owned" : "Attached",
                    ThemeColors.Success);
            }
            else if (state.PortalConnectionState == PortalConnectionState.Error) {
                this._connectionBadge.SetStatus("Error", ThemeColors.Error);
            }
            else {
                this._connectionBadge.SetStatus("Disconnected", ThemeColors.Disabled);
            }

            if (state.CurrentOperation.IsBusy) {
                this._operationBadge.SetStatus("Busy", ThemeColors.Warning);
            }
            else if (state.LastOperationSummary != null && state.LastOperationSummary.Errors > 0) {
                this._operationBadge.SetStatus("Error", ThemeColors.Error);
            }
            else {
                this._operationBadge.SetStatus("Ready", ThemeColors.Info);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) {
            if (disposing) {
                this._toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Loads the square application icon used in the compact shell header.
        /// </summary>
        /// <returns>The header bitmap, or the application icon as a fallback.</returns>
        private static Image LoadHeaderImage() {
            var resourcePath = Path.Combine(
                Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory,
                "Resources",
                "icoEidoTiaWorkbench.png");
            if (File.Exists(resourcePath)) {
                return Image.FromFile(resourcePath);
            }

            var icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            return icon == null ? null : icon.ToBitmap();
        }
    }
}
