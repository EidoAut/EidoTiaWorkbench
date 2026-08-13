// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays a temporary floating notification inside a parent control.
    /// </summary>
    public sealed class ToastNotification : Label {
        private readonly Timer _timer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ToastNotification"/> class.
        /// </summary>
        public ToastNotification() {
            this.AutoSize = false;

            // Toast dimensions are fixed so transient notifications do not jitter as messages change.
            this.Width = 360;
            this.Height = 38;
            this.TextAlign = ContentAlignment.MiddleLeft;
            this.Padding = new Padding(12, 0, 12, 0);
            this.BackColor = ThemeColors.TextPrimary;
            this.ForeColor = Color.White;
            this.Font = ThemeFonts.Body;
            this.Visible = false;

            this._timer = new Timer { Interval = 4200 };
            this._timer.Tick += (s, e) => {
                this._timer.Stop();
                this.Visible = false;
            };
        }

        /// <summary>
        /// Shows a temporary notification near the top-right corner of the parent control.
        /// </summary>
        /// <param name="message">Message text to display.</param>
        /// <param name="parent">Parent control that hosts the notification.</param>
        public void ShowToast(string message, Control parent) {
            if (parent == null) {
                return;
            }

            this.Text = message;
            this.Parent = parent;
            this.Location = new Point(parent.ClientSize.Width - this.Width - 18, 18);
            this.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this.BringToFront();
            this.Visible = true;
            this._timer.Stop();
            this._timer.Start();
        }
    }
}
