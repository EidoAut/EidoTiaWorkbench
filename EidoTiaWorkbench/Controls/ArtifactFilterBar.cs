// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactFilterBar.cs
// Provides scope, kind, device, search, and changed-only filters for artifact lists.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides scope, kind, device, search, and changed-only filters for artifact lists.
    /// </summary>
    public sealed class ArtifactFilterBar : UserControl {
        private readonly ComboBox _scope;
        private readonly ComboBox _kind;
        private readonly TextBox _device;
        private readonly TextBox _search;
        private readonly CheckBox _changedOnly;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactFilterBar"/> class.
        /// </summary>
        public ArtifactFilterBar() {
            this.Dock = DockStyle.Top;
            this.AutoSize = false;
            this.Height = 132;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = ThemeColors.SurfaceSecondary;

            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 3,
                RowCount = 2,
                BackColor = ThemeColors.SurfaceSecondary,
                Padding = new Padding(8, 8, 8, 8),
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            this._scope = CreateCombo(90);
            this._scope.Items.AddRange(new object[] { "All", "PLC", "HMI" });
            this._scope.SelectedIndex = 0;

            this._kind = CreateCombo(132);
            this._kind.Items.Add("Artifact kind");
            foreach (var value in ArtifactCatalog.All.Where(d => d.CanScan).Select(d => d.Kind)) {
                this._kind.Items.Add(value);
            }

            this._kind.SelectedIndex = 0;

            this._device = CreateTextBox(150, "Device");
            this._search = CreateTextBox(220, "Search text");
            this._changedOnly = new ThemedCheckBox {
                Text = "Changed only",
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0),
            };

            this._scope.SelectedIndexChanged += this.RaiseChanged;
            this._kind.SelectedIndexChanged += this.RaiseChanged;
            this._device.TextChanged += this.RaiseChanged;
            this._search.TextChanged += this.RaiseChanged;
            this._changedOnly.CheckedChanged += this.RaiseChanged;

            layout.Controls.Add(CreateField("Scope", this._scope), 0, 0);
            layout.Controls.Add(CreateField("Kind", this._kind), 1, 0);
            layout.Controls.Add(CreateField("Device", this._device), 2, 0);
            var search = CreateField("Search artifacts", this._search);
            layout.Controls.Add(search, 0, 1);
            layout.SetColumnSpan(search, 2);
            layout.Controls.Add(CreateField("State", this._changedOnly), 2, 1);
            this.Controls.Add(layout);
            AppTheme.ApplyThemeTree(this);
        }

        /// <summary>
        /// Occurs when any filter value changes.
        /// </summary>
        public event EventHandler FilterChanged;

        /// <summary>
        /// Gets the selected artifact scope.
        /// </summary>
        public string Scope {
            get {
                return this._scope.SelectedItem as string ?? "All";
            }
        }

        /// <summary>
        /// Gets the selected artifact kind, or <see langword="null"/> when all kinds are included.
        /// </summary>
        public ExportKind? Kind {
            get {
                return this._kind.SelectedItem is ExportKind ? (ExportKind?)this._kind.SelectedItem : null;
            }
        }

        /// <summary>
        /// Gets the device filter text.
        /// </summary>
        public string DeviceText {
            get {
                return this._device.Text;
            }
        }

        /// <summary>
        /// Gets the free-text search filter.
        /// </summary>
        public string SearchText {
            get {
                return this._search.Text;
            }
        }

        /// <summary>
        /// Gets a value indicating whether only changed artifacts should be shown.
        /// </summary>
        public bool ChangedOnly {
            get {
                return this._changedOnly.Checked;
            }
        }

        /// <summary>
        /// Creates a styled combo box with an intentional preferred width for compact filter scanning.
        /// </summary>
        /// <param name="width">Preferred control width.</param>
        /// <returns>The configured combo box.</returns>
        private static ComboBox CreateCombo(int width) {
            var combo = new ThemedComboBox {
                Width = width,
                Margin = Padding.Empty,
            };
            return combo;
        }

        /// <summary>
        /// Creates a styled text box with an intentional preferred width for compact filter scanning.
        /// </summary>
        /// <param name="width">Preferred control width.</param>
        /// <param name="placeholder">Placeholder text stored for UI metadata.</param>
        /// <returns>The configured text box.</returns>
        private static TextBox CreateTextBox(int width, string placeholder) {
            var textBox = new TextBox {
                Width = width,
                Height = 28,
                Margin = Padding.Empty,
                Tag = placeholder,
            };
            AppTheme.StyleInput(textBox);
            return textBox;
        }

        /// <summary>
        /// Wraps a compact filter control with a small label for scanability.
        /// </summary>
        /// <param name="labelText">Label shown above the filter control.</param>
        /// <param name="input">Filter control.</param>
        /// <returns>The configured field container.</returns>
        private static Control CreateField(string labelText, Control input) {
            var field = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ThemeColors.SurfaceSecondary,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 10, 0),
                Padding = Padding.Empty,
            };
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            field.Controls.Add(
                new Label {
                    AutoSize = true,
                    Text = labelText,
                    Font = ThemeFonts.Secondary,
                    ForeColor = ThemeColors.TextSecondary,
                    Margin = new Padding(0, 0, 0, 2),
                },
                0,
                0);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            input.MinimumSize = new Size(0, input.MinimumSize.Height);
            input.Width = 80;
            field.Controls.Add(input, 0, 1);
            return field;
        }

        /// <summary>
        /// Raises the filter changed event after any filter control changes.
        /// </summary>
        /// <param name="sender">Control that raised the source event.</param>
        /// <param name="e">Source event arguments.</param>
        private void RaiseChanged(object sender, EventArgs e) {
            this.FilterChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
