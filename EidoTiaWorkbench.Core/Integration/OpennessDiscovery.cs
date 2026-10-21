// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// OpennessDiscovery.cs
// Reads both registry views and validates installed API identities without loading Siemens.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>Reads both registry views and validates installed API identities without loading Siemens.</summary>
    public sealed class OpennessDiscovery {
        /// <summary>Registry root containing installed API versions in both Windows registry views.</summary>
        public const string RegistryPath = @"SOFTWARE\Siemens\Automation\Openness";

        /// <summary>Parses a declared API major version; returns zero when provenance is unknown.</summary>
        /// <param name="value">Version label, assembly version or installation path.</param>
        /// <returns>The major version, or zero for an unrecognized declaration.</returns>
        public static int ParseVersion(string value) {
            var match = Regex.Match(value ?? string.Empty, @"(?:^|[\\ /Vv])(?<v>\d{2})(?:\.\d+)*(?:$|[\\ /])");
            return match.Success ? int.Parse(match.Groups["v"].Value) : 0;
        }

        /// <summary>Checks API identity and runtime prerequisites; this does not certify live engineering support.</summary>
        /// <param name="path">Candidate API directory.</param>
        /// <param name="source">Origin recorded for diagnostics.</param>
        /// <returns>Validated evidence or an unusable installation with the failure reason.</returns>
        public static OpennessInstallation Validate(string path, string source = "Custom") {
            var result = new OpennessInstallation { ApiPath = path, Source = source, MajorVersion = ParseVersion(path) };
            try {
                if (string.IsNullOrWhiteSpace(path)) {
                    throw new ArgumentException("An Openness API directory is required.", nameof(path));
                }

                result.ApiPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
                var modular = File.Exists(Path.Combine(result.ApiPath, "Siemens.Engineering.Base.dll"));
                var names = modular ? new[] { "Siemens.Engineering.Base", "Siemens.Engineering.Step7", "Siemens.Engineering.WinCC" } : new[] { "Siemens.Engineering", "Siemens.Engineering.Hmi" };
                int major = 0;
                foreach (var name in names) {
                    var identity = AssemblyName.GetAssemblyName(Path.Combine(result.ApiPath, name + ".dll"));
                    var token = BitConverter.ToString(identity.GetPublicKeyToken()).Replace("-", string.Empty).ToLowerInvariant();
                    if (identity.Name != name || token != "29bfe5fdf4ba5d3b") {
                        throw new InvalidDataException("Unexpected Siemens assembly identity: " + name);
                    }

                    if (major != 0 && major != identity.Version.Major) {
                        throw new InvalidDataException("Mixed Openness versions.");
                    }

                    major = identity.Version.Major;
                    if (identity.ProcessorArchitecture == ProcessorArchitecture.X86) {
                        throw new InvalidDataException("An x86 API cannot be used by the x64 worker.");
                    }
                }

                result.MajorVersion = major;
                if (modular && major != 21) {
                    throw new InvalidDataException("Modular API version has no verified adapter.");
                }

                if (modular && !string.Equals(new DirectoryInfo(result.ApiPath).Name, "net48", StringComparison.OrdinalIgnoreCase)) {
                    throw new InvalidDataException("Select the V21 net48 API, not the modern .NET API.");
                }

                result.IsValid = true;
                result.Diagnostic = result.HasAdapter ? "API identities verified; x64 .NET Framework 4.8 worker. Live engineering validation required." : "Detected classic API; adapter not implemented or verified.";
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is BadImageFormatException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is System.Security.SecurityException) {
                result.Diagnostic = ex.Message;
            }

            return result;
        }

        /// <summary>Collects installation evidence without executing Siemens code.</summary>
        /// <param name="customPath">Optional user-supplied installation or API directory.</param>
        /// <returns>Deduplicated installations, including unusable candidates with diagnostics.</returns>
        public List<OpennessInstallation> Discover(string customPath = null) {
            var candidates = new List<OpennessInstallation>();
            this.AddPath(candidates, customPath, "Custom");
            this.AddPath(candidates, Environment.GetEnvironmentVariable("TIA_OPENNESS"), "Environment");
            this.AddPath(candidates, Environment.GetEnvironmentVariable("TIA_OPENNESS_V21"), "Legacy environment");
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
                try {
                    using (var hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var key = hive.OpenSubKey(RegistryPath)) {
                        if (key != null) {
                            this.ReadRegistry(key, candidates, 0);
                        }
                    }
                }
                catch (System.Security.SecurityException ex) {
                    candidates.Add(new OpennessInstallation { Source = "Registry", Diagnostic = ex.Message });
                }
                catch (UnauthorizedAccessException ex) {
                    candidates.Add(new OpennessInstallation { Source = "Registry", Diagnostic = ex.Message });
                }
            }

            foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }) {
                var automation = Path.Combine(root, "Siemens", "Automation");
                if (!Directory.Exists(automation)) {
                    continue;
                }

                foreach (var portal in Directory.GetDirectories(automation, "Portal V*")) {
                    this.AddPath(candidates, portal, "Standard path");
                }
            }

            return candidates.GroupBy(x => x.ApiPath ?? x.Source, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First()).OrderByDescending(x => x.MajorVersion).ToList();
        }

        private void AddPath(List<OpennessInstallation> results, string path, string source) {
            try {
                this.AddPathCore(results, path, source);
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException || ex is NotSupportedException) {
                results.Add(new OpennessInstallation { ApiPath = path, Source = source, Diagnostic = ex.Message });
            }
        }

        private void AddPathCore(List<OpennessInstallation> results, string path, string source) {
            if (string.IsNullOrWhiteSpace(path)) {
                return;
            }

            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
                path = Path.GetDirectoryName(path);
            }

            var paths = new List<string> { path, Path.Combine(path, "net48") };
            var publicApi = Path.Combine(path, "PublicAPI");
            if (Directory.Exists(publicApi)) {
                foreach (var version in Directory.GetDirectories(publicApi, "V*")) {
                    paths.Add(version);
                    paths.Add(Path.Combine(version, "net48"));
                }
            }

            var found = paths.Where(p => File.Exists(Path.Combine(p, "Siemens.Engineering.Base.dll")) || File.Exists(Path.Combine(p, "Siemens.Engineering.dll"))).ToList();
            if (found.Count == 0) {
                results.Add(Validate(path, source));
            }
            else {
                foreach (var candidate in found) {
                    results.Add(Validate(candidate, source));
                }
            }
        }

        private void ReadRegistry(RegistryKey key, List<OpennessInstallation> results, int depth) {
            if (depth > 6) {
                return;
            }

            foreach (var name in key.GetValueNames()) {
                var value = key.GetValue(name) as string;
                if (!string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value)) {
                    this.AddPath(results, value, key.Name);
                }
            }

            foreach (var name in key.GetSubKeyNames()) {
                using (var child = key.OpenSubKey(name)) {
                    if (child != null) {
                        this.ReadRegistry(child, results, depth + 1);
                    }
                }
            }
        }
    }
}
