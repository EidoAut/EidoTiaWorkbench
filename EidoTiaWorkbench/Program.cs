// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Forms;
using EidoTiaWorkbench.Theme;

namespace EidoTiaWorkbench {
    /// <summary>
    /// Main application entry point class.
    /// Responsible for initializing runtime settings,
    /// resolving Siemens Openness assemblies,
    /// and launching the main UI form.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Environment variable that can override the TIA Portal Openness V21 PublicAPI location.
        /// </summary>
        private const string OpennessEnvironmentVariable = "TIA_OPENNESS_V21";

        /// <summary>
        /// Siemens assemblies that must be loadable before the UI starts.
        /// </summary>
        private static readonly string[] RequiredOpennessAssemblies =
        {
            "Siemens.Engineering.Base.dll",
            "Siemens.Engineering.Step7.dll",
            "Siemens.Engineering.WinCC.dll",
        };

        /// <summary>
        /// Synchronizes custom Siemens assembly resolution.
        /// </summary>
        private static readonly object AssemblyResolveLock = new object();

        /// <summary>
        /// Tracks assemblies currently being resolved to prevent recursive load loops.
        /// </summary>
        private static readonly HashSet<string> AssembliesBeingResolved =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Main entry point for the application.
        /// Configures assembly resolution for Siemens TIA Portal Openness,
        /// enables Windows visual styles,
        /// and starts the main application form.
        /// </summary>
        [STAThread]
        private static void Main() {
            // -----------------------------------------------------------------
            // Resolve Siemens TIA Portal Openness V21 assemblies dynamically
            // -----------------------------------------------------------------
            // The Openness API (Siemens.Engineering.Base.dll,
            // Siemens.Engineering.Step7.dll, etc.) is not always
            // located in the application directory. Therefore we attempt to
            // resolve it at runtime using:
            //
            // 1) Environment variable: TIA_OPENNESS_V21
            // 2) Common default PublicAPI\V21\net48 installation paths
            // -----------------------------------------------------------------
            if (!Environment.Is64BitProcess) {
                ShowOpennessStartupError(
                    ProductInfo.Name + " debe ejecutarse como proceso x64 para cargar TIA Portal Openness V21.");
                return;
            }

            string resolutionMessage;
            var opennessDir = ResolveOpennessV21Directory(out resolutionMessage);
            if (string.IsNullOrWhiteSpace(opennessDir)) {
                ShowOpennessStartupError(resolutionMessage);
                return;
            }

            var portalRoot = ResolvePortalRootFromOpennessDirectory(opennessDir);
            var portalBinDir = string.IsNullOrWhiteSpace(portalRoot)
                ? null
                : Path.Combine(portalRoot, "Bin");
            var assemblySearchDirectories = BuildAssemblySearchDirectories(opennessDir, portalBinDir);

            AddToProcessPath(assemblySearchDirectories);

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                ResolveSiemensAssembly(args, assemblySearchDirectories);

            try {
                LoadRequiredOpennessAssemblies(opennessDir);
            } catch (Exception ex) {
                ShowOpennessStartupError(
                    "TIA Portal Openness V21 was found, but its DLLs could not be loaded.\r\n\r\n" +
                    "PublicAPI path used:\r\n" + opennessDir + "\r\n\r\n" +
                    "Portal Bin path used:\r\n" + (portalBinDir ?? "(not detected)") + "\r\n\r\n" +
                    "Details:\r\n" + ex.Message);
                return;
            }

            // -----------------------------------------------------------------
            // Initialize Windows Forms application
            // -----------------------------------------------------------------

            // Enable Windows visual styles for modern UI appearance.
            System.Windows.Forms.Application.EnableVisualStyles();

            // Set text rendering compatibility.
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            // Load the persisted Eido light/dark palette before any shell controls are created.
            AppTheme.LoadPreference();

            // Launch the main application window.
            System.Windows.Forms.Application.Run(new MainShellForm());
        }

        /// <summary>
        /// Resolves the TIA Portal Openness V21 PublicAPI directory from environment or default paths.
        /// </summary>
        /// <param name="message">Diagnostic message when resolution fails.</param>
        /// <returns>The resolved net48 PublicAPI directory, or <see langword="null"/> when missing.</returns>
        private static string ResolveOpennessV21Directory(out string message) {
            var envValue = Environment.GetEnvironmentVariable(OpennessEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(envValue)) {
                var envResolved = NormalizeOpennessDirectory(envValue);
                if (!string.IsNullOrWhiteSpace(envResolved)) {
                    message = null;
                    return envResolved;
                }

                message =
                    "The " + OpennessEnvironmentVariable + " environment variable is defined, " +
                    "but it does not point to a valid TIA Portal Openness V21 installation.\r\n\r\n" +
                    "Current value:\r\n" + envValue + "\r\n\r\n" +
                    "It must point to the PublicAPI\\V21 or PublicAPI\\V21\\net48 folder.";
                return null;
            }

            foreach (var candidate in GetDefaultOpennessCandidates()) {
                var resolved = NormalizeOpennessDirectory(candidate);
                if (!string.IsNullOrWhiteSpace(resolved)) {
                    message = null;
                    return resolved;
                }
            }

            message =
                "TIA Portal Openness V21 was not found.\r\n\r\n" +
                "Checked paths:\r\n" +
                string.Join("\r\n", GetDefaultOpennessCandidates()) + "\r\n\r\n" +
                "You can also set " + OpennessEnvironmentVariable + " to the PublicAPI\\V21 " +
                "or PublicAPI\\V21\\net48 path.";
            return null;
        }

        /// <summary>
        /// Gets default installation paths checked for TIA Portal Openness V21.
        /// </summary>
        /// <returns>Candidate PublicAPI directories.</returns>
        private static string[] GetDefaultOpennessCandidates() {
            return new[]
            {
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Siemens\Automation\Portal V21\PublicAPI\V21"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    @"Siemens\Automation\Portal V21\PublicAPI\V21\net48"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Siemens\Automation\Portal V21\PublicAPI\V21"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    @"Siemens\Automation\Portal V21\PublicAPI\V21\net48"),
            };
        }

        /// <summary>
        /// Normalizes a configured Openness directory to the net48 PublicAPI folder.
        /// </summary>
        /// <param name="candidate">Configured or default directory path.</param>
        /// <returns>The usable net48 directory, or <see langword="null"/> when invalid.</returns>
        private static string NormalizeOpennessDirectory(string candidate) {
            if (string.IsNullOrWhiteSpace(candidate)) {
                return null;
            }

            var path = candidate.Trim().Trim('"');

            try {
                path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            } catch {
                return null;
            }

            if (ContainsRequiredAssemblies(path)) {
                return path;
            }

            var net48 = Path.Combine(path, "net48");
            return ContainsRequiredAssemblies(net48) ? net48 : null;
        }

        /// <summary>
        /// Checks whether a directory contains the Siemens assemblies needed at startup.
        /// </summary>
        /// <param name="directory">Directory to inspect.</param>
        /// <returns><see langword="true"/> when all required assemblies are present; otherwise <see langword="false"/>.</returns>
        private static bool ContainsRequiredAssemblies(string directory) {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) {
                return false;
            }

            foreach (var fileName in RequiredOpennessAssemblies) {
                if (!File.Exists(Path.Combine(directory, fileName))) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Loads required Siemens Openness assemblies from the resolved PublicAPI directory.
        /// </summary>
        /// <param name="opennessDir">Resolved PublicAPI net48 directory.</param>
        private static void LoadRequiredOpennessAssemblies(string opennessDir) {
            foreach (var fileName in RequiredOpennessAssemblies) {
                var path = Path.Combine(opennessDir, fileName);
                Assembly.LoadFrom(path);
            }
        }

        /// <summary>
        /// Resolves the TIA Portal installation root from an Openness PublicAPI directory.
        /// </summary>
        /// <param name="opennessDir">Resolved PublicAPI directory.</param>
        /// <returns>The Portal root directory, or <see langword="null"/> when it cannot be inferred.</returns>
        private static string ResolvePortalRootFromOpennessDirectory(string opennessDir) {
            if (string.IsNullOrWhiteSpace(opennessDir)) {
                return null;
            }

            try {
                var dir = new DirectoryInfo(opennessDir);

                // Typical V21 shape: <Portal V21>\PublicAPI\V21\net48
                if (string.Equals(dir.Name, "net48", StringComparison.OrdinalIgnoreCase) &&
                    dir.Parent != null &&
                    dir.Parent.Parent != null &&
                    dir.Parent.Parent.Parent != null) {
                    return dir.Parent.Parent.Parent.FullName;
                }

                // Fallback shape: <Portal V21>\PublicAPI\V21
                if (string.Equals(dir.Name, "V21", StringComparison.OrdinalIgnoreCase) &&
                    dir.Parent != null &&
                    dir.Parent.Parent != null) {
                    return dir.Parent.Parent.FullName;
                }
            } catch {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Builds the Siemens assembly search path for the custom resolver.
        /// </summary>
        /// <param name="opennessDir">Resolved PublicAPI directory.</param>
        /// <param name="portalBinDir">Resolved Portal Bin directory.</param>
        /// <returns>Existing directories that should be searched for Siemens assemblies.</returns>
        private static string[] BuildAssemblySearchDirectories(string opennessDir, string portalBinDir) {
            var dirs = new[]
            {
                opennessDir,
                portalBinDir,
            };

            return Array.FindAll(dirs, d => !string.IsNullOrWhiteSpace(d) && Directory.Exists(d));
        }

        /// <summary>
        /// Prepends Siemens directories to the process PATH for native dependency resolution.
        /// </summary>
        /// <param name="directories">Directories to prepend.</param>
        private static void AddToProcessPath(string[] directories) {
            if (directories == null || directories.Length == 0) {
                return;
            }

            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var updatedPath = string.Join(Path.PathSeparator.ToString(), directories) +
                              Path.PathSeparator +
                              existingPath;

            Environment.SetEnvironmentVariable("PATH", updatedPath);
        }

        /// <summary>
        /// Resolves Siemens managed assemblies from the configured Openness and Portal directories.
        /// </summary>
        /// <param name="args">Assembly resolve event arguments.</param>
        /// <param name="searchDirectories">Directories searched for the requested assembly.</param>
        /// <returns>The loaded Siemens assembly, or <see langword="null"/> when not handled.</returns>
        private static Assembly ResolveSiemensAssembly(ResolveEventArgs args, string[] searchDirectories) {
            var assemblyName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrWhiteSpace(assemblyName) ||
                !assemblyName.StartsWith("Siemens.", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var loadedName = loadedAssembly.GetName().Name;
                if (string.Equals(loadedName, assemblyName, StringComparison.OrdinalIgnoreCase)) {
                    return loadedAssembly;
                }
            }

            lock (AssemblyResolveLock) {
                if (AssembliesBeingResolved.Contains(assemblyName)) {
                    return null;
                }

                AssembliesBeingResolved.Add(assemblyName);
            }

            try {
                foreach (var directory in searchDirectories) {
                    var path = Path.Combine(directory, assemblyName + ".dll");
                    if (File.Exists(path)) {
                        return Assembly.LoadFrom(path);
                    }
                }

                return null;
            } finally {
                lock (AssemblyResolveLock) {
                    AssembliesBeingResolved.Remove(assemblyName);
                }
            }
        }

        /// <summary>
        /// Displays a startup error when Openness V21 cannot be loaded.
        /// </summary>
        /// <param name="detail">Detailed diagnostic text for the user.</param>
        private static void ShowOpennessStartupError(string detail) {
            MessageBox.Show(
                ProductInfo.Name + " cannot start because TIA Portal Openness V21 is unavailable or cannot be loaded.\r\n\r\n" +
                detail,
                "TIA Portal Openness V21",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
