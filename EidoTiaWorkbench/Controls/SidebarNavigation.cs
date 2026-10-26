// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// SidebarNavigation.cs
// Provides the shell sidebar navigation with main and bottom action groups.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides the shell sidebar navigation with main and bottom action groups.
    /// </summary>
    public sealed class SidebarNavigation : UserControl {
        private readonly Dictionary<string, Button> _buttons = new Dictionary<string, Button>(
            StringComparer.OrdinalIgnoreCase);

        private readonly FlowLayoutPanel _top;
        private readonly FlowLayoutPanel _bottom;
        private string _activeKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="SidebarNavigation"/> class.
        /// </summary>
        public SidebarNavigation() {
            this.Dock = DockStyle.Fill;

            // The sidebar uses a stable width so navigation labels and icons align across views.
            this.Width = 220;
            this.BackColor = ThemeColors.Sidebar;
            this.Padding = new Padding(10, 12, 10, 12);

            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            this._top = CreatePanel();
            this._bottom = CreatePanel();
            this._bottom.AutoSize = true;
            this._bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this._top.SizeChanged += (s, e) => ResizeNavigationControls(this._top);
            this._bottom.SizeChanged += (s, e) => ResizeNavigationControls(this._bottom);
            this._top.Controls.Add(CreateBrandBlock());
            layout.Controls.Add(this._top, 0, 0);
            layout.Controls.Add(this._bottom, 0, 1);
            this.Controls.Add(layout);
        }

        /// <summary>
        /// Occurs when the user clicks a navigation item.
        /// </summary>
        public event EventHandler<NavigationEventArgs> NavigationRequested;

        /// <summary>
        /// Adds a navigation item to the main group.
        /// </summary>
        /// <param name="key">Stable navigation key.</param>
        /// <param name="text">Text shown on the button.</param>
        /// <param name="icon">Icon name passed to the icon provider.</param>
        public void AddMainItem(string key, string text, string icon) {
            if (key == "Dashboard" || key == "Export" || key == "Activity") {
                this._top.Controls.Add(new Label {
                    Text = key == "Dashboard" ? "WORKSPACE" : key == "Export" ? "ENGINEERING" : "SYSTEM",
                    AutoSize = true,
                    ForeColor = ThemeColors.TextSecondary,
                    Font = ThemeFonts.Badge,
                    Margin = new Padding(12, 8, 0, 4),
                });
            }

            var button = this.CreateButton(key, text, icon);
            this._top.Controls.Add(button);
            this._buttons[key] = button;
        }

        /// <summary>
        /// Adds a navigation item to the bottom group.
        /// </summary>
        /// <param name="key">Stable navigation key.</param>
        /// <param name="text">Text shown on the button.</param>
        /// <param name="icon">Icon name passed to the icon provider.</param>
        public void AddBottomItem(string key, string text, string icon) {
            var button = this.CreateButton(key, text, icon);
            this._bottom.Controls.Add(button);
            this._buttons[key] = button;
        }

        /// <summary>
        /// Marks one navigation item as active and updates button colors.
        /// </summary>
        /// <param name="key">Stable navigation key to activate.</param>
        public void SetActive(string key) {
            this._activeKey = key;
            foreach (var pair in this._buttons) {
                var active = string.Equals(pair.Key, this._activeKey, StringComparison.OrdinalIgnoreCase);
                ((WorkbenchNavigationButton)pair.Value).Selected = active;
                pair.Value.Invalidate();
                pair.Value.BackColor = active ? ThemeColors.SidebarHover : ThemeColors.Sidebar;
                pair.Value.ForeColor = active ? ThemeColors.PrimaryHover : ThemeColors.TextSecondary;
                pair.Value.FlatAppearance.BorderColor = active ? ThemeColors.SidebarHover : ThemeColors.Sidebar;
            }
        }

        /// <summary>
        /// Fits navigation buttons to the available scroll viewport.
        /// </summary>
        /// <param name="panel">Navigation group whose children need resizing.</param>
        private static void ResizeNavigationControls(FlowLayoutPanel panel) {
            foreach (Control child in panel.Controls) {
                child.Width = Math.Max(
                    100,
                    panel.ClientSize.Width - child.Margin.Horizontal - SystemInformation.VerticalScrollBarWidth);
            }
        }

        private static FlowLayoutPanel CreatePanel() {
            return new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,

                // Vertical navigation should scroll instead of wrapping into multiple columns.
                WrapContents = false,
                AutoScroll = true,
            };
        }

        /// <summary>
        /// Creates the compact Eido identity block shown above navigation.
        /// </summary>
        /// <returns>The configured brand block.</returns>
        private static Control CreateBrandBlock() {
            var block = new TableLayoutPanel {
                Width = 196,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3,
                Margin = new Padding(0, 0, 0, 16),
                Padding = new Padding(8, 4, 8, 8),
                BackColor = ThemeColors.Sidebar,
            };
            block.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            block.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            block.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            block.Controls.Add(
                new Label {
                    Text = "EIDO",
                    AutoSize = true,
                    Font = ThemeFonts.SectionTitle,
                    ForeColor = ThemeColors.PrimaryHover,
                    Margin = Padding.Empty,
                },
                0,
                0);
            block.Controls.Add(
                new Label {
                    Text = "TIA WORKBENCH",
                    AutoSize = true,
                    Font = ThemeFonts.Badge,
                    ForeColor = ThemeColors.TextPrimary,
                    Margin = new Padding(0, 1, 0, 0),
                },
                0,
                1);
            return block;
        }

        /// <summary>
        /// Creates a sidebar navigation button.
        /// </summary>
        /// <param name="key">Stable navigation key.</param>
        /// <param name="text">Button text.</param>
        /// <param name="icon">Icon provider key.</param>
        /// <returns>The configured navigation button.</returns>
        private Button CreateButton(string key, string text, string icon) {
            var button = new WorkbenchNavigationButton {
                Text = text,
                Name = key,

                // Sidebar buttons have fixed geometry to keep the navigation rail visually stable.
                Width = 196,
                Height = 40,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Image = IconProvider.Get(icon, Color.White, 17),
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemeColors.Sidebar,
                ForeColor = ThemeColors.TextPrimary,
                Font = ThemeFonts.Body,
                Margin = new Padding(0, 0, 0, 4),
                Padding = new Padding(8, 0, 8, 0),
                Cursor = Cursors.Hand,
            };
            button.FlatAppearance.BorderColor = ThemeColors.Sidebar;
            button.FlatAppearance.MouseOverBackColor = ThemeColors.SidebarHover;
            button.Click += (s, e) => this.NavigationRequested?.Invoke(this, new NavigationEventArgs(key));
            return button;
        }
    }
}
