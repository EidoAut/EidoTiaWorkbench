// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ImportPreflightValidator.cs
// Validates repository artifacts before any Siemens Openness import call is made.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Validates repository artifacts before any Siemens Openness import call is made.
    /// </summary>
    public sealed class ImportPreflightValidator {
        /// <summary>
        /// Validates selected repository artifacts and dependency-planner diagnostics.
        /// </summary>
        /// <param name="selection">Repository artifacts selected for import.</param>
        /// <param name="dependencyPlan">Dependency plan produced for the same selection.</param>
        /// <returns>A preflight result containing human-readable issues.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is <see langword="null"/>.</exception>
        public ImportPreflightResult Validate(
            IReadOnlyCollection<RepositoryArtifact> selection,
            ImportDependencyPlan dependencyPlan) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            var result = new ImportPreflightResult();
            var artifacts = selection.Where(a => a != null && a.Item != null).ToList();
            var namesByKind = BuildNamesByKind(artifacts);

            ValidateSelection(artifacts, result);
            ValidateDependencies(dependencyPlan, result);

            foreach (var artifact in artifacts) {
                ValidateRepositoryArtifact(artifact, result);
                if (ArtifactCatalog.Get(artifact.Item.Kind).Format == ArtifactFormat.Xml) {
                    ValidateXmlArtifact(artifact, artifacts, namesByKind, result);
                }
            }

            if (result.Issues.Count == 0) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Info,
                    "Preflight",
                    null,
                    "Repository, manifest, XML, and dependency validation completed without local issues."));
            }

            return result;
        }

        /// <summary>
        /// Validates duplicate logical selections before deeper repository checks.
        /// </summary>
        /// <param name="artifacts">Selected artifacts.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateSelection(
            IEnumerable<RepositoryArtifact> artifacts,
            ImportPreflightResult result) {
            foreach (var duplicate in artifacts
                .GroupBy(a => a.Item.LogicalKey, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "Repository validation",
                    duplicate.First(),
                    "The same logical artifact is selected more than once: " + duplicate.Key + "."));
            }
        }

        /// <summary>
        /// Converts dependency-planner issues into blocking preflight errors.
        /// </summary>
        /// <param name="dependencyPlan">Dependency plan to inspect.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateDependencies(
            ImportDependencyPlan dependencyPlan,
            ImportPreflightResult result) {
            if (dependencyPlan == null) {
                return;
            }

            foreach (var issue in dependencyPlan.Issues) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "Dependency validation",
                    issue.Artifact,
                    issue.Message));
            }
        }

        /// <summary>
        /// Validates repository state, capability, and basic manifest-derived metadata.
        /// </summary>
        /// <param name="artifact">Artifact to inspect.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateRepositoryArtifact(
            RepositoryArtifact artifact,
            ImportPreflightResult result) {
            var descriptor = ArtifactCatalog.Get(artifact.Item.Kind);
            if (artifact.State == RepositoryArtifactState.Unsupported || artifact.Item.Kind == ExportKind.Unsupported) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "Repository validation",
                    artifact,
                    "Unsupported artifact category or repository path."));
            }

            if (artifact.State == RepositoryArtifactState.InvalidXml) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "XML validation",
                    artifact,
                    "Repository payload is not well-formed XML."));
            }

            if (!artifact.Item.CanImport || !descriptor.CanImport) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "Repository validation",
                    artifact,
                    "Artifact is not import-capable through the confirmed Openness V21 pipeline. " + descriptor.Limitations));
            }

            if (string.IsNullOrWhiteSpace(artifact.RepositoryPath) || !File.Exists(artifact.RepositoryPath)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "Repository validation",
                    artifact,
                    "Repository payload file is missing: " + artifact.RepositoryPath + "."));
            }

            if (string.IsNullOrWhiteSpace(artifact.Item.LogicalKey) ||
                string.IsNullOrWhiteSpace(artifact.Item.CpuPath) ||
                string.IsNullOrWhiteSpace(artifact.Item.Name)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "Manifest validation",
                    artifact,
                    "Manifest or repository path does not provide a complete logical key, target path, and artifact name."));
            }

            if (artifact.State == RepositoryArtifactState.Identical && string.IsNullOrWhiteSpace(artifact.Sha256)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Warning,
                    "Manifest validation",
                    artifact,
                    "Artifact is marked identical but the current payload hash could not be calculated."));
            }

            if (!string.IsNullOrWhiteSpace(artifact.Warning)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Warning,
                    "Repository validation",
                    artifact,
                    artifact.Warning));
            }
        }

        /// <summary>
        /// Parses and validates one XML payload with common Openness repository checks.
        /// </summary>
        /// <param name="artifact">Artifact whose XML is validated.</param>
        /// <param name="allArtifacts">All selected artifacts.</param>
        /// <param name="namesByKind">Selected artifact names indexed by kind.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateXmlArtifact(
            RepositoryArtifact artifact,
            IReadOnlyCollection<RepositoryArtifact> allArtifacts,
            IDictionary<ExportKind, HashSet<string>> namesByKind,
            ImportPreflightResult result) {
            XDocument document;
            try {
                document = XDocument.Load(artifact.RepositoryPath, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            }
            catch (XmlException ex) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "XML validation",
                    artifact,
                    "Invalid XML at line " + ex.LineNumber + ", position " + ex.LinePosition + ": " + ex.Message));
                return;
            }

            if (document.Root == null) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "XML hierarchy validation",
                    artifact,
                    "XML document has no root element."));
                return;
            }

            ValidateExpectedRoot(artifact, document.Root, result);
            ValidateDuplicateIds(artifact, document, result);
            ValidateMandatoryIds(artifact, document, result);
            ValidateObjectReferences(artifact, document, result);
            ValidateHmiReferences(artifact, document, allArtifacts, namesByKind, result);
            ValidateHmiDatatypeMetadata(artifact, document, result);
        }

        /// <summary>
        /// Warns when the XML root looks unrelated to the expected artifact family.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="root">Root XML element.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateExpectedRoot(RepositoryArtifact artifact, XElement root, ImportPreflightResult result) {
            var rootName = root.Name.LocalName ?? string.Empty;
            if (artifact.Item.Kind == ExportKind.HmiScreen &&
                rootName.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) < 0 &&
                root.Descendants().Take(32).All(e => e.Name.LocalName.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) < 0)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Warning,
                    "XML hierarchy validation",
                    artifact,
                    "HMI screen XML does not expose an obvious screen root or screen child element."));
            }

            if (artifact.Item.IsPlc &&
                rootName.IndexOf("Document", StringComparison.OrdinalIgnoreCase) < 0 &&
                rootName.IndexOf("SW.", StringComparison.OrdinalIgnoreCase) < 0) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Warning,
                    "XML hierarchy validation",
                    artifact,
                    "PLC XML root does not look like a standard TIA Openness document root."));
            }
        }

        /// <summary>
        /// Detects duplicate XML ID-like attributes inside one payload.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateDuplicateIds(RepositoryArtifact artifact, XDocument document, ImportPreflightResult result) {
            var ids = document.Descendants()
                .SelectMany(e => e.Attributes())
                .Where(IsIdAttribute)
                .Where(a => !string.IsNullOrWhiteSpace(a.Value))
                .GroupBy(a => a.Value, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Take(20);

            foreach (var duplicate in ids) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Error,
                    "XML validation",
                    artifact,
                    "Duplicate XML ID detected: " + duplicate.Key + "."));
            }
        }

        /// <summary>
        /// Warns when a named Openness object node has no ID-like metadata.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateMandatoryIds(RepositoryArtifact artifact, XDocument document, ImportPreflightResult result) {
            foreach (var element in document.Descendants().Where(HasObjectName).Take(200)) {
                if (!element.Attributes().Any(IsIdAttribute)) {
                    result.Add(new ImportValidationIssue(
                        ImportValidationSeverity.Warning,
                        "XML validation",
                        artifact,
                        "Named XML object has no ID-like attribute: " + DescribeElement(element) + "."));
                }
            }
        }

        /// <summary>
        /// Warns when local ID references point to IDs that are not present in the same XML document.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateObjectReferences(RepositoryArtifact artifact, XDocument document, ImportPreflightResult result) {
            var idValues = new HashSet<string>(
                document.Descendants()
                    .SelectMany(e => e.Attributes())
                    .Where(IsIdAttribute)
                    .Select(a => a.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v)),
                StringComparer.OrdinalIgnoreCase);

            foreach (var reference in document.Descendants()
                .SelectMany(e => e.Attributes())
                .Where(IsReferenceAttribute)
                .Where(a => LooksLikeLocalReference(a.Value))
                .Take(200)) {
                if (!idValues.Contains(reference.Value)) {
                    result.Add(new ImportValidationIssue(
                        ImportValidationSeverity.Warning,
                        "XML reference validation",
                        artifact,
                        "XML reference does not resolve to a local ID: " + reference.Name.LocalName + "='" + reference.Value + "'."));
                }
            }
        }

        /// <summary>
        /// Checks common HMI screen references to templates and tag tables against selected dependencies.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="allArtifacts">All selected artifacts.</param>
        /// <param name="namesByKind">Selected artifact names indexed by kind.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateHmiReferences(
            RepositoryArtifact artifact,
            XDocument document,
            IReadOnlyCollection<RepositoryArtifact> allArtifacts,
            IDictionary<ExportKind, HashSet<string>> namesByKind,
            ImportPreflightResult result) {
            if (!artifact.Item.IsHmi) {
                return;
            }

            ValidateNamedReferences(
                artifact,
                document,
                namesByKind,
                ExportKind.HmiTemplate,
                "Template",
                "Missing referenced HMI template in the selected import set.",
                result);
            ValidateNamedReferences(
                artifact,
                document,
                namesByKind,
                ExportKind.HmiTagTable,
                "TagTable",
                "Missing referenced HMI tag table in the selected import set.",
                result);

            if (artifact.Item.Kind == ExportKind.HmiScreen &&
                allArtifacts.Any(a => a.Item.Kind == ExportKind.HmiTemplate && SameTarget(a, artifact))) {
                return;
            }
        }

        /// <summary>
        /// Warns when HMI datatype metadata is empty or explicitly unknown.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateHmiDatatypeMetadata(RepositoryArtifact artifact, XDocument document, ImportPreflightResult result) {
            if (!artifact.Item.IsHmi) {
                return;
            }

            foreach (var attribute in document.Descendants()
                .SelectMany(e => e.Attributes())
                .Where(a => a.Name.LocalName.IndexOf("DataType", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(a => string.IsNullOrWhiteSpace(a.Value) || string.Equals(a.Value, "Unknown", StringComparison.OrdinalIgnoreCase))
                .Take(20)) {
                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Warning,
                    "HMI metadata validation",
                    artifact,
                    "HMI datatype metadata is empty or unknown: " + attribute.Name.LocalName + "."));
            }
        }

        /// <summary>
        /// Validates named XML references against selected artifact names of a required kind.
        /// </summary>
        /// <param name="artifact">Artifact being validated.</param>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="namesByKind">Selected artifact names indexed by kind.</param>
        /// <param name="kind">Required dependency kind.</param>
        /// <param name="nameMarker">XML name marker to inspect.</param>
        /// <param name="message">Issue message to emit when the reference is missing.</param>
        /// <param name="result">Mutable validation result.</param>
        private static void ValidateNamedReferences(
            RepositoryArtifact artifact,
            XDocument document,
            IDictionary<ExportKind, HashSet<string>> namesByKind,
            ExportKind kind,
            string nameMarker,
            string message,
            ImportPreflightResult result) {
            HashSet<string> names;
            if (!namesByKind.TryGetValue(kind, out names) || names.Count == 0) {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var value in GetReferenceValues(document, nameMarker).Take(20)) {
                if (!LooksLikeArtifactName(value) || names.Contains(value)) {
                    continue;
                }

                result.Add(new ImportValidationIssue(
                    ImportValidationSeverity.Warning,
                    "Dependency validation",
                    artifact,
                    message + " Reference: " + value + "."));
            }
        }

        /// <summary>
        /// Builds selected artifact names indexed by kind.
        /// </summary>
        /// <param name="artifacts">Artifacts to index.</param>
        /// <returns>Case-insensitive artifact name index by kind.</returns>
        private static IDictionary<ExportKind, HashSet<string>> BuildNamesByKind(IEnumerable<RepositoryArtifact> artifacts) {
            var result = new Dictionary<ExportKind, HashSet<string>>();
            foreach (var artifact in artifacts) {
                HashSet<string> names;
                if (!result.TryGetValue(artifact.Item.Kind, out names)) {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    result[artifact.Item.Kind] = names;
                }

                names.Add(artifact.Item.Name);
            }

            return result;
        }

        /// <summary>
        /// Gets XML attribute and element values whose names suggest a specific dependency type.
        /// </summary>
        /// <param name="document">Parsed XML document.</param>
        /// <param name="nameMarker">Name marker such as Template or TagTable.</param>
        /// <returns>Candidate reference values.</returns>
        private static IEnumerable<string> GetReferenceValues(XDocument document, string nameMarker) {
            foreach (var attribute in document.Descendants().SelectMany(e => e.Attributes())) {
                if (attribute.Name.LocalName.IndexOf(nameMarker, StringComparison.OrdinalIgnoreCase) >= 0) {
                    yield return attribute.Value;
                }
            }

            foreach (var element in document.Descendants()) {
                if (element.Name.LocalName.IndexOf(nameMarker, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !element.HasElements) {
                    yield return element.Value;
                }
            }
        }

        /// <summary>
        /// Determines whether an XML attribute should be considered an object ID.
        /// </summary>
        /// <param name="attribute">Attribute to inspect.</param>
        /// <returns><see langword="true"/> when the attribute name is ID-like.</returns>
        private static bool IsIdAttribute(XAttribute attribute) {
            var name = attribute.Name.LocalName;
            return string.Equals(name, "ID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "UId", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Guid", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether an XML attribute appears to reference another object.
        /// </summary>
        /// <param name="attribute">Attribute to inspect.</param>
        /// <returns><see langword="true"/> when the attribute name suggests a reference.</returns>
        private static bool IsReferenceAttribute(XAttribute attribute) {
            var name = attribute.Name.LocalName;
            return name.IndexOf("Reference", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.EndsWith("Ref", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("RefId", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether an element has object-name metadata that commonly requires a stable ID.
        /// </summary>
        /// <param name="element">Element to inspect.</param>
        /// <returns><see langword="true"/> when the element has a name and is object-like.</returns>
        private static bool HasObjectName(XElement element) {
            return element.Attributes().Any(a => string.Equals(a.Name.LocalName, "Name", StringComparison.OrdinalIgnoreCase)) &&
                   element.Attributes().Any(a => a.Name.LocalName.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 a.Name.LocalName.IndexOf("Class", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Builds a compact element description for validation messages.
        /// </summary>
        /// <param name="element">Element to describe.</param>
        /// <returns>Element name plus line information when available.</returns>
        private static string DescribeElement(XElement element) {
            var line = element as IXmlLineInfo;
            if (line != null && line.HasLineInfo()) {
                return element.Name.LocalName + " at line " + line.LineNumber;
            }

            return element.Name.LocalName;
        }

        /// <summary>
        /// Determines whether a value looks like an internal ID reference instead of a path or expression.
        /// </summary>
        /// <param name="value">Value to inspect.</param>
        /// <returns><see langword="true"/> when the value resembles a local ID.</returns>
        private static bool LooksLikeLocalReference(string value) {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf("/", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("\\", StringComparison.Ordinal) >= 0 ||
                value.IndexOf(":", StringComparison.Ordinal) >= 0 ||
                value.Length > 128) {
                return false;
            }

            return value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.');
        }

        /// <summary>
        /// Determines whether a reference value is likely to be a concrete artifact name.
        /// </summary>
        /// <param name="value">Reference value to inspect.</param>
        /// <returns><see langword="true"/> when the value is useful for dependency checks.</returns>
        private static bool LooksLikeArtifactName(string value) {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 128 ||
                value.IndexOf("{", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("}", StringComparison.Ordinal) >= 0 ||
                value.IndexOf(":", StringComparison.Ordinal) >= 0) {
                return false;
            }

            return value.Any(char.IsLetter);
        }

        /// <summary>
        /// Determines whether two artifacts share a target path.
        /// </summary>
        /// <param name="left">First artifact.</param>
        /// <param name="right">Second artifact.</param>
        /// <returns><see langword="true"/> when target paths match.</returns>
        private static bool SameTarget(RepositoryArtifact left, RepositoryArtifact right) {
            return string.Equals(left.Item.CpuPath, right.Item.CpuPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
