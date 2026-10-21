// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// IconProvider.cs
// Generates simple first-party bitmap icons for the desktop UI.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Generates simple first-party bitmap icons for the desktop UI.
    /// </summary>
    public static class IconProvider {
        /// <summary>
        /// Creates a bitmap icon for a known UI symbol name.
        /// </summary>
        /// <param name="name">Icon name such as dashboard, project, export, import, or warning.</param>
        /// <param name="color">Stroke color used to draw the icon.</param>
        /// <param name="size">Square icon size in pixels.</param>
        /// <returns>A transparent bitmap containing the requested icon.</returns>
        public static Bitmap Get(string name, Color color, int size = 18) {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                using (var pen = new Pen(color, Math.Max(1.6F, size / 11F))) {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;

                    var key = (name ?? string.Empty).ToLowerInvariant();
                    if (key == "dashboard") {
                        DrawDashboard(g, pen, size);
                    }
                    else if (key == "project") {
                        DrawProject(g, pen, size);
                    }
                    else if (key == "export") {
                        DrawArrow(g, pen, size, true);
                    }
                    else if (key == "import") {
                        DrawArrow(g, pen, size, false);
                    }
                    else if (key == "activity") {
                        DrawActivity(g, pen, size);
                    }
                    else if (key == "settings") {
                        DrawSettings(g, pen, size);
                    }
                    else if (key == "plc") {
                        DrawChip(g, pen, size);
                    }
                    else if (key == "hmi" || key == "screen") {
                        DrawScreen(g, pen, size);
                    }
                    else if (key == "warning") {
                        DrawWarning(g, pen, size);
                    }
                    else if (key == "error") {
                        DrawError(g, pen, size);
                    }
                    else if (key == "success") {
                        DrawSuccess(g, pen, size);
                    }
                    else {
                        DrawDocument(g, pen, size);
                    }
                }
            }

            return bmp;
        }

        /// <summary>
        /// Draws a dashboard grid icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawDashboard(Graphics g, Pen pen, int s) {
            var w = (s / 2) - 3;
            g.DrawRectangle(pen, 2, 2, w, w);
            g.DrawRectangle(pen, (s / 2) + 1, 2, w, w);
            g.DrawRectangle(pen, 2, (s / 2) + 1, w, w);
            g.DrawRectangle(pen, (s / 2) + 1, (s / 2) + 1, w, w);
        }

        /// <summary>
        /// Draws a project folder icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawProject(Graphics g, Pen pen, int s) {
            g.DrawRectangle(pen, 2, 5, s - 4, s - 7);
            g.DrawLine(pen, 4, 5, 7, 2);
            g.DrawLine(pen, 7, 2, s - 5, 2);
        }

        /// <summary>
        /// Draws an import or export arrow icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        /// <param name="right">True to draw a right-facing arrow; false for left-facing.</param>
        private static void DrawArrow(Graphics g, Pen pen, int s, bool right) {
            var y = s / 2;
            if (right) {
                g.DrawLine(pen, 3, y, s - 4, y);
                g.DrawLine(pen, s - 7, y - 4, s - 3, y);
                g.DrawLine(pen, s - 7, y + 4, s - 3, y);
            }
            else {
                g.DrawLine(pen, s - 3, y, 4, y);
                g.DrawLine(pen, 7, y - 4, 3, y);
                g.DrawLine(pen, 7, y + 4, 3, y);
            }
        }

        /// <summary>
        /// Draws an activity trend icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawActivity(Graphics g, Pen pen, int s) {
            g.DrawLine(pen, 2, s - 4, 5, s - 8);
            g.DrawLine(pen, 5, s - 8, 8, s - 5);
            g.DrawLine(pen, 8, s - 5, 12, 4);
            g.DrawLine(pen, 12, 4, s - 2, 7);
        }

        /// <summary>
        /// Draws a settings icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawSettings(Graphics g, Pen pen, int s) {
            g.DrawEllipse(pen, 4, 4, s - 8, s - 8);
            g.DrawLine(pen, s / 2, 1, s / 2, 5);
            g.DrawLine(pen, s / 2, s - 5, s / 2, s - 1);
            g.DrawLine(pen, 1, s / 2, 5, s / 2);
            g.DrawLine(pen, s - 5, s / 2, s - 1, s / 2);
        }

        /// <summary>
        /// Draws a PLC chip icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawChip(Graphics g, Pen pen, int s) {
            g.DrawRectangle(pen, 4, 4, s - 8, s - 8);
            for (var i = 5; i < s - 4; i += 4) {
                g.DrawLine(pen, i, 1, i, 4);
                g.DrawLine(pen, i, s - 4, i, s - 1);
            }
        }

        /// <summary>
        /// Draws an HMI screen icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawScreen(Graphics g, Pen pen, int s) {
            g.DrawRectangle(pen, 2, 3, s - 4, s - 7);
            g.DrawLine(pen, s / 2, s - 4, s / 2, s - 1);
            g.DrawLine(pen, (s / 2) - 4, s - 1, (s / 2) + 4, s - 1);
        }

        /// <summary>
        /// Draws a warning triangle icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawWarning(Graphics g, Pen pen, int s) {
            g.DrawPolygon(pen, new[] { new Point(s / 2, 2), new Point(s - 2, s - 2), new Point(2, s - 2) });
            g.DrawLine(pen, s / 2, 7, s / 2, s - 7);
            g.DrawEllipse(pen, (s / 2) - 1, s - 5, 2, 2);
        }

        /// <summary>
        /// Draws an error icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawError(Graphics g, Pen pen, int s) {
            g.DrawEllipse(pen, 2, 2, s - 4, s - 4);
            g.DrawLine(pen, 6, 6, s - 6, s - 6);
            g.DrawLine(pen, s - 6, 6, 6, s - 6);
        }

        /// <summary>
        /// Draws a success checkmark icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawSuccess(Graphics g, Pen pen, int s) {
            g.DrawLine(pen, 3, s / 2, (s / 2) - 1, s - 5);
            g.DrawLine(pen, (s / 2) - 1, s - 5, s - 3, 4);
        }

        /// <summary>
        /// Draws a generic document icon.
        /// </summary>
        /// <param name="g">Graphics surface.</param>
        /// <param name="pen">Pen used for icon strokes.</param>
        /// <param name="s">Icon size.</param>
        private static void DrawDocument(Graphics g, Pen pen, int s) {
            g.DrawRectangle(pen, 4, 2, s - 8, s - 4);
            g.DrawLine(pen, 7, 7, s - 7, 7);
            g.DrawLine(pen, 7, 11, s - 7, 11);
        }
    }
}
