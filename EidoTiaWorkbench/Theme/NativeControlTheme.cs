// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// NativeControlTheme.cs
// Requests native scrollbar themes without replacing keyboard or accessibility behavior.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EidoTiaWorkbench.Theme {
    /// <summary>Applies an available Windows theme to native control chrome.</summary>
    internal static class NativeControlTheme {
        /// <summary>Rethemes an existing handle and its future recreations.</summary>
        /// <param name="control">Native control that owns its scrollbar handles.</param>
        internal static void Apply(Control control) {
            control.HandleCreated -= HandleCreated;
            control.HandleCreated += HandleCreated;
            if (control.IsHandleCreated) {
                ApplyHandle(control);
            }
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string subApplication, string subIdentifiers);

        private static void HandleCreated(object sender, EventArgs e) => ApplyHandle((Control)sender);

        private static void ApplyHandle(Control control) {
            if (!SystemInformation.HighContrast) {
                SetWindowTheme(control.Handle, ThemeColors.UseDarkPalette ? "DarkMode_Explorer" : "Explorer", null);
            }
        }
    }
}
