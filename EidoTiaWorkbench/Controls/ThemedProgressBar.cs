// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ThemedProgressBar.cs
// Draws progress and owns a UI-thread animation timer, disposed with the control.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>Theme-aware progress track with real determinate values or an indeterminate activity marker.</summary>
    public sealed class ThemedProgressBar : ProgressBar {
        private readonly Timer _animation;
        private int _offset;

        /// <summary>Initializes a new instance of the <see cref="ThemedProgressBar"/> class.</summary>
        public ThemedProgressBar() {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this._animation = new Timer { Interval = 40 };
            this._animation.Tick += (sender, e) => {
                if (this.Visible && this.Style == ProgressBarStyle.Marquee) {
                    this._offset = (this._offset + 6) % Math.Max(1, this.Width);
                    this.Invalidate();
                }
            };
            this._animation.Start();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(ThemeColors.Border);
            var width = this.Style == ProgressBarStyle.Marquee ? Math.Max(20, this.Width / 5) : (int)(this.Width * (this.Value - this.Minimum) / (double)Math.Max(1, this.Maximum - this.Minimum));
            using (var brush = new SolidBrush(ThemeColors.Primary)) {
                e.Graphics.FillRectangle(brush, this.Style == ProgressBarStyle.Marquee ? this._offset - width : 0, 0, width, this.Height);
            }
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) {
            if (disposing) {
                this._animation.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
