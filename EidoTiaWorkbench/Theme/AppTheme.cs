// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// AppTheme.cs
// Applies shared visual styling to forms and common WinForms controls.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EidoTiaWorkbench.Controls;
using Microsoft.Win32;

namespace EidoTiaWorkbench.Theme {
    /// <summary>
    /// Applies shared visual styling to forms and common WinForms controls.
    /// </summary>
    public static class AppTheme {
        private const string SettingsDirectoryName = "EidoTiaWorkbench";
        private const string SettingsFileName = "theme.txt";

        private static ThemePreference _currentPreference = ThemePreference.Dark;

        /// <summary>
        /// Gets the currently selected theme preference.
        /// </summary>
        public static ThemePreference CurrentPreference {
            get {
                return _currentPreference;
            }
        }

        /// <summary>
        /// Loads the persisted theme preference and applies its effective palette.
        /// </summary>
        public static void LoadPreference() {
            var preference = ThemePreference.Dark;
            try {
                var path = GetSettingsFilePath();
                if (File.Exists(path)) {
                    ThemePreference parsed;
                    if (Enum.TryParse(File.ReadAllText(path).Trim(), true, out parsed)) {
                        preference = parsed;
                    }
                }
            }
            catch {
                preference = ThemePreference.Dark;
            }

            SetPreference(preference);
        }

        /// <summary>
        /// Stores the current theme preference for future launches.
        /// </summary>
        public static void SavePreference() {
            try {
                var path = GetSettingsFilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                File.WriteAllText(path, _currentPreference.ToString());
            }
            catch {
                // Theme persistence should never block the application.
            }
        }

        /// <summary>
        /// Sets the active theme preference.
        /// </summary>
        /// <param name="preference">Theme preference selected by the user.</param>
        public static void SetPreference(ThemePreference preference) {
            _currentPreference = preference;
            ThemeColors.UseDarkPalette = ResolveDarkPalette(preference);
        }

        /// <summary>
        /// Changes the preference and rethemes an existing control tree in place.
        /// </summary>
        /// <param name="root">Root control to repaint.</param>
        /// <param name="preference">Theme preference selected by the user.</param>
        public static void ChangePreference(Control root, ThemePreference preference) {
            var previous = ThemeColors.Snapshot();
            SetPreference(preference);
            SavePreference();
            ApplyThemeTree(root, previous);
        }

        /// <summary>
        /// Applies base application colors, font, and DPI scaling to a form.
        /// </summary>
        /// <param name="form">Form to style.</param>
        public static void ApplyTo(Form form) {
            form.BackColor = ThemeColors.Background;
            form.ForeColor = ThemeColors.TextPrimary;
            form.Font = ThemeFonts.Body;
            form.AutoScaleMode = AutoScaleMode.Dpi;
        }

        /// <summary>
        /// Applies base application colors and font to a control.
        /// </summary>
        /// <param name="control">Control to style.</param>
        public static void ApplyTo(Control control) {
            control.BackColor = ThemeColors.Background;
            control.ForeColor = ThemeColors.TextPrimary;
            control.Font = ThemeFonts.Body;
        }

        /// <summary>
        /// Applies primary panel surface styling.
        /// </summary>
        /// <param name="control">Panel-like control to style.</param>
        public static void StylePanel(Control control) {
            control.BackColor = ThemeColors.Surface;
            control.ForeColor = ThemeColors.TextPrimary;
        }

        /// <summary>
        /// Applies secondary panel surface styling.
        /// </summary>
        /// <param name="control">Panel-like control to style.</param>
        public static void StyleSecondaryPanel(Control control) {
            control.BackColor = ThemeColors.SurfaceSecondary;
            control.ForeColor = ThemeColors.TextPrimary;
        }

        /// <summary>
        /// Applies shared table styling to a data grid.
        /// </summary>
        /// <param name="grid">Grid control to style.</param>
        public static void StyleGrid(DataGridView grid) {
            grid.BackgroundColor = ThemeColors.Surface;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = (int)(40 * grid.DeviceDpi / 96F);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = ThemeColors.Border;
            grid.Font = ThemeFonts.Body;
            grid.ColumnHeadersDefaultCellStyle.BackColor = ThemeColors.SurfaceSecondary;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = ThemeColors.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.Font = ThemeFonts.SectionTitle;
            grid.DefaultCellStyle.BackColor = ThemeColors.Surface;
            grid.DefaultCellStyle.ForeColor = ThemeColors.TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = ThemeColors.OperationSurface;
            grid.DefaultCellStyle.SelectionForeColor = ThemeColors.TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.BackColor = ThemeColors.InputSurface;
            grid.AlternatingRowsDefaultCellStyle.ForeColor = ThemeColors.TextPrimary;
            grid.RowHeadersDefaultCellStyle.BackColor = ThemeColors.SurfaceSecondary;
            grid.RowHeadersDefaultCellStyle.ForeColor = ThemeColors.TextPrimary;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = ThemeColors.SurfaceSecondary;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = ThemeColors.TextPrimary;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = ThemeColors.OperationSurface;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = ThemeColors.TextPrimary;
            grid.RowTemplate.Height = (int)(36 * grid.DeviceDpi / 96F);
            grid.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        }

        /// <summary>
        /// Applies shared input styling to a control.
        /// </summary>
        /// <param name="control">Input control to style.</param>
        public static void StyleInput(Control control) {
            control.BackColor = ThemeColors.InputSurface;
            control.ForeColor = ThemeColors.TextPrimary;
            control.Font = ThemeFonts.Body;

            var textBox = control as TextBox;
            if (textBox != null) {
                if (textBox.BorderStyle != BorderStyle.None) {
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }
            }

            var comboBox = control as ComboBox;
            if (comboBox != null) {
                comboBox.FlatStyle = FlatStyle.Flat;
                comboBox.IntegralHeight = false;
            }
        }

        /// <summary>
        /// Applies shared list styling to a list view.
        /// </summary>
        /// <param name="list">List view to style.</param>
        public static void StyleList(ListView list) {
            list.BackColor = ThemeColors.Surface;
            list.ForeColor = ThemeColors.TextPrimary;
            list.BorderStyle = BorderStyle.None;
            list.Font = ThemeFonts.Body;
            list.OwnerDraw = true;

            // Re-theming must not accumulate paint handlers.
            list.DrawColumnHeader -= DrawListHeader;
            list.DrawColumnHeader += DrawListHeader;
            list.DrawItem -= DrawListItem;
            list.DrawItem += DrawListItem;
            list.DrawSubItem -= DrawListSubItem;
            list.DrawSubItem += DrawListSubItem;
        }

        /// <summary>
        /// Applies shared tree styling to a tree view.
        /// </summary>
        /// <param name="tree">Tree view to style.</param>
        public static void StyleTree(TreeView tree) {
            tree.BackColor = ThemeColors.InputSurface;
            tree.ForeColor = ThemeColors.TextPrimary;
            tree.LineColor = ThemeColors.BorderStrong;
            tree.Font = ThemeFonts.Body;
            tree.ItemHeight = (int)(32 * tree.DeviceDpi / 96F);
            tree.Indent = (int)(24 * tree.DeviceDpi / 96F);
            tree.FullRowSelect = true;
            tree.ShowLines = false;
            tree.DrawMode = TreeViewDrawMode.OwnerDrawAll;
            tree.DrawNode -= DrawTreeNode;
            tree.DrawNode += DrawTreeNode;
            tree.BorderStyle = BorderStyle.None;
            StyleTreeNodes(tree.Nodes);
        }

        /// <summary>
        /// Applies the active palette to a control tree.
        /// </summary>
        /// <param name="root">Root control to repaint.</param>
        public static void ApplyThemeTree(Control root) {
            ApplyThemeTree(root, null);
        }

        /// <summary>
        /// Applies the active palette to a control tree, replacing colors from a previous palette when available.
        /// </summary>
        /// <param name="root">Root control to repaint.</param>
        /// <param name="previous">Previous palette, or <see langword="null"/> for a direct pass.</param>
        public static void ApplyThemeTree(Control root, ThemePalette previous) {
            if (root == null) {
                return;
            }

            ApplyControlTheme(root, previous);
            foreach (Control child in root.Controls) {
                ApplyThemeTree(child, previous);
            }
        }

        private static void DrawTreeNode(object sender, DrawTreeNodeEventArgs e) {
            var tree = (TreeView)sender;
            var selected = tree.SelectedNode == e.Node;
            var bounds = new Rectangle(
                e.Node.Bounds.Left,
                e.Bounds.Top,
                Math.Max(e.Node.Bounds.Width, tree.ClientSize.Width - e.Node.Bounds.Left),
                e.Bounds.Height);
            using (var brush = new SolidBrush(selected ? ThemeColors.OperationSurface : ThemeColors.InputSurface)) {
                e.Graphics.FillRectangle(brush, 0, e.Bounds.Top, tree.ClientSize.Width, e.Bounds.Height);
            }

            var iconSize = tree.ImageList?.ImageSize.Width ?? 16;
            var checkSize = ThemeMetrics.Scale(tree, 14);
            var check = new Rectangle(
                bounds.Left - iconSize - checkSize - 6,
                bounds.Top + ((bounds.Height - checkSize) / 2),
                checkSize,
                checkSize);
            if (tree.CheckBoxes) {
                using (var brush = new SolidBrush(e.Node.Checked ? ThemeColors.Primary : ThemeColors.InputSurface))
                using (var border = new Pen(ThemeColors.BorderStrong)) {
                    e.Graphics.FillRectangle(brush, check);
                    e.Graphics.DrawRectangle(border, check);
                }

                if (e.Node.Checked) {
                    TextRenderer.DrawText(
                        e.Graphics,
                        "✓",
                        tree.Font,
                        check,
                        ThemeColors.OnPrimary,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                }
            }

            if (e.Node.Nodes.Count > 0) {
                var expander = new Rectangle(
                    check.Left - ThemeMetrics.Scale(tree, 20),
                    bounds.Top,
                    ThemeMetrics.Scale(tree, 16),
                    bounds.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    e.Node.IsExpanded ? "⌄" : "›",
                    tree.Font,
                    expander,
                    ThemeColors.TextSecondary,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            if (tree.ImageList != null && tree.ImageList.Images.ContainsKey(e.Node.ImageKey)) {
                tree.ImageList.Draw(
                    e.Graphics,
                    bounds.Left - iconSize - 2,
                    bounds.Top + ((bounds.Height - iconSize) / 2),
                    tree.ImageList.Images.IndexOfKey(e.Node.ImageKey));
            }

            var item = e.Node.Tag as Core.Model.ExportItem;
            var text = item == null
                || item.CanExport
                || item.CanImport
                ? ThemeColors.TextPrimary
                : ThemeColors.Disabled;
            TextRenderer.DrawText(
                e.Graphics,
                e.Node.Text,
                tree.Font,
                bounds,
                text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (selected && tree.Focused) {
                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    bounds,
                    ThemeColors.TextPrimary,
                    ThemeColors.OperationSurface);
            }
        }

        private static void DrawListHeader(object sender, DrawListViewColumnHeaderEventArgs e) {
            using (var brush = new SolidBrush(ThemeColors.SurfaceSecondary)) {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            var textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(
                e.Graphics,
                e.Header.Text,
                ((ListView)sender).Font,
                Rectangle.Inflate(e.Bounds, -8, 0),
                ThemeColors.TextPrimary,
                textFlags);
            using (var pen = new Pen(ThemeColors.BorderStrong)) {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
        }

        private static void DrawListItem(object sender, DrawListViewItemEventArgs e) {
            if (((ListView)sender).View != View.Details) {
                e.DrawDefault = true;
            }
        }

        private static void DrawListSubItem(object sender, DrawListViewSubItemEventArgs e) {
            var background = e.Item.Selected ? ThemeColors.OperationSurface : ThemeColors.Surface;
            using (var brush = new SolidBrush(background)) {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }

            var textFlags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(
                e.Graphics,
                e.SubItem.Text,
                ((ListView)sender).Font,
                Rectangle.Inflate(e.Bounds, -8, 0),
                ThemeColors.TextPrimary,
                textFlags);
            if (e.ColumnIndex == 0 && e.Item.Focused && ((ListView)sender).Focused) {
                e.DrawFocusRectangle(e.Item.Bounds);
            }
        }

        /// <summary>
        /// Builds the local file path used to persist the theme preference.
        /// </summary>
        /// <returns>The theme settings file path.</returns>
        private static string GetSettingsFilePath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsDirectoryName,
                SettingsFileName);
        }

        /// <summary>
        /// Resolves whether the given preference should use the dark palette.
        /// </summary>
        /// <param name="preference">Theme preference to resolve.</param>
        /// <returns><see langword="true"/> when the dark palette should be used.</returns>
        private static bool ResolveDarkPalette(ThemePreference preference) {
            if (preference == ThemePreference.Dark) {
                return true;
            }

            if (preference == ThemePreference.Light) {
                return false;
            }

            try {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) {
                    var value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value is int) {
                        return ((int)value) == 0;
                    }
                }
            }
            catch {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Applies role-aware theme styling to one control.
        /// </summary>
        /// <param name="control">Control to style.</param>
        /// <param name="previous">Previous palette, or <see langword="null"/> when not switching themes.</param>
        private static void ApplyControlTheme(Control control, ThemePalette previous) {
            ReplacePaletteColors(control, previous);
            if (control is TreeView
                || control is ListView
                || control is TextBox
                || control is DataGridView
                || (control is ScrollableControl
                && ((ScrollableControl)control).AutoScroll)) {
                NativeControlTheme.Apply(control);
            }

            if (control.ForeColor == SystemColors.ControlText || control.ForeColor == Color.Black) {
                control.ForeColor = ThemeColors.TextPrimary;
            }

            if (control.BackColor == SystemColors.Control || control.BackColor == SystemColors.Window) {
                control.BackColor = ThemeColors.Surface;
            }

            var form = control as Form;
            if (form != null) {
                ApplyTo(form);
            }

            var themedButton = control as ThemedButton;
            if (themedButton != null) {
                themedButton.ApplyTheme();
            }

            var artifactTree = control as ArtifactTreeControl;
            if (artifactTree != null) {
                artifactTree.ApplyTheme();
            }

            var themedComboBox = control as ThemedComboBox;
            if (themedComboBox != null) {
                themedComboBox.ApplyTheme();
            }

            var textBox = control as TextBox;
            if (textBox != null) {
                StyleInput(textBox);
            }

            var comboBox = control as ComboBox;
            if (comboBox != null) {
                StyleInput(comboBox);
            }

            var grid = control as DataGridView;
            if (grid != null) {
                StyleGrid(grid);
            }

            var list = control as ListView;
            if (list != null) {
                StyleList(list);
            }

            var tree = control as TreeView;
            if (tree != null) {
                StyleTree(tree);
            }

            var checkBox = control as CheckBox;
            if (checkBox != null) {
                checkBox.BackColor = checkBox.Parent == null ? ThemeColors.Background : checkBox.Parent.BackColor;
                checkBox.ForeColor = ThemeColors.TextPrimary;
            }

            var radioButton = control as RadioButton;
            if (radioButton != null) {
                radioButton.BackColor = radioButton.Parent == null
                    ? ThemeColors.Background
                    : radioButton.Parent.BackColor;
                radioButton.ForeColor = ThemeColors.TextPrimary;
            }

            var statusStrip = control as StatusStrip;
            if (statusStrip != null) {
                statusStrip.BackColor = ThemeColors.Surface;
                statusStrip.ForeColor = ThemeColors.TextSecondary;
                foreach (ToolStripItem item in statusStrip.Items) {
                    item.ForeColor = ThemeColors.TextSecondary;
                    var link = item as ToolStripStatusLabel;
                    if (link != null && link.IsLink) {
                        link.LinkColor = ThemeColors.Primary;
                        link.ActiveLinkColor = ThemeColors.PrimaryHover;
                    }
                }
            }
        }

        /// <summary>
        /// Replaces colors that match a previous palette with their active equivalents.
        /// </summary>
        /// <param name="control">Control whose direct colors should be updated.</param>
        /// <param name="previous">Previous palette, or <see langword="null"/> when not switching themes.</param>
        private static void ReplacePaletteColors(Control control, ThemePalette previous) {
            if (previous == null) {
                return;
            }

            control.BackColor = MapColor(control.BackColor, previous);
            control.ForeColor = MapColor(control.ForeColor, previous);
        }

        /// <summary>
        /// Maps one color from a previous palette to the active palette.
        /// </summary>
        /// <param name="color">Color currently applied to a control.</param>
        /// <param name="previous">Previous palette to compare against.</param>
        /// <returns>The active equivalent color when one is known; otherwise the original color.</returns>
        private static Color MapColor(Color color, ThemePalette previous) {
            if (color.ToArgb() == previous.Background.ToArgb()) {
                return ThemeColors.Background;
            }

            if (color.ToArgb() == previous.Surface.ToArgb()) {
                return ThemeColors.Surface;
            }

            if (color.ToArgb() == previous.SurfaceSecondary.ToArgb()) {
                return ThemeColors.SurfaceSecondary;
            }

            if (color.ToArgb() == previous.InputSurface.ToArgb()) {
                return ThemeColors.InputSurface;
            }

            if (color.ToArgb() == previous.Border.ToArgb()) {
                return ThemeColors.Border;
            }

            if (color.ToArgb() == previous.BorderStrong.ToArgb()) {
                return ThemeColors.BorderStrong;
            }

            if (color.ToArgb() == previous.Primary.ToArgb()) {
                return ThemeColors.Primary;
            }

            if (color.ToArgb() == previous.PrimaryHover.ToArgb()) {
                return ThemeColors.PrimaryHover;
            }

            if (color.ToArgb() == previous.OnPrimary.ToArgb()) {
                return ThemeColors.OnPrimary;
            }

            if (color.ToArgb() == previous.TextPrimary.ToArgb()) {
                return ThemeColors.TextPrimary;
            }

            if (color.ToArgb() == previous.TextSecondary.ToArgb()) {
                return ThemeColors.TextSecondary;
            }

            if (color.ToArgb() == previous.Success.ToArgb()) {
                return ThemeColors.Success;
            }

            if (color.ToArgb() == previous.Warning.ToArgb()) {
                return ThemeColors.Warning;
            }

            if (color.ToArgb() == previous.WarningSurface.ToArgb()) {
                return ThemeColors.WarningSurface;
            }

            if (color.ToArgb() == previous.Error.ToArgb()) {
                return ThemeColors.Error;
            }

            if (color.ToArgb() == previous.Info.ToArgb()) {
                return ThemeColors.Info;
            }

            if (color.ToArgb() == previous.Disabled.ToArgb()) {
                return ThemeColors.Disabled;
            }

            if (color.ToArgb() == previous.Sidebar.ToArgb()) {
                return ThemeColors.Sidebar;
            }

            if (color.ToArgb() == previous.SidebarHover.ToArgb()) {
                return ThemeColors.SidebarHover;
            }

            if (color.ToArgb() == previous.OperationSurface.ToArgb()) {
                return ThemeColors.OperationSurface;
            }

            return color;
        }

        /// <summary>
        /// Applies readable text colors to existing tree nodes.
        /// </summary>
        /// <param name="nodes">Tree node collection to update recursively.</param>
        private static void StyleTreeNodes(TreeNodeCollection nodes) {
            foreach (TreeNode node in nodes) {
                if (node.ForeColor == Color.Empty ||
                    node.ForeColor.ToArgb() == SystemColors.WindowText.ToArgb() ||
                    node.ForeColor.ToArgb() == Color.Black.ToArgb()) {
                    node.ForeColor = ThemeColors.TextPrimary;
                }

                if (node.ForeColor.ToArgb() == ThemeColors.Disabled.ToArgb()) {
                    node.ForeColor = ThemeColors.Disabled;
                }

                StyleTreeNodes(node.Nodes);
            }
        }
    }
}
