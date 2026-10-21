// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// Program.cs
// Starts the themed desktop shell; engineering APIs remain in a separate process.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Windows.Forms;
using EidoTiaWorkbench.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench {
    /// <summary>Starts the offline-capable desktop. Siemens is loaded only inside an engineering worker.</summary>
    internal static class Program {
        [STAThread]
        private static void Main() {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            AppTheme.LoadPreference();
            System.Windows.Forms.Application.Run(new MainShellForm());
        }
    }
}
