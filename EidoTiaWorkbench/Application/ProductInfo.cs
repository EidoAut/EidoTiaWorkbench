// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ProductInfo.cs
// Provides product identity used by the desktop shell and generated artifacts.
//
// Developed by EIDO Automation
// =========================================================================
namespace EidoTiaWorkbench.Application {
    /// <summary>
    /// Provides product identity used by the desktop shell and generated artifacts.
    /// </summary>
    public static class ProductInfo {
        /// <summary>
        /// Gets the user-facing product name.
        /// </summary>
        public const string Name = "Eido TIA Workbench";

        /// <summary>
        /// Gets a filesystem-safe product folder name.
        /// </summary>
        public const string DataFolderName = "EidoTiaWorkbench";

        /// <summary>
        /// Gets the default operation report file name.
        /// </summary>
        public const string OperationReportFileName = "eido-tia-workbench-operation-report.txt";

        /// <summary>
        /// Gets the short technical strapline shown in the application chrome.
        /// </summary>
        public const string Strapline = "EIDO / ENGINEERING WORKSPACE";
    }
}
