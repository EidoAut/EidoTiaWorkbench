// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ThemeMetrics.cs
// Defines logical spacing and dimensions; WinForms scales layout values for DPI.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Windows.Forms;

namespace EidoTiaWorkbench.Theme {
    /// <summary>Shared logical dimensions for the desktop design system.</summary>
    public static class ThemeMetrics {
        /// <summary>Extra-small spacing in logical pixels.</summary>
        public const int Xs = 4;

        /// <summary>Small spacing in logical pixels.</summary>
        public const int S = 8;

        /// <summary>Medium spacing in logical pixels.</summary>
        public const int M = 12;

        /// <summary>Large spacing in logical pixels.</summary>
        public const int L = 16;

        /// <summary>Page and section spacing in logical pixels.</summary>
        public const int Xl = 24;

        /// <summary>Major section spacing in logical pixels.</summary>
        public const int Xxl = 32;

        /// <summary>Minimum comfortable action height in logical pixels.</summary>
        public const int ActionHeight = 40;

        /// <summary>Converts a logical painting dimension to device pixels.</summary>
        /// <param name="control">Control whose monitor DPI applies.</param>
        /// <param name="value">Logical dimension at 96 DPI.</param>
        /// <returns>The rounded device dimension.</returns>
        public static int Scale(Control control, int value) => (int)Math.Round(value * Math.Max(control.DeviceDpi / 96.0, control.Font.Size / ThemeFonts.Body.Size));
    }
}
