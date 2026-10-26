// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// VisualValidation.cs
// Renders offline shell pages and verifies navigation, contrast and import-preview routing.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using EidoTiaWorkbench.Forms;
using EidoTiaWorkbench.Theme;
using EidoTiaWorkbench.Views;

namespace EidoTiaWorkbench.VisualTests {
    /// <summary>Checks offline presentation contracts and writes synthetic layout renders.</summary>
    internal static class VisualValidation {
        [STAThread]
        private static int Main(string[] args) {
            try {
                if (args.Length > 0 && args[0].StartsWith("--fixture-", StringComparison.Ordinal)) {
                    AuditRegressionTests.Run(args);
                    return 0;
                }

                AuditRegressionTests.Run(new string[0]);
                System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                System.Windows.Forms.Application.EnableVisualStyles();
                System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                if (args.Length > 0 && args[0] == "--preview") {
                    DesignPreview.Open();
                    return 0;
                }

                if (args.Length > 1 && args[0] == "--design") {
                    DesignPreview.Render(args[1], args.Length > 2 ? args[2] : null);
                    return 0;
                }

                foreach (var preference in new[] { ThemePreference.Light, ThemePreference.Dark }) {
                    AppTheme.SetPreference(preference);
                    foreach (var foreground in new[] { ThemeColors.TextPrimary, ThemeColors.TextSecondary, ThemeColors.TextMuted, ThemeColors.Disabled }) {
                        foreach (var background in new[] { ThemeColors.Background, ThemeColors.Surface, ThemeColors.SurfaceSecondary, ThemeColors.OperationSurface, ThemeColors.Sidebar }) {
                            var first = Luminance(foreground);
                            var second = Luminance(background);
                            var contrast = (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
                            if (contrast < 4.5) {
                                throw new InvalidOperationException("Insufficient text contrast: " + preference + " " + contrast);
                            }
                        }
                    }
                }

                foreach (var preference in new[] { ThemePreference.Light, ThemePreference.Dark }) {
                    AppTheme.SetPreference(preference);
                    CheckContrast(ThemeColors.OnPrimary, ThemeColors.Primary, "Primary action");
                    CheckContrast(ThemeColors.Warning, ThemeColors.WarningSurface, "Warning banner");
                    foreach (var accent in new[] { ThemeColors.Success, ThemeColors.Warning, ThemeColors.Error, ThemeColors.Info }) {
                        var surface = ThemeColors.Surface;
                        var background = Color.FromArgb((int)((accent.R * 0.13) + (surface.R * 0.87)), (int)((accent.G * 0.13) + (surface.G * 0.87)), (int)((accent.B * 0.13) + (surface.B * 0.87)));
                        CheckContrast(accent, background, "Semantic badge");
                    }
                }

                AppTheme.SetPreference(ThemePreference.Dark);
                var destination = Path.GetFullPath(args[0]);
                Directory.CreateDirectory(destination);
                foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f }) {
                    using (var form = new MainShellForm(false)) {
                        form.ShowInTaskbar = false;
                        form.Opacity = 0;
                        form.Show();
                        form.Location = new Point(-20000, -20000);
                        form.Scale(new SizeF(scale, scale));
                        form.ClientSize = new Size(scale == 1f ? 1280 : 1920, scale == 1f ? 720 : 1080);
                        form.CreateControl();
                        var navigate = typeof(MainShellForm).GetMethod("Navigate", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (scale == 1f) {
                            var header = Descendants(form).OfType<EidoTiaWorkbench.Controls.ApplicationHeader>().Single();
                            var toggle = Descendants(header).OfType<Button>().Single(x => x.AccessibleName == "Switch color theme");
                            foreach (var expected in new[] { ThemePreference.Light, ThemePreference.Dark }) {
                                toggle.PerformClick();
                                if (AppTheme.CurrentPreference != expected) {
                                    throw new InvalidOperationException("Theme toggle failed.");
                                }

                                AppTheme.LoadPreference();
                                if (AppTheme.CurrentPreference != expected) {
                                    throw new InvalidOperationException("Theme was not persisted.");
                                }

                                navigate.Invoke(form, new object[] { "Settings" });
                                if (form.BackColor.ToArgb() != ThemeColors.Background.ToArgb()) {
                                    throw new InvalidOperationException("Shell palette did not update.");
                                }

                                navigate.Invoke(form, new object[] { "Dashboard" });
                            }
                        }

                        foreach (var page in new[] { "Dashboard", "Project", "Export", "Import", "Validation", "Settings" }) {
                            navigate.Invoke(form, new object[] { page });
                            form.PerformLayout();
                            System.Windows.Forms.Application.DoEvents();
                            foreach (Control child in form.Controls[0].Controls) {
                                if (child.Right > child.Parent.ClientSize.Width + 1 || child.Bottom > child.Parent.ClientSize.Height + 1) {
                                    throw new InvalidOperationException("Shell chrome is clipped: " + page + " at scale " + scale);
                                }
                            }

                            using (var bitmap = new Bitmap(form.Width, form.Height)) {
                                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                                bitmap.Save(Path.Combine(destination, page + "-" + (int)(scale * 100) + ".png"));
                            }
                        }

                        CheckBusyInteraction(form, navigate, destination, scale);

                        if (AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name.StartsWith("Siemens."))) {
                            throw new InvalidOperationException("Desktop loaded proprietary assemblies.");
                        }
                    }
                }

                using (var import = new ImportView()) {
                    var preview = 0;
                    var real = 0;
                    import.DryRunRequested += (s, e) => preview++;
                    import.ImportSelectedRequested += (s, e) => real++;
                    var button = Descendants(import).OfType<Button>().Single(x => x.Text == "Preview selected");

                    // Invoke the control's event dispatch without requiring an interactive desktop.
                    typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button, new object[] { EventArgs.Empty });
                    if (preview != 1 || real != 0) {
                        throw new InvalidOperationException("Dry run dispatched a real import.");
                    }
                }

                Console.WriteLine("PASS desktop navigation, offline startup, dry-run routing and 24 synthetic layout renders.");
                return 0;
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                return 1;
            }
        }

        /// <summary>Checks busy progress layout and Activity access while synthetic log events arrive.</summary>
        private static void CheckBusyInteraction(MainShellForm form, MethodInfo navigate, string destination, float scale) {
            var state = (EidoTiaWorkbench.Application.ApplicationState)typeof(MainShellForm)
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
            foreach (var phase in new[] {
                EidoTiaWorkbench.Application.OperationState.Exporting,
                EidoTiaWorkbench.Application.OperationState.Importing,
            }) {
                var page = phase == EidoTiaWorkbench.Application.OperationState.Exporting ? "Export" : "Import";
                navigate.Invoke(form, new object[] { page });
                state.CurrentOperation = new EidoTiaWorkbench.Application.CurrentOperationInfo {
                    IsBusy = true,
                    State = phase,
                    Name = phase.ToString(),
                    StatusText = phase + " artifacts...",
                    CurrentArtifact = "CPU / Blocks / FB_Test",
                    Maximum = 100,
                    Progress = 25,
                    CanCancel = true,
                };
                for (var i = 0; i < 500; i++) {
                    state.AddActivity(new EidoTiaWorkbench.Application.ActivityEntry {
                        Time = DateTime.Now,
                        Operation = "Busy probe",
                        Message = "Artifact " + i,
                    });
                }

                PumpUi();
                var overlay = Descendants(form).OfType<EidoTiaWorkbench.Controls.OperationOverlay>().Single();
                foreach (var label in Descendants(overlay).OfType<Label>()) {
                    if (!label.Visible || label.Height < label.Font.Height || string.IsNullOrWhiteSpace(label.Text)) {
                        throw new InvalidOperationException("Busy progress text is missing or clipped.");
                    }
                }

                if (Descendants(form).OfType<StatusStrip>().Single().Items.OfType<ToolStripProgressBar>().Any()) {
                    throw new InvalidOperationException("Redundant status progress is still present.");
                }

                using (var bitmap = new Bitmap(form.Width, form.Height)) {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    bitmap.Save(Path.Combine(destination, "Busy-" + phase + "-" + (int)(scale * 100) + ".png"));
                }

                navigate.Invoke(form, new object[] { "Activity" });
                PumpUi();
                var activity = Descendants(form).OfType<ActivityView>().Single();
                if (Descendants(activity).OfType<Button>().Any(button => !button.Enabled) || form.UseWaitCursor) {
                    throw new InvalidOperationException("Activity tools are blocked during engineering work.");
                }

                var header = Descendants(form).OfType<EidoTiaWorkbench.Controls.ApplicationHeader>().Single();
                Descendants(header).OfType<Button>().Single(button => button.Text == "Activity").PerformClick();
                var split = (SplitContainer)typeof(MainShellForm)
                    .GetField("_contentSplit", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                if (split.Panel2Collapsed) {
                    throw new InvalidOperationException("Activity drawer did not open while busy.");
                }

                Descendants(header).OfType<Button>().Single(button => button.Text == "Activity").PerformClick();
                state.CurrentOperation = EidoTiaWorkbench.Application.CurrentOperationInfo.Idle;
            }
        }

        /// <summary>Lets real WinForms timers and posted input run during the synthetic busy-operation checks.</summary>
        private static void PumpUi() {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 350) {
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(5);
            }
        }

        private static void CheckContrast(Color foreground, Color background, string role) {
            var first = Luminance(foreground);
            var second = Luminance(background);
            var ratio = (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
            if (ratio < 4.5) {
                throw new InvalidOperationException(role + " contrast below 4.5:1: " + ratio);
            }
        }

        /// <summary>Traverses the complete control tree to locate composed view actions.</summary>
        /// <param name="root">Parent whose descendants are visited.</param>
        /// <returns>Descendants in depth-first order, excluding the parent.</returns>
        private static IEnumerable<Control> Descendants(Control root) {
            foreach (Control child in root.Controls) {
                yield return child;
                foreach (var descendant in Descendants(child)) {
                    yield return descendant;
                }
            }
        }

        /// <summary>Converts sRGB channels to relative luminance for text contrast checks.</summary>
        /// <param name="color">Opaque color to evaluate.</param>
        /// <returns>Linear weighted luminance between zero and one.</returns>
        private static double Luminance(Color color) {
            Func<byte, double> linear = channel => {
                var value = channel / 255.0;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            };
            return (0.2126 * linear(color.R)) + (0.7152 * linear(color.G)) + (0.0722 * linear(color.B));
        }
    }
}
