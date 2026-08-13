// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;

namespace EidoTiaWorkbench.Presentation {
    /// <summary>
    /// Provides shared presenter helpers for reporting asynchronous command failures.
    /// </summary>
    public abstract class PresenterBase {
        /// <summary>
        /// Runs an asynchronous UI command and reports any exception to the owner window.
        /// </summary>
        /// <param name="owner">Window that owns the error dialog.</param>
        /// <param name="action">Asynchronous command to execute.</param>
        protected static async void FireAndReport(IWin32Window owner, Func<System.Threading.Tasks.Task> action) {
            try {
                await action();
            } catch (Exception ex) {
                MessageBox.Show(owner, ex.Message, ProductInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
