// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Base button that applies the shared visual style for action roles.
    /// </summary>
    public class ThemedButton : Button {
        private ThemedButtonRole _role = ThemedButtonRole.Secondary;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemedButton"/> class.
        /// </summary>
        public ThemedButton() {
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.FlatStyle = FlatStyle.Flat;

            // Action buttons auto-size from content but keep a consistent minimum click target.
            this.MinimumSize = new Size(96, 34);
            this.Padding = new Padding(12, 5, 12, 5);
            this.Font = ThemeFonts.Body;
            this.Cursor = Cursors.Hand;
            this.FlatAppearance.BorderSize = 1;
            this.ApplyTheme();
        }

        /// <summary>
        /// Gets or sets the visual role used to render the button.
        /// </summary>
        public ThemedButtonRole Role {
            get {
                return this._role;
            }

            set {
                this._role = value;
                this.ApplyTheme();
            }
        }

        /// <summary>
        /// Reapplies the active application palette to the button.
        /// </summary>
        public void ApplyTheme() {
            this.ApplyRole();
        }

        /// <summary>
        /// Reapplies role styling when the enabled state changes.
        /// </summary>
        /// <param name="e">Event data for the enabled state change.</param>
        protected override void OnEnabledChanged(System.EventArgs e) {
            base.OnEnabledChanged(e);
            this.ApplyTheme();
        }

        /// <summary>
        /// Applies colors and borders for the current visual role.
        /// </summary>
        private void ApplyRole() {
            if (!this.Enabled) {
                this.BackColor = ThemeColors.SurfaceSecondary;
                this.ForeColor = ThemeColors.Disabled;
                this.FlatAppearance.BorderColor = ThemeColors.Border;
                return;
            }

            if (this._role == ThemedButtonRole.Primary) {
                this.BackColor = ThemeColors.Primary;
                this.ForeColor = ThemeColors.OnPrimary;
                this.FlatAppearance.BorderColor = ThemeColors.Primary;
                this.FlatAppearance.MouseOverBackColor = ThemeColors.PrimaryHover;
            } else if (this._role == ThemedButtonRole.Danger) {
                this.BackColor = ThemeColors.Error;
                this.ForeColor = Color.White;
                this.FlatAppearance.BorderColor = ThemeColors.Error;
                this.FlatAppearance.MouseOverBackColor = ThemeColors.Error;
            } else {
                this.BackColor = ThemeColors.Surface;
                this.ForeColor = ThemeColors.TextPrimary;
                this.FlatAppearance.BorderColor = ThemeColors.Border;
                this.FlatAppearance.MouseOverBackColor = ThemeColors.SurfaceSecondary;
            }
        }
    }
}
