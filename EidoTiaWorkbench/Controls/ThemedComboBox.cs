// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Combo box with Eido theme colors and enough vertical space for the shared UI font.
    /// </summary>
    public sealed class ThemedComboBox : ComboBox {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemedComboBox"/> class.
        /// </summary>
        public ThemedComboBox() {
            this.DropDownStyle = ComboBoxStyle.DropDownList;
            this.DrawMode = DrawMode.OwnerDrawFixed;
            this.FlatStyle = FlatStyle.Flat;
            this.ItemHeight = 24;
            this.MinimumSize = new Size(0, 30);
            this.DropDownHeight = 260;
            this.IntegralHeight = false;
            this.Margin = new Padding(0, 2, 8, 0);
            this.ApplyTheme();
        }

        /// <summary>
        /// Reapplies active theme colors and sizing.
        /// </summary>
        public void ApplyTheme() {
            this.BackColor = ThemeColors.InputSurface;
            this.ForeColor = ThemeColors.TextPrimary;
            this.Font = ThemeFonts.Body;
            this.ItemHeight = 24;
            this.Invalidate();
        }

        /// <summary>
        /// Draws both dropdown items and the selected item with the active palette.
        /// </summary>
        /// <param name="e">Draw event data.</param>
        protected override void OnDrawItem(DrawItemEventArgs e) {
            if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) {
                return;
            }

            var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            var backColor = selected ? ThemeColors.SurfaceSecondary : ThemeColors.InputSurface;
            var foreColor = this.Enabled ? ThemeColors.TextPrimary : ThemeColors.Disabled;
            using (var brush = new SolidBrush(backColor)) {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            var text = e.Index >= 0 && e.Index < this.Items.Count
                ? this.GetItemText(this.Items[e.Index])
                : this.Text;
            var textBounds = new Rectangle(
                e.Bounds.Left + 8,
                e.Bounds.Top,
                Math.Max(1, e.Bounds.Width - 12),
                e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                this.Font,
                textBounds,
                foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if ((e.State & DrawItemState.Focus) == DrawItemState.Focus) {
                e.DrawFocusRectangle();
            }
        }
    }
}
