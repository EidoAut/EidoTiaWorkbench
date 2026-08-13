// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

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

        /// <summary>
        /// Application window background color.
        /// </summary>
        public static Color Background {
            get {
                return UseDarkPalette ? Color.FromArgb(0x0B, 0x1F, 0x2E) : Color.FromArgb(0xF3, 0xF7, 0xFA);
            }
        }

        /// <summary>
        /// Primary surface color for panels and cards.
        /// </summary>
        public static Color Surface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x13, 0x2C, 0x3B) : Color.White;
            }
        }

        /// <summary>
        /// Secondary surface color for headers and subtle bands.
        /// </summary>
        public static Color SurfaceSecondary {
            get {
                return UseDarkPalette ? Color.FromArgb(0x18, 0x38, 0x4A) : Color.FromArgb(0xEA, 0xF3, 0xF6);
            }
        }

        /// <summary>
        /// Input field surface color.
        /// </summary>
        public static Color InputSurface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x0F, 0x2A, 0x3A) : Color.FromArgb(0xF5, 0xFA, 0xFC);
            }
        }

        /// <summary>
        /// Border color for separators and framed controls.
        /// </summary>
        public static Color Border {
            get {
                return UseDarkPalette ? Color.FromArgb(0x3F, 0x72, 0x8A) : Color.FromArgb(0xC8, 0xD8, 0xE0);
            }
        }

        /// <summary>
        /// Strong border color for active technical chrome.
        /// </summary>
        public static Color BorderStrong {
            get {
                return UseDarkPalette ? Color.FromArgb(0x67, 0xA9, 0xC4) : Color.FromArgb(0x86, 0xBF, 0xD0);
            }
        }

        /// <summary>
        /// Primary accent color for main actions.
        /// </summary>
        public static Color Primary {
            get {
                return UseDarkPalette ? Color.FromArgb(0x6D, 0xCA, 0xE8) : Color.FromArgb(0x00, 0x7F, 0x9E);
            }
        }

        /// <summary>
        /// Hover color for primary action controls.
        /// </summary>
        public static Color PrimaryHover {
            get {
                return UseDarkPalette ? Color.FromArgb(0x94, 0xE6, 0xFF) : Color.FromArgb(0x00, 0x5F, 0x76);
            }
        }

        /// <summary>
        /// Text color used on primary action backgrounds.
        /// </summary>
        public static Color OnPrimary {
            get {
                return UseDarkPalette ? Color.FromArgb(0x06, 0x20, 0x2C) : Color.White;
            }
        }

        /// <summary>
        /// Main text color for high-priority content.
        /// </summary>
        public static Color TextPrimary {
            get {
                return UseDarkPalette ? Color.FromArgb(0xFA, 0xFD, 0xFF) : Color.FromArgb(0x0B, 0x17, 0x20);
            }
        }

        /// <summary>
        /// Muted text color for supporting content.
        /// </summary>
        public static Color TextSecondary {
            get {
                return UseDarkPalette ? Color.FromArgb(0xBC, 0xD0, 0xDD) : Color.FromArgb(0x52, 0x63, 0x71);
            }
        }

        /// <summary>
        /// Success state color.
        /// </summary>
        public static Color Success {
            get {
                return UseDarkPalette ? Color.FromArgb(0x58, 0xFF, 0x9E) : Color.FromArgb(0x15, 0x80, 0x3D);
            }
        }

        /// <summary>
        /// Warning state color.
        /// </summary>
        public static Color Warning {
            get {
                return UseDarkPalette ? Color.FromArgb(0xFF, 0xF0, 0x8A) : Color.FromArgb(0xB7, 0x79, 0x1F);
            }
        }

        /// <summary>
        /// Warning banner background color.
        /// </summary>
        public static Color WarningSurface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x44, 0x38, 0x17) : Color.FromArgb(0xFF, 0xF6, 0xD6);
            }
        }

        /// <summary>
        /// Error state color.
        /// </summary>
        public static Color Error {
            get {
                return UseDarkPalette ? Color.FromArgb(0xFF, 0x78, 0x90) : Color.FromArgb(0xC2, 0x41, 0x5A);
            }
        }

        /// <summary>
        /// Informational state color.
        /// </summary>
        public static Color Info {
            get {
                return UseDarkPalette ? Color.FromArgb(0x94, 0xE6, 0xFF) : Color.FromArgb(0x14, 0x7C, 0xA3);
            }
        }

        /// <summary>
        /// Disabled control color.
        /// </summary>
        public static Color Disabled {
            get {
                return UseDarkPalette ? Color.FromArgb(0x8F, 0xA2, 0xB0) : Color.FromArgb(0x94, 0xA3, 0xB8);
            }
        }

        /// <summary>
        /// Sidebar background color.
        /// </summary>
        public static Color Sidebar {
            get {
                return UseDarkPalette ? Color.FromArgb(0x08, 0x17, 0x25) : Color.FromArgb(0x00, 0x07, 0x0C);
            }
        }

        /// <summary>
        /// Sidebar item hover color.
        /// </summary>
        public static Color SidebarHover {
            get {
                return UseDarkPalette ? Color.FromArgb(0x12, 0x34, 0x4A) : Color.FromArgb(0x08, 0x23, 0x35);
            }
        }

        /// <summary>
        /// Operation overlay background color.
        /// </summary>
        public static Color OperationSurface {
            get {
                return UseDarkPalette ? Color.FromArgb(0x16, 0x44, 0x57) : Color.FromArgb(0xE7, 0xF8, 0xFC);
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
