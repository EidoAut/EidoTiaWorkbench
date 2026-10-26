// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ThemeFonts.cs
// Creates shared fonts used by themed WinForms controls.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Creates shared fonts used by themed WinForms controls.
    /// </summary>
    public static class ThemeFonts {
        private static readonly Font _pageTitleFont = new Font("Segoe UI", 22F, FontStyle.Bold, GraphicsUnit.Point);
        private static readonly Font _sectionTitleFont = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point);
        private static readonly Font _bodyFont = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point);
        private static readonly Font _secondaryFont = new Font(
            "Segoe UI",
            10.5F,
            FontStyle.Regular,
            GraphicsUnit.Point);

        private static readonly Font _badgeFont = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);

        /// <summary>
        /// Gets a large bold font for page titles.
        /// </summary>
        public static Font PageTitle {
            get {
                return _pageTitleFont;
            }
        }

        /// <summary>
        /// Gets a bold font for section headings.
        /// </summary>
        public static Font SectionTitle {
            get {
                return _sectionTitleFont;
            }
        }

        /// <summary>
        /// Gets the default body font for regular UI text.
        /// </summary>
        public static Font Body {
            get {
                return _bodyFont;
            }
        }

        /// <summary>
        /// Gets a smaller font for secondary labels and hints.
        /// </summary>
        public static Font Secondary {
            get {
                return _secondaryFont;
            }
        }

        /// <summary>
        /// Gets a compact bold font for status badges.
        /// </summary>
        public static Font Badge {
            get {
                return _badgeFont;
            }
        }
    }
}
