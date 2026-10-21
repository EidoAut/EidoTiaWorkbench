// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ThemedCheckBox.cs
// Paints readable checkbox states while preserving native keyboard and accessibility behavior.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>Checkbox with semantic surfaces and a visible keyboard focus cue.</summary>
    public sealed class ThemedCheckBox : CheckBox {
        private bool _hover;

        /// <summary>Initializes a new instance of the <see cref="ThemedCheckBox"/> class.</summary>
        public ThemedCheckBox() {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.Font = ThemeFonts.Body;
            this.MinimumSize = new Size(0, 32);
        }

        /// <inheritdoc />
        public override Size GetPreferredSize(Size proposedSize) {
            var text = TextRenderer.MeasureText(this.Text, this.Font);
            return new Size(text.Width + ThemeMetrics.Scale(this, 32), Math.Max(text.Height + 12, ThemeMetrics.Scale(this, 32)));
        }

        /// <inheritdoc />
        protected override void OnMouseEnter(EventArgs e) {
            this._hover = true;
            this.Invalidate();
            base.OnMouseEnter(e);
        }

        /// <inheritdoc />
        protected override void OnMouseLeave(EventArgs e) {
            this._hover = false;
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(this.Parent?.BackColor ?? ThemeColors.Surface);
            var side = ThemeMetrics.Scale(this, 16);
            var box = new Rectangle(1, (this.Height - side) / 2, side, side);
            using (var brush = new SolidBrush(this.Checked ? ThemeColors.Primary : ThemeColors.InputSurface))
            using (var pen = new Pen(this._hover || this.Focused ? ThemeColors.Primary : ThemeColors.BorderStrong)) {
                e.Graphics.FillRectangle(brush, box);
                e.Graphics.DrawRectangle(pen, box);
            }

            if (this.CheckState != CheckState.Unchecked) {
                TextRenderer.DrawText(e.Graphics, this.CheckState == CheckState.Indeterminate ? "−" : "✓", this.Font, box, ThemeColors.OnPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            var bounds = new Rectangle(side + 8, 0, Math.Max(0, this.Width - side - 8), this.Height);
            TextRenderer.DrawText(e.Graphics, this.Text, this.Font, bounds, this.Enabled ? ThemeColors.TextPrimary : ThemeColors.Disabled, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (this.Focused && this.ShowFocusCues) {
                ControlPaint.DrawFocusRectangle(e.Graphics, bounds, ThemeColors.TextPrimary, this.BackColor);
            }
        }
    }
}
