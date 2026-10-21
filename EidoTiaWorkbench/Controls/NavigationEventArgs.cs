// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// NavigationEventArgs.cs
// Provides event data for a sidebar navigation request.
//
// Developed by EIDO Automation
// =========================================================================
using System;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides event data for a sidebar navigation request.
    /// </summary>
    public sealed class NavigationEventArgs : EventArgs {
        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationEventArgs"/> class.
        /// </summary>
        /// <param name="key">Stable navigation key requested by the user.</param>
        public NavigationEventArgs(string key) {
            this.Key = key;
        }

        /// <summary>
        /// Gets the stable navigation key requested by the user.
        /// </summary>
        public string Key {
            get;
        }
    }
}
