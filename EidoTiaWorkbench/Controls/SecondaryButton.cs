// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// SecondaryButton.cs
// Provides the standard secondary action button style.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides the standard secondary action button style.
    /// </summary>
    public sealed class SecondaryButton : ThemedButton {
        /// <summary>
        /// Initializes a new instance of the <see cref="SecondaryButton"/> class.
        /// </summary>
        public SecondaryButton() {
            this.Role = ThemedButtonRole.Secondary;
        }
    }
}
