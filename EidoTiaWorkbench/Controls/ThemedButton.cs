// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ThemedButton.cs
// Draws button states, rounded borders and keyboard focus with the active palette.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>Draws accessible themed button states with rounded borders and keyboard focus cues.</summary>
    public class ThemedButton : Button {
        private ThemedButtonRole _role = ThemedButtonRole.Secondary;
        private bool _hover;
        private bool _pressed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemedButton"/> class. Creates a keyboard-accessible action
        /// button using the active semantic palette.
        /// </summary>
        public ThemedButton() {
            this.SetStyle(
                ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
                true);
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.UseVisualStyleBackColor = false;
            this.MinimumSize = new Size(104, 40);
            this.Padding = new Padding(12, 5, 12, 5);
            this.Font = ThemeFonts.Body;
            this.Cursor = Cursors.Hand;
            this.ApplyTheme();
        }

        /// <summary>Gets or sets the semantic action role used to choose surface, border and text colors.</summary>
        public ThemedButtonRole Role {
            get => this._role;
            set {
                this._role = value;
                this.ApplyTheme();
            }
        }

        /// <summary>Gets a value indicating whether pointer hover feedback is active.</summary>
        protected bool IsPointerOver => this._hover;

        /// <summary>
        /// Reapplies the semantic palette and schedules repainting without changing interaction state.
        /// </summary>
        public void ApplyTheme() {
            this.BackColor = ThemeColors.SurfaceSecondary;
            this.ForeColor = ThemeColors.TextPrimary;
            this.Invalidate();
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) {
                this._pressed = true;
                this.Invalidate();
            }

            base.OnKeyDown(e);
        }

        /// <inheritdoc />
        protected override void OnKeyUp(KeyEventArgs e) {
            this._pressed = false;
            this.Invalidate();
            base.OnKeyUp(e);
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
            this._pressed = false;
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        /// <inheritdoc />
        protected override void OnMouseDown(MouseEventArgs e) {
            this._pressed = e.Button == MouseButtons.Left;
            this.Invalidate();
            base.OnMouseDown(e);
        }

        /// <inheritdoc />
        protected override void OnMouseUp(MouseEventArgs e) {
            this._pressed = false;
            this.Invalidate();
            base.OnMouseUp(e);
        }

        /// <inheritdoc />
        protected override void OnMouseCaptureChanged(EventArgs e) {
            this._pressed = false;
            this.Invalidate();
            base.OnMouseCaptureChanged(e);
        }

        /// <inheritdoc />
        protected override void OnEnabledChanged(EventArgs e) {
            base.OnEnabledChanged(e);
            this.Cursor = this.Enabled ? Cursors.Hand : Cursors.Default;
            this.Invalidate();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e) {
            var parent = this.Parent?.BackColor ?? ThemeColors.Surface;
            e.Graphics.Clear(parent);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var fill = this._role == ThemedButtonRole.Primary ? ThemeColors.Primary : ThemeColors.SurfaceSecondary;
            var border = this._role == ThemedButtonRole.Primary ? ThemeColors.Primary : ThemeColors.BorderStrong;
            var text = this._role == ThemedButtonRole.Primary ? ThemeColors.OnPrimary : ThemeColors.TextPrimary;
            if (this._role == ThemedButtonRole.Danger) {
                fill = RoundedDrawing.Blend(ThemeColors.Error, parent, 0.14F);
                border = text = ThemeColors.Error;
            }

            if (!this.Enabled) {
                fill = ThemeColors.SurfaceSecondary;
                border = ThemeColors.Border;
                text = ThemeColors.Disabled;
            }
            else if (this._hover || this._pressed) {
                fill = RoundedDrawing.Blend(ThemeColors.Primary, fill, this._pressed ? 0.22F : 0.10F);
                border = ThemeColors.PrimaryHover;
            }

            using (var path = RoundedDrawing.Path(
                new RectangleF(0.5F, 0.5F, this.Width - 1, this.Height - 1),
                6F * this.DeviceDpi / 96F))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border)) {
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }

            var bounds = Rectangle.Inflate(this.ClientRectangle, -10, 0);
            if (this.Image != null) {
                var x = bounds.Left;
                e.Graphics.DrawImage(this.Image, x, (this.Height - this.Image.Height) / 2);
                bounds.X += this.Image.Width + 6;
                bounds.Width -= this.Image.Width + 6;
            }

            var textFlags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(
                e.Graphics,
                this.Text,
                this.Font,
                bounds,
                text,
                textFlags);
            if (this.Focused && this.ShowFocusCues) {
                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    Rectangle.Inflate(this.ClientRectangle, -4, -4),
                    text,
                    fill);
            }
        }
    }
}
