// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// AboutForm.cs
// Displays product, license, version, and corporate information.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Forms {
    /// <summary>
    /// Displays product, license, version, and corporate information.
    /// </summary>
    public sealed class AboutForm : Form {
        /// <summary>
        /// Initializes a new instance of the <see cref="AboutForm"/> class.
        /// </summary>
        public AboutForm() {
            AppTheme.ApplyTo(this);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.Text = "About " + ProductInfo.Name;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(680, 440);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2,
                Padding = new Padding(24),
                BackColor = ThemeColors.Surface,
            };

            // The logo column is fixed intentionally so the vertical corporate mark keeps its aspect ratio.
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var logoPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 1,
                Margin = new Padding(0, 0, 22, 0),
            };
            logoPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            logoPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var logo = new PictureBox {
                Image = LoadLogoImage(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            logoPanel.Controls.Add(logo, 0, 0);

            var textPanel = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Margin = new Padding(0),
            };
            textPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            textPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var title = new Label {
                Text = ProductInfo.Name,
                AutoSize = true,
                Font = ThemeFonts.PageTitle,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 0, 0, 12),
            };
            textPanel.Controls.Add(title, 0, 0);
            textPanel.Controls.Add(CreateLine("Version 0.1"), 0, 1);
            textPanel.Controls.Add(CreateLine("Isolated Openness adapters | V21 available"), 0, 2);
            textPanel.Controls.Add(CreateLine("MIT License"), 0, 3);
            textPanel.Controls.Add(CreateLine("Copyright © 2026 EIDO AUTOMATION, S.L.U."), 0, 4);
            textPanel.Controls.Add(CreateDescription(), 0, 5);

            var buttons = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Margin = new Padding(0, 18, 0, 0),
            };
            var ok = new PrimaryButton {
                Text = "OK",
                DialogResult = DialogResult.OK,
            };
            buttons.Controls.Add(ok);

            root.Controls.Add(logoPanel, 0, 0);
            root.Controls.Add(textPanel, 1, 0);
            root.Controls.Add(buttons, 1, 1);
            this.Controls.Add(root);
            AppTheme.ApplyThemeTree(this);
            this.AcceptButton = ok;
            this.CancelButton = ok;
        }

        /// <summary>
        /// Creates one body text line for the about dialog.
        /// </summary>
        /// <param name="text">Text shown on the line.</param>
        /// <returns>The configured label.</returns>
        private static Label CreateLine(string text) {
            return new Label {
                Text = text,
                AutoSize = true,
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextPrimary,
                Margin = new Padding(0, 2, 0, 4),
            };
        }

        /// <summary>
        /// Creates the product description label for the about dialog.
        /// </summary>
        /// <returns>The configured description label.</returns>
        private static Label CreateDescription() {
            return new Label {
                Text = "Scans, exports, imports, and validates Siemens TIA Portal PLC and HMI artifacts in a structured engineering workspace.",
                AutoSize = true,

                // Dialog copy wraps beside the logo instead of pushing the fixed dialog wider.
                MaximumSize = new Size(350, 0),
                Font = ThemeFonts.Body,
                ForeColor = ThemeColors.TextSecondary,
                Margin = new Padding(0, 16, 0, 0),
            };
        }

        /// <summary>
        /// Loads the product logo from copied content without using preserialized .resx resources.
        /// </summary>
        /// <returns>The product logo image, or a generated fallback icon when the file is unavailable.</returns>
        private static Image LoadLogoImage() {
            var basePath = Path.GetDirectoryName(typeof(AboutForm).Assembly.Location) ??
                           System.Windows.Forms.Application.StartupPath;
            var path = Path.Combine(basePath, "Resources", "logo.png");
            if (File.Exists(path)) {
                try {
                    using (var stream = File.OpenRead(path))
                    using (var image = Image.FromStream(stream)) {
                        return new Bitmap(image);
                    }
                }
                catch {
                    // Fall back to a generated mark; About must never crash because branding media is missing.
                }
            }

            return IconProvider.Get("project", ThemeColors.Primary, 96);
        }
    }
}
