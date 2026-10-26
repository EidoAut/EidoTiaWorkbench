// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// IntegrationSettings.cs
// Persists selection outside engineering repositories; manual selection never silently falls back.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>
    /// Persists selection outside engineering repositories; manual selection never silently falls back.
    /// </summary>
    [DataContract]
    public sealed class IntegrationSettings {
        /// <summary>Gets the per-user settings path outside engineering repositories.</summary>
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EidoTiaWorkbench",
            "integration.xml");

        /// <summary>
        /// Gets or sets the manually selected API directory; null or empty requests automatic selection.
        /// </summary>
        [DataMember]
        public string SelectedApiPath {
            get; set;
        }

        /// <summary>Gets or sets an additional installation or PublicAPI search directory.</summary>
        [DataMember]
        public string CustomPath {
            get; set;
        }

        /// <summary>Reads persisted selection, returning defaults when the file does not exist.</summary>
        /// <param name="path">Settings file, or null for the per-user default.</param>
        /// <returns>The deserialized selection; no Siemens assemblies are loaded.</returns>
        /// <exception cref="SerializationException">Existing settings are malformed.</exception>
        /// <exception cref="IOException">The settings file cannot be read.</exception>
        public static IntegrationSettings Load(string path = null) {
            path = path ?? DefaultPath;
            if (!File.Exists(path)) {
                return new IntegrationSettings();
            }

            using (var stream = File.OpenRead(path)) {
                return (IntegrationSettings)new DataContractSerializer(typeof(IntegrationSettings)).ReadObject(stream);
            }
        }

        /// <summary>Atomically replaces the settings file and removes temporary staging files.</summary>
        /// <param name="path">Destination file, or null for the per-user default.</param>
        /// <exception cref="IOException">Staging or promotion failed; an existing destination is preserved.</exception>
        public void Save(string path = null) {
            path = path ?? DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var stream = File.Create(temporary)) {
                    new DataContractSerializer(typeof(IntegrationSettings)).WriteObject(stream, this);
                }

                if (File.Exists(path)) {
                    File.Replace(temporary, path, null);
                }
                else {
                    File.Move(temporary, path);
                }
            }
            finally {
                if (File.Exists(temporary)) {
                    File.Delete(temporary);
                }
            }
        }

        /// <summary>Selects a usable installation without falling back from an unavailable manual choice.</summary>
        /// <param name="installations">Validated discovery candidates.</param>
        /// <returns>The selected usable adapter, or null when the request cannot be satisfied.</returns>
        public OpennessInstallation Select(IEnumerable<OpennessInstallation> installations) {
            var eligible = installations.Where(x => x.CanUse);
            return string.IsNullOrWhiteSpace(this.SelectedApiPath)
                ? eligible.OrderByDescending(x => x.MajorVersion).FirstOrDefault()
                : eligible.FirstOrDefault(
                    x => string.Equals(x.ApiPath, this.SelectedApiPath, StringComparison.OrdinalIgnoreCase));
        }
    }
}
