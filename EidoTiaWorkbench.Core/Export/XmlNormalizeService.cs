// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// XmlNormalizeService.cs
// Normalizes XML output to be stable and workspace-friendly: - UTF-8 (no BOM) - LF line endings -
// consistent indentation - stable attribute ordering (by namespace + local name) - optional scrubbing of
// volatile fields (regex-based) Notes: - Elements are NOT reordered because element order can be
// semantically meaningful for TIA exports. - Scrubbing is intentionally opt-in/configurable to avoid
// accidentally removing meaningful data.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Normalizes XML output to be stable and workspace-friendly:
    /// - UTF-8 (no BOM)
    /// - LF line endings
    /// - consistent indentation
    /// - stable attribute ordering (by namespace + local name)
    /// - optional scrubbing of volatile fields (regex-based)
    ///
    /// Notes:
    /// - Elements are NOT reordered because element order can be semantically meaningful for TIA exports.
    /// - Scrubbing is intentionally opt-in/configurable to avoid accidentally removing meaningful data.
    /// </summary>
    public sealed class XmlNormalizeService {
        private readonly Options _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlNormalizeService"/> class.
        /// </summary>
        public XmlNormalizeService()
            : this(new Options()) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="XmlNormalizeService"/> class.
        /// </summary>
        /// <param name="options">Options controlling formatting and scrubber behavior.</param>
        public XmlNormalizeService(Options options) {
            this._options = options ?? new Options();
        }

        /// <summary>
        /// Normalizes the provided XML string according to the configured options.
        /// </summary>
        /// <param name="xmlContent">Raw XML content to normalize.</param>
        /// <returns>Normalized XML text using deterministic formatting and configured scrubbers.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="xmlContent"/> is <see langword="null"/>.
        /// </exception>
        public string Normalize(string xmlContent) {
            if (xmlContent == null) {
                throw new ArgumentNullException(nameof(xmlContent));
            }

            // Secure-ish parser settings: prohibit DTDs and disable external entity resolution.
            var readerSettings = new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,

                // NOTE: We ignore whitespace so that output is controlled solely by XmlWriter formatting.
                IgnoreWhitespace = true,
            };

            XDocument doc;
            using (var sr = new StringReader(xmlContent))
            using (var xr = XmlReader.Create(sr, readerSettings)) {
                doc = XDocument.Load(xr, LoadOptions.None);
            }

            // Stable attribute ordering improves diffs when TIA changes attribute order between exports.
            if (this._options.SortAttributes && doc.Root != null) {
                SortAttributesRecursively(doc.Root);
            }

            // Output settings: UTF-8 no BOM, LF newlines, 2-space indent for clean diffs.
            var writerSettings = new XmlWriterSettings {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = this._options.OmitXmlDeclaration,
            };

            string formatted;
            using (var sw = new Utf8StringWriter(writerSettings.Encoding))
            using (var xw = XmlWriter.Create(sw, writerSettings)) {
                doc.Save(xw);
                xw.Flush();
                formatted = sw.ToString();
            }

            // Optional post-format scrubbers (best-effort).
            if (this._options.Scrubbers.Count > 0) {
                foreach (var s in this._options.Scrubbers) {
                    if (string.IsNullOrWhiteSpace(s?.Pattern)) {
                        continue;
                    }

                    formatted = s.ToRegex().Replace(formatted, s.Replacement ?? string.Empty);
                }
            }

            return formatted;
        }

        /// <summary>
        /// Reads an XML file (UTF-8), normalizes it, and writes it back in place (UTF-8 no BOM).
        /// </summary>
        /// <param name="filePath">Path to the XML file to normalize in place.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is empty.</exception>
        public void NormalizeFileInPlace(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path is required.", nameof(filePath));
            }

            var raw = File.ReadAllText(filePath, Encoding.UTF8);
            var norm = this.Normalize(raw);

            // Ensure stable encoding on disk (no BOM).
            File.WriteAllText(filePath, norm, new UTF8Encoding(false));
        }

        /// <summary>
        /// Sorts attributes recursively for deterministic output:
        /// - Namespace declarations are preserved (kept at the front).
        /// - Other attributes are sorted by namespace URI then local name (ordinal).
        /// </summary>
        /// <param name="element">Root element whose subtree is normalized.</param>
        private static void SortAttributesRecursively(XElement element) {
            // Preserve namespace declarations (order is not strictly semantic, but keeping them as-is helps readability).
            var attrs = element.Attributes().ToList();
            var nsDecls = attrs.Where(a => a.IsNamespaceDeclaration).ToList();

            var normal = attrs.Where(a => !a.IsNamespaceDeclaration)
                .OrderBy(a => a.Name.NamespaceName, StringComparer.Ordinal)
                .ThenBy(a => a.Name.LocalName, StringComparer.Ordinal)
                .ToList();

            element.ReplaceAttributes(nsDecls.Concat(normal));

            foreach (var child in element.Elements()) {
                SortAttributesRecursively(child);
            }
        }

        /// <summary>
        /// Normalization options controlling output formatting and optional scrubbing.
        /// </summary>
        public sealed class Options {
            /// <summary>
            /// Gets or sets a value indicating whether the XML declaration should be omitted.
            /// </summary>
            public bool OmitXmlDeclaration { get; set; } = false;

            /// <summary>
            /// Gets or sets a value indicating whether non-namespace attributes should be sorted deterministically.
            /// Namespace declarations are preserved as-is for readability and familiarity.
            /// </summary>
            public bool SortAttributes { get; set; } = true;

            /// <summary>
            /// Gets the optional list of regex replacements applied after formatting.
            /// Use this to remove volatile fields (timestamps/guids/build IDs) if needed.
            /// </summary>
            public List<RegexReplacement> Scrubbers { get; } = new List<RegexReplacement>();
        }

        /// <summary>
        /// Defines a regex-based replacement used by <see cref="Options.Scrubbers"/>.
        /// </summary>
        public sealed class RegexReplacement {
            /// <summary>
            /// Gets or sets the regex pattern to match. If null/empty, the scrubber is ignored.
            /// </summary>
            public string Pattern {
                get; set;
            }

            /// <summary>
            /// Gets or sets the replacement text. Empty text removes matches.
            /// </summary>
            public string Replacement { get; set; } = string.Empty;

            /// <summary>
            /// Builds a compiled, culture-invariant regex.
            /// </summary>
            /// <returns>A compiled regex using the configured pattern.</returns>
            public Regex ToRegex() =>
                new Regex(this.Pattern ?? string.Empty, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// StringWriter that reports UTF-8 encoding so XmlWriter produces the correct declaration when enabled.
        /// </summary>
        private sealed class Utf8StringWriter : StringWriter {
            /// <summary>
            /// Initializes a new instance of the <see cref="Utf8StringWriter"/> class.
            /// </summary>
            /// <param name="encoding">Encoding reported by the writer.</param>
            public Utf8StringWriter(Encoding encoding) =>
                this.Encoding = encoding ?? Encoding.UTF8;

            /// <summary>
            /// Gets the encoding that should be reported to XML writers.
            /// </summary>
            public override Encoding Encoding {
                get;
            }
        }
    }
}
