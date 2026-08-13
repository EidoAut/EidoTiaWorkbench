// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Controls;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench.Views {
    /// <summary>
    /// Hosts the activity log page.
    /// </summary>
    public sealed class ActivityView : UserControl {
        private readonly ActivityLogControl _activity;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityView"/> class.
        /// </summary>
        public ActivityView() {
            this.Dock = DockStyle.Fill;
            this.BackColor = ThemeColors.Background;
            this.Padding = new Padding(18);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var header = new PageHeader();
            header.SetText("Activity", "Structured operation log with filters and technical detail.");
            this._activity = new ActivityLogControl();
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(this._activity, 0, 1);
            this.Controls.Add(root);
        }

        /// <summary>
        /// Gets the activity log control hosted by the view.
        /// </summary>
        public ActivityLogControl ActivityLog {
            get {
                return this._activity;
            }
        }

        /// <summary>
        /// Binds activity entries from application state to the hosted log control.
        /// </summary>
        /// <param name="state">Application state containing activity entries.</param>
        public void Bind(ApplicationState state) {
            this._activity.Bind(state.Activity);
        }
    }
}
