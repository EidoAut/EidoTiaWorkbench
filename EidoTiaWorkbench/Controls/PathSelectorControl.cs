// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// PathSelectorControl.cs
// Provides a text box and browse button for selecting a folder or project file path.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Controls {
    /// <summary>
    /// Provides a text box and browse button for selecting a folder or project file path.
    /// </summary>
    public sealed class PathSelectorControl : UserControl {
        private readonly Panel _inputFrame;
        private readonly TextBox _textBox;
        private readonly SecondaryButton _browseButton;
        private readonly ToolTip _toolTip;
        private bool _folderMode = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="PathSelectorControl"/> class.
        /// </summary>
        public PathSelectorControl() {
            this.AutoSize = false;
            this.Dock = DockStyle.Fill;

            // The selector needs a minimum width for the text field plus the auto-sized Browse button.
            this.MinimumSize = new Size(280, 42);
            this.Height = 42;
            this._toolTip = new ToolTip();

            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

            this._inputFrame = new Panel {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = ThemeColors.InputSurface,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 1, 10, 1),
                Padding = new Padding(8, 7, 8, 0),
            };
            this._textBox = new TextBox {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
            };
            this._browseButton = new SecondaryButton {
                Text = "Browse",
                Anchor = AnchorStyles.None,
                AutoSize = false,
                Size = new Size(104, 40),
                Margin = Padding.Empty,
            };
            this._browseButton.Click += this.BrowseButton_Click;
            this._textBox.TextChanged += (s, e) => {
                this._toolTip.SetToolTip(this._textBox, this._textBox.Text);
                this.PathChanged?.Invoke(this, EventArgs.Empty);
            };
            AppTheme.StyleInput(this._textBox);
            this._inputFrame.Controls.Add(this._textBox);

            layout.Controls.Add(this._inputFrame, 0, 0);
            layout.Controls.Add(this._browseButton, 1, 0);
            this.Controls.Add(layout);
        }

        /// <summary>
        /// Occurs when the selected path text changes.
        /// </summary>
        public event EventHandler PathChanged;

        /// <summary>
        /// Gets or sets a value indicating whether the browse button selects folders instead of files.
        /// </summary>
        public bool FolderMode {
            get {
                return this._folderMode;
            }

            set {
                this._folderMode = value;
            }
        }

        /// <summary>
        /// Gets or sets the currently selected path text.
        /// </summary>
        public string SelectedPath {
            get {
                return this._textBox.Text;
            }

            set {
                this._textBox.Text = value ?? string.Empty;
                this._toolTip.SetToolTip(this._textBox, this._textBox.Text);
            }
        }

        /// <summary>
        /// Gets or sets the title shown by the browse dialog.
        /// </summary>
        public string DialogTitle {
            get; set;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing) {
            if (disposing) {
                this._toolTip.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Opens the configured folder or file picker and updates the selected path.
        /// </summary>
        /// <param name="sender">Browse button that raised the event.</param>
        /// <param name="e">Click event arguments.</param>
        private void BrowseButton_Click(object sender, EventArgs e) {
            if (this._folderMode) {
                using (var dlg = new FolderBrowserDialog()) {
                    dlg.Description = this.DialogTitle ?? "Select folder";
                    if (Directory.Exists(this.SelectedPath)) {
                        dlg.SelectedPath = this.SelectedPath;
                    }

                    if (dlg.ShowDialog(this) == DialogResult.OK) {
                        this.SelectedPath = dlg.SelectedPath;
                    }
                }
            }
            else {
                using (var dlg = new OpenFileDialog()) {
                    dlg.Title = this.DialogTitle ?? "Select file";
                    dlg.Filter = "TIA Project (*.ap*)|*.ap*|All files (*.*)|*.*";
                    if (File.Exists(this.SelectedPath)) {
                        dlg.FileName = this.SelectedPath;
                    }

                    if (dlg.ShowDialog(this) == DialogResult.OK) {
                        this.SelectedPath = dlg.FileName;
                    }
                }
            }
        }
    }
}
