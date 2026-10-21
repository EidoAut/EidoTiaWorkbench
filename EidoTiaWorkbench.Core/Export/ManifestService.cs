// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ManifestService.cs
// Reads/writes the manifest file used for incremental exports.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using EidoTiaWorkbench.Core.Model;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Reads/writes the manifest file used for incremental exports.
    /// </summary>
    /// <remarks>
    /// The manifest stores:
    /// - Relative file path in the repository (key)
    /// - SHA-256 hash of the exported (optionally normalized) content
    /// - Timestamp of the last successful export
    ///
    /// Implementation notes:
    /// - Uses DataContractJsonSerializer to avoid extra dependencies.
    /// - Designed to be compatible with .NET Framework 4.8.
    /// - The manifest location is fixed under: &lt;repoRoot&gt;/meta/manifest.json.
    /// </remarks>
    public sealed class ManifestService {
        /// <summary>
        /// Default file name for the incremental manifest.
        /// Stored under the "meta" folder inside the repo root.
        /// </summary>
        private const string DefaultManifestName = "manifest.json";

        /// <summary>
        /// Builds the stable manifest key used for schema 3 artifact inventory entries.
        /// </summary>
        /// <param name="item">Scanned artifact whose logical identity should be persisted.</param>
        /// <returns>A stable manifest key that does not depend on a payload path.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
        public static string GetArtifactKey(ExportItem item) {
            if (item == null) {
                throw new ArgumentNullException(nameof(item));
            }

            if (!string.IsNullOrWhiteSpace(item.LogicalKey)) {
                return item.LogicalKey;
            }

            return (item.CpuPath ?? string.Empty) + "||" + item.Kind + "||" + (item.GroupPath ?? string.Empty).Replace("\\", "/") + "||" + (item.Name ?? string.Empty);
        }

        /// <summary>
        /// Determines whether a manifest entry should be offered by the retry command.
        /// </summary>
        /// <param name="entry">Manifest entry to inspect.</param>
        /// <returns><see langword="true"/> for failed, aborted, or explicitly retryable entries.</returns>
        public static bool IsRetryableEntry(ManifestEntry entry) {
            if (entry == null) {
                return false;
            }

            if (entry.IsRetryable) {
                return true;
            }

            return string.Equals(entry.LastStatus, ArtifactExportStatus.Failed.ToString(), StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(entry.LastStatus, ArtifactExportStatus.AbortedSessionLost.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads the manifest from disk. If it does not exist, returns a new empty manifest.
        /// </summary>
        /// <param name="repoRoot">Root folder of the structured export repository.</param>
        /// <returns>The loaded manifest, or an empty manifest when no file exists.</returns>
        public Manifest Load(string repoRoot) {
            var path = this.GetManifestPath(repoRoot);
            if (!File.Exists(path)) {
                return new Manifest();
            }

            using (var fs = File.OpenRead(path)) {
                var ser = new DataContractJsonSerializer(typeof(Manifest));
                var obj = ser.ReadObject(fs) as Manifest;

                // Defensive: handle corrupt/empty files gracefully by returning a new manifest.
                return obj ?? new Manifest();
            }
        }

        /// <summary>
        /// Saves the given manifest to disk (overwriting any existing manifest).
        /// </summary>
        /// <param name="repoRoot">Root folder of the structured export repository.</param>
        /// <param name="manifest">Manifest content to serialize.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="manifest"/> is <see langword="null"/>.
        /// </exception>
        public void Save(string repoRoot, Manifest manifest) {
            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            var path = this.GetManifestPath(repoRoot);

            // Ensure the meta folder exists.
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                using (var fs = File.Create(temporary)) {
                    var ser = new DataContractJsonSerializer(typeof(Manifest));
                    ser.WriteObject(fs, manifest);
                    fs.Flush(true);
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

        /// <summary>
        /// Removes manifest entries that no longer exist on disk.
        /// This keeps incremental state clean when files are deleted/renamed externally.
        /// </summary>
        /// <param name="repoRoot">Root folder used to resolve manifest entry paths.</param>
        /// <param name="manifest">Manifest whose entries should be pruned.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repoRoot"/> is empty.</exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="manifest"/> is <see langword="null"/>.
        /// </exception>
        public void PruneMissingEntries(string repoRoot, Manifest manifest) {
            if (string.IsNullOrWhiteSpace(repoRoot)) {
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            }

            if (manifest == null) {
                throw new ArgumentNullException(nameof(manifest));
            }

            var toRemove = new List<string>();

            // Keys are stored with forward slashes; convert to OS path separators for File.Exists checks.
            foreach (var kv in manifest.Entries) {
                var entry = kv.Value;
                if (entry != null && this.HasNoPayload(entry)) {
                    continue;
                }

                var payloads = this.GetPayloads(kv.Key, entry);
                if (payloads.Count == 0) {
                    continue;
                }

                var hasExistingPayload = false;
                foreach (var payload in payloads) {
                    var rel = (payload ?? string.Empty).Replace("/", Path.DirectorySeparatorChar.ToString());
                    var full = Path.Combine(repoRoot, rel);
                    if (File.Exists(full)) {
                        hasExistingPayload = true;
                        break;
                    }
                }

                if (!hasExistingPayload) {
                    toRemove.Add(kv.Key);
                }
            }

            // Remove after enumeration to avoid modifying the dictionary while iterating.
            foreach (var k in toRemove) {
                manifest.Entries.Remove(k);
            }
        }

        /// <summary>
        /// Returns the absolute manifest path for a given repo root:
        /// &lt;repoRoot&gt;/meta/manifest.json.
        /// </summary>
        /// <param name="repoRoot">Root folder of the structured export repository.</param>
        /// <returns>The absolute path of the manifest JSON file.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repoRoot"/> is empty.</exception>
        public string GetManifestPath(string repoRoot) {
            if (string.IsNullOrWhiteSpace(repoRoot)) {
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            }

            return Path.Combine(repoRoot, "meta", DefaultManifestName);
        }

        /// <summary>
        /// Determines whether a manifest entry intentionally has no valid payload.
        /// </summary>
        /// <param name="entry">Manifest entry to inspect.</param>
        /// <returns><see langword="true"/> when the entry represents inventory/result only.</returns>
        private bool HasNoPayload(ManifestEntry entry) {
            return entry != null &&
                   string.IsNullOrWhiteSpace(entry.PrimaryPath) &&
                   (entry.Files == null || entry.Files.Count == 0);
        }

        /// <summary>
        /// Returns payload paths declared by a manifest entry, falling back to legacy path keys.
        /// </summary>
        /// <param name="key">Manifest dictionary key.</param>
        /// <param name="entry">Manifest entry value.</param>
        /// <returns>Repository-relative payload paths.</returns>
        private List<string> GetPayloads(string key, ManifestEntry entry) {
            var result = new List<string>();
            if (entry != null) {
                if (entry.Files != null && entry.Files.Count > 0) {
                    result.AddRange(entry.Files);
                }
                else if (!string.IsNullOrWhiteSpace(entry.PrimaryPath)) {
                    result.Add(entry.PrimaryPath);
                }
            }

            if (result.Count == 0 && !string.IsNullOrWhiteSpace(key) && key.IndexOf("||", StringComparison.Ordinal) < 0) {
                result.Add(key);
            }

            return result;
        }

        /// <summary>
        /// Manifest data model persisted as JSON.
        /// Entries are keyed by repo-relative file path (using forward slashes).
        /// </summary>
        [DataContract]
        public sealed class Manifest {
            /// <summary>
            /// Gets or sets the tool version that produced this manifest for future migrations.
            /// </summary>
            [DataMember]
            public string ToolVersion { get; set; } = "0.3";

            /// <summary>
            /// Gets or sets the manifest schema version.
            /// </summary>
            [DataMember]
            public int SchemaVersion { get; set; } = 3;

            /// <summary>
            /// Gets or sets the TIA Portal version associated with the export run.
            /// </summary>
            [DataMember]
            public string TiaVersion { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the Openness API version associated with the export run.
            /// </summary>
            [DataMember]
            public string OpennessVersion { get; set; } = string.Empty;

            /// <summary>
            /// Gets or sets the UTC timestamp at which the last export run started.
            /// </summary>
            [DataMember]
            public string RunStartedUtc {
                get; set;
            }

            /// <summary>
            /// Gets or sets the UTC timestamp at which the last export run completed.
            /// </summary>
            [DataMember]
            public string RunCompletedUtc {
                get; set;
            }

            /// <summary>
            /// Gets or sets the global status of the last export run.
            /// </summary>
            [DataMember]
            public string RunStatus {
                get; set;
            }

            /// <summary>
            /// Gets or sets the repository folder represented by this manifest.
            /// </summary>
            [DataMember]
            public string RepositoryRoot {
                get; set;
            }

            /// <summary>
            /// Gets or sets the operation report path associated with the last export run.
            /// </summary>
            [DataMember]
            public string OperationReportPath {
                get; set;
            }

            /// <summary>
            /// Gets or sets the logical key of the first artifact that detected session loss.
            /// </summary>
            [DataMember]
            public string FirstSessionFailureKey {
                get; set;
            }

            /// <summary>
            /// Gets or sets the map of relative path to manifest entry metadata.
            /// Uses case-insensitive comparer to reduce issues on Windows file systems.
            /// </summary>
            [DataMember]
            public Dictionary<string, ManifestEntry> Entries {
                get; set;
            }

                = new Dictionary<string, ManifestEntry>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// One manifest entry describing a single exported file.
        /// </summary>
        [DataContract]
        public sealed class ManifestEntry {
            /// <summary>
            /// Gets or sets the SHA-256 hash of the written content after optional XML normalization.
            /// </summary>
            [DataMember]
            public string Sha256 {
                get; set;
            }

            /// <summary>
            /// Gets or sets the ISO-8601 UTC timestamp of the last successful export.
            /// </summary>
            [DataMember]
            public string LastExportUtc {
                get; set;
            }

            /// <summary>
            /// Gets or sets the artifact kind stored in this manifest entry.
            /// </summary>
            [DataMember]
            public string ArtifactKind {
                get; set;
            }

            /// <summary>
            /// Gets or sets the artifact domain stored in the central capability catalog.
            /// </summary>
            [DataMember]
            public string Domain {
                get; set;
            }

            /// <summary>
            /// Gets or sets the concrete artifact type stored in the central capability catalog.
            /// </summary>
            [DataMember]
            public string ArtifactType {
                get; set;
            }

            /// <summary>
            /// Gets or sets the repository category stored in the central capability catalog.
            /// </summary>
            [DataMember]
            public string Category {
                get; set;
            }

            /// <summary>
            /// Gets or sets the logical identifier used to resolve the artifact again.
            /// </summary>
            [DataMember]
            public string LogicalKey {
                get; set;
            }

            /// <summary>
            /// Gets or sets the PLC CPU path or HMI target path used to resolve the artifact.
            /// </summary>
            [DataMember]
            public string TargetPath {
                get; set;
            }

            /// <summary>
            /// Gets or sets the optional target type discovered from TIA Portal.
            /// </summary>
            [DataMember]
            public string TargetType {
                get; set;
            }

            /// <summary>
            /// Gets or sets the artifact group or folder path inside its PLC or HMI target.
            /// </summary>
            [DataMember]
            public string GroupPath {
                get; set;
            }

            /// <summary>
            /// Gets or sets the TIA object name.
            /// </summary>
            [DataMember]
            public string Name {
                get; set;
            }

            /// <summary>
            /// Gets or sets the payload format recorded for the artifact.
            /// </summary>
            [DataMember]
            public string Format {
                get; set;
            }

            /// <summary>
            /// Gets or sets the primary repository-relative payload path.
            /// </summary>
            [DataMember]
            public string PrimaryPath {
                get; set;
            }

            /// <summary>
            /// Gets or sets all repository-relative files that belong to this artifact.
            /// </summary>
            [DataMember]
            public List<string> Files {
                get; set;
            }

                = new List<string>();

            /// <summary>
            /// Gets or sets per-file hashes keyed by repository-relative path.
            /// </summary>
            [DataMember]
            public Dictionary<string, string> FileHashes {
                get; set;
            }

                = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Gets or sets the aggregate artifact hash used for payload comparison.
            /// </summary>
            [DataMember]
            public string AggregateHash {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the artifact was export-capable when scanned.
            /// </summary>
            [DataMember]
            public bool CanExport {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the artifact was import-capable when scanned.
            /// </summary>
            [DataMember]
            public bool CanImport {
                get; set;
            }

            /// <summary>
            /// Gets or sets the export/import strategy used by the tool.
            /// </summary>
            [DataMember]
            public string Strategy {
                get; set;
            }

            /// <summary>
            /// Gets or sets the concrete export handler selected by preflight.
            /// </summary>
            [DataMember]
            public string ExporterName {
                get; set;
            }

            /// <summary>
            /// Gets or sets the export capability flags observed before Openness was invoked.
            /// </summary>
            [DataMember]
            public string CapabilityDetail {
                get; set;
            }

            /// <summary>
            /// Gets or sets the final status of the last export operation that updated this entry.
            /// </summary>
            [DataMember]
            public string LastStatus {
                get; set;
            }

            /// <summary>
            /// Gets or sets limitations confirmed from the public Openness API.
            /// </summary>
            [DataMember]
            public string Limitations {
                get; set;
            }

            /// <summary>
            /// Gets or sets the TIA Portal version associated with this artifact export.
            /// </summary>
            [DataMember]
            public string TiaVersion {
                get; set;
            }

            /// <summary>
            /// Gets or sets dependency keys relevant for import planning.
            /// </summary>
            [DataMember]
            public List<string> Dependencies {
                get; set;
            }

                = new List<string>();

            /// <summary>
            /// Gets or sets warnings captured during scan or export.
            /// </summary>
            [DataMember]
            public List<string> Warnings {
                get; set;
            }

                = new List<string>();

            /// <summary>
            /// Gets or sets the UTC timestamp of the last attempt, whether or not payload was produced.
            /// </summary>
            [DataMember]
            public string LastAttemptUtc {
                get; set;
            }

            /// <summary>
            /// Gets or sets the attempt duration in milliseconds.
            /// </summary>
            [DataMember]
            public double LastAttemptDurationMs {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the last run attempted to export this artifact.
            /// </summary>
            [DataMember]
            public bool Attempted {
                get; set;
            }

            /// <summary>
            /// Gets or sets the short result message for the last attempt.
            /// </summary>
            [DataMember]
            public string Message {
                get; set;
            }

            /// <summary>
            /// Gets or sets detailed diagnostic text for the last attempt.
            /// </summary>
            [DataMember]
            public string TechnicalDetail {
                get; set;
            }

            /// <summary>
            /// Gets or sets the exception type captured during the last failed attempt.
            /// </summary>
            [DataMember]
            public string ExceptionType {
                get; set;
            }

            /// <summary>
            /// Gets or sets the HRESULT captured during the last failed attempt.
            /// </summary>
            [DataMember]
            public string HResult {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether the last condition appears transient.
            /// </summary>
            [DataMember]
            public bool IsTransient {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether retrying this artifact is meaningful.
            /// </summary>
            [DataMember]
            public bool IsRetryable {
                get; set;
            }

            /// <summary>
            /// Gets or sets the exact unavailable reason, if any.
            /// </summary>
            [DataMember]
            public string UnavailableReason {
                get; set;
            }

            /// <summary>
            /// Gets or sets the capability check that made an item unavailable.
            /// </summary>
            [DataMember]
            public string UnavailableCondition {
                get; set;
            }

            /// <summary>
            /// Gets or sets the logical key of the first session-loss artifact related to this entry.
            /// </summary>
            [DataMember]
            public string SessionFailureKey {
                get; set;
            }

            /// <summary>
            /// Gets or sets the session health status observed after the attempt.
            /// </summary>
            [DataMember]
            public string SessionHealthStatus {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether controlled session recovery was attempted.
            /// </summary>
            [DataMember]
            public bool SessionRecoveryAttempted {
                get; set;
            }

            /// <summary>
            /// Gets or sets a value indicating whether controlled session recovery restored a valid project.
            /// </summary>
            [DataMember]
            public bool SessionRecoverySucceeded {
                get; set;
            }

            /// <summary>
            /// Gets or sets diagnostic text for the controlled recovery attempt.
            /// </summary>
            [DataMember]
            public string SessionRecoveryDetail {
                get; set;
            }
        }
    }
}
