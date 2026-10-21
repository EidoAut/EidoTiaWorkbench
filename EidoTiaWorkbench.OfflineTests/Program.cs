// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// Program.cs
// Runs offline contracts or explicitly requested isolated V21 lifecycle checks.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using EidoTiaWorkbench.Tests;

namespace EidoTiaWorkbench.OfflineTests {
    /// <summary>Runs offline contracts and explicitly selected worker integration probes.</summary>
    internal static class Program {
        [STAThread]
        private static int Main(string[] args) {
            try {
                if (args.Length > 0 && args[0] == "--worker") {
                    ModernizationTests.WorkerSmoke();
                }
                else if (args.Length > 0 && args[0] == "--lifecycle") {
                    ModernizationTests.WorkerLifecycle();
                }
                else {
                    ModernizationTests.Run();
                }

                Console.WriteLine("PASS " + (args.Length > 0 ? args[0] : "offline modernization contracts"));
                return 0;
            }
            catch (Exception ex) {
                Console.WriteLine(ex);
                return 1;
            }
        }
    }
}
