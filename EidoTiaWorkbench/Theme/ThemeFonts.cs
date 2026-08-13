// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Drawing;

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Creates shared fonts used by themed WinForms controls.
    /// </summary>
    public static class ThemeFonts {
        /// <summary>
        /// Gets a large bold font for page titles.
        /// </summary>
        public static Font PageTitle {
            get {
                return new Font("Segoe UI", 19F, FontStyle.Bold, GraphicsUnit.Point);
            }
        }

        /// <summary>
        /// Gets a bold font for section headings.
        /// </summary>
        public static Font SectionTitle {
            get {
                return new Font("Segoe UI", 11.5F, FontStyle.Bold, GraphicsUnit.Point);
            }
        }

        /// <summary>
        /// Gets the default body font for regular UI text.
        /// </summary>
        public static Font Body {
            get {
                return new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        /// <summary>
        /// Gets a smaller font for secondary labels and hints.
        /// </summary>
        public static Font Secondary {
            get {
                return new Font("Segoe UI", 8.75F, FontStyle.Regular, GraphicsUnit.Point);
            }
        }

        /// <summary>
        /// Gets a compact bold font for status badges.
        /// </summary>
        public static Font Badge {
            get {
                return new Font("Segoe UI", 8.5F, FontStyle.Bold, GraphicsUnit.Point);
            }
        }
    }
}
