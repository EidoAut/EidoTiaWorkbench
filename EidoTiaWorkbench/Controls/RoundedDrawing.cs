// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// RoundedDrawing.cs
// Builds disposable rounded paths and blends colors for owner-drawn controls.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>Builds disposable rounded paths and blends colors for owner-drawn controls.</summary>
    internal static class RoundedDrawing {
        /// <summary>Interpolates opaque RGB channels for control backgrounds.</summary>
        /// <param name="foreground">Color at an amount of one.</param>
        /// <param name="background">Color at an amount of zero.</param>
        /// <param name="amount">Foreground contribution between zero and one.</param>
        /// <returns>The blended opaque color.</returns>
        internal static Color Blend(Color foreground, Color background, float amount) => Color.FromArgb(
            (int)((foreground.R * amount) + (background.R * (1 - amount))),
            (int)((foreground.G * amount) + (background.G * (1 - amount))),
            (int)((foreground.B * amount) + (background.B * (1 - amount))));

        /// <summary>Creates a closed rounded rectangle, limiting the corner diameter to its bounds.</summary>
        /// <param name="bounds">Rectangle to trace in device coordinates.</param>
        /// <param name="radius">Requested corner radius in device units.</param>
        /// <returns>A new path that the caller must dispose.</returns>
        internal static GraphicsPath Path(RectangleF bounds, float radius) {
            var path = new GraphicsPath();
            var diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
