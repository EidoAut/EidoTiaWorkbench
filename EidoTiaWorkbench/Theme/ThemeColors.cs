// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ThemeColors.cs
// Provides the shared color palette used by the WinForms interface.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Provides the shared color palette used by the WinForms interface.
    /// </summary>
    public static class ThemeColors {
        /// <summary>
        /// Gets or sets a value indicating whether the dark Eido palette is active.
        /// </summary>
        public static bool UseDarkPalette {
            get; set;
        }

        /// <summary>Gets muted text for supporting metadata on base surfaces.</summary>
        public static Color TextMuted => UseDarkPalette ? Color.FromArgb(129, 147, 157) : Color.FromArgb(80, 99, 112);

        /// <summary>
        /// Gets the application window background color.
        /// </summary>
        public static Color Background {
            get {
                return UseDarkPalette ? Color.FromArgb(0x00, 0x07, 0x0C) : Color.FromArgb(0xF3, 0xF7, 0xFA);
            }
        }

        /// <summary>
        /// Gets the primary surface color for panels and cards.
        /// </summary>
        public static Color Surface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x00, 0x0C, 0x16) : Color.White;
            }
        }

        /// <summary>
        /// Gets the secondary surface color for headers and subtle bands.
        /// </summary>
        public static Color SurfaceSecondary {
            get {
                return UseDarkPalette ? Color.FromArgb(0x07, 0x13, 0x1D) : Color.FromArgb(0xEA, 0xF3, 0xF6);
            }
        }

        /// <summary>
        /// Gets the input field surface color.
        /// </summary>
        public static Color InputSurface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x00, 0x0A, 0x12) : Color.FromArgb(0xF5, 0xFA, 0xFC);
            }
        }

        /// <summary>
        /// Gets the border color for separators and framed controls.
        /// </summary>
        public static Color Border {
            get {
                return UseDarkPalette ? Color.FromArgb(0x1A, 0x2A, 0x34) : Color.FromArgb(0xC8, 0xD8, 0xE0);
            }
        }

        /// <summary>
        /// Gets the strong border color for active technical chrome.
        /// </summary>
        public static Color BorderStrong {
            get {
                return UseDarkPalette ? Color.FromArgb(0x26, 0x39, 0x46) : Color.FromArgb(0x86, 0xBF, 0xD0);
            }
        }

        /// <summary>
        /// Gets the primary accent color for main actions.
        /// </summary>
        public static Color Primary {
            get {
                return UseDarkPalette ? Color.FromArgb(0x60, 0xC0, 0xE0) : Color.FromArgb(0x00, 0x67, 0x7F);
            }
        }

        /// <summary>
        /// Gets the hover color for primary action controls.
        /// </summary>
        public static Color PrimaryHover {
            get {
                return UseDarkPalette ? Color.FromArgb(0x80, 0xDC, 0xFF) : Color.FromArgb(0x00, 0x5F, 0x76);
            }
        }

        /// <summary>
        /// Gets the text color used on primary action backgrounds.
        /// </summary>
        public static Color OnPrimary {
            get {
                return UseDarkPalette ? Color.FromArgb(0x00, 0x07, 0x0C) : Color.White;
            }
        }

        /// <summary>
        /// Gets the main text color for high-priority content.
        /// </summary>
        public static Color TextPrimary {
            get {
                return UseDarkPalette ? Color.FromArgb(0xEE, 0xF7, 0xFB) : Color.FromArgb(0x0B, 0x17, 0x20);
            }
        }

        /// <summary>
        /// Gets the muted text color for supporting content.
        /// </summary>
        public static Color TextSecondary {
            get {
                return UseDarkPalette ? Color.FromArgb(0xBA, 0xC8, 0xD1) : Color.FromArgb(0x52, 0x63, 0x71);
            }
        }

        /// <summary>
        /// Gets the success state color.
        /// </summary>
        public static Color Success {
            get {
                return UseDarkPalette ? Color.FromArgb(0x37, 0xFF, 0x8B) : Color.FromArgb(0x12, 0x66, 0x2F);
            }
        }

        /// <summary>
        /// Gets the warning state color.
        /// </summary>
        public static Color Warning {
            get {
                return UseDarkPalette ? Color.FromArgb(0xFF, 0xE6, 0x6B) : Color.FromArgb(0x80, 0x54, 0x14);
            }
        }

        /// <summary>
        /// Gets the warning banner background color.
        /// </summary>
        public static Color WarningSurface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x35, 0x2D, 0x17) : Color.FromArgb(0xFF, 0xF6, 0xD6);
            }
        }

        /// <summary>
        /// Gets the error state color.
        /// </summary>
        public static Color Error {
            get {
                return UseDarkPalette ? Color.FromArgb(0xFF, 0x5E, 0x7A) : Color.FromArgb(0xAC, 0x29, 0x44);
            }
        }

        /// <summary>
        /// Gets the informational state color.
        /// </summary>
        public static Color Info {
            get {
                return UseDarkPalette ? Color.FromArgb(0x80, 0xDC, 0xFF) : Color.FromArgb(0x00, 0x64, 0x81);
            }
        }

        /// <summary>
        /// Gets the disabled control color.
        /// </summary>
        public static Color Disabled {
            get {
                return UseDarkPalette ? Color.FromArgb(0xAC, 0xBA, 0xC4) : Color.FromArgb(0x59, 0x68, 0x76);
            }
        }

        /// <summary>
        /// Gets the sidebar background color.
        /// </summary>
        public static Color Sidebar {
            get {
                return UseDarkPalette ? Color.FromArgb(0x00, 0x0A, 0x12) : Color.FromArgb(0xE7, 0xEF, 0xF3);
            }
        }

        /// <summary>
        /// Gets the sidebar item hover color.
        /// </summary>
        public static Color SidebarHover {
            get {
                return UseDarkPalette ? Color.FromArgb(0x0D, 0x20, 0x2C) : Color.FromArgb(0xD5, 0xE7, 0xEE);
            }
        }

        /// <summary>
        /// Gets the operation overlay background color.
        /// </summary>
        public static Color OperationSurface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x0D, 0x20, 0x2C) : Color.FromArgb(0xE7, 0xF8, 0xFC);
            }
        }

        /// <summary>
        /// Creates a point-in-time snapshot of the active palette.
        /// </summary>
        /// <returns>The active palette colors.</returns>
        public static ThemePalette Snapshot() {
            return new ThemePalette(
                Background,
                Surface,
                SurfaceSecondary,
                InputSurface,
                Border,
                BorderStrong,
                Primary,
                PrimaryHover,
                OnPrimary,
                TextPrimary,
                TextSecondary,
                Success,
                Warning,
                WarningSurface,
                Error,
                Info,
                Disabled,
                Sidebar,
                SidebarHover,
                OperationSurface);
        }
    }
}
