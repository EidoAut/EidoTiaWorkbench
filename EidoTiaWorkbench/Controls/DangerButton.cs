// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides the standard destructive action button style.
    /// </summary>
    public sealed class DangerButton : ThemedButton {
        /// <summary>
        /// Initializes a new instance of the <see cref="DangerButton"/> class.
        /// </summary>
        public DangerButton() {
            this.Role = ThemedButtonRole.Danger;
        }
    }
}
