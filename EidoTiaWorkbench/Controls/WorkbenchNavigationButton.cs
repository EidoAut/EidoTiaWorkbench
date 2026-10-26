// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// WorkbenchNavigationButton.cs
// Draws navigation selection, focus and pointer states using the active palette.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>Draws navigation selection, focus and pointer states using the active palette.</summary>
    internal sealed class WorkbenchNavigationButton : ThemedButton {
        /// <summary>Gets or sets a value indicating whether this is the current navigation destination.</summary>
        public bool Selected {
            get; set;
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(this.Parent?.BackColor ?? ThemeColors.Sidebar);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = this.Selected
                ? ThemeColors.OperationSurface
                : this.IsPointerOver
                ? ThemeColors.SurfaceSecondary
                : ThemeColors.Sidebar;
            var accent = this.Selected ? ThemeColors.Primary : ThemeColors.Border;
            using (var path = RoundedDrawing.Path(new RectangleF(1, 1, this.Width - 3, this.Height - 3), 6))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(accent)) {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var scale = Math.Max(this.DeviceDpi / 96F, this.Font.Size / ThemeFonts.Body.Size);
            var icon = new Rectangle(
                (int)(14 * scale),
                (this.Height - (int)(24 * scale)) / 2,
                (int)(24 * scale),
                (int)(24 * scale));
            using (var brush = new SolidBrush(ThemeColors.SurfaceSecondary)) {
                e.Graphics.FillRectangle(brush, icon);
            }

            var monogram = this.Text.Length > 1 ? this.Text.Substring(0, 2).ToUpperInvariant() : this.Text;
            TextRenderer.DrawText(
                e.Graphics,
                monogram,
                this.Font,
                icon,
                ThemeColors.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            var bounds = new Rectangle(icon.Right + 12, 0, Math.Max(0, this.Width - icon.Right - 18), this.Height);
            var textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(
                e.Graphics,
                this.Text,
                this.Font,
                bounds,
                this.Selected ? ThemeColors.TextPrimary : ThemeColors.TextSecondary,
                textFlags);
            if (this.Selected) {
                using (var brush = new SolidBrush(ThemeColors.Primary)) {
                    e.Graphics.FillRectangle(brush, 2, 9, 3, this.Height - 18);
                }
            }

            if (this.Focused && this.ShowFocusCues) {
                ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(this.ClientRectangle, -4, -4));
            }
        }
    }
}
