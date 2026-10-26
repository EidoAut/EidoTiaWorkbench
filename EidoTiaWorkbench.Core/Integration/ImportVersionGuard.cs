// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportVersionGuard.cs
// Rejects declared cross-version imports without rewriting Siemens payloads.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using EidoTiaWorkbench.Core.Export;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>Rejects declared cross-version imports without rewriting Siemens payloads.</summary>
    public static class ImportVersionGuard {
        /// <summary>Checks XML and nearest manifest provenance before an import can mutate a project.</summary>
        /// <param name="payloadPath">Existing payload file; XML is parsed with external entities prohibited.</param>
        /// <param name="targetMajor">Major version of the selected adapter.</param>
        /// <exception cref="InvalidDataException">A declared source version differs from the target.</exception>
        /// <exception cref="IOException">The payload or its manifest cannot be read.</exception>
        public static void Validate(string payloadPath, int targetMajor) {
            if (string.Equals(Path.GetExtension(payloadPath), ".xml", StringComparison.OrdinalIgnoreCase)) {
                using (var reader = XmlReader.Create(
                    payloadPath,
                    new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) {
                    var document = XDocument.Load(reader);
                    foreach (var engineering in document.Descendants().Where(x => x.Name.LocalName == "Engineering")) {
                        Check((string)engineering.Attribute("version"), targetMajor, "XML");
                    }
                }
            }

            var directory = new FileInfo(payloadPath).Directory;
            while (directory != null) {
                if (File.Exists(Path.Combine(directory.FullName, "meta", "manifest.json"))) {
                    var manifest = new ManifestService().Load(directory.FullName);
                    Check(manifest.TiaVersion, targetMajor, "Manifest TIA");
                    Check(manifest.OpennessVersion, targetMajor, "Manifest Openness");
                    break;
                }

                directory = directory.Parent;
            }
        }

        private static void Check(string source, int targetMajor, string evidence) {
            var major = OpennessDiscovery.ParseVersion(source);
            if (major != 0 && major != targetMajor) {
                throw new InvalidDataException(
                    evidence + " declares " + source + "; selected adapter is V" + targetMajor +
                        ". Cross-version import is not verified.");
            }
        }
    }
}
