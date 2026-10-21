// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ArtifactTreeControl.cs
// Displays exportable artifacts in a checkable hierarchy grouped by project, device, CPU, and category.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Displays exportable artifacts in a checkable hierarchy grouped by project, device, CPU, and category.
    /// </summary>
    public sealed class ArtifactTreeControl : UserControl {
        private readonly TreeView _tree;
        private Func<ExportItem, bool> _isItemSelectable = item => item != null && item.CanExport;
        private bool _suppressCheckEvents;
        private int _iconPalette;

        /// <summary>
        /// Initializes a new instance of the <see cref="ArtifactTreeControl"/> class.
        /// </summary>
        public ArtifactTreeControl() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.InputSurface;

            this._tree = new TreeView {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                BorderStyle = BorderStyle.None,
                HideSelection = false,
                HotTracking = true,
                ItemHeight = 32,
                Indent = 22,
                ShowNodeToolTips = true,
            };
            this.ApplyTheme();
            this._tree.AfterCheck += this.Tree_AfterCheck;
            this._tree.AfterSelect += (s, e) => this.SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            this.Controls.Add(this._tree);
        }

        /// <summary>
        /// Occurs when the currently selected tree node changes.
        /// </summary>
        public event EventHandler SelectedItemChanged;

        /// <summary>
        /// Occurs when checked artifacts change.
        /// </summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// Gets the artifact associated with the selected tree node.
        /// </summary>
        public ExportItem SelectedItem {
            get {
                return this._tree.SelectedNode != null ? this._tree.SelectedNode.Tag as ExportItem : null;
            }
        }

        /// <summary>
        /// Rebuilds the tree from a new artifact collection while preserving checked logical keys.
        /// </summary>
        /// <param name="items">Artifacts to display.</param>
        public void SetItems(IEnumerable<ExportItem> items) {
            this.SetItems(items, null);
        }

        /// <summary>
        /// Rebuilds the tree from a new artifact collection using an explicit default checked set.
        /// </summary>
        /// <param name="items">Artifacts to display.</param>
        /// <param name="defaultCheckedKeys">Logical keys to check when the caller wants a fresh default selection.</param>
        public void SetItems(IEnumerable<ExportItem> items, IEnumerable<string> defaultCheckedKeys) {
            var checkedKeys = new HashSet<string>(
                defaultCheckedKeys ?? this.GetCheckedItems().Select(i => i.LogicalKey),
                StringComparer.OrdinalIgnoreCase);

            var expanded = new HashSet<string>(StringComparer.Ordinal);
            var selectedKey = this.SelectedItem?.LogicalKey;
            foreach (TreeNode node in this._tree.Nodes) {
                Walk(node, n => {
                    if (n.IsExpanded) {
                        expanded.Add(n.FullPath);
                    }
                });
            }

            this._tree.BeginUpdate();
            this._suppressCheckEvents = true;
            try {
                this._tree.Nodes.Clear();
                var root = new TreeNode("Project") {
                    ImageKey = "project",
                    SelectedImageKey = "project",
                };
                var plcRoot = new TreeNode("PLC") {
                    ImageKey = "plc",
                    SelectedImageKey = "plc",
                };
                var hmiRoot = new TreeNode("HMI") {
                    ImageKey = "hmi",
                    SelectedImageKey = "hmi",
                };

                foreach (var item in (items ?? Enumerable.Empty<ExportItem>())
                    .OrderBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.Kind)
                    .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)) {
                    AddExportItem(item.IsHmi ? hmiRoot : plcRoot, item, checkedKeys, this._isItemSelectable);
                }

                root.Nodes.Add(plcRoot);
                root.Nodes.Add(hmiRoot);
                root.Expand();
                plcRoot.Expand();
                hmiRoot.Expand();
                this._tree.Nodes.Add(root);
                Walk(root, n => {
                    if (expanded.Contains(n.FullPath)) {
                        n.Expand();
                    }

                    if (selectedKey != null && (n.Tag as ExportItem)?.LogicalKey == selectedKey) {
                        this._tree.SelectedNode = n;
                    }
                });
                AppTheme.StyleTree(this._tree);
            }
            finally {
                this._suppressCheckEvents = false;
                this._tree.EndUpdate();
            }

            this.SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Configures which artifact capability controls checkability.
        /// </summary>
        /// <param name="isItemSelectable">Predicate used to decide whether an item can be checked.</param>
        public void ConfigureSelection(Func<ExportItem, bool> isItemSelectable) {
            this._isItemSelectable = isItemSelectable ?? (item => item != null && item.CanExport);
        }

        /// <summary>
        /// Reapplies the active Eido palette and icon colors to the tree control.
        /// </summary>
        public void ApplyTheme() {
            this.BackColor = ThemeColors.InputSurface;
            var palette = ThemeColors.Primary.ToArgb();
            if (this._tree.ImageList == null || palette != this._iconPalette) {
                var oldImages = this._tree.ImageList;
                this._tree.ImageList = BuildImageList();
                this._iconPalette = palette;
                oldImages?.Dispose();
            }

            AppTheme.StyleTree(this._tree);
        }

        /// <summary>
        /// Gets all checked export items in the tree.
        /// </summary>
        /// <returns>Checked export items.</returns>
        public List<ExportItem> GetCheckedItems() {
            var result = new List<ExportItem>();
            foreach (TreeNode node in this._tree.Nodes) {
                Walk(
                    node,
                    n => {
                        if (n.Checked && n.Tag is ExportItem && this._isItemSelectable((ExportItem)n.Tag)) {
                            result.Add((ExportItem)n.Tag);
                        }
                    });
            }

            return result;
        }

        /// <summary>
        /// Checks every node in the tree and raises the selection changed event.
        /// </summary>
        public void SelectAll() {
            this._suppressCheckEvents = true;
            try {
                foreach (TreeNode node in this._tree.Nodes) {
                    this.SetCheckedRecursive(node, true);
                }
            }
            finally {
                this._suppressCheckEvents = false;
            }

            this.SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clears all checked nodes and raises the selection changed event.
        /// </summary>
        public void ClearSelection() {
            this._suppressCheckEvents = true;
            try {
                foreach (TreeNode node in this._tree.Nodes) {
                    this.SetCheckedRecursive(node, false);
                }
            }
            finally {
                this._suppressCheckEvents = false;
            }

            this.SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) {
            if (disposing) {
                this._tree.ImageList?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Adds one PLC export item below the PLC tree root.
        /// </summary>
        /// <param name="plcRoot">PLC tree root node.</param>
        /// <param name="item">Export item to add.</param>
        /// <param name="checkedKeys">Logical keys that should be checked.</param>
        /// <param name="isItemSelectable">Predicate that decides whether the leaf can be checked.</param>
        private static void AddExportItem(
            TreeNode plcRoot,
            ExportItem item,
            HashSet<string> checkedKeys,
            Func<ExportItem, bool> isItemSelectable) {
            var deviceCpu = SplitCpuPath(item.CpuPath);
            var deviceNode = EnsureNode(plcRoot, deviceCpu.Item1, "plc");
            var cpuNode = EnsureNode(deviceNode, deviceCpu.Item2, "plc");
            var categoryNode = EnsureNode(cpuNode, ArtifactCatalog.Get(item.Kind).Category, GetKindIconKey(item.Kind));
            var parent = EnsurePathNode(categoryNode, item.GroupPath);
            var icon = GetKindIconKey(item.Kind);
            var leaf = new TreeNode(item.Name) {
                Tag = item,
                ImageKey = icon,
                SelectedImageKey = icon,
                Checked = isItemSelectable(item) && checkedKeys.Contains(item.LogicalKey),
                ForeColor = isItemSelectable(item) ? ThemeColors.TextPrimary : ThemeColors.Disabled,
                ToolTipText = BuildTooltip(item),
            };
            parent.Nodes.Add(leaf);
        }

        /// <summary>
        /// Builds a concise capability tooltip for one artifact leaf.
        /// </summary>
        /// <param name="item">Artifact represented by the tree leaf.</param>
        /// <returns>Capability and limitation text.</returns>
        private static string BuildTooltip(ExportItem item) {
            var descriptor = ArtifactCatalog.Get(item.Kind);
            var capability = "Export: " + (item.CanExport ? "yes" : "no") +
                             " | Import: " + (item.CanImport ? "yes" : "no");
            var reason = !string.IsNullOrWhiteSpace(item.UnavailableReason)
                ? item.UnavailableReason
                : descriptor.Limitations;
            return string.IsNullOrWhiteSpace(reason)
                ? capability
                : capability + Environment.NewLine + reason;
        }

        /// <summary>
        /// Splits a target path into device and CPU/HMI item display segments.
        /// </summary>
        /// <param name="cpuPath">Slash-separated target path.</param>
        /// <returns>A tuple containing parent device path and final item name.</returns>
        private static Tuple<string, string> SplitCpuPath(string cpuPath) {
            var text = (cpuPath ?? "Device/CPU").Replace("\\", "/");
            var idx = text.LastIndexOf('/');
            if (idx <= 0 || idx >= text.Length - 1) {
                return Tuple.Create("Device", text);
            }

            return Tuple.Create(text.Substring(0, idx), text.Substring(idx + 1));
        }

        /// <summary>
        /// Ensures a slash-separated folder path exists below a tree node.
        /// </summary>
        /// <param name="root">Root node that owns the path.</param>
        /// <param name="path">Slash-separated folder path.</param>
        /// <returns>The final folder node.</returns>
        private static TreeNode EnsurePathNode(TreeNode root, string path) {
            var current = root;
            var parts = (path ?? string.Empty).Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts) {
                current = EnsureNode(current, part, "folder");
            }

            return current;
        }

        /// <summary>
        /// Ensures a child node with the given text exists.
        /// </summary>
        /// <param name="parent">Parent tree node.</param>
        /// <param name="text">Child node text.</param>
        /// <param name="icon">Image key used by the child node.</param>
        /// <returns>The existing or newly created child node.</returns>
        private static TreeNode EnsureNode(TreeNode parent, string text, string icon) {
            foreach (TreeNode child in parent.Nodes) {
                if (child.Tag == null && string.Equals(child.Text, text, StringComparison.OrdinalIgnoreCase)) {
                    return child;
                }
            }

            var node = new TreeNode(text) {
                ImageKey = icon,
                SelectedImageKey = icon,
            };
            parent.Nodes.Add(node);
            return node;
        }

        /// <summary>
        /// Updates parent checked states after a child node changes.
        /// </summary>
        /// <param name="node">Node whose ancestors should be refreshed.</param>
        private static void UpdateParents(TreeNode node) {
            if (node.Parent == null) {
                return;
            }

            var any = false;
            foreach (TreeNode child in node.Parent.Nodes) {
                any |= child.Checked;
            }

            node.Parent.Checked = any;
            UpdateParents(node.Parent);
        }

        /// <summary>
        /// Walks a node and all descendants depth-first.
        /// </summary>
        /// <param name="node">Root node to walk.</param>
        /// <param name="action">Action invoked for each node.</param>
        private static void Walk(TreeNode node, Action<TreeNode> action) {
            action(node);
            foreach (TreeNode child in node.Nodes) {
                Walk(child, action);
            }
        }

        /// <summary>
        /// Gets the tree category label for an export kind.
        /// </summary>
        /// <param name="kind">Export kind.</param>
        /// <returns>The category label.</returns>
        private static string GetCategory(ExportKind kind) {
            switch (kind) {
                case ExportKind.Udt:
                    return "Types";
                case ExportKind.PlcTagTable:
                    return "Tag tables";
                case ExportKind.HmiScreen:
                    return "Screens";
                default:
                    return "Blocks";
            }
        }

        /// <summary>
        /// Gets the image-list key used for an export kind.
        /// </summary>
        /// <param name="kind">Export kind.</param>
        /// <returns>The icon image key.</returns>
        private static string GetKindIconKey(ExportKind kind) {
            switch (kind) {
                case ExportKind.Fb:
                    return "fb";
                case ExportKind.Fc:
                    return "fc";
                case ExportKind.Ob:
                    return "ob";
                case ExportKind.Udt:
                    return "types";
                case ExportKind.PlcTagTable:
                    return "tag";
                case ExportKind.Db:
                    return "db";
                case ExportKind.HmiScreen:
                    return "screen";
                default:
                    return "block";
            }
        }

        /// <summary>
        /// Builds the tree image list used for PLC and HMI artifacts.
        /// </summary>
        /// <returns>The configured image list.</returns>
        private static ImageList BuildImageList() {
            var list = new ImageList {
                ImageSize = new Size(24, 24),
                ColorDepth = ColorDepth.Depth32Bit,
            };
            list.Images.Add("project", IconProvider.Get("project", ThemeColors.TextSecondary));
            list.Images.Add("plc", IconProvider.Get("plc", ThemeColors.Primary));
            list.Images.Add("hmi", IconProvider.Get("hmi", ThemeColors.Info));
            list.Images.Add("folder", IconProvider.Get("project", ThemeColors.Warning));
            list.Images.Add("types", CreateKindImage("DT", ThemeColors.Info));
            list.Images.Add("fb", CreateKindImage("FB", ThemeColors.Primary));
            list.Images.Add("fc", CreateKindImage("FC", ThemeColors.Info));
            list.Images.Add("ob", CreateKindImage("OB", ThemeColors.TextSecondary));
            list.Images.Add("tag", IconProvider.Get("document", ThemeColors.Success));
            list.Images.Add("db", CreateKindImage("DB", ThemeColors.Warning));
            list.Images.Add("block", IconProvider.Get("document", ThemeColors.TextPrimary));
            list.Images.Add("screen", IconProvider.Get("screen", ThemeColors.Info));
            return list;
        }

        /// <summary>Creates a compact block-type marker for the owned tree image list.</summary>
        /// <param name="text">Two-letter engineering kind.</param>
        /// <param name="color">Semantic marker foreground.</param>
        /// <returns>A bitmap owned by the image-list builder.</returns>
        private static Bitmap CreateKindImage(string text, Color color) {
            var bitmap = new Bitmap(24, 24);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe UI", 9F, FontStyle.Bold))
            using (var pen = new Pen(ThemeColors.BorderStrong)) {
                graphics.Clear(ThemeColors.InputSurface);
                graphics.DrawRectangle(pen, 0, 2, 23, 19);
                TextRenderer.DrawText(graphics, text, font, new Rectangle(0, 0, 24, 24), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            return bitmap;
        }

        /// <summary>
        /// Applies a checked state to a node and all descendants.
        /// </summary>
        /// <param name="node">Root node to update.</param>
        /// <param name="value">Checked state to apply.</param>
        private void SetCheckedRecursive(TreeNode node, bool value) {
            node.Checked = value && this.IsNodeExportable(node);
            foreach (TreeNode child in node.Nodes) {
                this.SetCheckedRecursive(child, value);
            }
        }

        /// <summary>
        /// Determines whether a node can participate in export selection.
        /// </summary>
        /// <param name="node">Tree node to inspect.</param>
        /// <returns><see langword="true"/> when the node or one of its descendants is exportable.</returns>
        private bool IsNodeExportable(TreeNode node) {
            if (node.Tag is ExportItem) {
                return this._isItemSelectable((ExportItem)node.Tag);
            }

            foreach (TreeNode child in node.Nodes) {
                if (this.IsNodeExportable(child)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Propagates checked states after the user checks or unchecks a tree node.
        /// </summary>
        /// <param name="sender">Tree view that raised the event.</param>
        /// <param name="e">Tree check event arguments.</param>
        private void Tree_AfterCheck(object sender, TreeViewEventArgs e) {
            if (this._suppressCheckEvents) {
                return;
            }

            this._suppressCheckEvents = true;
            try {
                if (e.Node.Tag is ExportItem && !((ExportItem)e.Node.Tag).CanExport) {
                    e.Node.Checked = false;
                    UpdateParents(e.Node);
                    return;
                }

                foreach (TreeNode child in e.Node.Nodes) {
                    this.SetCheckedRecursive(child, e.Node.Checked);
                }

                UpdateParents(e.Node);
            }
            finally {
                this._suppressCheckEvents = false;
            }

            this.SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
