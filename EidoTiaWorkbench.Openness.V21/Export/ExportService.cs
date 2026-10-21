// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ExportService.cs
// Exports selected PLC artifacts from the open TIA Portal project into structured workspace payload files.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;
using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.Types;
using static EidoTiaWorkbench.Core.Export.ManifestService;

namespace EidoTiaWorkbench.Core.Export {
    /// <summary>
    /// Exports selected PLC artifacts from the open TIA Portal project into structured workspace payload files.
    /// </summary>
    /// <remarks>
    /// Supported artifacts include UDTs, OB/FB/FC/DB blocks, and PLC tag tables when the installed
    /// Openness API exposes them. Calls must run on the same STA thread as the TIA Portal instance.
    /// </remarks>
    public sealed class ExportService {
        private readonly TiaSessionService _session;
        private readonly XmlNormalizeService _xmlNormalize;
        private readonly ManifestService _manifestService;
        private readonly ILogger _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportService"/> class.
        /// </summary>
        /// <param name="session">TIA Portal session service used to access the open project.</param>
        /// <param name="xmlNormalize">XML normalization service used when normalized exports are requested.</param>
        /// <param name="manifestService">Manifest service used for incremental export state.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>, <paramref name="xmlNormalize"/>, or
        /// <paramref name="manifestService"/> is <see langword="null"/>.
        /// </exception>
        public ExportService(TiaSessionService session, XmlNormalizeService xmlNormalize, ManifestService manifestService)
            : this(session, xmlNormalize, manifestService, NullLogger.Instance) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportService"/> class.
        /// </summary>
        /// <param name="session">TIA Portal session service used to access the open project.</param>
        /// <param name="xmlNormalize">XML normalization service used when normalized exports are requested.</param>
        /// <param name="manifestService">Manifest service used for incremental export state.</param>
        /// <param name="logger">Logger that receives export progress and failure details.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/>, <paramref name="xmlNormalize"/>, or
        /// <paramref name="manifestService"/> is <see langword="null"/>.
        /// </exception>
        public ExportService(TiaSessionService session, XmlNormalizeService xmlNormalize, ManifestService manifestService, ILogger logger) {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
            this._xmlNormalize = xmlNormalize ?? throw new ArgumentNullException(nameof(xmlNormalize));
            this._manifestService = manifestService ?? throw new ArgumentNullException(nameof(manifestService));
            this._log = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Exports the given selection into the repository root.
        /// If <paramref name="incremental"/> is true, unchanged items are skipped using the manifest hashes.
        /// </summary>
        /// <param name="repoRoot">Root folder where the export run should be created or updated.</param>
        /// <param name="selection">Artifacts selected for export.</param>
        /// <param name="normalizeXml">Whether exported XML should be normalized before it is written.</param>
        /// <param name="incremental">Whether unchanged files should be skipped through manifest hash comparison.</param>
        /// <param name="progress">
        /// Optional callback receiving processed count, total count, current item, and exception if any.
        /// </param>
        /// <returns>A result object containing output path, counters, warnings, and errors.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repoRoot"/> is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is <see langword="null"/>.</exception>
        public ExportResult Export(
            string repoRoot,
            IReadOnlyCollection<ExportItem> selection,
            bool normalizeXml,
            bool incremental,
            Action<int, int, ExportItem, Exception> progress = null) {
            return this.Export(repoRoot, selection, normalizeXml, preserveHmiXml: true, incremental: incremental, progress: progress);
        }

        /// <summary>
        /// Exports the given selection into the repository root with independent PLC and HMI XML policies.
        /// </summary>
        /// <param name="repoRoot">Root folder where the export run should be created or updated.</param>
        /// <param name="selection">Artifacts selected for export.</param>
        /// <param name="normalizePlcXml">Whether PLC XML should be normalized before writing.</param>
        /// <param name="preserveHmiXml">Whether HMI screen XML should be preserved exactly as Openness exported it.</param>
        /// <param name="incremental">Whether unchanged files should be skipped through manifest hash comparison.</param>
        /// <param name="progress">
        /// Optional callback receiving processed count, total count, current item, and exception if any.
        /// </param>
        /// <returns>A result object containing output path, counters, warnings, and errors.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repoRoot"/> is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is <see langword="null"/>.</exception>
        public ExportResult Export(
            string repoRoot,
            IReadOnlyCollection<ExportItem> selection,
            bool normalizePlcXml,
            bool preserveHmiXml,
            bool incremental,
            Action<int, int, ExportItem, Exception> progress = null) {
            return this.Export(
                repoRoot,
                selection,
                normalizePlcXml,
                preserveHmiXml,
                incremental,
                progress,
                CancellationToken.None);
        }

        /// <summary>
        /// Exports the given selection with cooperative cancellation between artifacts.
        /// </summary>
        /// <param name="repoRoot">Root folder where the export run should be created or updated.</param>
        /// <param name="selection">Artifacts selected for export.</param>
        /// <param name="normalizePlcXml">Whether PLC XML should be normalized before writing.</param>
        /// <param name="preserveHmiXml">Whether HMI screen XML should be preserved exactly as Openness exported it.</param>
        /// <param name="incremental">Whether unchanged files should be skipped through manifest hash comparison.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="cancellationToken">Cancellation token checked before each artifact is started.</param>
        /// <returns>A result object containing artifact-level outcomes, output paths, counters, warnings, and errors.</returns>
        public ExportResult Export(
            string repoRoot,
            IReadOnlyCollection<ExportItem> selection,
            bool normalizePlcXml,
            bool preserveHmiXml,
            bool incremental,
            Action<int, int, ExportItem, Exception> progress,
            CancellationToken cancellationToken) {
            if (string.IsNullOrWhiteSpace(repoRoot)) {
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            }

            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            var initialHealth = this._session.CheckHealthOnSta();
            if (!initialHealth.IsHealthy) {
                throw new InvalidOperationException(initialHealth.Message, initialHealth.Exception);
            }

            var projectName = string.IsNullOrWhiteSpace(initialHealth.ProjectName) ? "Project" : initialHealth.ProjectName;
            var expectedProjectName = initialHealth.ProjectName;
            var expectedProjectPath = initialHealth.ProjectPath;
            var runRoot = incremental
                ? Path.Combine(repoRoot, PathHelper.Sanitize(projectName))
                : Path.Combine(repoRoot, PathHelper.Sanitize(projectName) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(runRoot);

            var manifest = incremental ? this._manifestService.Load(runRoot) : new Manifest();
            var sourceVersion = EidoTiaWorkbench.Core.Integration.OpennessDiscovery.ParseVersion(manifest.TiaVersion);
            if (sourceVersion != 0 && sourceVersion != 21) {
                throw new InvalidOperationException("Incremental export cannot mix TIA versions in one manifest.");
            }

            manifest.TiaVersion = "V21";
            manifest.OpennessVersion = typeof(Siemens.Engineering.TiaPortal).Assembly.GetName().Version.ToString();
            var result = new ExportResult {
                OutputRoot = runRoot,
                ManifestPath = this._manifestService.GetManifestPath(runRoot),
            };

            manifest.RepositoryRoot = runRoot;
            manifest.RunStartedUtc = result.StartedUtc.ToString("o");

            var ordered = ExportOrderPolicy.OrderForExport(selection, manifest.FirstSessionFailureKey).ToList();

            var collidingKeys = FindOutputPathCollisions(runRoot, ordered);
            var total = ordered.Count;
            var processed = 0;
            var sessionFailureKey = default(string);
            var sessionRecoveryAttempted = false;

            for (var index = 0; index < ordered.Count; index++) {
                var item = ordered[index];
                if (cancellationToken.IsCancellationRequested) {
                    processed = MarkRemainingCancelled(ordered, index, processed, total, manifest, result, progress);
                    break;
                }

                var healthBefore = this._session.CheckHealthOnSta();
                if (!healthBefore.IsHealthy) {
                    var recoveryDetail = string.Empty;
                    var recoveryAttempted = false;
                    var recoverySucceeded = false;
                    if (!sessionRecoveryAttempted) {
                        sessionRecoveryAttempted = true;
                        recoveryAttempted = true;
                        var recoveryHealth = this._session.TryRecoverProjectOnSta(expectedProjectName, expectedProjectPath);
                        recoverySucceeded = recoveryHealth.IsHealthy;
                        recoveryDetail = BuildRecoveryDetail(healthBefore, recoveryHealth);
                        healthBefore = recoveryHealth;
                    }

                    if (healthBefore.IsHealthy) {
                        this._log.Warn("Export session recovered before artifact attempt: " + item);
                    }
                    else {
                        sessionFailureKey = item.LogicalKey;
                        processed = MarkRemainingAborted(
                            ordered,
                            index,
                            processed,
                            total,
                            manifest,
                            result,
                            progress,
                            sessionFailureKey,
                            healthBefore,
                            recoveryAttempted,
                            recoverySucceeded,
                            recoveryDetail);
                        break;
                    }
                }

                var descriptor = ArtifactCatalog.Get(item.Kind);
                var adapter = ArtifactAdapterRegistry.Get(item.Kind);
                var outPath = PathHelper.GetOutputFilePath(runRoot, item);
                var rel = MakeRepoRelative(runRoot, outPath);
                var existingKey = default(string);
                var existing = FindManifestEntry(manifest, item, rel, out existingKey);
                var record = new ArtifactExportRecord(item, ArtifactExportStatus.Failed);
                record.AttemptedUtc = DateTime.UtcNow;
                var preflight = ArtifactExportPreflight.Evaluate(item, descriptor, adapter);
                record.ExporterName = preflight.ExporterName;
                record.Strategy = preflight.ApiUsed;
                record.CapabilityDetail = preflight.CapabilityDetail;

                try {
                    if (collidingKeys.Contains(item.LogicalKey)) {
                        throw new InvalidOperationException(
                            "Sanitized export path collides with another selected artifact: " + rel);
                    }

                    if (!preflight.CanAttemptExport) {
                        record.Status = preflight.BlockedStatus;
                        record.Message = preflight.Message;
                        record.UnavailableReason = preflight.Reason;
                        record.UnavailableCondition = preflight.Condition;
                        record.IsTransient = false;
                        record.IsRetryable = false;
                        record.SessionHealthStatus = healthBefore.Status.ToString();
                        record.CompleteNow();
                        UpsertManifestEntry(manifest, item, record, existing, existingKey);
                        AddRecord(result, item, record, new ExportWarningException(record.Message, item));
                        processed++;
                        progress?.Invoke(processed, total, item, new ExportWarningException(record.Message, item));
                        continue;
                    }

                    var xml = this.ExportSingleToString(item);
                    if (item.IsPlc && normalizePlcXml) {
                        xml = this._xmlNormalize.Normalize(xml);
                    }
                    else if (item.IsHmi && !preserveHmiXml && normalizePlcXml) {
                        xml = this._xmlNormalize.Normalize(xml);
                    }

                    var candidateHash = HashHelper.Sha256Hex(xml);
                    if (incremental &&
                        existing != null &&
                        string.Equals(existing.Sha256, candidateHash, StringComparison.OrdinalIgnoreCase) &&
                        HasExistingPayload(runRoot, existing)) {
                        record.Status = ArtifactExportStatus.SkippedUnchanged;
                        record.Message = "Artifact was unchanged; previous payload was kept.";
                        record.PrimaryPath = string.IsNullOrWhiteSpace(existing.PrimaryPath) ? rel : existing.PrimaryPath;
                        record.Hash = candidateHash;
                        record.IsTransient = false;
                        record.IsRetryable = false;
                        record.CompleteNow();
                        UpsertManifestEntry(manifest, item, record, existing, existingKey);
                        AddRecord(result, item, record, null);
                        processed++;
                        progress?.Invoke(processed, total, item, null);
                        continue;
                    }

                    var promotedHash = TransactionalFileWriter.PromoteUtf8Text(outPath, xml, descriptor.Format);
                    record.Status = ArtifactExportStatus.Succeeded;
                    record.Message = "Exported successfully.";
                    record.PrimaryPath = rel;
                    record.Hash = promotedHash;
                    record.IsTransient = false;
                    record.IsRetryable = false;
                    record.SessionHealthStatus = this._session.CheckHealthOnSta().Status.ToString();
                    record.CompleteNow();
                    UpsertManifestEntry(manifest, item, record, existing, existingKey);
                    AddRecord(result, item, record, null);
                    processed++;
                    progress?.Invoke(processed, total, item, null);
                }
                catch (Exception ex) {
                    var healthAfter = this._session.CheckHealthOnSta();
                    var classification = ExportFailureClassifier.Classify(ex, healthAfter, item);
                    var recoveryAttempted = false;
                    var recoverySucceeded = false;
                    var recoveryDetail = string.Empty;
                    var retryCurrentAfterRecovery = false;
                    if (ExportFailureIsolationPolicy.ShouldAttemptControlledRecovery(classification, sessionRecoveryAttempted)) {
                        sessionRecoveryAttempted = true;
                        recoveryAttempted = true;
                        var recoveryHealth = this._session.TryRecoverProjectOnSta(expectedProjectName, expectedProjectPath);
                        recoverySucceeded = recoveryHealth.IsHealthy;
                        recoveryDetail = BuildRecoveryDetail(healthAfter, recoveryHealth);
                        healthAfter = recoveryHealth;
                        classification = ExportFailureClassifier.Classify(ex, healthAfter, item);
                        if (recoverySucceeded) {
                            this._log.Warn("Export session recovered after artifact failure: " + item);
                            retryCurrentAfterRecovery = true;
                        }
                    }

                    if (retryCurrentAfterRecovery) {
                        index--;
                        continue;
                    }

                    record.Status = classification.Status;
                    record.Message = classification.Message;
                    record.TechnicalDetail = classification.TechnicalDetail;
                    record.ExceptionType = ex.GetType().FullName;
                    record.HResult = ExportFailureClassifier.GetHResult(ex);
                    record.IsTransient = classification.IsTransient;
                    record.IsRetryable = classification.IsRetryable;
                    record.SessionHealthStatus = healthAfter.Status.ToString();
                    record.SessionRecoveryAttempted = recoveryAttempted;
                    record.SessionRecoverySucceeded = recoverySucceeded;
                    record.SessionRecoveryDetail = recoveryDetail;
                    record.CompleteNow();

                    if (ExportFailureIsolationPolicy.ShouldAbortQueue(classification)) {
                        sessionFailureKey = item.LogicalKey;
                        record.SessionFailureKey = sessionFailureKey;
                        result.FirstSessionFailure = record;
                    }

                    UpsertManifestEntry(manifest, item, record, existing, existingKey);
                    AddRecord(result, item, record, ex);
                    processed++;
                    progress?.Invoke(processed, total, item, ex);

                    if (ExportFailureIsolationPolicy.ShouldAbortQueue(classification)) {
                        processed = MarkRemainingAborted(
                            ordered,
                            index + 1,
                            processed,
                            total,
                            manifest,
                            result,
                            progress,
                            sessionFailureKey,
                            healthAfter,
                            recoveryAttempted,
                            recoverySucceeded,
                            recoveryDetail);
                        break;
                    }
                }
            }

            result.CompletedUtc = DateTime.UtcNow;
            result.Recalculate();
            result.OperationReportPath = GetOperationReportPath(runRoot);
            manifest.RunCompletedUtc = result.CompletedUtc.ToString("o");
            manifest.RunStatus = result.Status.ToString();
            manifest.OperationReportPath = result.OperationReportPath;
            manifest.FirstSessionFailureKey = result.FirstSessionFailure == null ? null : result.FirstSessionFailure.LogicalKey;

            this._manifestService.PruneMissingEntries(runRoot, manifest);
            this._manifestService.Save(runRoot, manifest);
            WriteTiaVersionHint(runRoot, manifest.TiaVersion);
            WriteOperationReport(result, manifest);

            return result;
        }

        /// <summary>
        /// Adds a record to the export result and mirrors legacy warning/error lists for existing callers.
        /// </summary>
        /// <param name="result">Run result being built.</param>
        /// <param name="item">Artifact associated with the record.</param>
        /// <param name="record">Artifact-level result record.</param>
        /// <param name="exception">Optional exception associated with the record.</param>
        private static void AddRecord(ExportResult result, ExportItem item, ArtifactExportRecord record, Exception exception) {
            result.Artifacts.Add(record);

            if (record.Status == ArtifactExportStatus.Failed) {
                result.Errors.Add(new ExportError(item, exception ?? new InvalidOperationException(record.Message)));
            }
            else if (record.Status == ArtifactExportStatus.Restricted ||
                       record.Status == ArtifactExportStatus.Unavailable ||
                       record.Status == ArtifactExportStatus.Unsupported) {
                result.Warnings.Add(new ExportWarning(item, exception ?? new ExportWarningException(record.Message, item)));
            }
        }

        /// <summary>
        /// Finds the manifest entry for an artifact, supporting both schema 3 logical keys and legacy path keys.
        /// </summary>
        /// <param name="manifest">Manifest to inspect.</param>
        /// <param name="item">Artifact identity.</param>
        /// <param name="relativePath">Repository-relative payload path expected for this run.</param>
        /// <param name="entryKey">Manifest key that contained the returned entry.</param>
        /// <returns>The matching manifest entry, or <see langword="null"/> when absent.</returns>
        private static ManifestEntry FindManifestEntry(Manifest manifest, ExportItem item, string relativePath, out string entryKey) {
            entryKey = null;
            if (manifest == null || manifest.Entries == null) {
                return null;
            }

            var logicalKey = GetArtifactKey(item);
            ManifestEntry entry;
            if (manifest.Entries.TryGetValue(logicalKey, out entry)) {
                entryKey = logicalKey;
                return entry;
            }

            if (!string.IsNullOrWhiteSpace(relativePath) && manifest.Entries.TryGetValue(relativePath, out entry)) {
                entryKey = relativePath;
                return entry;
            }

            foreach (var pair in manifest.Entries) {
                var value = pair.Value;
                if (value == null) {
                    continue;
                }

                if (string.Equals(value.LogicalKey, logicalKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(value.PrimaryPath, relativePath, StringComparison.OrdinalIgnoreCase) ||
                    (value.Files != null && value.Files.Any(f => string.Equals(f, relativePath, StringComparison.OrdinalIgnoreCase)))) {
                    entryKey = pair.Key;
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Writes or updates one manifest entry from an artifact result, preserving previous valid payload metadata on failure.
        /// </summary>
        /// <param name="manifest">Manifest to update.</param>
        /// <param name="item">Scanned artifact identity.</param>
        /// <param name="record">Final artifact result.</param>
        /// <param name="existing">Existing manifest entry, if any.</param>
        /// <param name="existingKey">Key that contained the existing entry.</param>
        private static void UpsertManifestEntry(Manifest manifest, ExportItem item, ArtifactExportRecord record, ManifestEntry existing, string existingKey) {
            var descriptor = ArtifactCatalog.Get(item.Kind);
            var key = GetArtifactKey(item);
            var entry = existing ?? new ManifestEntry();

            entry.ArtifactKind = item.Kind.ToString();
            entry.Domain = descriptor.Domain.ToString();
            entry.ArtifactType = item.Kind.ToString();
            entry.Category = descriptor.Category;
            entry.LogicalKey = item.LogicalKey;
            entry.TargetPath = item.CpuPath;
            entry.TargetType = item.TargetType;
            entry.GroupPath = item.GroupPath;
            entry.Name = item.Name;
            entry.Format = descriptor.Format.ToString();
            entry.CanExport = item.CanExport;
            entry.CanImport = item.CanImport;
            entry.Strategy = record.Strategy;
            entry.ExporterName = record.ExporterName;
            entry.CapabilityDetail = record.CapabilityDetail;
            entry.Limitations = descriptor.Limitations;
            entry.TiaVersion = manifest.TiaVersion;
            entry.Dependencies = item.Dependencies.ToList();
            entry.LastStatus = record.Status.ToString();
            entry.LastAttemptUtc = record.AttemptedUtc.ToString("o");
            entry.LastAttemptDurationMs = record.Duration.TotalMilliseconds;
            entry.Attempted = record.Status != ArtifactExportStatus.Cancelled;
            entry.Message = record.Message;
            entry.TechnicalDetail = record.TechnicalDetail;
            entry.ExceptionType = record.ExceptionType;
            entry.HResult = record.HResult;
            entry.IsTransient = record.IsTransient;
            entry.IsRetryable = record.IsRetryable;
            entry.UnavailableReason = record.UnavailableReason;
            entry.UnavailableCondition = record.UnavailableCondition;
            entry.SessionFailureKey = record.SessionFailureKey;
            entry.SessionHealthStatus = record.SessionHealthStatus;
            entry.SessionRecoveryAttempted = record.SessionRecoveryAttempted;
            entry.SessionRecoverySucceeded = record.SessionRecoverySucceeded;
            entry.SessionRecoveryDetail = record.SessionRecoveryDetail;
            entry.Warnings = BuildManifestWarnings(item, record);

            if (record.Status == ArtifactExportStatus.Succeeded) {
                entry.Sha256 = record.Hash;
                entry.LastExportUtc = record.CompletedUtc.ToString("o");
                entry.PrimaryPath = record.PrimaryPath;
                entry.Files = new List<string> { record.PrimaryPath };
                entry.FileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { record.PrimaryPath, record.Hash },
                };
                entry.AggregateHash = record.Hash;
            }
            else if (record.Status == ArtifactExportStatus.SkippedUnchanged) {
                entry.Sha256 = string.IsNullOrWhiteSpace(record.Hash) ? entry.Sha256 : record.Hash;
                entry.PrimaryPath = string.IsNullOrWhiteSpace(record.PrimaryPath) ? entry.PrimaryPath : record.PrimaryPath;
                if ((entry.Files == null || entry.Files.Count == 0) && !string.IsNullOrWhiteSpace(entry.PrimaryPath)) {
                    entry.Files = new List<string> { entry.PrimaryPath };
                }
            }
            else if (existing == null) {
                entry.Sha256 = string.Empty;
                entry.PrimaryPath = string.Empty;
                entry.Files = new List<string>();
                entry.FileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                entry.AggregateHash = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(existingKey) &&
                !string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase)) {
                manifest.Entries.Remove(existingKey);
            }

            manifest.Entries[key] = entry;
        }

        /// <summary>
        /// Builds warning strings stored in the manifest without duplicating full activity log entries.
        /// </summary>
        /// <param name="item">Artifact identity.</param>
        /// <param name="record">Artifact result record.</param>
        /// <returns>Manifest warning list for the entry.</returns>
        private static List<string> BuildManifestWarnings(ExportItem item, ArtifactExportRecord record) {
            var warnings = new List<string>();
            if (!string.IsNullOrWhiteSpace(item.UnavailableReason)) {
                warnings.Add(item.UnavailableReason);
            }

            if (record.Status == ArtifactExportStatus.Restricted ||
                record.Status == ArtifactExportStatus.Unavailable ||
                record.Status == ArtifactExportStatus.Unsupported ||
                record.Status == ArtifactExportStatus.AbortedSessionLost ||
                record.Status == ArtifactExportStatus.Cancelled) {
                warnings.Add(record.Message);
            }

            return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Marks all remaining artifacts as cancelled after a cooperative user request.
        /// </summary>
        /// <param name="items">Ordered artifact list.</param>
        /// <param name="startIndex">First unprocessed index.</param>
        /// <param name="processed">Number of artifacts already processed.</param>
        /// <param name="total">Total artifacts in the run.</param>
        /// <param name="manifest">Manifest to update.</param>
        /// <param name="result">Run result to update.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <returns>Updated processed count.</returns>
        private static int MarkRemainingCancelled(
            IList<ExportItem> items,
            int startIndex,
            int processed,
            int total,
            Manifest manifest,
            ExportResult result,
            Action<int, int, ExportItem, Exception> progress) {
            for (var i = startIndex; i < items.Count; i++) {
                var item = items[i];
                var record = new ArtifactExportRecord(item, ArtifactExportStatus.Cancelled);
                record.Message = "Export cancelled by user before this artifact was attempted.";
                record.IsTransient = true;
                record.IsRetryable = true;
                record.CompleteNow();
                string existingKey;
                var relative = MakeRepoRelative(result.OutputRoot, PathHelper.GetOutputFilePath(result.OutputRoot, item));
                var existing = FindManifestEntry(manifest, item, relative, out existingKey);
                UpsertManifestEntry(manifest, item, record, existing, existingKey);
                AddRecord(result, item, record, null);
                processed++;
                progress?.Invoke(processed, total, item, new ExportWarningException(record.Message, item));
            }

            return processed;
        }

        /// <summary>
        /// Marks all remaining artifacts as aborted because the TIA project/session is no longer valid.
        /// </summary>
        /// <param name="items">Ordered artifact list.</param>
        /// <param name="startIndex">First unprocessed index.</param>
        /// <param name="processed">Number of artifacts already processed.</param>
        /// <param name="total">Total artifacts in the run.</param>
        /// <param name="manifest">Manifest to update.</param>
        /// <param name="result">Run result to update.</param>
        /// <param name="progress">Optional progress callback.</param>
        /// <param name="sessionFailureKey">Logical key of the first artifact that detected session loss.</param>
        /// <param name="health">Health state explaining why the run aborted.</param>
        /// <param name="recoveryAttempted">Whether controlled recovery was attempted for the session failure.</param>
        /// <param name="recoverySucceeded">Whether controlled recovery restored a valid project reference.</param>
        /// <param name="recoveryDetail">Diagnostic text describing the recovery attempt.</param>
        /// <returns>Updated processed count.</returns>
        private static int MarkRemainingAborted(
            IList<ExportItem> items,
            int startIndex,
            int processed,
            int total,
            Manifest manifest,
            ExportResult result,
            Action<int, int, ExportItem, Exception> progress,
            string sessionFailureKey,
            TiaSessionHealth health,
            bool recoveryAttempted,
            bool recoverySucceeded,
            string recoveryDetail) {
            for (var i = startIndex; i < items.Count; i++) {
                var item = items[i];
                var record = new ArtifactExportRecord(item, ArtifactExportStatus.AbortedSessionLost);
                record.Message = "Export was not attempted because the TIA session or project was already lost.";
                record.TechnicalDetail = health == null ? string.Empty : health.ToDiagnosticString();
                record.SessionFailureKey = sessionFailureKey;
                record.SessionHealthStatus = health == null ? string.Empty : health.Status.ToString();
                record.SessionRecoveryAttempted = recoveryAttempted;
                record.SessionRecoverySucceeded = recoverySucceeded;
                record.SessionRecoveryDetail = recoveryDetail;
                record.IsTransient = true;
                record.IsRetryable = true;
                record.CompleteNow();
                if (result.FirstSessionFailure == null && i == startIndex) {
                    result.FirstSessionFailure = record;
                }

                string existingKey;
                var relative = MakeRepoRelative(result.OutputRoot, PathHelper.GetOutputFilePath(result.OutputRoot, item));
                var existing = FindManifestEntry(manifest, item, relative, out existingKey);
                UpsertManifestEntry(manifest, item, record, existing, existingKey);
                AddRecord(result, item, record, null);
                processed++;
                progress?.Invoke(processed, total, item, new InvalidOperationException(record.Message));
            }

            return processed;
        }

        /// <summary>
        /// Builds a stable diagnostic string for one controlled session recovery attempt.
        /// </summary>
        /// <param name="before">Health observed before recovery.</param>
        /// <param name="after">Health observed after recovery.</param>
        /// <returns>Single diagnostic text value for reports and manifests.</returns>
        private static string BuildRecoveryDetail(TiaSessionHealth before, TiaSessionHealth after) {
            var initial = before == null ? "Session health before recovery was not captured." : before.ToDiagnosticString();
            var recovered = after == null ? "Session recovery did not return a health result." : after.ToDiagnosticString();
            return "Before recovery: " + initial + Environment.NewLine + "After recovery: " + recovered;
        }

        /// <summary>
        /// Determines whether the previous manifest payload still exists on disk.
        /// </summary>
        /// <param name="repoRoot">Repository root used to resolve relative payload paths.</param>
        /// <param name="entry">Manifest entry to inspect.</param>
        /// <returns><see langword="true"/> when at least one declared payload file exists.</returns>
        private static bool HasExistingPayload(string repoRoot, ManifestEntry entry) {
            if (entry == null) {
                return false;
            }

            var files = entry.Files != null && entry.Files.Count > 0
                ? entry.Files
                : new List<string> { entry.PrimaryPath };
            foreach (var file in files) {
                if (string.IsNullOrWhiteSpace(file)) {
                    continue;
                }

                if (File.Exists(Path.Combine(repoRoot, file.Replace("/", Path.DirectorySeparatorChar.ToString())))) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds the exact reason used for unavailable artifacts.
        /// </summary>
        /// <param name="item">Scanned artifact.</param>
        /// <param name="descriptor">Catalog descriptor.</param>
        /// <returns>Unavailable reason text.</returns>
        private static string BuildUnavailableReason(ExportItem item, ArtifactDescriptor descriptor) {
            if (!string.IsNullOrWhiteSpace(item.UnavailableReason)) {
                return item.UnavailableReason;
            }

            return !string.IsNullOrWhiteSpace(descriptor.Limitations)
                ? descriptor.Limitations
                : "No public Openness export operation is enabled for this artifact.";
        }

        /// <summary>
        /// Identifies which capability check made an artifact unavailable.
        /// </summary>
        /// <param name="item">Scanned artifact.</param>
        /// <param name="descriptor">Catalog descriptor.</param>
        /// <param name="adapter">Adapter descriptor.</param>
        /// <returns>Name of the failed capability condition.</returns>
        private static string BuildUnavailableCondition(ExportItem item, ArtifactDescriptor descriptor, IArtifactAdapter adapter) {
            if (!item.CanExport) {
                return "item.CanExport";
            }

            if (!descriptor.CanExport) {
                return "descriptor.CanExport";
            }

            if (!adapter.CanExport) {
                return "adapter.CanExport";
            }

            return string.Empty;
        }

        /// <summary>
        /// Returns the operation report path for a run root.
        /// </summary>
        /// <param name="runRoot">Export run root.</param>
        /// <returns>Absolute report path.</returns>
        private static string GetOperationReportPath(string runRoot) {
            return Path.Combine(runRoot, "meta", "operation-report.txt");
        }

        /// <summary>
        /// Writes a human-readable operation report from artifact-level results.
        /// </summary>
        /// <param name="result">Completed export result.</param>
        /// <param name="manifest">Manifest written for the same run.</param>
        private static void WriteOperationReport(ExportResult result, Manifest manifest) {
            var path = result.OperationReportPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
            var sb = new StringBuilder();
            sb.AppendLine("Eido TIA Workbench operation report");
            sb.AppendLine("Status: " + result.Status);
            sb.AppendLine("Repository: " + result.OutputRoot);
            sb.AppendLine("Manifest: " + result.ManifestPath);
            sb.AppendLine("Started UTC: " + result.StartedUtc.ToString("o"));
            sb.AppendLine("Completed UTC: " + result.CompletedUtc.ToString("o"));
            sb.AppendLine("Duration: " + (result.CompletedUtc - result.StartedUtc));
            sb.AppendLine();
            sb.AppendLine("Totals");
            sb.AppendLine("Scanned: " + result.Artifacts.Count);
            sb.AppendLine("Exported: " + result.Exported);
            sb.AppendLine("Skipped unchanged: " + result.Skipped);
            sb.AppendLine("Restricted: " + result.Restricted);
            sb.AppendLine("Unavailable: " + result.Unavailable);
            sb.AppendLine("Unsupported: " + result.Unsupported);
            sb.AppendLine("Failed: " + result.Errors.Count);
            sb.AppendLine("Aborted session lost: " + result.AbortedSessionLost);
            sb.AppendLine("Cancelled: " + result.Cancelled);
            sb.AppendLine("Warnings: " + result.Warnings.Count);
            sb.AppendLine("Exported percent: " + FormatPercent(result.Exported, result.Artifacts.Count));
            sb.AppendLine();
            AppendRunDiagnosis(sb, result);
            sb.AppendLine();
            AppendGroupedTotals(sb, "Totals by domain", result.Artifacts.GroupBy(r => r.Domain ?? string.Empty));
            AppendGroupedTotals(sb, "Totals by type", result.Artifacts.GroupBy(r => r.Kind.ToString()));
            AppendGroupedTotals(sb, "Totals by status", result.Artifacts.GroupBy(r => r.Status.ToString()));
            sb.AppendLine();

            if (result.FirstSessionFailure != null) {
                sb.AppendLine("First session failure");
                sb.AppendLine(FormatRecordLine(result.FirstSessionFailure));
                sb.AppendLine(result.FirstSessionFailure.TechnicalDetail);
                sb.AppendLine();
            }

            AppendRecordSection(sb, "Restricted artifacts", result.Artifacts.Where(r => r.Status == ArtifactExportStatus.Restricted));
            AppendRecordSection(sb, "Unavailable artifacts", result.Artifacts.Where(r => r.Status == ArtifactExportStatus.Unavailable));
            AppendRecordSection(sb, "Unsupported artifacts", result.Artifacts.Where(r => r.Status == ArtifactExportStatus.Unsupported));
            AppendRecordSection(sb, "Real errors", result.Artifacts.Where(r => r.Status == ArtifactExportStatus.Failed));
            AppendRecordSection(sb, "Aborted by session loss", result.Artifacts.Where(r => r.Status == ArtifactExportStatus.AbortedSessionLost));
            AppendRecordSection(sb, "Cancelled artifacts", result.Artifacts.Where(r => r.Status == ArtifactExportStatus.Cancelled));
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// Appends a concise explanation of global status, root cause, and session-loss cascade.
        /// </summary>
        /// <param name="sb">Report builder.</param>
        /// <param name="result">Completed export result.</param>
        private static void AppendRunDiagnosis(StringBuilder sb, ExportResult result) {
            sb.AppendLine("Run diagnosis");
            if (result.Status == ExportRunStatus.Completed) {
                sb.AppendLine("  Queue completed without artifact issues.");
                return;
            }

            if (result.Status == ExportRunStatus.CompletedWithIssues) {
                if (ExportOrderPolicy.HasOnlyFinalIsolatedSessionLoss(result.Artifacts) &&
                    result.FirstSessionFailure != null) {
                    var finalCascade = result.Artifacts.Count(r =>
                        r.Status == ArtifactExportStatus.AbortedSessionLost &&
                        !string.Equals(r.LogicalKey, result.FirstSessionFailure.LogicalKey, StringComparison.OrdinalIgnoreCase));
                    sb.AppendLine("  Safe export phase completed before the final isolated artifact phase.");
                    sb.AppendLine("  A final isolated artifact caused TIA session/project loss; completed payloads were retained.");
                    sb.AppendLine("  Root artifact: " + FormatRecordLine(result.FirstSessionFailure));
                    sb.AppendLine("  Final isolated not-attempted artifacts: " + finalCascade);
                    if (result.FirstSessionFailure.SessionRecoveryAttempted) {
                        sb.AppendLine("  Controlled recovery attempted: " + result.FirstSessionFailure.SessionRecoverySucceeded);
                        if (!string.IsNullOrWhiteSpace(result.FirstSessionFailure.SessionRecoveryDetail)) {
                            sb.AppendLine("  Recovery detail: " + result.FirstSessionFailure.SessionRecoveryDetail.Replace(Environment.NewLine, " | "));
                        }
                    }

                    return;
                }

                sb.AppendLine("  Queue completed. Artifact-level issues were isolated and did not abort remaining exports.");
                return;
            }

            if (result.Status == ExportRunStatus.Cancelled) {
                sb.AppendLine("  Queue stopped by user cancellation.");
                return;
            }

            if (result.Status == ExportRunStatus.Failed) {
                sb.AppendLine("  Export failed before a complete artifact-level result set was produced.");
                return;
            }

            var first = result.FirstSessionFailure;
            if (first == null) {
                sb.AppendLine("  Queue aborted by session loss before a root artifact could be identified.");
                return;
            }

            var cascade = result.Artifacts.Count(r =>
                r.Status == ArtifactExportStatus.AbortedSessionLost &&
                !string.Equals(r.LogicalKey, first.LogicalKey, StringComparison.OrdinalIgnoreCase));
            sb.AppendLine("  Queue aborted by confirmed TIA session/project loss.");
            sb.AppendLine("  Root artifact: " + FormatRecordLine(first));
            sb.AppendLine("  Cascaded not-attempted artifacts: " + cascade);
            if (first.SessionRecoveryAttempted) {
                sb.AppendLine("  Controlled recovery attempted: " + first.SessionRecoverySucceeded);
                if (!string.IsNullOrWhiteSpace(first.SessionRecoveryDetail)) {
                    sb.AppendLine("  Recovery detail: " + first.SessionRecoveryDetail.Replace(Environment.NewLine, " | "));
                }
            }
        }

        /// <summary>
        /// Appends grouped count lines to an operation report.
        /// </summary>
        /// <param name="sb">Report builder.</param>
        /// <param name="title">Section title.</param>
        /// <param name="groups">Groups to summarize.</param>
        private static void AppendGroupedTotals(StringBuilder sb, string title, IEnumerable<IGrouping<string, ArtifactExportRecord>> groups) {
            sb.AppendLine(title);
            foreach (var group in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)) {
                sb.AppendLine("  " + (string.IsNullOrWhiteSpace(group.Key) ? "-" : group.Key) + ": " + group.Count());
            }
        }

        /// <summary>
        /// Appends a detailed artifact section to the operation report.
        /// </summary>
        /// <param name="sb">Report builder.</param>
        /// <param name="title">Section title.</param>
        /// <param name="records">Records to include.</param>
        private static void AppendRecordSection(StringBuilder sb, string title, IEnumerable<ArtifactExportRecord> records) {
            var list = records.OrderBy(r => r.Target, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Kind)
                .ThenBy(r => r.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            sb.AppendLine(title + " (" + list.Count + ")");
            foreach (var record in list) {
                sb.AppendLine("  " + FormatRecordLine(record));
                if (!string.IsNullOrWhiteSpace(record.TechnicalDetail)) {
                    sb.AppendLine("    Detail: " + record.TechnicalDetail.Replace(Environment.NewLine, " | "));
                }

                if (!string.IsNullOrWhiteSpace(record.SessionRecoveryDetail)) {
                    sb.AppendLine("    Recovery: " + record.SessionRecoveryDetail.Replace(Environment.NewLine, " | "));
                }
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Formats one artifact result as a compact report line.
        /// </summary>
        /// <param name="record">Record to format.</param>
        /// <returns>Single-line artifact result text.</returns>
        private static string FormatRecordLine(ArtifactExportRecord record) {
            return record.Status + " | " + record.Kind + " | " + record.Target + " | " + record.GroupPath + " | " +
                   record.Name + " | " + record.Message + " | key=" + record.LogicalKey +
                   " | path=" + (record.PrimaryPath ?? string.Empty) +
                   " | exporter=" + (record.ExporterName ?? string.Empty) +
                   " | capability=" + (record.CapabilityDetail ?? string.Empty) +
                   " | health=" + (record.SessionHealthStatus ?? string.Empty) +
                   " | recoveryAttempted=" + record.SessionRecoveryAttempted +
                   " | recoverySucceeded=" + record.SessionRecoverySucceeded +
                   " | retryable=" + record.IsRetryable +
                   " | hresult=" + (record.HResult ?? string.Empty);
        }

        /// <summary>
        /// Formats an exported percentage without throwing on zero-total runs.
        /// </summary>
        /// <param name="value">Successful export count.</param>
        /// <param name="total">Total artifact count.</param>
        /// <returns>Percentage text.</returns>
        private static string FormatPercent(int value, int total) {
            if (total <= 0) {
                return "0.00%";
            }

            return ((double)value * 100D / total).ToString("0.00") + "%";
        }

        /// <summary>
        /// Removes UI-level root group labels from a GroupPath so it matches Openness group roots.
        /// </summary>
        /// <param name="kind">Artifact kind whose implicit Openness root should be considered.</param>
        /// <param name="groupPath">Group path captured from the UI or repository.</param>
        /// <returns>A group path relative to the real Openness root collection.</returns>
        private static string NormalizeRootGroupPath(ExportKind kind, string groupPath) {
            var p = (groupPath ?? string.Empty).Trim().Replace("\\", "/");
            if (p.Length == 0) {
                return string.Empty;
            }

            // Root group names are UI conveniences; Openness roots are implicit.
            // We therefore trim optional root names if present.
            var descriptor = ArtifactCatalog.Get(kind);
            var roots = new[]
            {
                descriptor.Category,
                descriptor.DisplayName,
                "PLC data types",
                "PLC tags",
                "PLC tag tables",
                "Program blocks",
                "Program Blocks",
                "Screens",
                "Templates",
                "Popups",
                "SlideIns",
                "Tags",
                "Scripts",
            };

            foreach (var r in roots) {
                if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase)) {
                    return string.Empty;
                }

                if (p.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase)) {
                    return p.Substring(r.Length + 1);
                }
            }

            return p;
        }

        // -------------------------
        // PLC resolution
        // -------------------------

        /// <summary>
        /// Resolves the PlcSoftware instance from a CPU path string.
        /// Expected cpuPath format: "&lt;DeviceName&gt;/&lt;CpuItemName&gt;".
        ///
        /// IMPORTANT: Device names in TIA can contain '/', so we split at the last '/'.
        /// </summary>
        /// <param name="project">Open TIA project that contains the target PLC device.</param>
        /// <param name="cpuPath">Device and CPU item path stored in the export item.</param>
        /// <returns>The resolved PLC software instance, or <see langword="null"/> when no match is found.</returns>
        private static PlcSoftware ResolvePlcSoftware(Project project, string cpuPath) {
            var p = (cpuPath ?? string.Empty).Trim();
            var lastSlash = p.LastIndexOf('/');
            if (lastSlash <= 0 || lastSlash >= p.Length - 1) {
                return null;
            }

            var deviceName = p.Substring(0, lastSlash);
            var cpuName = p.Substring(lastSlash + 1);

            var device = project.Devices.FirstOrDefault(d =>
                string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
            if (device == null) {
                return null;
            }

            // CPU can be nested anywhere under device items (rack, module, etc.).
            var cpu = FindAllDeviceItems(device).FirstOrDefault(di =>
                string.Equals(di.Name, cpuName, StringComparison.OrdinalIgnoreCase));
            if (cpu == null) {
                return null;
            }

            var swContainer = cpu.GetService<SoftwareContainer>();
            if (swContainer == null) {
                return null;
            }

            return swContainer.Software as PlcSoftware;
        }

        /// <summary>
        /// Iterates all DeviceItems in a device tree using a stack (non-recursive).
        /// </summary>
        /// <param name="device">Device whose nested items should be traversed.</param>
        /// <returns>All device items under the device, including nested module items.</returns>
        private static IEnumerable<DeviceItem> FindAllDeviceItems(Device device) {
            var stack = new Stack<DeviceItem>(device.DeviceItems);
            while (stack.Count > 0) {
                var di = stack.Pop();
                yield return di;

                foreach (var child in di.DeviceItems) {
                    stack.Push(child);
                }
            }
        }

        // -------------------------
        // Types (UDTs) resolution (reflection-based, robust)
        // -------------------------

        /// <summary>
        /// Resolves a UDT (PlcType) within the TypeGroup hierarchy using reflection.
        /// Reflection is used to tolerate differences across Openness versions.
        /// </summary>
        /// <param name="plc">PLC software instance that owns the type group.</param>
        /// <param name="groupPath">Type group path relative to the Openness type root.</param>
        /// <param name="name">UDT name to resolve.</param>
        /// <returns>The resolved PLC type, or <see langword="null"/> when it cannot be found.</returns>
        private static PlcType ResolveUdt(PlcSoftware plc, string groupPath, string name) {
            object root = plc.TypeGroup;
            if (root == null) {
                return null;
            }

            var group = ResolveGroupByPath(root, groupPath);
            if (group == null) {
                return null;
            }

            // Many Openness versions expose types under a "Types" property.
            foreach (var t in GetChildren(group, "Types").OfType<object>()) {
                var tName = GetName(t);
                if (string.Equals(tName, name, StringComparison.OrdinalIgnoreCase)) {
                    return t as PlcType;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a nested group by walking a '/'-delimited path under the given root group.
        /// </summary>
        /// <param name="rootGroup">Root Openness group object used as the traversal starting point.</param>
        /// <param name="groupPath">Slash-delimited relative group path.</param>
        /// <returns>The resolved group object, or <see langword="null"/> when any segment is missing.</returns>
        private static object ResolveGroupByPath(object rootGroup, string groupPath) {
            if (rootGroup == null) {
                return null;
            }

            if (string.IsNullOrWhiteSpace(groupPath)) {
                return rootGroup;
            }

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            object current = rootGroup;
            foreach (var part in parts) {
                // Groups are typically exposed under a "Groups" property.
                var groups = GetChildren(current, "Groups");
                object next = null;

                foreach (var g in groups) {
                    if (string.Equals(GetName(g), part, StringComparison.OrdinalIgnoreCase)) {
                        next = g;
                        break;
                    }
                }

                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Reads an IEnumerable child collection from an object property by name (reflection-based).
        /// Returns an empty enumerable if the property doesn't exist.
        /// </summary>
        /// <param name="obj">Openness object that may expose the requested collection property.</param>
        /// <param name="propertyName">Public property name to read.</param>
        /// <returns>The collection value as <see cref="IEnumerable"/>, or an empty collection when unavailable.</returns>
        private static IEnumerable GetChildren(object obj, string propertyName) {
            if (obj == null) {
                return Array.Empty<object>();
            }

            var p = ReflectionCache.GetProperty(obj.GetType(), propertyName);
            if (p == null) {
                return Array.Empty<object>();
            }

            var val = p.GetValue(obj, null);
            return val as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Returns the "Name" property of an Openness object via reflection.
        /// </summary>
        /// <param name="obj">Openness object that may expose a Name property.</param>
        /// <returns>The object name, or <see langword="null"/> when no readable name is available.</returns>
        private static string GetName(object obj) {
            if (obj == null) {
                return null;
            }

            var p = ReflectionCache.GetProperty(obj.GetType(), "Name");
            return p == null ? null : p.GetValue(obj, null) as string;
        }

        /// <summary>
        /// Reads a public property value from an Openness object.
        /// </summary>
        /// <param name="obj">Object that may expose the property.</param>
        /// <param name="propertyName">Public property name to read.</param>
        /// <returns>The property value, or <see langword="null"/> when unavailable.</returns>
        private static object GetPropertyValue(object obj, string propertyName) {
            if (obj == null || string.IsNullOrWhiteSpace(propertyName)) {
                return null;
            }

            var property = ReflectionCache.GetProperty(obj.GetType(), propertyName);
            return property == null ? null : property.GetValue(obj, null);
        }

        /// <summary>
        /// Resolves an HMI folder hierarchy using a configurable child folder property.
        /// </summary>
        /// <param name="root">Root folder object.</param>
        /// <param name="folderPath">Slash-separated folder path relative to the root.</param>
        /// <param name="folderCollectionProperty">Property that exposes child folders.</param>
        /// <returns>The resolved folder, or <see langword="null"/> when any segment is missing.</returns>
        private static object ResolveFolderByPath(object root, string folderPath, string folderCollectionProperty) {
            if (root == null) {
                return null;
            }

            if (string.IsNullOrWhiteSpace(folderPath)) {
                return root;
            }

            object current = root;
            foreach (var part in folderPath.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                var next = GetChildren(current, folderCollectionProperty)
                    .Cast<object>()
                    .FirstOrDefault(f => string.Equals(GetName(f), part, StringComparison.OrdinalIgnoreCase));
                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Resolves one named artifact from either a direct composition or a named collection property.
        /// </summary>
        /// <param name="owner">Owner object or composition to inspect.</param>
        /// <param name="collectionProperty">Optional collection property on the owner.</param>
        /// <param name="name">Artifact name to match.</param>
        /// <returns>The resolved artifact, or <see langword="null"/> when not found.</returns>
        private static object ResolveNamedItem(object owner, string collectionProperty, string name) {
            if (owner == null) {
                return null;
            }

            var collection = string.IsNullOrWhiteSpace(collectionProperty)
                ? owner
                : GetPropertyValue(owner, collectionProperty);
            if (collection == null) {
                return null;
            }

            var find = collection.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m =>
                    string.Equals(m.Name, "Find", StringComparison.OrdinalIgnoreCase) &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(string));
            if (find != null) {
                object found;
                try {
                    found = find.Invoke(collection, new object[] { name });
                }
                catch (TargetInvocationException ex) {
                    var unwrapped = UnwrapReflectionException(ex, collection.GetType().FullName + ".Find");
                    AddExceptionData(unwrapped, "OpennessCollectionType", collection.GetType().FullName);
                    AddExceptionData(unwrapped, "FindName", name);
                    AddExceptionData(unwrapped, "CollectionProperty", collectionProperty ?? string.Empty);
                    ExceptionDispatchInfo.Capture(unwrapped).Throw();
                    throw;
                }

                if (found != null) {
                    return found;
                }
            }

            foreach (var candidate in collection as IEnumerable ?? Array.Empty<object>()) {
                if (string.Equals(GetName(candidate), name, StringComparison.OrdinalIgnoreCase)) {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Splits a path whose final segment is an owning object name.
        /// </summary>
        /// <param name="ownerPath">Slash-separated path ending with the owner name.</param>
        /// <returns>Tuple containing group path and owner name.</returns>
        private static Tuple<string, string> SplitOwnerPath(string ownerPath) {
            var normalized = (ownerPath ?? string.Empty).Replace("\\", "/").Trim('/');
            var index = normalized.LastIndexOf('/');
            if (index < 0) {
                return Tuple.Create(string.Empty, normalized);
            }

            return Tuple.Create(normalized.Substring(0, index), normalized.Substring(index + 1));
        }

        /// <summary>
        /// Invokes the public Openness Export method on a resolved artifact.
        /// </summary>
        /// <param name="artifact">Resolved Openness artifact.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        /// <param name="kind">Artifact kind being exported.</param>
        /// <param name="logger">Logger that receives reflection diagnostics.</param>
        private static void InvokeExport(object artifact, FileInfo destination, ExportKind kind, ILogger logger) {
            if (artifact == null) {
                throw new InvalidOperationException("Cannot export a null Openness artifact.");
            }

            var methods = artifact.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => string.Equals(m.Name, "Export", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var withOptions = methods.FirstOrDefault(m => {
                var p = m.GetParameters();
                return p.Length == 2 &&
                       p[0].ParameterType == typeof(FileInfo) &&
                       p[1].ParameterType == typeof(Siemens.Engineering.ExportOptions);
            });
            if (withOptions != null) {
                var exportOption = OpennessExportOptionPolicy.ForArtifact(kind);
                LogReflectiveExport(logger, artifact, destination, kind, withOptions, exportOption);
                try {
                    withOptions.Invoke(artifact, new object[] { destination, exportOption });
                    return;
                }
                catch (TargetInvocationException ex) {
                    ThrowExportInvocationException(ex, artifact, destination, kind, withOptions, exportOption);
                    throw;
                }
            }

            var fileOnly = methods.FirstOrDefault(m => {
                var p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType == typeof(FileInfo);
            });
            if (fileOnly != null) {
                LogReflectiveExport(logger, artifact, destination, kind, fileOnly, null);
                try {
                    fileOnly.Invoke(artifact, new object[] { destination });
                    return;
                }
                catch (TargetInvocationException ex) {
                    ThrowExportInvocationException(ex, artifact, destination, kind, fileOnly, null);
                    throw;
                }
            }

            throw new MissingMethodException(artifact.GetType().FullName, "Export");
        }

        /// <summary>
        /// Logs the exact reflected export method and option selected for an artifact.
        /// </summary>
        /// <param name="logger">Logger that receives the diagnostic line.</param>
        /// <param name="artifact">Resolved Openness artifact.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        /// <param name="kind">Artifact kind being exported.</param>
        /// <param name="method">Reflected method selected for export.</param>
        /// <param name="exportOption">Openness export option, or null for file-only overloads.</param>
        private static void LogReflectiveExport(
            ILogger logger,
            object artifact,
            FileInfo destination,
            ExportKind kind,
            MethodInfo method,
            object exportOption) {
            if (logger == null) {
                return;
            }

            logger.Info(
                "Openness reflective export | ExportKind=" + kind +
                " | ArtifactType=" + (artifact == null ? string.Empty : artifact.GetType().FullName) +
                " | ArtifactName=" + GetDiagnosticProperty(artifact, "Name") +
                " | Method=" + FormatMethodSignature(method) +
                " | ExportOption=" + FormatExportOption(exportOption) +
                " | Destination=" + (destination == null ? string.Empty : destination.FullName));
        }

        /// <summary>
        /// Adds non-invasive diagnostic data to an Openness exception before it leaves the reflection boundary.
        /// </summary>
        /// <param name="exception">Exception raised by the reflected Export call.</param>
        /// <param name="artifact">Resolved Openness artifact being exported.</param>
        /// <param name="destination">Temporary export destination.</param>
        /// <param name="kind">Artifact kind being exported.</param>
        /// <param name="method">Reflection method selected for export.</param>
        /// <param name="exportOption">Openness export option passed to the method, when applicable.</param>
        /// <returns>The same exception instance with diagnostic data attached when possible.</returns>
        private static Exception EnrichExportException(
            Exception exception,
            object artifact,
            FileInfo destination,
            ExportKind kind,
            MethodInfo method,
            object exportOption) {
            if (exception == null) {
                return new InvalidOperationException("Openness export failed without an inner exception.");
            }

            AddExceptionData(exception, "ExportKind", kind.ToString());
            AddExceptionData(exception, "OpennessArtifactType", artifact == null ? string.Empty : artifact.GetType().FullName);
            AddExceptionData(exception, "OpennessArtifactName", GetDiagnosticProperty(artifact, "Name"));
            AddExceptionData(exception, "ExportDestination", destination == null ? string.Empty : destination.FullName);
            AddExceptionData(exception, "ExportMethod", method == null ? string.Empty : method.ToString());
            AddExceptionData(exception, "ExportMethodSignature", FormatMethodSignature(method));
            AddExceptionData(exception, "ExportOption", FormatExportOption(exportOption));

            return exception;
        }

        /// <summary>
        /// Unwraps and throws an Openness export reflection failure while preserving the Siemens stack trace.
        /// </summary>
        /// <param name="exception">Reflection wrapper exception from MethodInfo.Invoke.</param>
        /// <param name="artifact">Resolved Openness artifact being exported.</param>
        /// <param name="destination">Temporary export destination.</param>
        /// <param name="kind">Artifact kind being exported.</param>
        /// <param name="method">Reflection method selected for export.</param>
        /// <param name="exportOption">Openness export option passed to the method, when applicable.</param>
        private static void ThrowExportInvocationException(
            TargetInvocationException exception,
            object artifact,
            FileInfo destination,
            ExportKind kind,
            MethodInfo method,
            object exportOption) {
            var unwrapped = UnwrapReflectionException(
                exception,
                artifact == null ? "Export" : artifact.GetType().FullName + ".Export");
            EnrichExportException(unwrapped, artifact, destination, kind, method, exportOption);
            ExceptionDispatchInfo.Capture(unwrapped).Throw();
        }

        /// <summary>
        /// Preserves reflection wrapper details while returning the underlying Openness exception.
        /// </summary>
        /// <param name="exception">Reflection wrapper exception from MethodInfo.Invoke.</param>
        /// <param name="memberName">Reflected member being invoked.</param>
        /// <returns>The inner Openness exception when present; otherwise the wrapper exception.</returns>
        private static Exception UnwrapReflectionException(TargetInvocationException exception, string memberName) {
            var unwrapped = exception.InnerException ?? exception;
            AddExceptionData(unwrapped, "ReflectionMember", memberName);
            AddExceptionData(unwrapped, "ReflectionWrapperType", exception.GetType().FullName);
            AddExceptionData(unwrapped, "ReflectionWrapperMessage", exception.Message);
            AddExceptionData(unwrapped, "ReflectionWrapperToString", exception.ToString());
            if (exception.InnerException != null) {
                AddExceptionData(unwrapped, "ReflectionInnerType", exception.InnerException.GetType().FullName);
            }

            return unwrapped;
        }

        /// <summary>
        /// Formats a reflected method signature with fully qualified parameter types.
        /// </summary>
        /// <param name="method">Method to format.</param>
        /// <returns>A stable signature string for logs and diagnostic data.</returns>
        private static string FormatMethodSignature(MethodInfo method) {
            if (method == null) {
                return string.Empty;
            }

            var parameters = method.GetParameters()
                .Select(p => (p.ParameterType.FullName ?? p.ParameterType.Name) + " " + p.Name)
                .ToArray();
            return (method.ReturnType.FullName ?? method.ReturnType.Name) + " " +
                   method.Name + "(" + string.Join(", ", parameters) + ")";
        }

        /// <summary>
        /// Formats the export option passed to a reflected Openness export overload.
        /// </summary>
        /// <param name="exportOption">Export option value, or null for file-only overloads.</param>
        /// <returns>Human-readable option text.</returns>
        private static string FormatExportOption(object exportOption) {
            if (exportOption == null) {
                return "<file-only>";
            }

            try {
                return exportOption + " (" + Convert.ToInt64(exportOption) + ")";
            }
            catch {
                return exportOption.ToString();
            }
        }

        /// <summary>
        /// Reads a simple public property from a reflected Openness object for diagnostics.
        /// </summary>
        /// <param name="artifact">Openness artifact to inspect.</param>
        /// <param name="propertyName">Public instance property name.</param>
        /// <returns>The property value as text, or an empty value when unavailable.</returns>
        private static string GetDiagnosticProperty(object artifact, string propertyName) {
            if (artifact == null || string.IsNullOrWhiteSpace(propertyName)) {
                return string.Empty;
            }

            try {
                var property = artifact.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || property.GetIndexParameters().Length > 0) {
                    return string.Empty;
                }

                var value = property.GetValue(artifact, null);
                return value == null ? string.Empty : value.ToString();
            }
            catch {
                return string.Empty;
            }
        }

        /// <summary>
        /// Adds a single diagnostic value to an exception data dictionary.
        /// </summary>
        /// <param name="exception">Exception to enrich.</param>
        /// <param name="key">Stable diagnostic key.</param>
        /// <param name="value">Diagnostic value.</param>
        private static void AddExceptionData(Exception exception, string key, object value) {
            if (exception == null || string.IsNullOrWhiteSpace(key)) {
                return;
            }

            try {
                exception.Data[key] = value ?? string.Empty;
            }
            catch {
                // Exception.Data is diagnostic only; never replace the original Openness exception.
            }
        }

        /// <summary>
        /// Builds a stable warning message for artifacts that are visible but not exportable.
        /// </summary>
        /// <param name="item">Concrete artifact that was skipped.</param>
        /// <param name="descriptor">Catalog descriptor for the artifact kind.</param>
        /// <returns>A user-facing warning message.</returns>
        private static string BuildUnavailableMessage(ExportItem item, ArtifactDescriptor descriptor) {
            var reason = !string.IsNullOrWhiteSpace(item.UnavailableReason)
                ? item.UnavailableReason
                : descriptor.Limitations;
            if (string.IsNullOrWhiteSpace(reason)) {
                reason = "The public Openness API does not expose an export operation for this artifact in this pipeline.";
            }

            return descriptor.DisplayName + " is not exportable: " + reason;
        }

        /// <summary>
        /// Determines whether an export exception represents a confirmed TIA restriction.
        /// </summary>
        /// <param name="exception">Exception raised by Openness.</param>
        /// <returns><see langword="true"/> when the message indicates protection or permission denial.</returns>
        private static bool IsRestrictedExport(Exception exception) {
            var text = FlattenExceptionText(exception);
            return text.IndexOf("not permitted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("not allowed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("protected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Builds a restriction message from an Openness exception.
        /// </summary>
        /// <param name="exception">Exception raised by Openness.</param>
        /// <returns>Restriction message used in logs and reports.</returns>
        private static string BuildRestrictedMessage(Exception exception) {
            return "TIA Portal rejected the export because the artifact is protected or restricted. " +
                   FlattenExceptionText(exception);
        }

        /// <summary>
        /// Flattens nested exception messages into one searchable text value.
        /// </summary>
        /// <param name="exception">Root exception to inspect.</param>
        /// <returns>Concatenated exception messages.</returns>
        private static string FlattenExceptionText(Exception exception) {
            var parts = new List<string>();
            var current = exception;
            while (current != null) {
                if (!string.IsNullOrWhiteSpace(current.Message)) {
                    parts.Add(current.Message);
                }

                current = current.InnerException;
            }

            return string.Join(" ", parts.ToArray());
        }

        // -------------------------
        // Blocks resolution
        // -------------------------

        /// <summary>
        /// Resolves a program block (OB/FB/FC/DB) from the block group hierarchy.
        /// </summary>
        /// <param name="plc">PLC software instance that owns the block group.</param>
        /// <param name="groupPath">Block group path relative to the Openness block root.</param>
        /// <param name="name">Block name to resolve.</param>
        /// <returns>The resolved PLC block, or <see langword="null"/> when it cannot be found.</returns>
        private static PlcBlock ResolveBlock(PlcSoftware plc, string groupPath, string name) {
            var group = ResolveBlockGroup(plc.BlockGroup, groupPath);
            if (group == null) {
                return null;
            }

            return group.Blocks.FirstOrDefault(b =>
                string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves a PlcBlockGroup by walking a '/'-delimited group path.
        /// </summary>
        /// <param name="root">Root block group used as the traversal starting point.</param>
        /// <param name="groupPath">Slash-delimited block group path.</param>
        /// <returns>The resolved block group, or <see langword="null"/> when any segment is missing.</returns>
        private static PlcBlockGroup ResolveBlockGroup(PlcBlockGroup root, string groupPath) {
            if (root == null) {
                return null;
            }

            if (string.IsNullOrWhiteSpace(groupPath)) {
                return root;
            }

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            foreach (var part in parts) {
                var next = current.Groups.FirstOrDefault(g =>
                    string.Equals(g.Name, part, StringComparison.OrdinalIgnoreCase));

                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        // -------------------------
        // PLC Tag Tables resolution
        // -------------------------

        /// <summary>
        /// Resolves a PLC Tag Table by name within the tag table group hierarchy.
        /// Returns null when tag tables are not exposed by the installed Openness API.
        /// </summary>
        /// <param name="plc">PLC software instance that may expose tag table groups.</param>
        /// <param name="groupPath">Tag table group path relative to the Openness tag table root.</param>
        /// <param name="name">Tag table name to resolve.</param>
        /// <returns>The resolved tag table, or <see langword="null"/> when it cannot be found.</returns>
        private static PlcTagTable ResolvePlcTagTable(PlcSoftware plc, string groupPath, string name) {
            var root = TryGetTagTableRoot(plc);
            if (root == null) {
                return null;
            }

            var group = ResolveTagTableGroup(root, groupPath);
            if (group == null) {
                return null;
            }

            return group.TagTables.FirstOrDefault(t =>
                string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Attempts to locate the tag table root group in a version-tolerant way
        /// (different Openness versions use different property names).
        /// </summary>
        /// <param name="plc">PLC software instance to inspect for known tag table root properties.</param>
        /// <returns>The tag table root group, or <see langword="null"/> when this API build does not expose it.</returns>
        private static PlcTagTableGroup TryGetTagTableRoot(PlcSoftware plc) {
            // Openness variants expose different property names.
            var prop = plc.GetType().GetProperty("PlcTagTableGroup");
            if (prop != null) {
                var val = prop.GetValue(plc, null) as PlcTagTableGroup;
                if (val != null) {
                    return val;
                }
            }

            prop = plc.GetType().GetProperty("TagTableGroup");
            if (prop != null) {
                var val = prop.GetValue(plc, null) as PlcTagTableGroup;
                if (val != null) {
                    return val;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a PlcTagTableGroup by walking a '/'-delimited group path.
        /// </summary>
        /// <param name="root">Root tag table group used as the traversal starting point.</param>
        /// <param name="groupPath">Slash-delimited tag table group path.</param>
        /// <returns>The resolved tag table group, or <see langword="null"/> when any segment is missing.</returns>
        private static PlcTagTableGroup ResolveTagTableGroup(PlcTagTableGroup root, string groupPath) {
            if (root == null) {
                return null;
            }

            if (string.IsNullOrWhiteSpace(groupPath)) {
                return root;
            }

            var parts = groupPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            foreach (var part in parts) {
                var next = current.Groups.FirstOrDefault(g =>
                    string.Equals(g.Name, part, StringComparison.OrdinalIgnoreCase));

                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        // -------------------------
        // Helpers
        // -------------------------

        /// <summary>
        /// Resolves the classic WinCC HMI target from a target path string.
        /// Expected path format: "&lt;DeviceName&gt;/&lt;DeviceItemName&gt;".
        /// </summary>
        /// <param name="project">Open TIA project that contains the target HMI device.</param>
        /// <param name="targetPath">Device and HMI item path stored in the export item.</param>
        /// <returns>The resolved classic HMI target, or <see langword="null"/> when no match is found.</returns>
        private static HmiTarget ResolveHmiTarget(Project project, string targetPath) {
            var p = (targetPath ?? string.Empty).Trim();
            var lastSlash = p.LastIndexOf('/');
            if (lastSlash <= 0 || lastSlash >= p.Length - 1) {
                return ResolveHmiTargetBySingleName(project, p);
            }

            var deviceName = p.Substring(0, lastSlash);
            var itemName = p.Substring(lastSlash + 1);

            var device = project.Devices.FirstOrDefault(d =>
                string.Equals(d.Name, deviceName, StringComparison.OrdinalIgnoreCase));
            if (device == null) {
                return null;
            }

            var item = FindAllDeviceItems(device).FirstOrDefault(di =>
                string.Equals(di.Name, itemName, StringComparison.OrdinalIgnoreCase));
            if (item == null) {
                return null;
            }

            var container = item.GetService<SoftwareContainer>();
            return container == null ? null : container.Software as HmiTarget;
        }

        /// <summary>
        /// Resolves an HMI target by matching either the device name or HMI target name.
        /// </summary>
        /// <param name="project">Open TIA project to search.</param>
        /// <param name="targetName">Single target name to match against device, item, and HMI target names.</param>
        /// <returns>The resolved classic HMI target, or <see langword="null"/> when no match is found.</returns>
        private static HmiTarget ResolveHmiTargetBySingleName(Project project, string targetName) {
            foreach (var device in project.Devices) {
                foreach (var item in FindAllDeviceItems(device)) {
                    var container = item.GetService<SoftwareContainer>();
                    var target = container == null ? null : container.Software as HmiTarget;
                    if (target == null) {
                        continue;
                    }

                    if (string.Equals(device.Name, targetName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item.Name, targetName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(target.Name, targetName, StringComparison.OrdinalIgnoreCase)) {
                        return target;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a screen folder by walking the classic WinCC screen folder hierarchy.
        /// </summary>
        /// <param name="root">Root screen folder from the HMI target.</param>
        /// <param name="folderPath">Slash-delimited screen folder path.</param>
        /// <returns>The resolved screen folder, or <see langword="null"/> when any segment is missing.</returns>
        private static ScreenFolder ResolveScreenFolder(ScreenFolder root, string folderPath) {
            if (root == null) {
                return null;
            }

            if (string.IsNullOrWhiteSpace(folderPath)) {
                return root;
            }

            var parts = folderPath.Replace("\\", "/")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            foreach (var part in parts) {
                var next = current.Folders.FirstOrDefault(f =>
                    string.Equals(f.Name, part, StringComparison.OrdinalIgnoreCase));
                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Resolves a classic HMI screen inside the selected target and folder.
        /// </summary>
        /// <param name="project">Open TIA project that contains the HMI target.</param>
        /// <param name="item">Export item describing the target, folder, and screen name.</param>
        /// <returns>The resolved screen, or <see langword="null"/> when target, folder, or screen is missing.</returns>
        private static Screen ResolveHmiScreen(Project project, ExportItem item) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                return null;
            }

            var folder = ResolveScreenFolder(target.ScreenFolder, NormalizeRootGroupPath(item.Kind, item.GroupPath));
            if (folder == null) {
                return null;
            }

            return folder.Screens.FirstOrDefault(s =>
                string.Equals(s.Name, item.Name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds selected items whose repository output paths collide after Windows path sanitization.
        /// </summary>
        /// <param name="runRoot">Export run root used to calculate repository output paths.</param>
        /// <param name="items">Selected export items to inspect.</param>
        /// <returns>Logical keys for items that would write to the same output path.</returns>
        private static HashSet<string> FindOutputPathCollisions(string runRoot, IEnumerable<ExportItem> items) {
            var paths = new Dictionary<string, List<ExportItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items) {
                var outputPath = PathHelper.GetOutputFilePath(runRoot, item);
                List<ExportItem> bucket;
                if (!paths.TryGetValue(outputPath, out bucket)) {
                    bucket = new List<ExportItem>();
                    paths[outputPath] = bucket;
                }

                bucket.Add(item);
            }

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in paths.Where(p => p.Value.Count > 1)) {
                foreach (var item in pair.Value) {
                    result.Add(item.LogicalKey);
                }
            }

            return result;
        }

        /// <summary>
        /// Converts an absolute path into a repo-root relative path using forward slashes.
        /// </summary>
        /// <param name="repoRoot">Repository root used as the relative path base.</param>
        /// <param name="fullPath">Absolute path to convert.</param>
        /// <returns>A repository-relative path with forward slashes.</returns>
        private static string MakeRepoRelative(string repoRoot, string fullPath) {
            var root = Path.GetFullPath(repoRoot).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(fullPath);

            if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                return full.Substring(root.Length).Replace("\\", "/");
            }

            // Fallback: normalize separators.
            return full.Replace("\\", "/");
        }

        /// <summary>
        /// Writes a "meta/tia.txt" hint file with the TIA version (or a default).
        /// This helps consumers understand which TIA version produced the export.
        /// </summary>
        /// <param name="repoRoot">Repository root where the meta folder should be written.</param>
        /// <param name="tiaVersion">TIA Portal version text recorded for compatibility checks.</param>
        private static void WriteTiaVersionHint(string repoRoot, string tiaVersion) {
            var metaDir = Path.Combine(repoRoot, "meta");
            Directory.CreateDirectory(metaDir);

            File.WriteAllText(
                Path.Combine(metaDir, "tia.txt"),
                tiaVersion ?? "V21",
                new UTF8Encoding(false));
        }

        /// <summary>
        /// Best-effort file delete (swallows IO errors).
        /// </summary>
        /// <param name="path">File path to delete during temporary export cleanup.</param>
        private static void TryDelete(string path) {
            try {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
            catch { /* ignore */
            }
        }

        /// <summary>
        /// Best-effort directory delete (swallows IO errors).
        /// </summary>
        /// <param name="path">Directory path to delete during temporary export cleanup.</param>
        private static void TryDeleteDirectory(string path) {
            try {
                if (Directory.Exists(path)) {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch { /* ignore */
            }
        }

        /// <summary>
        /// Verifies an Openness artifact exposes a file-based export method before invoking it.
        /// </summary>
        /// <param name="artifact">Resolved Openness artifact.</param>
        /// <param name="capabilityName">Capability name used in diagnostics when the method is missing.</param>
        private static void EnsureExportMethodAvailable(object artifact, string capabilityName) {
            if (artifact == null) {
                throw new InvalidOperationException("Cannot inspect export capability for a null Openness artifact.");
            }

            var hasFileExport = artifact.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Any(m => {
                    if (!string.Equals(m.Name, "Export", StringComparison.OrdinalIgnoreCase)) {
                        return false;
                    }

                    var p = m.GetParameters();
                    return (p.Length == 2 &&
                            p[0].ParameterType == typeof(FileInfo) &&
                            p[1].ParameterType == typeof(Siemens.Engineering.ExportOptions)) ||
                           (p.Length == 1 && p[0].ParameterType == typeof(FileInfo));
                });
            if (!hasFileExport) {
                throw new MissingMethodException(artifact.GetType().FullName, capabilityName);
            }
        }

        // NOTE: legacy collision-avoidance output naming has been removed in favor of a single
        // deterministic naming policy shared with Import (PathHelper.GetOutputFilePath).

        /// <summary>
        /// Best-effort source generation from XML (SCL/ST extraction or markdown summaries).
        /// NOTE: This method is currently not called (sources are disabled on purpose).
        /// </summary>
        /// <param name="xmlPath">XML output path used as the source-extract location anchor.</param>
        /// <param name="item">Export item whose kind controls the source-extract format.</param>
        /// <param name="xml">Exported XML content used for best-effort extraction.</param>
        private void TryWriteSources(string xmlPath, ExportItem item, string xml) {
            try {
                var folder = Path.GetDirectoryName(xmlPath) ?? ".";
                var sourcesDir = Path.Combine(folder, "_sources");
                Directory.CreateDirectory(sourcesDir);

                var baseName = Path.GetFileNameWithoutExtension(xmlPath);

                // Prefer structured text when present (SCL/ST). Otherwise generate a lightweight summary.
                var st = SourceExtract.TryExtractStructuredText(xml);

                if (item.Kind == ExportKind.Db) {
                    // Always write a .db file for DBs.
                    var dbText = st ?? SourceExtract.TryExtractDbText(xml) ?? "// No readable DB source found in the exported XML.\r\n";
                    File.WriteAllText(Path.Combine(sourcesDir, baseName + ".db"), dbText, new UTF8Encoding(false));
                    return;
                }

                if (item.Kind == ExportKind.Ob || item.Kind == ExportKind.Fb || item.Kind == ExportKind.Fc) {
                    // If structured text exists, store it as .scl; otherwise store a summary markdown.
                    if (!string.IsNullOrWhiteSpace(st)) {
                        File.WriteAllText(Path.Combine(sourcesDir, baseName + ".scl"), st, new UTF8Encoding(false));
                        return;
                    }

                    var md = SourceExtract.BuildGraphicalSummaryMarkdown(xml, item);
                    File.WriteAllText(Path.Combine(sourcesDir, baseName + ".md"), md, new UTF8Encoding(false));
                    return;
                }

                // Other artifact kinds: do nothing for now.
            }
            catch (Exception ex) {
                // Source generation is best-effort; never fail the export because of it.
                this._log.Warn("Source generation failed for " + item + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Exports a single item to an XML string by exporting to a temp file first
        /// (Openness export API is file-based).
        /// </summary>
        /// <param name="item">Artifact to export through Openness.</param>
        /// <returns>The XML text produced by the Openness export call.</returns>
        private string ExportSingleToString(ExportItem item) {
            // Isolate temp files per operation to simplify cleanup.
            var tmpDir = Path.Combine(Path.GetTempPath(), "tia_git_export", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            var tmp = Path.Combine(tmpDir, "export" + ArtifactCatalog.Get(item.Kind).Extension);

            try {
                this.ExportSingleToFile(item, tmp);
                if (!File.Exists(tmp)) {
                    throw new ExportWarningException("Openness did not create a repository payload for this artifact.", item);
                }

                // Read as UTF-8 (Openness typically produces UTF-8 XML).
                return File.ReadAllText(tmp, Encoding.UTF8);
            }
            finally {
                // Best-effort cleanup: do not throw if cleanup fails.
                TryDelete(tmp);
                TryDeleteDirectory(tmpDir);
            }
        }

        /// <summary>
        /// Exports a single item to the given file path using the Openness Export() method.
        /// </summary>
        /// <param name="item">Artifact to resolve and export.</param>
        /// <param name="filePath">Temporary file path passed to the Openness file-based export API.</param>
        private void ExportSingleToFile(ExportItem item, string filePath) {
            var project = this._session.GetOpenProjectOnSta();
            var fi = new FileInfo(filePath);
            var descriptor = ArtifactCatalog.Get(item.Kind);
            if (!item.CanExport || !descriptor.CanExport) {
                throw new ExportWarningException(BuildUnavailableMessage(item, descriptor), item);
            }

            // Inventory/UI may include a friendly root label ("Blocks", "PLC data types", "TagTables").
            // In Openness these roots are implicit, so we normalize to a path relative to the real root group.
            var groupPath = NormalizeRootGroupPath(item.Kind, item.GroupPath);

            switch (item.Kind) {
                case ExportKind.Udt:
                    var plcForType = ResolvePlcSoftware(project, item.CpuPath);
                    if (plcForType == null) {
                        throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);
                    }

                    var udt = ResolveUdt(plcForType, groupPath, item.Name);
                    if (udt == null) {
                        throw new InvalidOperationException("UDT not found: " + item);
                    }

                    udt.Export(fi, Siemens.Engineering.ExportOptions.WithDefaults);
                    return;

                case ExportKind.Ob:
                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Db:
                    var plcForBlock = ResolvePlcSoftware(project, item.CpuPath);
                    if (plcForBlock == null) {
                        throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);
                    }

                    var block = ResolveBlock(plcForBlock, groupPath, item.Name);
                    if (block == null) {
                        throw new InvalidOperationException("Block not found: " + item);
                    }

                    block.Export(fi, Siemens.Engineering.ExportOptions.WithDefaults);
                    return;

                case ExportKind.PlcTagTable:
                    var plcForTagTable = ResolvePlcSoftware(project, item.CpuPath);
                    if (plcForTagTable == null) {
                        throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);
                    }

                    var table = ResolvePlcTagTable(plcForTagTable, groupPath, item.Name);
                    if (table == null) {
                        throw new ExportWarningException("PLC tag table not found (or not exposed by this Openness build).", item);
                    }

                    table.Export(fi, Siemens.Engineering.ExportOptions.WithDefaults);
                    return;

                case ExportKind.PlcWatchTable:
                    this.ExportGenericPlcArtifact(project, item, "WatchAndForceTableGroup", "WatchTables", fi);
                    return;

                case ExportKind.PlcForceTable:
                    this.ExportGenericPlcArtifact(project, item, "WatchAndForceTableGroup", "ForceTables", fi);
                    return;

                case ExportKind.PlcUserConstant:
                    this.ExportPlcUserConstant(project, item, fi);
                    return;

                case ExportKind.TechnologicalObject:
                    this.ExportGenericPlcArtifact(project, item, "TechnologicalObjectGroup", "TechnologicalObjects", fi);
                    return;

                case ExportKind.HmiScreen:
                    var screen = ResolveHmiScreen(project, item);
                    if (screen == null) {
                        throw new InvalidOperationException("HMI screen not found: " + item);
                    }

                    // Screen import validates Siemens-maintained dependent geometry values strictly.
                    // Exporting defaults can serialize stale dependent values that TIA then rejects.
                    screen.Export(fi, OpennessExportOptionPolicy.ForArtifact(item.Kind));
                    return;

                case ExportKind.HmiTemplate:
                    this.ExportHmiFolderArtifact(project, item, "ScreenTemplateFolder", "ScreenTemplates", fi);
                    return;

                case ExportKind.HmiPopup:
                    this.ExportHmiFolderArtifact(project, item, "ScreenPopupFolder", "ScreenPopups", fi);
                    return;

                case ExportKind.HmiSlideIn:
                    this.ExportHmiFlatArtifact(project, item, "ScreenSlideinFolder", "ScreenSlideins", fi);
                    return;

                case ExportKind.HmiTagTable:
                    this.ExportHmiFolderArtifact(project, item, "TagFolder", "TagTables", fi);
                    return;

                case ExportKind.HmiConnection:
                    this.ExportHmiCompositionArtifact(project, item, "Connections", fi);
                    return;

                case ExportKind.HmiTextList:
                    this.ExportHmiCompositionArtifact(project, item, "TextLists", fi);
                    return;

                case ExportKind.HmiGraphicList:
                    this.ExportHmiGraphicList(project, item, fi);
                    return;

                case ExportKind.HmiCycle:
                    this.ExportHmiCompositionArtifact(project, item, "Cycles", fi);
                    return;

                case ExportKind.HmiVBScript:
                    this.ExportHmiFolderArtifact(project, item, "VBScriptFolder", "VBScripts", fi);
                    return;

                case ExportKind.HmiGlobalElements:
                    this.ExportHmiSingletonArtifact(project, item, "ScreenGlobalElements", fi);
                    return;

                case ExportKind.HmiScreenOverview:
                    this.ExportHmiSingletonArtifact(project, item, "ScreenOverview", fi);
                    return;

                default:
                    throw new ExportWarningException(BuildUnavailableMessage(item, descriptor), item);
            }
        }

        /// <summary>
        /// Exports a PLC artifact that is located inside a generic group hierarchy.
        /// </summary>
        /// <param name="project">Open project that owns the PLC target.</param>
        /// <param name="item">Artifact identity collected during scan.</param>
        /// <param name="rootProperty">PLC software property that exposes the root group.</param>
        /// <param name="collectionProperty">Group property containing the artifact collection.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportGenericPlcArtifact(Project project, ExportItem item, string rootProperty, string collectionProperty, FileInfo destination) {
            var plc = ResolvePlcSoftware(project, item.CpuPath);
            if (plc == null) {
                throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);
            }

            var root = GetPropertyValue(plc, rootProperty);
            var group = ResolveGroupByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath));
            var artifact = ResolveNamedItem(group, collectionProperty, item.Name);
            if (artifact == null) {
                throw new InvalidOperationException("PLC artifact not found: " + item);
            }

            InvokeExport(artifact, destination, item.Kind, this._log);
        }

        /// <summary>
        /// Exports one PLC user constant from the tag table that owns it.
        /// </summary>
        /// <param name="project">Open project that owns the PLC target.</param>
        /// <param name="item">Constant identity; the group path includes the owning tag table name.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportPlcUserConstant(Project project, ExportItem item, FileInfo destination) {
            var plc = ResolvePlcSoftware(project, item.CpuPath);
            if (plc == null) {
                throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);
            }

            var split = SplitOwnerPath(item.GroupPath);
            var tagRoot = TryGetTagTableRoot(plc);
            var tableGroup = ResolveTagTableGroup(tagRoot, split.Item1);
            var table = tableGroup == null
                ? null
                : tableGroup.TagTables.FirstOrDefault(t => string.Equals(t.Name, split.Item2, StringComparison.OrdinalIgnoreCase));
            var constant = ResolveNamedItem(table, "UserConstants", item.Name);
            if (constant == null) {
                throw new InvalidOperationException("PLC user constant not found: " + item);
            }

            InvokeExport(constant, destination, item.Kind, this._log);
        }

        /// <summary>
        /// Exports a classic HMI artifact stored inside a folder hierarchy.
        /// </summary>
        /// <param name="project">Open project that owns the HMI target.</param>
        /// <param name="item">Artifact identity collected during scan.</param>
        /// <param name="rootProperty">HMI target property that exposes the root folder.</param>
        /// <param name="collectionProperty">Folder property containing the artifact collection.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportHmiFolderArtifact(Project project, ExportItem item, string rootProperty, string collectionProperty, FileInfo destination) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                throw new InvalidOperationException("Could not resolve HMI target for: " + item.CpuPath);
            }

            var root = GetPropertyValue(target, rootProperty);
            var folder = ResolveFolderByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Folders");
            var artifact = ResolveNamedItem(folder, collectionProperty, item.Name);
            if (artifact == null) {
                throw new InvalidOperationException("HMI artifact not found: " + item);
            }

            InvokeExport(artifact, destination, item.Kind, this._log);
        }

        /// <summary>
        /// Exports a classic HMI artifact stored under a flat system folder.
        /// </summary>
        /// <param name="project">Open project that owns the HMI target.</param>
        /// <param name="item">Artifact identity collected during scan.</param>
        /// <param name="rootProperty">HMI target property that exposes the root object.</param>
        /// <param name="collectionProperty">Root property containing the artifact collection.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportHmiFlatArtifact(Project project, ExportItem item, string rootProperty, string collectionProperty, FileInfo destination) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                throw new InvalidOperationException("Could not resolve HMI target for: " + item.CpuPath);
            }

            var root = GetPropertyValue(target, rootProperty);
            var artifact = ResolveNamedItem(root, collectionProperty, item.Name);
            if (artifact == null) {
                throw new InvalidOperationException("HMI artifact not found: " + item);
            }

            InvokeExport(artifact, destination, item.Kind, this._log);
        }

        /// <summary>
        /// Exports a classic HMI artifact stored directly in a target-level composition.
        /// </summary>
        /// <param name="project">Open project that owns the HMI target.</param>
        /// <param name="item">Artifact identity collected during scan.</param>
        /// <param name="compositionProperty">HMI target property exposing the composition.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportHmiCompositionArtifact(Project project, ExportItem item, string compositionProperty, FileInfo destination) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                throw new InvalidOperationException("Could not resolve HMI target for: " + item.CpuPath);
            }

            var composition = GetPropertyValue(target, compositionProperty);
            var artifact = ResolveNamedItem(composition, null, item.Name);
            if (artifact == null) {
                throw new InvalidOperationException("HMI artifact not found: " + item);
            }

            InvokeExport(artifact, destination, item.Kind, this._log);
        }

        /// <summary>
        /// Exports a classic HMI graphic list through an explicit capability-aware handler.
        /// </summary>
        /// <param name="project">Open project that owns the HMI target.</param>
        /// <param name="item">Graphic list identity collected during scan.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportHmiGraphicList(Project project, ExportItem item, FileInfo destination) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                throw new InvalidOperationException("Could not resolve HMI target for: " + item.CpuPath);
            }

            var composition = GetPropertyValue(target, "GraphicLists");
            if (composition == null) {
                throw new ExportWarningException("HMI graphic list composition is not exposed by this Openness build.", item);
            }

            var artifact = ResolveNamedItem(composition, null, item.Name);
            if (artifact == null) {
                throw new InvalidOperationException("HMI graphic list not found: " + item);
            }

            EnsureExportMethodAvailable(artifact, "GraphicList.Export");
            InvokeExport(artifact, destination, item.Kind, this._log);
        }

        /// <summary>
        /// Exports a classic HMI singleton artifact exposed directly by the target.
        /// </summary>
        /// <param name="project">Open project that owns the HMI target.</param>
        /// <param name="item">Artifact identity collected during scan.</param>
        /// <param name="singletonProperty">HMI target property exposing the singleton.</param>
        /// <param name="destination">Destination file passed to Openness.</param>
        private void ExportHmiSingletonArtifact(Project project, ExportItem item, string singletonProperty, FileInfo destination) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                throw new InvalidOperationException("Could not resolve HMI target for: " + item.CpuPath);
            }

            var artifact = GetPropertyValue(target, singletonProperty);
            if (artifact == null) {
                throw new InvalidOperationException("HMI singleton artifact not found: " + item);
            }

            InvokeExport(artifact, destination, item.Kind, this._log);
        }
    }
}
