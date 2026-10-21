// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ProgressBarColor.cs
// Provides helper methods to change the visual state color of a Windows Forms control using the Win32 API.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EidoTiaWorkbench {
    /// <summary>
    /// Provides helper methods to change the visual state color of a
    /// Windows Forms <see cref="ProgressBar"/> control using the Win32 API.
    /// </summary>
    /// <remarks>
    /// Supported on Windows 7 and later.
    /// </remarks>
    internal static class ProgressBarColor {
        private const int PbmSetState = 0x0410 + 16;
        private const int PbstNormal = 0x0001;
        private const int PbstError = 0x0002;
        private const int PbstPaused = 0x0003;

        /// <summary>
        /// Sets the progress bar state to Normal (green).
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        public static void SetNormal(ProgressBar bar) {
            if (bar == null || bar.IsDisposed) {
                return;
            }

            TrySet(bar, PbstNormal);
        }

        /// <summary>
        /// Sets the progress bar state to Error (red).
        /// Typically used when an export/import operation fails.
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        public static void SetError(ProgressBar bar) {
            if (bar == null || bar.IsDisposed) {
                return;
            }

            TrySet(bar, PbstError);
        }

        /// <summary>
        /// Sets the progress bar state to the native paused visual state (yellow).
        /// Useful for non-critical issues or partial completions.
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        public static void SetWarning(ProgressBar bar) {
            if (bar == null || bar.IsDisposed) {
                return;
            }

            TrySet(bar, PbstPaused);
        }

        /// <summary>
        /// Attempts to send the PBM_SETSTATE message safely.
        /// </summary>
        /// <param name="bar">Target progress bar.</param>
        /// <param name="state">Desired visual state.</param>
        private static void TrySet(ProgressBar bar, int state) {
            try {
                SendMessage(
                    bar.Handle,
                    PbmSetState,
                    (IntPtr)state,
                    IntPtr.Zero);
            }
            catch {
                // Silently ignore failures to avoid UI crashes.
                // Typically occurs if handle is not yet created.
            }
        }

        /// <summary>
        /// Sends a Windows message to a progress bar handle.
        /// </summary>
        /// <param name="hWnd">Target window handle.</param>
        /// <param name="msg">Message identifier.</param>
        /// <param name="wParam">Message word parameter.</param>
        /// <param name="lParam">Message long parameter.</param>
        /// <returns>The message result returned by Windows.</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(
            IntPtr hWnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam);
    }
}
