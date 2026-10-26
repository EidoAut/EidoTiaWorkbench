// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ConflictResolutionForm.cs
// Explains import conflict policies without issuing an engineering command.
//
// Developed by EIDO Automation
// =========================================================================
using System.Drawing;
using System.Windows.Forms;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Forms {
    /// <summary>
    /// Explains import conflict policies without issuing an engineering command.
    /// </summary>
    public sealed class ConflictResolutionForm : Form {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConflictResolutionForm"/> class.
        /// </summary>
        public ConflictResolutionForm() {
            AppTheme.ApplyTo(this);
            this.Text = "Conflict resolution";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(520, 320);

            this.ClientSize = new Size(640, 440);
            var stack = new FlowLayoutPanel {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(24),
            };
            var header = new PageHeader();
            header.SetText("Conflict policies", "Choose the policy in Import before confirming engineering changes.");
            stack.Controls.Add(header);
            foreach (var policy in new[] {
                new[] { "Ask", "Requires an explicit decision before changing an existing artifact." },
                new[] { "Skip", "Keeps existing project artifacts unchanged." },
                new[] { "Replace", "Overwrites existing artifacts. Enable and verify backups before proceeding." },
            }) {
                var card = new InfoCard { Width = 560 };
                card.SetContent(policy[0], policy[1]);
                stack.Controls.Add(card);
            }

            var close = new SecondaryButton { Text = "Close", DialogResult = DialogResult.Cancel };
            stack.Controls.Add(close);
            this.AcceptButton = close;
            this.CancelButton = close;
            this.Controls.Add(stack);
            AppTheme.ApplyThemeTree(this);
        }
    }
}
