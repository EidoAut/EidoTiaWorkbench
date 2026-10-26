// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// IntegrationSettingsControl.cs
// Edits persisted runtime selection; discovery never loads Siemens into the desktop.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Core.Integration;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>Edits persisted runtime selection; discovery never loads Siemens into the desktop.</summary>
    public sealed class IntegrationSettingsControl : UserControl {
        private readonly ComboBox _versions = new ThemedComboBox { Dock = DockStyle.Top };
        private readonly TextBox _custom = new TextBox { Dock = DockStyle.Top };
        private readonly Label _details = new Label { AutoSize = true, Dock = DockStyle.Top };
        private MainController _controller;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettingsControl"/> class. Creates runtime-selection
        /// fields without starting an engineering session.
        /// </summary>
        public IntegrationSettingsControl() {
            this.Dock = DockStyle.Top;
            this.BackColor = ThemeColors.Surface;
            this.ForeColor = ThemeColors.TextPrimary;
            this.AutoSize = true;
            this.Margin = new Padding(0, 0, 0, 16);
            this.Padding = new Padding(16);
            var stack = new TableLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stack.Controls.Add(
                new Label { Text = "TIA Portal Integration", Font = ThemeFonts.SectionTitle, AutoSize = true });
            stack.Controls.Add(
                new Label {
                    AutoSize = true,
                    Text = "Disconnect before applying another API. Custom path: installation or PublicAPI directory.",
                });
            stack.Controls.Add(this._custom);
            stack.Controls.Add(this._versions);
            var actions = new FlowLayoutPanel {
                AutoSize = true,
                Dock = DockStyle.Top,
                WrapContents = true,
                Padding = Padding.Empty,
            };
            var refresh = new SecondaryButton {
                Text = "Refresh versions",
                AutoSize = true,
                Margin = new Padding(0, 0, 8, 0),
            };
            var apply = new PrimaryButton { Text = "Apply integration", AutoSize = true, Margin = Padding.Empty };
            actions.Controls.Add(refresh);
            actions.Controls.Add(apply);
            stack.Controls.Add(actions);
            stack.Controls.Add(this._details);
            foreach (Control child in stack.Controls) {
                child.Margin = new Padding(0, 0, 0, 8);
            }

            this.Controls.Add(stack);
            this._versions.SelectedIndexChanged += (s, e) => this.UpdateDetails();
            refresh.Click += (s, e) => this.RefreshVersions();
            apply.Click += (s, e) => {
                if (this._controller == null) {
                    return;
                }

                try {
                    this._controller.ApplyIntegrationSettings(
                        (this._versions.SelectedItem as OpennessInstallation)?.ApiPath,
                        this._custom.Text);
                    this.RefreshVersions();
                }
                catch (Exception ex) {
                    this._details.Text = "Settings could not be applied: " + ex.Message;
                }
            };
            this.Resize += (s, e) => this._details.MaximumSize = new Size(Math.Max(100, this.ClientSize.Width - 30), 0);
        }

        /// <summary>Binds runtime selection to the controller without opening a TIA session.</summary>
        /// <param name="controller">Owner of discovery results and persisted integration settings.</param>
        public void Configure(MainController controller) {
            this._controller = controller;
            this._custom.Text = controller.IntegrationSettings.CustomPath ?? string.Empty;
            this.RefreshVersions();
        }

        /// <summary>Rebuilds installation choices while retaining a saved path that discovery no longer finds.</summary>
        /// <remarks>Reports discovery failures in the details panel without changing the active integration.</remarks>
        private void RefreshVersions() {
            if (this._controller == null) {
                return;
            }

            try {
                var installations = new OpennessDiscovery().Discover(this._custom.Text);
                this._versions.Items.Clear();
                this._versions.Items.Add("Automatic (highest available adapter)");
                foreach (var installation in installations) {
                    this._versions.Items.Add(installation);
                }

                var selected = installations.FirstOrDefault(
                    x => string.Equals(
                        x.ApiPath,
                        this._controller.IntegrationSettings.SelectedApiPath,
                        StringComparison.OrdinalIgnoreCase));
                if (selected == null && !string.IsNullOrWhiteSpace(
                    this._controller.IntegrationSettings.SelectedApiPath)) {
                    selected = OpennessDiscovery.Validate(this._controller.IntegrationSettings.SelectedApiPath);
                    this._versions.Items.Add(selected);
                }

                this._versions.SelectedItem = (object)selected ?? this._versions.Items[0];
                this.UpdateDetails();
            }
            catch (Exception ex) {
                this._details.Text = "Discovery failed: " + ex.Message;
            }
        }

        /// <summary>Shows evidence and capability limits for the selection without applying it to the controller.</summary>
        private void UpdateDetails() {
            var installation = this._versions.SelectedItem as OpennessInstallation;
            this._details.Text = installation == null
                ? "Automatic selects the highest installed version with an implemented adapter. " +
                    "V17–V20 are recognized only.\r\nActive: " +
                    (this._controller?.SelectedInstallation?.ToString() ?? "Offline")
                : installation + "\r\nSource: " + installation.Source + "\r\n" + installation.Diagnostic +
                    "\r\nCapabilities: " + installation.Capabilities;
            this._details.ForeColor = installation != null
                && !installation.CanUse
                ? ThemeColors.Warning
                : ThemeColors.TextSecondary;
        }
    }
}
