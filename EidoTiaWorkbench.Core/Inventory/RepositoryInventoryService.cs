// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// RepositoryInventoryService.cs
// Scans the structured export repository without requiring TIA Portal.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Core.Inventory {
    /// <summary>
    /// Scans the structured export repository without requiring TIA Portal.
    /// </summary>
    public sealed class RepositoryInventoryService {
        private readonly ManifestService _manifestService;

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryInventoryService"/> class.
        /// </summary>
        public RepositoryInventoryService()
            : this(new ManifestService()) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RepositoryInventoryService"/> class.
        /// </summary>
        /// <param name="manifestService">Manifest service used to read repository metadata.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="manifestService"/> is <see langword="null"/>.
        /// </exception>
        public RepositoryInventoryService(ManifestService manifestService) {
            this._manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
        }

        /// <summary>
        /// Scans a repository folder and returns importable XML artifacts with manifest comparison state.
        /// </summary>
        /// <param name="repoRoot">Root folder of the structured export repository.</param>
        /// <returns>Repository artifacts ordered by relative XML path.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repoRoot"/> is empty.</exception>
        /// <exception cref="DirectoryNotFoundException">
        /// Thrown when <paramref name="repoRoot"/> does not exist.
        /// </exception>
        public List<RepositoryArtifact> Scan(string repoRoot) {
            if (string.IsNullOrWhiteSpace(repoRoot)) {
                throw new ArgumentException("Repository path is required.", nameof(repoRoot));
            }

            if (!Directory.Exists(repoRoot)) {
                throw new DirectoryNotFoundException(repoRoot);
            }

            // Invalid manifests must remain visible failures, never silently become path-only inventories.
            var manifest = this._manifestService.Load(repoRoot);

            var root = Path.GetFullPath(repoRoot);
            var extensions = new HashSet<string>(
                ArtifactCatalog.All.Select(d => d.Extension),
                StringComparer.OrdinalIgnoreCase);
            var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                .Where(p => !IsUnderMetaFolder(root, p))
                .Where(p => extensions.Contains(Path.GetExtension(p)))
                .Where(p => IsPrimaryPayload(root, p, manifest))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<RepositoryArtifact>();
            foreach (var file in files) {
                result.Add(BuildArtifact(root, file, manifest));
            }

            foreach (var pair in manifest.Entries) {
                var entry = pair.Value;
                if (entry == null) {
                    throw new InvalidDataException("Manifest contains an empty artifact entry: " + pair.Key);
                }

                var payloads = new List<string>(entry.Files ?? new List<string>());
                if (!string.IsNullOrWhiteSpace(entry.PrimaryPath)) {
                    payloads.Add(entry.PrimaryPath);
                }

                if (payloads.Count == 0 && pair.Key.IndexOf("||", StringComparison.Ordinal) < 0 &&
                    !string.IsNullOrWhiteSpace(Path.GetExtension(pair.Key))) {
                    payloads.Add(pair.Key);
                }

                foreach (var relative in payloads.Distinct(StringComparer.OrdinalIgnoreCase)) {
                    var full = ManifestService.ResolvePayloadPath(root, relative);
                    if (!File.Exists(full)) {
                        throw new InvalidDataException("Manifest payload is missing: " + relative);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a repository artifact model for one XML file.
        /// </summary>
        /// <param name="repoRoot">Absolute repository root path.</param>
        /// <param name="filePath">Absolute XML file path.</param>
        /// <param name="manifest">Manifest used to compute comparison state.</param>
        /// <returns>The repository artifact model for the file.</returns>
        private static RepositoryArtifact BuildArtifact(
            string repoRoot,
            string filePath,
            ManifestService.Manifest manifest) {
            var relative = MakeRelativePath(repoRoot, filePath);
            ManifestService.ManifestEntry manifestEntry = null;
            if (manifest != null) {
                manifestEntry = FindManifestEntryByPayloadPath(manifest, relative.Replace("\\", "/"));
            }

            var item = TryCreateExportItem(relative, manifestEntry, out string warning);
            var state = RepositoryArtifactState.Unsupported;
            var hash = TryHash(filePath);
            var lastExport = default(string);

            if (manifestEntry != null) {
                lastExport = manifestEntry.LastExportUtc;
                state = string.Equals(hash, manifestEntry.Sha256, StringComparison.OrdinalIgnoreCase)
                    ? RepositoryArtifactState.Identical
                    : RepositoryArtifactState.Modified;
            }
            else if (item != null) {
                state = RepositoryArtifactState.New;
            }

            if (item != null &&
                ArtifactCatalog.Get(item.Kind).Format == ArtifactFormat.Xml &&
                !LooksLikeXml(filePath)) {
                state = RepositoryArtifactState.InvalidXml;
                warning = "The file is not valid XML.";
            }

            if (item == null) {
                item = new ExportItem(
                    "Repository",
                    string.Empty,
                    Path.GetFileNameWithoutExtension(filePath),
                    ExportKind.Unsupported,
                    "unsupported||" + relative.Replace("\\", "/"));
            }

            return new RepositoryArtifact(item, filePath, relative, state, hash, lastExport, warning);
        }

        /// <summary>
        /// Finds a manifest entry by payload path across schema 3 logical keys and legacy path keys.
        /// </summary>
        /// <param name="manifest">Manifest to inspect.</param>
        /// <param name="relativePath">Repository-relative payload path.</param>
        /// <returns>The matching entry, or <see langword="null"/> when none exists.</returns>
        private static ManifestService.ManifestEntry FindManifestEntryByPayloadPath(
            ManifestService.Manifest manifest,
            string relativePath) {
            if (manifest == null || manifest.Entries == null) {
                return null;
            }

            ManifestService.ManifestEntry entry;
            if (manifest.Entries.TryGetValue(relativePath, out entry)) {
                return entry;
            }

            foreach (var value in manifest.Entries.Values) {
                if (value == null) {
                    continue;
                }

                if (string.Equals(value.PrimaryPath, relativePath, StringComparison.OrdinalIgnoreCase) ||
                    (value.Files != null && value.Files.Any(
                        f => string.Equals(f, relativePath, StringComparison.OrdinalIgnoreCase)))) {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a repository file is the primary payload of a manifest entry.
        /// </summary>
        /// <param name="repoRoot">Absolute repository root path.</param>
        /// <param name="filePath">Candidate repository file path.</param>
        /// <param name="manifest">Manifest metadata, if available.</param>
        /// <returns><see langword="true"/> when the file should be exposed as an artifact.</returns>
        private static bool IsPrimaryPayload(string repoRoot, string filePath, ManifestService.Manifest manifest) {
            if (manifest == null || manifest.Entries == null || manifest.Entries.Count == 0) {
                return true;
            }

            var relative = MakeRelativePath(repoRoot, filePath).Replace("\\", "/");
            foreach (var entry in manifest.Entries.Values) {
                if (entry == null || entry.Files == null || entry.Files.Count == 0) {
                    continue;
                }

                if (!entry.Files.Any(f => string.Equals(f, relative, StringComparison.OrdinalIgnoreCase))) {
                    continue;
                }

                var primary = string.IsNullOrWhiteSpace(entry.PrimaryPath)
                    ? entry.Files[0]
                    : entry.PrimaryPath;
                return string.Equals(primary, relative, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        /// <summary>
        /// Attempts to translate a repository-relative XML path into an export item identity.
        /// </summary>
        /// <param name="relativePath">Repository-relative XML path.</param>
        /// <param name="manifestEntry">Manifest entry associated with the path, if present.</param>
        /// <param name="warning">Warning produced when the path cannot be mapped safely.</param>
        /// <returns>The reconstructed export item, or <see langword="null"/> for unsupported paths.</returns>
        private static ExportItem TryCreateExportItem(
            string relativePath,
            ManifestService.ManifestEntry manifestEntry,
            out string warning) {
            warning = null;

            if (manifestEntry != null) {
                ExportKind manifestKind;
                if (Enum.TryParse(manifestEntry.ArtifactKind ?? manifestEntry.ArtifactType, out manifestKind)) {
                    var descriptor = ArtifactCatalog.Get(manifestKind);
                    var manifestLogicalKey = string.IsNullOrWhiteSpace(manifestEntry.LogicalKey)
                        ? BuildLogicalKey(
                            manifestEntry.TargetPath,
                            manifestKind,
                            manifestEntry.GroupPath,
                            manifestEntry.Name)
                        : manifestEntry.LogicalKey;
                    return new ExportItem(
                        manifestEntry.TargetPath,
                        manifestEntry.GroupPath,
                        manifestEntry.Name,
                        manifestKind,
                        manifestLogicalKey,
                        manifestEntry.TargetType,
                        descriptor.CanExport,
                        descriptor.CanImport,
                        descriptor.Limitations,
                        manifestEntry.Dependencies,
                        null);
                }
            }

            var parts = relativePath
                .Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 4 && string.Equals(parts[0], "HMI", StringComparison.OrdinalIgnoreCase)) {
                return TryCreateHmiClassicItem(parts, out warning);
            }

            if (parts.Length >= 2 && string.Equals(parts[0], "PLC", StringComparison.OrdinalIgnoreCase)) {
                parts = parts.Skip(1).ToArray();
            }

            var categoryIndex = Array.FindIndex(
                parts,
                p => ArtifactCatalog.All.Any(d =>
                    d.Domain == ArtifactDomain.Plc &&
                    string.Equals(d.Category, p, StringComparison.OrdinalIgnoreCase)));

            if (categoryIndex <= 0 || categoryIndex >= parts.Length - 1) {
                warning = "Repository path does not match the expected CPU/category/artifact layout.";
                return null;
            }

            var category = parts[categoryIndex];
            var cpuPath = string.Join("/", parts.Take(categoryIndex).ToArray());
            var groupPath = string.Join(
                "/",
                parts.Skip(categoryIndex + 1).Take(parts.Length - categoryIndex - 2).ToArray());
            var fileName = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
            var name = StripDisambiguator(fileName);
            var kind = InferKind(category, name);

            if (kind == null) {
                warning = "Unsupported artifact category: " + category;
                return null;
            }

            var logicalKey = BuildLogicalKey(cpuPath, kind.Value, groupPath, name);
            return new ExportItem(cpuPath, groupPath, name, kind.Value, logicalKey);
        }

        /// <summary>
        /// Attempts to reconstruct a classic HMI screen item from the HMI repository layout.
        /// </summary>
        /// <param name="parts">Repository path segments.</param>
        /// <param name="warning">Warning produced when the HMI path cannot be mapped safely.</param>
        /// <returns>The reconstructed HMI screen item, or <see langword="null"/> for unsupported paths.</returns>
        private static ExportItem TryCreateHmiClassicItem(string[] parts, out string warning) {
            warning = null;

            var categoryIndex = Array.FindIndex(
                parts,
                p => ArtifactCatalog.All.Any(d =>
                    d.Domain == ArtifactDomain.HmiClassic &&
                    string.Equals(d.Category, p, StringComparison.OrdinalIgnoreCase)));
            if (categoryIndex <= 1 || categoryIndex >= parts.Length - 1) {
                warning = "Repository path does not match the expected HMI/<target>/<category>/<artifact> layout.";
                return null;
            }

            var descriptor = ArtifactCatalog.TryGetByCategory(ArtifactDomain.HmiClassic, parts[categoryIndex]);
            if (descriptor == null) {
                warning = "Unsupported HMI category: " + parts[categoryIndex];
                return null;
            }

            var targetPath = string.Join("/", parts.Skip(1).Take(categoryIndex - 1).ToArray());
            var groupPath = string.Join(
                "/",
                parts.Skip(categoryIndex + 1).Take(parts.Length - categoryIndex - 2).ToArray());
            var fileName = Path.GetFileNameWithoutExtension(parts[parts.Length - 1]);
            var name = StripDisambiguator(fileName);
            var logicalKey = BuildLogicalKey(targetPath, descriptor.Kind, groupPath, name);
            return new ExportItem(targetPath, groupPath, name, descriptor.Kind, logicalKey, "Classic HMI");
        }

        /// <summary>
        /// Infers the PLC artifact kind from a repository category and artifact name.
        /// </summary>
        /// <param name="category">Repository category segment.</param>
        /// <param name="name">Artifact name extracted from the file name.</param>
        /// <returns>The inferred export kind, or <see langword="null"/> when the category is unsupported.</returns>
        private static ExportKind? InferKind(string category, string name) {
            if (string.Equals(category, "Types", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.Udt;
            }

            if (string.Equals(category, "TagTables", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.PlcTagTable;
            }

            if (string.Equals(category, "WatchTables", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.PlcWatchTable;
            }

            if (string.Equals(category, "ForceTables", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.PlcForceTable;
            }

            if (string.Equals(category, "TechnologyObjects", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.TechnologicalObject;
            }

            if (string.Equals(category, "UserConstants", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.PlcUserConstant;
            }

            if (string.Equals(category, "ExternalSources", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.PlcExternalSource;
            }

            if (string.Equals(category, "AlarmTextLists", StringComparison.OrdinalIgnoreCase)) {
                return ExportKind.PlcAlarmTextList;
            }

            if (!string.Equals(category, "Blocks", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            var upper = (name ?? string.Empty).Trim().ToUpperInvariant();
            if (upper.StartsWith("OB")) {
                return ExportKind.Ob;
            }

            if (upper.StartsWith("FB")) {
                return ExportKind.Fb;
            }

            if (upper.StartsWith("FC")) {
                return ExportKind.Fc;
            }

            if (upper.StartsWith("DB")) {
                return ExportKind.Db;
            }

            return ExportKind.Fc;
        }

        /// <summary>
        /// Builds a stable logical key from repository or manifest identity fields.
        /// </summary>
        /// <param name="targetPath">Device, CPU, or HMI target path.</param>
        /// <param name="kind">Artifact kind.</param>
        /// <param name="groupPath">Artifact group path.</param>
        /// <param name="name">Artifact name.</param>
        /// <returns>The logical key used by selection and import workflows.</returns>
        private static string BuildLogicalKey(string targetPath, ExportKind kind, string groupPath, string name) {
            return (targetPath ?? string.Empty) + "||" + kind + "||" + (groupPath ?? string.Empty).Replace(
                "\\",
                "/") + "||" + (name ?? string.Empty);
        }

        /// <summary>
        /// Removes the PLC hash suffix from a file name when present.
        /// </summary>
        /// <param name="fileName">File name without extension.</param>
        /// <returns>The logical artifact name.</returns>
        private static string StripDisambiguator(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                return "Artifact";
            }

            var marker = fileName.LastIndexOf("__", StringComparison.Ordinal);
            if (marker > 0 && fileName.Length - marker == 10) {
                return fileName.Substring(0, marker);
            }

            return fileName;
        }

        /// <summary>
        /// Determines whether a file can be parsed as XML.
        /// </summary>
        /// <param name="filePath">Path to the file to inspect.</param>
        /// <returns>
        /// <see langword="true"/> when the file is well-formed XML; otherwise <see langword="false"/>.
        /// </returns>
        private static bool LooksLikeXml(string filePath) {
            try {
                XDocument.Load(filePath);
                return true;
            }
            catch {
                return false;
            }
        }

        /// <summary>
        /// Computes the repository comparison hash for a file.
        /// </summary>
        /// <param name="filePath">Path to the file to hash.</param>
        /// <returns>The SHA-256 hash, or <see langword="null"/> if the file cannot be read.</returns>
        private static string TryHash(string filePath) {
            try {
                return HashHelper.Sha256Hex(File.ReadAllBytes(filePath));
            }
            catch {
                return null;
            }
        }

        /// <summary>
        /// Determines whether a file belongs to the repository metadata folder.
        /// </summary>
        /// <param name="repoRoot">Absolute repository root path.</param>
        /// <param name="filePath">Absolute file path.</param>
        /// <returns>
        /// <see langword="true"/> when the file is under the metadata folder; otherwise <see langword="false"/>.
        /// </returns>
        private static bool IsUnderMetaFolder(string repoRoot, string filePath) {
            var rel = MakeRelativePath(repoRoot, filePath).Replace("\\", "/");
            return rel.StartsWith("meta/", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds a normalized repository-relative path when possible.
        /// </summary>
        /// <param name="root">Repository root path.</param>
        /// <param name="fullPath">Full path to make relative.</param>
        /// <returns>A slash-separated relative path, or the normalized full path when it is outside the root.</returns>
        private static string MakeRelativePath(string root, string fullPath) {
            var normalizedRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var normalizedPath = Path.GetFullPath(fullPath);

            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) {
                return normalizedPath.Substring(normalizedRoot.Length).Replace("\\", "/");
            }

            return normalizedPath.Replace("\\", "/");
        }
    }
}
