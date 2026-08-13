// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Stores a resolved set of UI colors so controls can be rethemed in place.
    /// </summary>
    public sealed class ThemePalette {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThemePalette"/> class.
        /// </summary>
        public ThemePalette(
            Color background,
            Color surface,
            Color surfaceSecondary,
            Color inputSurface,
            Color border,
            Color borderStrong,
            Color primary,
            Color primaryHover,
            Color onPrimary,
            Color textPrimary,
            Color textSecondary,
            Color success,
            Color warning,
            Color warningSurface,
            Color error,
            Color info,
            Color disabled,
            Color sidebar,
            Color sidebarHover,
            Color operationSurface) {
            this.Background = background;
            this.Surface = surface;
            this.SurfaceSecondary = surfaceSecondary;
            this.InputSurface = inputSurface;
            this.Border = border;
            this.BorderStrong = borderStrong;
            this.Primary = primary;
            this.PrimaryHover = primaryHover;
            this.OnPrimary = onPrimary;
            this.TextPrimary = textPrimary;
            this.TextSecondary = textSecondary;
            this.Success = success;
            this.Warning = warning;
            this.WarningSurface = warningSurface;
            this.Error = error;
            this.Info = info;
            this.Disabled = disabled;
            this.Sidebar = sidebar;
            this.SidebarHover = sidebarHover;
            this.OperationSurface = operationSurface;
        }

        /// <summary>
        /// Gets the application background color.
        /// </summary>
        public Color Background {
            get;
        }

        /// <summary>
        /// Gets the primary surface color.
        /// </summary>
        public Color Surface {
            get;
        }

        /// <summary>
        /// Gets the secondary surface color.
        /// </summary>
        public Color SurfaceSecondary {
            get;
        }

        /// <summary>
        /// Gets the input surface color.
        /// </summary>
        public Color InputSurface {
            get;
        }

        /// <summary>
        /// Gets the standard border color.
        /// </summary>
        public Color Border {
            get;
        }

        /// <summary>
        /// Gets the stronger border color.
        /// </summary>
        public Color BorderStrong {
            get;
        }

        /// <summary>
        /// Gets the primary action color.
        /// </summary>
        public Color Primary {
            get;
        }

        /// <summary>
        /// Gets the primary hover color.
        /// </summary>
        public Color PrimaryHover {
            get;
        }

        /// <summary>
        /// Gets the text color used on primary backgrounds.
        /// </summary>
        public Color OnPrimary {
            get;
        }

        /// <summary>
        /// Gets the primary text color.
        /// </summary>
        public Color TextPrimary {
            get;
        }

        /// <summary>
        /// Gets the secondary text color.
        /// </summary>
        public Color TextSecondary {
            get;
        }

        /// <summary>
        /// Gets the success color.
        /// </summary>
        public Color Success {
            get;
        }

        /// <summary>
        /// Gets the warning color.
        /// </summary>
        public Color Warning {
            get;
        }

        /// <summary>
        /// Gets the warning surface color.
        /// </summary>
        public Color WarningSurface {
            get;
        }

        /// <summary>
        /// Gets the error color.
        /// </summary>
        public Color Error {
            get;
        }

        /// <summary>
        /// Gets the information color.
        /// </summary>
        public Color Info {
            get;
        }

        /// <summary>
        /// Gets the disabled color.
        /// </summary>
        public Color Disabled {
            get;
        }

        /// <summary>
        /// Gets the sidebar color.
        /// </summary>
        public Color Sidebar {
            get;
        }

        /// <summary>
        /// Gets the sidebar hover color.
        /// </summary>
        public Color SidebarHover {
            get;
        }

        /// <summary>
        /// Gets the operation overlay surface color.
        /// </summary>
        public Color OperationSurface {
            get;
        }
    }
}
