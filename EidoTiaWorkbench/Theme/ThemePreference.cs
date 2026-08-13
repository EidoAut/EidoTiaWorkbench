// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Identifies the user preference used to resolve the effective application palette.
    /// </summary>
    public enum ThemePreference {
        /// <summary>
        /// Follow the Windows application theme when it can be detected.
        /// </summary>
        System,

        /// <summary>
        /// Use the light Eido application palette.
        /// </summary>
        Light,

        /// <summary>
        /// Use the dark Eido application palette.
        /// </summary>
        Dark,
    }
}
