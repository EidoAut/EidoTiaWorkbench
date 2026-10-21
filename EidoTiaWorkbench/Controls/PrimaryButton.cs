// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// PrimaryButton.cs
// Provides the standard primary action button style.
//
// Developed by EIDO Automation
// =========================================================================
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides the standard primary action button style.
    /// </summary>
    public sealed class PrimaryButton : ThemedButton {
        /// <summary>
        /// Initializes a new instance of the <see cref="PrimaryButton"/> class.
        /// </summary>
        public PrimaryButton() {
            this.Role = ThemedButtonRole.Primary;
        }
    }
}
