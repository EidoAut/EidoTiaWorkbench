// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// DesignPreview.cs
// Opens isolated UI fixtures and renders demonstration states without engineering clients.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Forms;
using EidoTiaWorkbench.Theme;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.VisualTests {
    /// <summary>Isolated design fixtures; no controller, presenter or engineering client is constructed.</summary>
    internal static class DesignPreview {
        private static readonly string[] Scenarios = { "Dashboard-empty", "Dashboard-project", "Project-empty", "Project-sessions", "Project-busy", "Export-tree", "Export-selected", "Import-conflicts", "Validation-results", "Activity-events", "Settings" };

        /// <summary>Renders demonstration states at the requested viewport and synthetic scale combinations.</summary>
        /// <param name="destination">Directory receiving clearly identified demonstration screenshots.</param>
        /// <param name="filter">Optional scenario name for a focused layout iteration.</param>
        public static void Render(string destination, string filter = null) {
            Directory.CreateDirectory(destination);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialGdi = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            var sizes = new[] { new Size(1280, 720), new Size(1366, 768), new Size(1920, 1080), new Size(2560, 1440) };
            var scales = new[] { 1F, 1.25F, 1.5F, 2F };
            foreach (var theme in new[] { ThemePreference.Dark, ThemePreference.Light }) {
                AppTheme.SetPreference(theme);
                for (var index = 0; index < sizes.Length; index++) {
                    foreach (var scenario in Scenarios.Where(s => filter == null || s == filter)) {
                        using (var form = Create(scenario)) {
                            form.Opacity = 0;
                            form.ShowInTaskbar = false;
                            form.Show();
                            ScaleFixture(form, scales[index]);
                            form.ClientSize = sizes[index];
                            form.PerformLayout();
                            System.Windows.Forms.Application.DoEvents();
                            if (scenario == "Export-selected") {
                                File.WriteAllLines(Path.Combine(destination, theme + "-layout-" + (int)(scales[index] * 100) + ".txt"), Descendants(form).Where(c => c is ArtifactFilterBar || c is ComboBox || c is TableLayoutPanel).Select(c => c.GetType().Name + " " + c.Bounds + " Preferred=" + c.PreferredSize + " Font=" + c.Font.Size + " Parent=" + c.Parent?.GetType().Name));
                            }

                            using (var bitmap = new Bitmap(form.Width, form.Height)) {
                                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                bitmap.Save(Path.Combine(destination, theme + "-" + scenario + "-" + (int)(scales[index] * 100) + ".png"));
                            }
                        }
                    }
                }
            }

            if (filter == null) {
                RenderDialogs(destination);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalGdi = GetGuiResources(Process.GetCurrentProcess().Handle, 0);
            Console.WriteLine("GDI objects before/after render suite: " + initialGdi + "/" + finalGdi);

            Console.WriteLine("PASS " + (filter == null ? 88 : 8) + " isolated demo renders, two themes, four viewport/scale pairs. Synthetic scaling is not native monitor DPI certification.");
        }

        /// <summary>Opens an interactive isolated preview with no engineering command handlers.</summary>
        public static void Open() {
            AppTheme.SetPreference(ThemePreference.Dark);
            using (var launcher = new Form { Text = "EIDO Design Preview — demonstration data only", ClientSize = new Size(460, 540) }) {
                AppTheme.ApplyTo(launcher);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16) };
                foreach (var scenario in Scenarios) {
                    var button = new SecondaryButton { Text = scenario, Width = 400 };
                    button.Click += (sender, e) => {
                        using (var preview = Create(scenario)) {
                            preview.ShowDialog(launcher);
                        }
                    };
                    buttons.Controls.Add(button);
                }

                launcher.Controls.Add(buttons);
                System.Windows.Forms.Application.Run(launcher);
            }
        }

        [DllImport("user32.dll")]
        private static extern int GetGuiResources(IntPtr process, int flags);

        /// <summary>Exercises larger layout and text metrics without changing the user's monitor settings.</summary>
        /// <param name="form">Isolated preview window.</param>
        /// <param name="factor">Synthetic scale relative to the current monitor.</param>
        private static void ScaleFixture(Form form, float factor) {
            var controls = Descendants(form).Concat(new[] { form }).ToList();
            var originals = controls.ToDictionary(control => control, control => control.Font);
            var fonts = originals.Values.Distinct().ToDictionary(font => font, font => new Font(font.FontFamily, font.Size * factor, font.Style, GraphicsUnit.Point));
            form.Scale(new SizeF(factor, factor));
            foreach (var control in controls) {
                control.Font = fonts[originals[control]];
                var tree = control as TreeView;
                if (tree != null) {
                    tree.ItemHeight = (int)(32 * factor);
                }

                var grid = control as DataGridView;
                if (grid != null) {
                    grid.ColumnHeadersDefaultCellStyle.Font = fonts[originals[control]];
                    grid.DefaultCellStyle.Font = fonts[originals[control]];
                    grid.ColumnHeadersHeight = (int)(40 * factor);
                    foreach (DataGridViewRow row in grid.Rows) {
                        row.Height = (int)(36 * factor);
                    }
                }

                var combo = control as ComboBox;
                if (combo != null) {
                    combo.ItemHeight = (int)(30 * factor);
                }
            }

            form.Disposed += (sender, e) => {
                foreach (var font in fonts.Values) {
                    font.Dispose();
                }
            };
        }

        private static void RenderDialogs(string destination) {
            foreach (var theme in new[] { ThemePreference.Dark, ThemePreference.Light }) {
                AppTheme.SetPreference(theme);
                foreach (var dialog in new Form[] { new AboutForm(), new ConflictResolutionForm(), new OperationSummaryForm(new OperationSummary { Operation = "DEMO import preflight", Status = "Review required", Ok = 18, Warnings = 2, Errors = 1 }, true) }) {
                    using (dialog) {
                        dialog.Text = "DEMO — " + dialog.Text;
                        dialog.Opacity = 0;
                        dialog.ShowInTaskbar = false;
                        dialog.Show();
                        dialog.PerformLayout();
                        System.Windows.Forms.Application.DoEvents();
                        using (var bitmap = new Bitmap(dialog.Width, dialog.Height)) {
                            dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                            bitmap.Save(Path.Combine(destination, theme + "-Dialog-" + dialog.GetType().Name + ".png"));
                        }
                    }
                }
            }
        }

        private static Form Create(string scenario) {
            var state = CreateState(scenario);
            var page = scenario.Split('-')[0];
            var form = new Form { Text = "DEMO — " + scenario + " — no Siemens connection", ClientSize = new Size(1280, 720) };
            AppTheme.ApplyTo(form);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var demo = new Label { Text = "DESIGN PREVIEW  ·  Demonstration data  ·  Engineering actions are not connected", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = ThemeColors.Warning, BackColor = ThemeColors.WarningSurface };
            layout.Controls.Add(demo, 0, 0);
            var header = new ApplicationHeader();
            header.SetSection(page, "Offline visual fixture · " + scenario);
            header.Bind(state);
            header.ThemeToggleRequested += (sender, e) => {
                var previous = ThemeColors.Snapshot();
                AppTheme.SetPreference(ThemeColors.UseDarkPalette ? ThemePreference.Light : ThemePreference.Dark);
                AppTheme.ApplyThemeTree(form, previous);
                header.Bind(state);
            };
            layout.Controls.Add(header, 0, 1);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var sidebar = new SidebarNavigation();
            foreach (var name in new[] { "Dashboard", "Project", "Export", "Import", "Validation", "Activity", "Settings" }) {
                sidebar.AddMainItem(name, name, name.ToLowerInvariant());
            }

            sidebar.SetActive(page);
            body.Controls.Add(sidebar, 0, 0);
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var operation = new OperationOverlay();
            operation.Bind(state.CurrentOperation);
            content.Controls.Add(operation, 0, 0);
            var view = CreateView(page, state);
            foreach (var heading in Descendants(view).OfType<PageHeader>()) {
                heading.Visible = false;
            }

            content.Controls.Add(view, 0, 1);
            body.Controls.Add(content, 1, 0);
            layout.Controls.Add(body, 0, 2);
            form.Controls.Add(layout);
            AppTheme.ApplyThemeTree(form);
            if (state.CurrentOperation.IsBusy) {
                foreach (var command in Descendants(view).OfType<ButtonBase>()) {
                    command.Enabled = false;
                }
            }

            form.Shown += (sender, e) => {
                var settings = view as SettingsView;
                settings?.Bind(state, @"C:\DEMO\logs\preview.log");
                if (page == "Export" || page == "Import") {
                    var tree = Descendants(view).OfType<TreeView>().Single();
                    if (tree.Nodes.Count > 0) {
                        var leaves = Nodes(tree.Nodes).Where(n => n.Tag is ExportItem).ToList();
                        if (leaves.Count > 0) {
                            leaves[0].EnsureVisible();
                            if (scenario == "Export-selected") {
                                tree.SelectedNode = leaves[0];
                                leaves[0].Checked = true;
                            }
                        }
                    }
                }
            };
            return form;
        }

        private static UserControl CreateView(string page, ApplicationState state) {
            switch (page) {
                case "Dashboard":
                    var dashboard = new DashboardView();
                    dashboard.Bind(state);
                    return dashboard;
                case "Project":
                    var project = new ProjectView();
                    project.Bind(state);
                    project.ShowInstances(state.PortalInstances);
                    return project;
                case "Export":
                    var export = new ExportView();
                    export.Bind(state);
                    return export;
                case "Import":
                    var import = new ImportView();
                    import.Bind(state);
                    return import;
                case "Activity":
                    var activity = new ActivityView();
                    activity.Bind(state);
                    return activity;
                case "Settings":
                    var settings = new SettingsView();
                    settings.Bind(state, @"C:\DEMO\logs\preview.log");
                    return settings;
                default:
                    var validation = new ValidationView();
                    validation.Bind(state);
                    Descendants(validation).OfType<DataGridView>().Single().DataSource = new[]
                    {
                        new ImportValidationIssue(ImportValidationSeverity.Error, "XML", null, "DEMO: duplicate engineering object identifier in FB_Conveyor."),
                        new ImportValidationIssue(ImportValidationSeverity.Warning, "Dependencies", null, "DEMO: UDT_ConveyorStatus must be available before import."),
                    };
                    return validation;
            }
        }

        private static ApplicationState CreateState(string scenario) {
            var state = new ApplicationState { IntegrationVersion = "V21 (DEMO)", SelectedRepository = @"C:\DEMO\EngineeringWorkspace" };
            if (!scenario.EndsWith("empty", StringComparison.Ordinal)) {
                state.ActiveProject = new ProjectDescriptor { Name = "DEMO_Conveyor", Path = @"C:\DEMO\DEMO_Conveyor.ap21", Version = "V21" };
                state.ActiveProject.PlcTargets.Add("PLC_Conveyor");
                state.ActiveProject.HmiTargets.Add("HMI_Operator");
                state.PortalConnectionState = PortalConnectionState.Connected;
                state.PortalOwnership = PortalOwnership.Attached;
                state.SetPortalInstances(new[] { new TiaPortalInstanceInfo(0, 12340, "DEMO TIA V21", state.ActiveProject.Path), new TiaPortalInstanceInfo(1, 12341, "DEMO TIA V21", @"C:\DEMO\Diagnostics.ap21") });
            }

            var artifacts = Enumerable.Range(1, 1200).Select(i => new ExportItem("PLC_Conveyor", "Program blocks/" + (i <= 40 ? "01_Control" : "Station_" + (i / 40)), "FB_Conveyor_" + i.ToString("D4"), ExportKind.Fb, "DEMO/FB/" + i)).ToList();
            state.SetLastScan(artifacts);
            state.SetRepositoryInventory(artifacts.Take(20).Select((item, index) => new RepositoryArtifact(item, @"C:\DEMO\" + item.Name + ".xml", item.Name + ".xml", index % 2 == 0 ? RepositoryArtifactState.Modified : RepositoryArtifactState.InvalidXml, null, null, index % 2 == 0 ? "DEMO: existing artifact differs; choose a conflict policy." : "DEMO: XML contains a duplicate identifier.")));
            for (var index = 0; index < 30; index++) {
                state.AddActivity(new ActivityEntry { Time = new DateTime(2026, 10, 18, 10, 0, 0).AddSeconds(index * 10), Severity = index % 4 == 0 ? ActivitySeverity.Warning : ActivitySeverity.Info, Operation = "Demo validation", Artifact = "FB_Conveyor_" + index, Message = index % 4 == 0 ? "Dependency requires review before import." : "Artifact inspected successfully.", Detail = "Demonstration event. No Siemens API was called." });
            }

            if (scenario == "Project-busy") {
                state.CurrentOperation = new CurrentOperationInfo { IsBusy = true, IsIndeterminate = true, Name = "Opening project", StatusText = "Opening project…", CurrentArtifact = @"C:\DEMO\DEMO_Conveyor.ap21", CanCancel = false };
            }

            return state;
        }

        private static IEnumerable<Control> Descendants(Control root) {
            foreach (Control child in root.Controls) {
                yield return child;
                foreach (var descendant in Descendants(child)) {
                    yield return descendant;
                }
            }
        }

        private static IEnumerable<TreeNode> Nodes(TreeNodeCollection nodes) {
            foreach (TreeNode node in nodes) {
                yield return node;
                foreach (var child in Nodes(node.Nodes)) {
                    yield return child;
                }
            }
        }
    }
}
