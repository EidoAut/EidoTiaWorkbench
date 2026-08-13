// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

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
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = ThemeColors.SurfaceSecondary;

            var layout = new FlowLayoutPanel {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = ThemeColors.SurfaceSecondary,
                Padding = new Padding(8, 8, 8, 8),
            };

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
            this._changedOnly = new CheckBox {
                Text = "Changed only",
                AutoSize = true,
                Margin = new Padding(0, 5, 0, 0),
            };

            this._scope.SelectedIndexChanged += this.RaiseChanged;
            this._kind.SelectedIndexChanged += this.RaiseChanged;
            this._device.TextChanged += this.RaiseChanged;
            this._search.TextChanged += this.RaiseChanged;
            this._changedOnly.CheckedChanged += this.RaiseChanged;

            layout.Controls.Add(CreateField("Scope", this._scope));
            layout.Controls.Add(CreateField("Kind", this._kind));
            layout.Controls.Add(CreateField("Device", this._device));
            layout.Controls.Add(CreateField("Search", this._search));
            layout.Controls.Add(CreateField("State", this._changedOnly));
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
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = ThemeColors.SurfaceSecondary,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(0, 0, 10, 0),
                Padding = Padding.Empty,
            };
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            field.Controls.Add(new Label {
                AutoSize = true,
                Text = labelText,
                Font = ThemeFonts.Secondary,
                ForeColor = ThemeColors.TextSecondary,
                Margin = new Padding(0, 0, 0, 2),
            }, 0, 0);
            input.Anchor = AnchorStyles.Left | AnchorStyles.Top;
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
