// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

using Siemens.Engineering;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Tags;

using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Core.Import {
    /// <summary>
    /// Imports XML artifacts from the structured export folder back into the open TIA project.
    /// </summary>
    /// <remarks>
    /// This is a pragmatic synchronization importer, not a full project reconstruction tool. It assumes the
    /// target PLC devices already exist in the project and must run on the same STA thread as TIA Portal.
    /// </remarks>
    public sealed class ImportService {
        private readonly TiaSessionService _session;
        private readonly ILogger _log;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportService"/> class.
        /// </summary>
        /// <param name="session">TIA Portal session service used to access the open project.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> is <see langword="null"/>.
        /// </exception>
        public ImportService(TiaSessionService session)
            : this(session, NullLogger.Instance) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImportService"/> class.
        /// </summary>
        /// <param name="session">TIA Portal session service used to access the open project.</param>
        /// <param name="logger">Logger that receives import failure details.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="session"/> is <see langword="null"/>.
        /// </exception>
        public ImportService(TiaSessionService session, ILogger logger) {
            this._session = session ?? throw new ArgumentNullException(nameof(session));
            this._log = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Imports the selected items from <paramref name="repoRoot"/> into the current project.
        /// </summary>
        /// <remarks>
        /// Import order:
        /// 1) UDTs
        /// 2) Blocks (OB/FB/FC/DB)
        /// 3) PLC tag tables
        ///
        /// This ordering reduces dependency issues (blocks may reference UDTs).
        /// </remarks>
        /// <param name="repoRoot">Root folder of the structured export repository.</param>
        /// <param name="selection">Artifacts selected for import.</param>
        /// <returns>A result object containing counters, warnings, and item-level errors.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="repoRoot"/> is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="selection"/> is <see langword="null"/>.</exception>
        public ImportResult Import(string repoRoot, IReadOnlyCollection<ExportItem> selection) {
            if (string.IsNullOrWhiteSpace(repoRoot)) {
                throw new ArgumentException("Repo root is required.", nameof(repoRoot));
            }

            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            var project = this._session.GetOpenProjectOnSta();
            var result = new ImportResult();
            var backupRoot = Path.Combine(
                repoRoot,
                "meta",
                "import-backups",
                DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            // Deterministic import order, matching export ordering policy.
            var ordered = selection
                .OrderBy(i => OrderKey(i.Kind))
                .ThenBy(i => i.CpuPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.GroupPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in ordered) {
                try {
                    // Resolve where Export wrote this artifact.
                    var xmlPath = PathHelper.GetOutputFilePath(repoRoot, item);
                    if (!File.Exists(xmlPath)) {
                        result.Errors.Add(new ImportError(item, new FileNotFoundException("Export XML not found", xmlPath)));
                        continue;
                    }

                    if (!item.CanImport || !ArtifactCatalog.Get(item.Kind).CanImport) {
                        result.Skipped++;
                        result.Warnings.Add("Import is not available for " + item + ": " + ArtifactCatalog.Get(item.Kind).Limitations);
                        continue;
                    }

                    if (item.IsHmi) {
                        var artifact = new RepositoryArtifact(
                            item,
                            xmlPath,
                            xmlPath,
                            RepositoryArtifactState.New,
                            null,
                            null,
                            null);
                        ImportHmiArtifact(project, artifact, ImportConflictPolicy.Ask, createBackup: true, backupRoot: backupRoot, result: result);
                    } else {
                        // Resolve the target PLC software instance.
                        var plc = ResolvePlcSoftware(project, item.CpuPath);
                        if (plc == null) {
                            throw new InvalidOperationException("Could not resolve PLC software for: " + item.CpuPath);
                        }

                        // Dispatch by kind and import via Openness.
                        ImportSingle(plc, item, xmlPath);
                        result.Imported++;
                    }
                } catch (Exception ex) {
                    // Import is best-effort across multiple items; record and continue.
                    result.Errors.Add(new ImportError(item, ex));
                    this._log.Error("Import failed for " + item, ex);
                }
            }

            return result;
        }

        /// <summary>
        /// Imports concrete repository files discovered by RepositoryInventoryService.
        /// This keeps the import workflow based on repository inventory instead of a prior TIA scan.
        /// </summary>
        /// <param name="selection">Repository artifacts selected for import.</param>
        /// <returns>A result object containing counters, warnings, and item-level errors.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <see langword="null"/>.
        /// </exception>
        public ImportResult ImportFiles(IReadOnlyCollection<RepositoryArtifact> selection) {
            return this.ImportFiles(selection, ImportConflictPolicy.Ask, createBackup: true, backupRoot: null);
        }

        /// <summary>
        /// Imports concrete repository files with an explicit conflict policy and optional backup folder.
        /// </summary>
        /// <param name="selection">Repository artifacts selected for import.</param>
        /// <param name="conflictPolicy">Policy applied when a target HMI screen already exists.</param>
        /// <param name="createBackup">Whether existing HMI screens should be exported before replacement.</param>
        /// <param name="backupRoot">Folder where HMI replacement backups should be written.</param>
        /// <returns>A result object containing counters, warnings, item-level errors, and backup location.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <see langword="null"/>.
        /// </exception>
        public ImportResult ImportFiles(
            IReadOnlyCollection<RepositoryArtifact> selection,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot) {
            return this.ImportFiles(
                selection,
                conflictPolicy,
                createBackup,
                backupRoot,
                CancellationToken.None,
                null);
        }

        /// <summary>
        /// Imports concrete repository files with progress reporting and cooperative cancellation.
        /// </summary>
        /// <param name="selection">Repository artifacts selected for import.</param>
        /// <param name="conflictPolicy">Policy applied when a target object already exists.</param>
        /// <param name="createBackup">Whether existing HMI artifacts should be exported before replacement.</param>
        /// <param name="backupRoot">Folder where replacement backups should be written.</param>
        /// <param name="cancellationToken">Token checked between artifacts.</param>
        /// <param name="progress">Optional progress callback invoked before and after each artifact.</param>
        /// <returns>A result object containing counters, warnings, item-level errors, and backup location.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <see langword="null"/>.
        /// </exception>
        public ImportResult ImportFiles(
            IReadOnlyCollection<RepositoryArtifact> selection,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            CancellationToken cancellationToken,
            Action<ImportProgressInfo> progress) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            var project = this._session.GetOpenProjectOnSta();
            var result = new ImportResult {
                BackupRoot = backupRoot,
            };

            var ordered = new ImportDependencyPlanner().Build(selection).OrderedArtifacts.ToList();
            var total = ordered.Count;
            var done = 0;
            this._log.Info("Import started. Artifacts: " + total + ". ConflictPolicy: " + conflictPolicy + ". Backup: " + createBackup + ".");

            foreach (var artifact in ordered) {
                if (cancellationToken.IsCancellationRequested) {
                    result.Cancelled = total - done;
                    result.Warnings.Add("Import cancelled before remaining artifacts were attempted: " + result.Cancelled + ".");
                    this._log.Warn("Import cancelled. Completed: " + done + ". Remaining: " + result.Cancelled + ".");
                    break;
                }

                var strategy = BuildImportStrategy(artifact, conflictPolicy, createBackup);
                var stopwatch = Stopwatch.StartNew();
                progress?.Invoke(new ImportProgressInfo {
                    Done = done,
                    Total = total,
                    Artifact = artifact,
                    Phase = "Importing " + artifact.Item.Kind,
                    Strategy = strategy,
                });
                this._log.Info(BuildImportLogLine("Started", artifact, strategy, 0));

                try {
                    if (artifact.State == RepositoryArtifactState.InvalidXml ||
                        artifact.State == RepositoryArtifactState.Unsupported) {
                        throw new InvalidOperationException(
                            "Artifact cannot be imported because its repository state is " + artifact.State + ".");
                    }

                    if (string.IsNullOrWhiteSpace(artifact.RepositoryPath) || !File.Exists(artifact.RepositoryPath)) {
                        throw new FileNotFoundException("Repository XML not found", artifact.RepositoryPath);
                    }

                    if (!artifact.Item.CanImport || !ArtifactCatalog.Get(artifact.Item.Kind).CanImport) {
                        result.Skipped++;
                        result.Warnings.Add("Import is not available for " + artifact.Item + ": " + ArtifactCatalog.Get(artifact.Item.Kind).Limitations);
                        stopwatch.Stop();
                        done++;
                        this._log.Warn(BuildImportLogLine("Skipped", artifact, strategy, stopwatch.ElapsedMilliseconds));
                        progress?.Invoke(new ImportProgressInfo {
                            Done = done,
                            Total = total,
                            Artifact = artifact,
                            Phase = "Skipped " + artifact.Item.Kind,
                            Strategy = strategy,
                            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        });
                        continue;
                    }

                    if (artifact.Item.IsHmi) {
                        ImportHmiArtifact(project, artifact, conflictPolicy, createBackup, backupRoot, result);
                    } else {
                        var plc = ResolvePlcSoftware(project, artifact.Item.CpuPath);
                        if (plc == null) {
                            throw new InvalidOperationException("Could not resolve PLC software for: " + artifact.Item.CpuPath);
                        }

                        ImportSingle(plc, artifact.Item, artifact.RepositoryPath);
                        result.Imported++;
                    }

                    stopwatch.Stop();
                    done++;
                    this._log.Info(BuildImportLogLine("Completed", artifact, strategy, stopwatch.ElapsedMilliseconds));
                    progress?.Invoke(new ImportProgressInfo {
                        Done = done,
                        Total = total,
                        Artifact = artifact,
                        Phase = "Imported " + artifact.Item.Kind,
                        Strategy = strategy,
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    });
                } catch (Exception ex) {
                    stopwatch.Stop();
                    done++;
                    AddImportExceptionContext(ex, artifact, strategy, conflictPolicy, createBackup);
                    string abortWarning;
                    var abortAfterFailure = this.ShouldAbortImportAfterFailure(ex, result, total, done, out abortWarning);
                    result.Errors.Add(new ImportError(artifact.Item, ex));
                    this._log.Error(BuildImportLogLine("Failed", artifact, strategy, stopwatch.ElapsedMilliseconds), ex);
                    if (!string.IsNullOrWhiteSpace(abortWarning)) {
                        this._log.Warn(abortWarning);
                    }

                    progress?.Invoke(new ImportProgressInfo {
                        Done = done,
                        Total = total,
                        Artifact = artifact,
                        Phase = "Import failed: " + artifact.Item.Kind,
                        Strategy = strategy,
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    });
                    if (abortAfterFailure) {
                        break;
                    }
                }
            }

            this._log.Info(
                "Import finished. Imported: " + result.Imported +
                ". Skipped: " + result.Skipped +
                ". Cancelled: " + result.Cancelled +
                ". Warnings: " + result.Warnings.Count +
                ". Errors: " + result.Errors.Count + ".");
            return result;
        }

        /// <summary>
        /// Previews concrete repository files against the open project without changing TIA Portal.
        /// </summary>
        /// <param name="selection">Repository artifacts selected for import.</param>
        /// <param name="conflictPolicy">Policy that would be applied during import.</param>
        /// <returns>A result object whose messages describe create, replace, skip, and conflict actions.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="selection"/> is <see langword="null"/>.
        /// </exception>
        public ImportResult PreviewFiles(IReadOnlyCollection<RepositoryArtifact> selection, ImportConflictPolicy conflictPolicy) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            var project = this._session.GetOpenProjectOnSta();
            var result = new ImportResult();

            var ordered = new ImportDependencyPlanner().Build(selection).OrderedArtifacts;
            foreach (var artifact in ordered) {
                try {
                    if (artifact.State == RepositoryArtifactState.InvalidXml ||
                        artifact.State == RepositoryArtifactState.Unsupported) {
                        result.Errors.Add(new ImportError(
                            artifact.Item,
                            new InvalidOperationException("Artifact cannot be imported because its repository state is " + artifact.State + ".")));
                        continue;
                    }

                    if (!artifact.Item.CanImport || !ArtifactCatalog.Get(artifact.Item.Kind).CanImport) {
                        result.Skipped++;
                        result.Warnings.Add("Import unsupported: " + artifact.Item + ". " + ArtifactCatalog.Get(artifact.Item.Kind).Limitations);
                        continue;
                    }

                    if (artifact.Item.IsHmi) {
                        PreviewHmiArtifact(project, artifact, conflictPolicy, result);
                    } else {
                        result.Imported++;
                        result.Warnings.Add("PLC artifact will be imported with the existing PLC importer: " + artifact.Item);
                    }
                } catch (Exception ex) {
                    result.Errors.Add(new ImportError(artifact.Item, ex));
                }
            }

            return result;
        }

        /// <summary>
        /// Provides deterministic ordering to reduce dependency-related import failures.
        /// </summary>
        /// <param name="kind">Artifact kind being ordered for import.</param>
        /// <returns>An integer sort bucket; lower values are imported first.</returns>
        private static int OrderKey(ExportKind kind) {
            return ArtifactAdapterRegistry.Get(kind).ImportOrder;
        }

        /// <summary>
        /// Builds a stable log line with artifact import context.
        /// </summary>
        /// <param name="phase">Import phase being logged.</param>
        /// <param name="artifact">Repository artifact being imported.</param>
        /// <param name="strategy">Import strategy selected for the artifact.</param>
        /// <param name="elapsedMilliseconds">Elapsed milliseconds for the artifact when known.</param>
        /// <returns>A structured human-readable import log line.</returns>
        private static string BuildImportLogLine(
            string phase,
            RepositoryArtifact artifact,
            string strategy,
            long elapsedMilliseconds) {
            var item = artifact.Item;
            return "Import " + phase +
                   " | ArtifactType=" + item.Kind +
                   " | ArtifactPath=" + artifact.RepositoryPath +
                   " | TargetComposition=" + item.CpuPath +
                   " | TargetGroup=" + item.GroupPath +
                   " | TargetGroupNormalized=" + NormalizeRootGroupPath(item.Kind, item.GroupPath) +
                   " | Strategy=" + strategy +
                   " | ElapsedMs=" + elapsedMilliseconds;
        }

        /// <summary>
        /// Describes how an artifact will be imported for progress and diagnostic logs.
        /// </summary>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="conflictPolicy">Conflict policy selected by the user.</param>
        /// <param name="createBackup">Whether replacement backups are enabled.</param>
        /// <returns>Readable import strategy.</returns>
        private static string BuildImportStrategy(
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            bool createBackup) {
            var descriptor = ArtifactCatalog.Get(artifact.Item.Kind);
            var replace = conflictPolicy == ImportConflictPolicy.Replace || conflictPolicy == ImportConflictPolicy.Ask;
            return descriptor.Domain +
                   " " + descriptor.ApiUsed +
                   " | ConflictPolicy=" + conflictPolicy +
                   " | Replace=" + replace +
                   " | ImportOptionsForCreate=None" +
                   " | ImportOptionsForReplace=" + (replace ? "Override" : "NotUsed") +
                   " | Backup=" + createBackup;
        }

        /// <summary>
        /// Adds artifact and strategy details to an exception before it is logged or returned in the result.
        /// </summary>
        /// <param name="exception">Exception to enrich.</param>
        /// <param name="artifact">Repository artifact being imported.</param>
        /// <param name="strategy">Import strategy selected for the artifact.</param>
        /// <param name="conflictPolicy">Conflict policy selected by the user.</param>
        /// <param name="createBackup">Whether replacement backups were enabled.</param>
        private static void AddImportExceptionContext(
            Exception exception,
            RepositoryArtifact artifact,
            string strategy,
            ImportConflictPolicy conflictPolicy,
            bool createBackup) {
            if (exception == null || artifact == null) {
                return;
            }

            var item = artifact.Item;
            AddExceptionData(exception, "ArtifactType", item.Kind);
            AddExceptionData(exception, "ArtifactPath", artifact.RepositoryPath);
            AddExceptionData(exception, "TargetComposition", item.CpuPath);
            AddExceptionData(exception, "TargetGroup", item.GroupPath);
            AddExceptionData(exception, "TargetGroupNormalized", NormalizeRootGroupPath(item.Kind, item.GroupPath));
            AddExceptionData(exception, "ImportStrategy", strategy);
            AddExceptionData(exception, "ConflictPolicy", conflictPolicy);
            AddExceptionData(exception, "CreateBackup", createBackup);
            AddExceptionData(exception, "ImportOptions", conflictPolicy == ImportConflictPolicy.Skip
                ? "Create=None; Replace=NotUsed"
                : "Create=None; Replace=Override");
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
            } catch (Exception) {
                // Exception.Data is best-effort diagnostic metadata.
            }
        }

        /// <summary>
        /// Adds several diagnostic values to an exception data dictionary.
        /// </summary>
        /// <param name="exception">Exception to enrich.</param>
        /// <param name="data">Diagnostic key/value pairs.</param>
        private static void AddExceptionData(Exception exception, params Tuple<string, object>[] data) {
            if (data == null) {
                return;
            }

            foreach (var entry in data) {
                if (entry != null) {
                    AddExceptionData(exception, entry.Item1, entry.Item2);
                }
            }
        }

        /// <summary>
        /// Creates a diagnostic key/value pair for reflection exception context.
        /// </summary>
        /// <param name="key">Stable diagnostic key.</param>
        /// <param name="value">Diagnostic value.</param>
        /// <returns>A tuple suitable for <see cref="AddExceptionData(Exception, Tuple{string, object}[])"/>.</returns>
        private static Tuple<string, object> DiagnosticData(string key, object value) {
            return Tuple.Create(key, value);
        }

        /// <summary>
        /// Preserves a TargetInvocationException wrapper as data while returning the real Siemens exception.
        /// </summary>
        /// <param name="memberName">Openness member being invoked through reflection.</param>
        /// <param name="exception">Reflection wrapper exception thrown by MethodInfo.Invoke.</param>
        /// <param name="context">Additional call-site diagnostics.</param>
        /// <returns>The inner Siemens exception when available; otherwise the original wrapper.</returns>
        private static Exception UnwrapInvocationException(
            string memberName,
            TargetInvocationException exception,
            params Tuple<string, object>[] context) {
            var unwrapped = exception.InnerException ?? exception;
            AddExceptionData(unwrapped, "ReflectionMember", memberName);
            AddExceptionData(unwrapped, "ReflectionWrapperType", exception.GetType().FullName);
            AddExceptionData(unwrapped, "ReflectionWrapperMessage", exception.Message);
            AddExceptionData(unwrapped, "ReflectionWrapperToString", exception.ToString());
            if (exception.InnerException != null) {
                AddExceptionData(unwrapped, "ReflectionInnerType", exception.InnerException.GetType().FullName);
            }

            AddExceptionData(unwrapped, context);
            return unwrapped;
        }

        /// <summary>
        /// Throws the real Siemens exception from a reflection call without losing its original stack trace.
        /// </summary>
        /// <param name="memberName">Openness member being invoked through reflection.</param>
        /// <param name="exception">Reflection wrapper exception thrown by MethodInfo.Invoke.</param>
        /// <param name="context">Additional call-site diagnostics.</param>
        private static void ThrowInvocationException(
            string memberName,
            TargetInvocationException exception,
            params Tuple<string, object>[] context) {
            var unwrapped = UnwrapInvocationException(memberName, exception, context);
            ExceptionDispatchInfo.Capture(unwrapped).Throw();
        }

        /// <summary>
        /// Builds a readable method signature for reflection diagnostics.
        /// </summary>
        /// <param name="method">Method being invoked.</param>
        /// <returns>A stable method signature.</returns>
        private static string FormatMethodSignature(MethodBase method) {
            if (method == null) {
                return string.Empty;
            }

            var owner = method.DeclaringType == null ? string.Empty : method.DeclaringType.FullName + ".";
            var parameters = string.Join(", ", method.GetParameters()
                .Select(p => p.ParameterType.FullName + " " + p.Name));
            return owner + method.Name + "(" + parameters + ")";
        }

        /// <summary>
        /// Formats an enum import option with its numeric value.
        /// </summary>
        /// <param name="option">Import option instance.</param>
        /// <returns>A readable enum value.</returns>
        private static string FormatEnumOption(object option) {
            if (option == null) {
                return string.Empty;
            }

            try {
                return option + " (" + Convert.ToInt64(option) + ")";
            } catch (Exception) {
                return Convert.ToString(option);
            }
        }

        /// <summary>
        /// Checks whether an Openness failure invalidated the current TIA session.
        /// </summary>
        /// <param name="exception">Exception from the failed artifact import.</param>
        /// <param name="result">Mutable import result receiving warnings and cancellation count.</param>
        /// <param name="total">Total artifact count in the import run.</param>
        /// <param name="done">Artifacts already attempted, including the failed artifact.</param>
        /// <param name="warning">Warning to log when the run must stop.</param>
        /// <returns>True when the remaining artifacts must not be attempted with stale Openness references.</returns>
        private bool ShouldAbortImportAfterFailure(
            Exception exception,
            ImportResult result,
            int total,
            int done,
            out string warning) {
            warning = null;
            TiaSessionHealth health;
            try {
                health = this._session.CheckHealthOnSta();
            } catch (Exception healthException) {
                AddExceptionData(exception, "SessionHealthCheckException", healthException.ToString());
                var remainingAfterHealthFailure = Math.Max(0, total - done);
                result.Cancelled += remainingAfterHealthFailure;
                warning = "Import aborted after artifact failure because TIA session health could not be checked. " +
                          "Remaining artifacts not attempted: " + remainingAfterHealthFailure + ".";
                result.Warnings.Add(warning);
                return true;
            }

            AddExceptionData(exception, "SessionHealthAfterFailure", health.ToDiagnosticString());
            if (health.IsHealthy) {
                return false;
            }

            var remaining = Math.Max(0, total - done);
            result.Cancelled += remaining;
            warning = "Import aborted after artifact failure because the TIA session is no longer healthy. " +
                      "Health=" + health.ToDiagnosticString() +
                      ". Remaining artifacts not attempted: " + remaining + ".";
            result.Warnings.Add(warning);
            return true;
        }

        /// <summary>
        /// Imports a single item into the provided PLC software instance.
        /// </summary>
        /// <param name="plc">PLC software instance that receives the imported artifact.</param>
        /// <param name="item">Artifact metadata used to select the correct import collection.</param>
        /// <param name="xmlPath">Repository XML file to import.</param>
        private static void ImportSingle(PlcSoftware plc, ExportItem item, string xmlPath) {
            var fi = new FileInfo(xmlPath);

            switch (item.Kind) {
                case ExportKind.Udt:
                    ImportUdt(plc, item, fi);
                    return;

                case ExportKind.Ob:
                case ExportKind.Fb:
                case ExportKind.Fc:
                case ExportKind.Db:
                    ImportBlock(plc, item, fi);
                    return;

                case ExportKind.PlcTagTable:
                    ImportTagTable(plc, item, fi);
                    return;

                case ExportKind.PlcWatchTable:
                    ImportGenericPlcArtifact(plc, item, "WatchAndForceTableGroup", "WatchTables", fi);
                    return;

                case ExportKind.PlcForceTable:
                    ImportGenericPlcArtifact(plc, item, "WatchAndForceTableGroup", "ForceTables", fi);
                    return;

                case ExportKind.PlcUserConstant:
                    ImportPlcUserConstant(plc, item, fi);
                    return;

                case ExportKind.TechnologicalObject:
                    ImportGenericPlcArtifact(plc, item, "TechnologicalObjectGroup", "TechnologicalObjects", fi);
                    return;

                case ExportKind.HmiScreen:
                    throw new InvalidOperationException("HMI screens require project-level import resolution.");

                default:
                    throw new InvalidOperationException("No PLC import handler is registered for " + item.Kind + ".");
            }
        }

        /// <summary>
        /// Imports a classic WinCC HMI screen and applies the selected conflict policy.
        /// </summary>
        /// <param name="project">Open TIA project containing the target HMI device.</param>
        /// <param name="artifact">Repository artifact whose XML file is imported.</param>
        /// <param name="conflictPolicy">Policy applied when a screen with the same name already exists.</param>
        /// <param name="createBackup">Whether to export the existing screen before replacement.</param>
        /// <param name="backupRoot">Backup root where existing screens are exported before replacement.</param>
        /// <param name="result">Mutable import result updated with counters and backup location.</param>
        private static void ImportHmiScreen(
            Project project,
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveRequiredHmiTarget(project, item);
            var folder = ResolveOrCreateScreenFolder(target.ScreenFolder, NormalizeRootGroupPath(item.Kind, item.GroupPath));
            ImportIntoOwnerCollection(folder, "Screens", artifact, conflictPolicy, createBackup, backupRoot, result);
        }

        /// <summary>
        /// Adds a dry-run description for a classic WinCC HMI screen.
        /// </summary>
        /// <param name="project">Open TIA project used for non-destructive target resolution.</param>
        /// <param name="artifact">Repository artifact being previewed.</param>
        /// <param name="conflictPolicy">Conflict policy used to classify the preview action.</param>
        /// <param name="result">Mutable import result receiving preview counters and messages.</param>
        private static void PreviewHmiScreen(
            Project project,
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                result.Errors.Add(new ImportError(item, new InvalidOperationException("HMI target not found: " + item.CpuPath)));
                return;
            }

            var folder = ResolveScreenFolder(target.ScreenFolder, NormalizeRootGroupPath(item.Kind, item.GroupPath));
            var existing = folder == null ? null : ResolveNamedItem(folder, "Screens", item.Name);
            RecordPreview(item, existing, conflictPolicy, "HMI screen", result);
        }

        /// <summary>
        /// Dispatches import for a classic HMI artifact using the central artifact kind.
        /// </summary>
        /// <param name="project">Open TIA project containing the destination HMI target.</param>
        /// <param name="artifact">Repository artifact whose payload is imported.</param>
        /// <param name="conflictPolicy">Policy applied when an object already exists.</param>
        /// <param name="createBackup">Whether existing objects are exported before replacement.</param>
        /// <param name="backupRoot">Backup root used when replacement backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportHmiArtifact(
            Project project,
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            switch (artifact.Item.Kind) {
                case ExportKind.HmiScreen:
                    ImportHmiScreen(project, artifact, conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiTemplate:
                    ImportHmiFolderArtifact(project, artifact, "ScreenTemplateFolder", "ScreenTemplates", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiPopup:
                    ImportHmiFolderArtifact(project, artifact, "ScreenPopupFolder", "ScreenPopups", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiSlideIn:
                    ImportHmiFlatArtifact(project, artifact, "ScreenSlideinFolder", "ScreenSlideins", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiTagTable:
                    ImportHmiFolderArtifact(project, artifact, "TagFolder", "TagTables", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiConnection:
                    ImportHmiCompositionArtifact(project, artifact, "Connections", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiTextList:
                    ImportHmiCompositionArtifact(project, artifact, "TextLists", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiGraphicList:
                    ImportHmiCompositionArtifact(project, artifact, "GraphicLists", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiCycle:
                    ImportHmiCompositionArtifact(project, artifact, "Cycles", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiVBScript:
                    ImportHmiFolderArtifact(project, artifact, "VBScriptFolder", "VBScripts", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiGlobalElements:
                    ImportHmiSingletonArtifact(project, artifact, "ScreenGlobalElements", "ImportScreenGlobalElements", conflictPolicy, createBackup, backupRoot, result);
                    return;

                case ExportKind.HmiScreenOverview:
                    ImportHmiSingletonArtifact(project, artifact, "ScreenOverview", "ImportScreenOverview", conflictPolicy, createBackup, backupRoot, result);
                    return;

                default:
                    throw new InvalidOperationException("No HMI import handler is registered for " + artifact.Item.Kind + ".");
            }
        }

        /// <summary>
        /// Adds a dry-run description for a classic HMI artifact.
        /// </summary>
        /// <param name="project">Open TIA project used for non-destructive resolution.</param>
        /// <param name="artifact">Repository artifact being previewed.</param>
        /// <param name="conflictPolicy">Policy applied when an object already exists.</param>
        /// <param name="result">Mutable import result receiving preview counters and warnings.</param>
        private static void PreviewHmiArtifact(
            Project project,
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            ImportResult result) {
            switch (artifact.Item.Kind) {
                case ExportKind.HmiScreen:
                    PreviewHmiScreen(project, artifact, conflictPolicy, result);
                    return;

                case ExportKind.HmiTemplate:
                    PreviewHmiFolderArtifact(project, artifact, "ScreenTemplateFolder", "ScreenTemplates", conflictPolicy, result);
                    return;

                case ExportKind.HmiPopup:
                    PreviewHmiFolderArtifact(project, artifact, "ScreenPopupFolder", "ScreenPopups", conflictPolicy, result);
                    return;

                case ExportKind.HmiSlideIn:
                    PreviewHmiFlatArtifact(project, artifact, "ScreenSlideinFolder", "ScreenSlideins", conflictPolicy, result);
                    return;

                case ExportKind.HmiTagTable:
                    PreviewHmiFolderArtifact(project, artifact, "TagFolder", "TagTables", conflictPolicy, result);
                    return;

                case ExportKind.HmiConnection:
                    PreviewHmiCompositionArtifact(project, artifact, "Connections", conflictPolicy, result);
                    return;

                case ExportKind.HmiTextList:
                    PreviewHmiCompositionArtifact(project, artifact, "TextLists", conflictPolicy, result);
                    return;

                case ExportKind.HmiGraphicList:
                    PreviewHmiCompositionArtifact(project, artifact, "GraphicLists", conflictPolicy, result);
                    return;

                case ExportKind.HmiCycle:
                    PreviewHmiCompositionArtifact(project, artifact, "Cycles", conflictPolicy, result);
                    return;

                case ExportKind.HmiVBScript:
                    PreviewHmiFolderArtifact(project, artifact, "VBScriptFolder", "VBScripts", conflictPolicy, result);
                    return;

                case ExportKind.HmiGlobalElements:
                    PreviewHmiSingletonArtifact(project, artifact, "ScreenGlobalElements", conflictPolicy, result);
                    return;

                case ExportKind.HmiScreenOverview:
                    PreviewHmiSingletonArtifact(project, artifact, "ScreenOverview", conflictPolicy, result);
                    return;

                default:
                    result.Errors.Add(new ImportError(
                        artifact.Item,
                        new InvalidOperationException("No HMI preview handler is registered for " + artifact.Item.Kind + ".")));
                    return;
            }
        }

        /// <summary>
        /// Imports a folder-based classic HMI artifact such as templates, popups, tag tables, or scripts.
        /// </summary>
        /// <param name="project">Open project containing the HMI target.</param>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="rootProperty">HMI target property exposing the root folder.</param>
        /// <param name="collectionProperty">Folder property exposing the import collection.</param>
        /// <param name="conflictPolicy">Policy applied when the named artifact exists.</param>
        /// <param name="createBackup">Whether existing artifacts are backed up before replacement.</param>
        /// <param name="backupRoot">Backup root used when backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportHmiFolderArtifact(
            Project project,
            RepositoryArtifact artifact,
            string rootProperty,
            string collectionProperty,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveRequiredHmiTarget(project, item);
            var root = GetPropertyValue(target, rootProperty);
            var folder = ResolveOrCreateGenericGroupByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Folders");
            ImportIntoOwnerCollection(folder, collectionProperty, artifact, conflictPolicy, createBackup, backupRoot, result);
        }

        /// <summary>
        /// Imports a flat classic HMI artifact stored below a target root object.
        /// </summary>
        /// <param name="project">Open project containing the HMI target.</param>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="rootProperty">HMI target property exposing the flat root object.</param>
        /// <param name="collectionProperty">Root property exposing the import collection.</param>
        /// <param name="conflictPolicy">Policy applied when the named artifact exists.</param>
        /// <param name="createBackup">Whether existing artifacts are backed up before replacement.</param>
        /// <param name="backupRoot">Backup root used when backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportHmiFlatArtifact(
            Project project,
            RepositoryArtifact artifact,
            string rootProperty,
            string collectionProperty,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveRequiredHmiTarget(project, item);
            var root = GetPropertyValue(target, rootProperty);
            ImportIntoOwnerCollection(root, collectionProperty, artifact, conflictPolicy, createBackup, backupRoot, result);
        }

        /// <summary>
        /// Imports a target-level classic HMI composition artifact.
        /// </summary>
        /// <param name="project">Open project containing the HMI target.</param>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="compositionProperty">HMI target property exposing the import composition.</param>
        /// <param name="conflictPolicy">Policy applied when the named artifact exists.</param>
        /// <param name="createBackup">Whether existing artifacts are backed up before replacement.</param>
        /// <param name="backupRoot">Backup root used when backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportHmiCompositionArtifact(
            Project project,
            RepositoryArtifact artifact,
            string compositionProperty,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var target = ResolveRequiredHmiTarget(project, artifact.Item);
            var composition = GetPropertyValue(target, compositionProperty);
            ImportIntoCollection(composition, artifact, conflictPolicy, createBackup, backupRoot, result);
        }

        /// <summary>
        /// Imports a singleton HMI artifact through the target-level import method.
        /// </summary>
        /// <param name="project">Open project containing the HMI target.</param>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="singletonProperty">HMI target property exposing the current singleton.</param>
        /// <param name="importMethodName">HMI target import method to invoke.</param>
        /// <param name="conflictPolicy">Policy applied when the singleton already exists.</param>
        /// <param name="createBackup">Whether the current singleton is backed up before replacement.</param>
        /// <param name="backupRoot">Backup root used when backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportHmiSingletonArtifact(
            Project project,
            RepositoryArtifact artifact,
            string singletonProperty,
            string importMethodName,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveRequiredHmiTarget(project, item);
            var existing = GetPropertyValue(target, singletonProperty);
            if (existing != null && conflictPolicy == ImportConflictPolicy.Skip) {
                result.Skipped++;
                result.Warnings.Add("Skipped existing HMI singleton: " + item);
                return;
            }

            if (existing != null && createBackup) {
                BackupExistingArtifact(existing, backupRoot, item);
                result.BackupRoot = backupRoot;
            }

            InvokeTargetImport(target, importMethodName, new FileInfo(artifact.RepositoryPath), existing != null);
            result.Imported++;
        }

        /// <summary>
        /// Imports an artifact into a collection property exposed by an owner object.
        /// </summary>
        /// <param name="owner">Owner object that exposes the target collection.</param>
        /// <param name="collectionProperty">Collection property to import into.</param>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="conflictPolicy">Policy applied when the named artifact exists.</param>
        /// <param name="createBackup">Whether existing artifacts are backed up before replacement.</param>
        /// <param name="backupRoot">Backup root used when backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportIntoOwnerCollection(
            object owner,
            string collectionProperty,
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var collection = GetPropertyValue(owner, collectionProperty);
            ImportIntoCollection(collection, artifact, conflictPolicy, createBackup, backupRoot, result);
        }

        /// <summary>
        /// Imports an artifact through an Openness collection while honoring conflict handling.
        /// </summary>
        /// <param name="collection">Openness collection that exposes Import and Find operations.</param>
        /// <param name="artifact">Repository artifact to import.</param>
        /// <param name="conflictPolicy">Policy applied when the named artifact exists.</param>
        /// <param name="createBackup">Whether existing artifacts are backed up before replacement.</param>
        /// <param name="backupRoot">Backup root used when backup is enabled.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void ImportIntoCollection(
            object collection,
            RepositoryArtifact artifact,
            ImportConflictPolicy conflictPolicy,
            bool createBackup,
            string backupRoot,
            ImportResult result) {
            var item = artifact.Item;
            var existing = ResolveNamedItem(collection, null, item.Name);
            if (existing != null && conflictPolicy == ImportConflictPolicy.Skip) {
                result.Skipped++;
                result.Warnings.Add("Skipped existing artifact: " + item);
                return;
            }

            if (existing != null && createBackup) {
                BackupExistingArtifact(existing, backupRoot, item);
                result.BackupRoot = backupRoot;
            }

            InvokeImport(collection, new FileInfo(artifact.RepositoryPath), existing != null);
            result.Imported++;
        }

        /// <summary>
        /// Adds a preview message for a folder-based classic HMI artifact.
        /// </summary>
        /// <param name="project">Open project used for target resolution.</param>
        /// <param name="artifact">Repository artifact being previewed.</param>
        /// <param name="rootProperty">HMI target property exposing the root folder.</param>
        /// <param name="collectionProperty">Folder property exposing the artifact collection.</param>
        /// <param name="conflictPolicy">Policy applied when an object already exists.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void PreviewHmiFolderArtifact(
            Project project,
            RepositoryArtifact artifact,
            string rootProperty,
            string collectionProperty,
            ImportConflictPolicy conflictPolicy,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                result.Errors.Add(new ImportError(item, new InvalidOperationException("HMI target not found: " + item.CpuPath)));
                return;
            }

            var root = GetPropertyValue(target, rootProperty);
            var folder = ResolveGenericGroupByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Folders");
            var existing = folder == null ? null : ResolveNamedItem(folder, collectionProperty, item.Name);
            RecordPreview(item, existing, conflictPolicy, "HMI artifact", result);
        }

        /// <summary>
        /// Adds a preview message for a flat classic HMI artifact.
        /// </summary>
        /// <param name="project">Open project used for target resolution.</param>
        /// <param name="artifact">Repository artifact being previewed.</param>
        /// <param name="rootProperty">HMI target property exposing the flat root object.</param>
        /// <param name="collectionProperty">Root property exposing the artifact collection.</param>
        /// <param name="conflictPolicy">Policy applied when an object already exists.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void PreviewHmiFlatArtifact(
            Project project,
            RepositoryArtifact artifact,
            string rootProperty,
            string collectionProperty,
            ImportConflictPolicy conflictPolicy,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                result.Errors.Add(new ImportError(item, new InvalidOperationException("HMI target not found: " + item.CpuPath)));
                return;
            }

            var root = GetPropertyValue(target, rootProperty);
            var existing = ResolveNamedItem(root, collectionProperty, item.Name);
            RecordPreview(item, existing, conflictPolicy, "HMI artifact", result);
        }

        /// <summary>
        /// Adds a preview message for a target-level classic HMI composition artifact.
        /// </summary>
        /// <param name="project">Open project used for target resolution.</param>
        /// <param name="artifact">Repository artifact being previewed.</param>
        /// <param name="compositionProperty">HMI target property exposing the artifact composition.</param>
        /// <param name="conflictPolicy">Policy applied when an object already exists.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void PreviewHmiCompositionArtifact(
            Project project,
            RepositoryArtifact artifact,
            string compositionProperty,
            ImportConflictPolicy conflictPolicy,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                result.Errors.Add(new ImportError(item, new InvalidOperationException("HMI target not found: " + item.CpuPath)));
                return;
            }

            var composition = GetPropertyValue(target, compositionProperty);
            var existing = ResolveNamedItem(composition, null, item.Name);
            RecordPreview(item, existing, conflictPolicy, "HMI artifact", result);
        }

        /// <summary>
        /// Adds a preview message for an HMI singleton artifact.
        /// </summary>
        /// <param name="project">Open project used for target resolution.</param>
        /// <param name="artifact">Repository artifact being previewed.</param>
        /// <param name="singletonProperty">HMI target property exposing the singleton.</param>
        /// <param name="conflictPolicy">Policy applied when the singleton already exists.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void PreviewHmiSingletonArtifact(
            Project project,
            RepositoryArtifact artifact,
            string singletonProperty,
            ImportConflictPolicy conflictPolicy,
            ImportResult result) {
            var item = artifact.Item;
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                result.Errors.Add(new ImportError(item, new InvalidOperationException("HMI target not found: " + item.CpuPath)));
                return;
            }

            var existing = GetPropertyValue(target, singletonProperty);
            RecordPreview(item, existing, conflictPolicy, "HMI singleton", result);
        }

        /// <summary>
        /// Records the create, replace, or skip action that an import would perform.
        /// </summary>
        /// <param name="item">Artifact identity being previewed.</param>
        /// <param name="existing">Resolved existing Openness object, or null for create.</param>
        /// <param name="conflictPolicy">Policy applied when an object already exists.</param>
        /// <param name="label">Human-readable artifact label for diagnostics.</param>
        /// <param name="result">Mutable import result receiving counters and warnings.</param>
        private static void RecordPreview(
            ExportItem item,
            object existing,
            ImportConflictPolicy conflictPolicy,
            string label,
            ImportResult result) {
            if (existing == null) {
                result.Imported++;
                result.Warnings.Add("Create " + label + ": " + item);
                return;
            }

            if (conflictPolicy == ImportConflictPolicy.Skip) {
                result.Skipped++;
                result.Warnings.Add("Skip existing " + label + ": " + item);
                return;
            }

            result.Imported++;
            result.Warnings.Add("Replace existing " + label + ": " + item);
        }

        /// <summary>
        /// Imports a UDT XML into the appropriate type group.
        /// Uses reflection because the TypeGroup structure varies across Openness versions/installations.
        /// </summary>
        /// <param name="plc">PLC software instance that owns the type group.</param>
        /// <param name="item">Repository artifact metadata describing group path and UDT name.</param>
        /// <param name="xml">XML file to import into the resolved type collection.</param>
        private static void ImportUdt(PlcSoftware plc, ExportItem item, FileInfo xml) {
            object root = plc.TypeGroup;
            if (root == null) {
                throw new InvalidOperationException("PLC TypeGroup is not available.");
            }

            // NOTE: GroupPath may include UI-only roots; if needed, normalize before calling this.
            var group = ResolveOrCreateGenericGroupByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Groups");

            // group.Types.Import(FileInfo, ImportOptions) or group.Types.Import(FileInfo)
            var typesCollection = GetPropertyValue(group, "Types");
            InvokeImport(typesCollection, xml, replace: true);
        }

        /// <summary>
        /// Imports a program block (OB/FB/FC/DB) XML into the target block group.
        /// </summary>
        /// <param name="plc">PLC software instance that owns the block group.</param>
        /// <param name="item">Repository artifact metadata describing group path and block name.</param>
        /// <param name="xml">XML file to import into the resolved block collection.</param>
        private static void ImportBlock(PlcSoftware plc, ExportItem item, FileInfo xml) {
            var group = ResolveOrCreateGenericGroupByPath(plc.BlockGroup, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Groups");

            // group.Blocks.Import(FileInfo, ImportOptions) or group.Blocks.Import(FileInfo)
            var blocksCollection = GetPropertyValue(group, "Blocks");
            InvokeImport(blocksCollection, xml, replace: true);
        }

        /// <summary>
        /// Imports a PLC tag table XML into the appropriate tag table group.
        /// </summary>
        /// <param name="plc">PLC software instance that owns the tag table group.</param>
        /// <param name="item">Repository artifact metadata describing group path and tag table name.</param>
        /// <param name="xml">XML file to import into the resolved tag table collection.</param>
        private static void ImportTagTable(PlcSoftware plc, ExportItem item, FileInfo xml) {
            var root = TryGetTagTableRoot(plc);
            if (root == null) {
                throw new InvalidOperationException("PLC tag tables are not exposed by this Openness build.");
            }

            var group = ResolveOrCreateGenericGroupByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Groups");

            var tablesCollection = GetPropertyValue(group, "TagTables");
            InvokeImport(tablesCollection, xml, replace: true);
        }

        /// <summary>
        /// Imports a PLC artifact stored inside a generic group hierarchy.
        /// </summary>
        /// <param name="plc">PLC software instance that receives the artifact.</param>
        /// <param name="item">Repository artifact identity.</param>
        /// <param name="rootProperty">PLC software property exposing the root group.</param>
        /// <param name="collectionProperty">Group property exposing the import collection.</param>
        /// <param name="xml">Repository payload file.</param>
        private static void ImportGenericPlcArtifact(
            PlcSoftware plc,
            ExportItem item,
            string rootProperty,
            string collectionProperty,
            FileInfo xml) {
            var root = GetPropertyValue(plc, rootProperty);
            if (root == null) {
                throw new InvalidOperationException("PLC root group is not available: " + rootProperty + ".");
            }

            var group = ResolveOrCreateGenericGroupByPath(root, NormalizeRootGroupPath(item.Kind, item.GroupPath), "Groups");
            var collection = GetPropertyValue(group, collectionProperty);
            InvokeImport(collection, xml, replace: true);
        }

        /// <summary>
        /// Imports a PLC user constant into the tag table that owns it.
        /// </summary>
        /// <param name="plc">PLC software instance that receives the constant.</param>
        /// <param name="item">Constant identity; the group path ends with the owner tag table name.</param>
        /// <param name="xml">Repository payload file.</param>
        private static void ImportPlcUserConstant(PlcSoftware plc, ExportItem item, FileInfo xml) {
            var split = SplitOwnerPath(NormalizeRootGroupPath(item.Kind, item.GroupPath));
            var tagRoot = TryGetTagTableRoot(plc);
            if (tagRoot == null) {
                throw new InvalidOperationException("PLC tag tables are not exposed by this Openness build.");
            }

            var tableGroup = ResolveOrCreateGenericGroupByPath(tagRoot, split.Item1, "Groups");
            var table = ResolveNamedItem(tableGroup, "TagTables", split.Item2);
            if (table == null) {
                throw new InvalidOperationException("PLC tag table for user constant was not found: " + split.Item2 + ".");
            }

            var constants = GetPropertyValue(table, "UserConstants");
            InvokeImport(constants, xml, replace: true);
        }

        /// <summary>
        /// Invokes Import(...) on an Openness collection via reflection.
        /// This avoids taking a hard dependency on specific ImportOptions enum members.
        /// </summary>
        /// <param name="opennessCollection">Openness collection object that exposes an Import method.</param>
        /// <param name="xml">XML file passed to the selected Import overload.</param>
        private static void InvokeImport(object opennessCollection, FileInfo xml) {
            InvokeImport(opennessCollection, xml, replace: true);
        }

        /// <summary>
        /// Invokes Import(...) on an Openness collection with the requested replacement mode.
        /// </summary>
        /// <param name="opennessCollection">Openness collection object that exposes an Import method.</param>
        /// <param name="xml">Payload file passed to the selected Import overload.</param>
        /// <param name="replace">Whether the ImportOptions argument should request replacement.</param>
        private static void InvokeImport(object opennessCollection, FileInfo xml, bool replace) {
            if (opennessCollection == null) {
                throw new InvalidOperationException("Target collection is null (cannot import).");
            }

            var t = opennessCollection.GetType();
            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => string.Equals(m.Name, "Import", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var m in methods) {
                var p = m.GetParameters();
                if (p.Length >= 2 && p[0].ParameterType == typeof(FileInfo) && p[1].ParameterType.IsEnum) {
                    var args = new object[p.Length];
                    var importOption = CreateImportOption(p[1].ParameterType, replace);
                    args[0] = xml;
                    args[1] = importOption;
                    for (var i = 2; i < args.Length; i++) {
                        args[i] = CreateDefaultEnumOption(p[i].ParameterType);
                    }

                    try {
                        m.Invoke(opennessCollection, args);
                        return;
                    } catch (TargetInvocationException ex) {
                        ThrowInvocationException(
                            t.FullName + ".Import",
                            ex,
                            DiagnosticData("OpennessCollectionType", t.FullName),
                            DiagnosticData("OpennessMethodSignature", FormatMethodSignature(m)),
                            DiagnosticData("ImportFile", xml.FullName),
                            DiagnosticData("ImportReplaceRequested", replace),
                            DiagnosticData("ImportOptionType", p[1].ParameterType.FullName),
                            DiagnosticData("ImportOptionValue", FormatEnumOption(importOption)));
                        throw;
                    }
                }
            }

            foreach (var m in methods) {
                var p = m.GetParameters();
                if (p.Length == 1 && p[0].ParameterType == typeof(FileInfo)) {
                    try {
                        m.Invoke(opennessCollection, new object[] { xml });
                        return;
                    } catch (TargetInvocationException ex) {
                        ThrowInvocationException(
                            t.FullName + ".Import",
                            ex,
                            DiagnosticData("OpennessCollectionType", t.FullName),
                            DiagnosticData("OpennessMethodSignature", FormatMethodSignature(m)),
                            DiagnosticData("ImportFile", xml.FullName),
                            DiagnosticData("ImportReplaceRequested", replace),
                            DiagnosticData("ImportOptionValue", "<file-only overload>"));
                        throw;
                    }
                }
            }

            throw new MissingMethodException(t.FullName, "Import");
        }

        /// <summary>
        /// Resolves the HMI target for an import item and throws a clear error when missing.
        /// </summary>
        /// <param name="project">Open TIA project to search.</param>
        /// <param name="item">Artifact identity containing the HMI target path.</param>
        /// <returns>The resolved classic HMI target.</returns>
        private static HmiTarget ResolveRequiredHmiTarget(Project project, ExportItem item) {
            var target = ResolveHmiTarget(project, item.CpuPath);
            if (target == null) {
                throw new InvalidOperationException("Could not resolve HMI target for: " + item.CpuPath);
            }

            return target;
        }

        /// <summary>
        /// Backs up an existing Openness artifact by invoking its Export method into the mirrored repository path.
        /// </summary>
        /// <param name="existing">Existing Openness artifact to export.</param>
        /// <param name="backupRoot">Backup root folder created for the import run.</param>
        /// <param name="item">Artifact identity used to build a deterministic backup path.</param>
        private static void BackupExistingArtifact(object existing, string backupRoot, ExportItem item) {
            if (existing == null) {
                return;
            }

            if (string.IsNullOrWhiteSpace(backupRoot)) {
                throw new InvalidOperationException("Backup folder is required before replacing an existing artifact.");
            }

            var backupPath = PathHelper.GetOutputFilePath(backupRoot, item);
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? ".");
            InvokeExport(existing, new FileInfo(backupPath), item.Kind);
        }

        /// <summary>
        /// Invokes the public Openness Export method on a resolved artifact.
        /// </summary>
        /// <param name="artifact">Resolved Openness artifact to export.</param>
        /// <param name="destination">Destination backup file.</param>
        /// <param name="kind">Artifact kind used to choose a round-trippable export option.</param>
        private static void InvokeExport(object artifact, FileInfo destination, ExportKind kind) {
            if (artifact == null) {
                throw new InvalidOperationException("Cannot export a null Openness artifact.");
            }

            var methods = artifact.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(m => string.Equals(m.Name, "Export", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var withOptions = methods.FirstOrDefault(m => {
                var p = m.GetParameters();
                return p.Length == 2 && p[0].ParameterType == typeof(FileInfo);
            });
            if (withOptions != null) {
                var exportOption = OpennessExportOptionPolicy.ForArtifact(kind);
                try {
                    withOptions.Invoke(artifact, new object[] { destination, exportOption });
                    return;
                } catch (TargetInvocationException ex) {
                    ThrowInvocationException(
                        artifact.GetType().FullName + ".Export",
                        ex,
                        DiagnosticData("OpennessArtifactType", artifact.GetType().FullName),
                        DiagnosticData("OpennessMethodSignature", FormatMethodSignature(withOptions)),
                        DiagnosticData("ExportFile", destination.FullName),
                        DiagnosticData("ExportOptionValue", FormatEnumOption(exportOption)));
                    throw;
                }
            }

            var fileOnly = methods.FirstOrDefault(m => {
                var p = m.GetParameters();
                return p.Length == 1 && p[0].ParameterType == typeof(FileInfo);
            });
            if (fileOnly != null) {
                try {
                    fileOnly.Invoke(artifact, new object[] { destination });
                    return;
                } catch (TargetInvocationException ex) {
                    ThrowInvocationException(
                        artifact.GetType().FullName + ".Export",
                        ex,
                        DiagnosticData("OpennessArtifactType", artifact.GetType().FullName),
                        DiagnosticData("OpennessMethodSignature", FormatMethodSignature(fileOnly)),
                        DiagnosticData("ExportFile", destination.FullName),
                        DiagnosticData("ExportOptionValue", "<file-only overload>"));
                    throw;
                }
            }

            throw new MissingMethodException(artifact.GetType().FullName, "Export");
        }

        /// <summary>
        /// Invokes a target-level HMI import method such as ImportScreenOverview.
        /// </summary>
        /// <param name="target">HMI target that exposes the import method.</param>
        /// <param name="methodName">Public method name to invoke.</param>
        /// <param name="xml">Repository payload file.</param>
        /// <param name="replace">Whether the import should override existing content.</param>
        private static void InvokeTargetImport(object target, string methodName, FileInfo xml, bool replace) {
            if (target == null) {
                throw new InvalidOperationException("Target is null (cannot import singleton artifact).");
            }

            var method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => {
                    var p = m.GetParameters();
                    return string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                           p.Length == 2 &&
                           p[0].ParameterType == typeof(FileInfo) &&
                           p[1].ParameterType.IsEnum;
                });
            if (method == null) {
                throw new MissingMethodException(target.GetType().FullName, methodName);
            }

            try {
                var importOption = CreateImportOption(method.GetParameters()[1].ParameterType, replace);
                var args = new object[] { xml, importOption };
                method.Invoke(target, args);
            } catch (TargetInvocationException ex) {
                ThrowInvocationException(
                    target.GetType().FullName + "." + methodName,
                    ex,
                    DiagnosticData("OpennessTargetType", target.GetType().FullName),
                    DiagnosticData("OpennessMethodSignature", FormatMethodSignature(method)),
                    DiagnosticData("ImportFile", xml.FullName),
                    DiagnosticData("ImportReplaceRequested", replace),
                    DiagnosticData("ImportOptionValue", FormatEnumOption(CreateImportOption(method.GetParameters()[1].ParameterType, replace))));
                throw;
            }
        }

        /// <summary>
        /// Creates the ImportOptions enum value requested by the conflict policy.
        /// </summary>
        /// <param name="enumType">Runtime enum type declared by the Openness method.</param>
        /// <param name="replace">Whether the value should be Override or None.</param>
        /// <returns>An enum value compatible with the Openness import method.</returns>
        private static object CreateImportOption(Type enumType, bool replace) {
            var name = replace ? "Override" : "None";
            if (Enum.IsDefined(enumType, name)) {
                return Enum.Parse(enumType, name);
            }

            return Enum.ToObject(enumType, 0);
        }

        /// <summary>
        /// Creates a neutral enum value for optional secondary import options.
        /// </summary>
        /// <param name="parameterType">Parameter type to populate.</param>
        /// <returns>A neutral enum value, <see cref="Type.Missing"/>, or <see langword="null"/> as appropriate.</returns>
        private static object CreateDefaultEnumOption(Type parameterType) {
            if (!parameterType.IsEnum) {
                return Type.Missing;
            }

            if (Enum.IsDefined(parameterType, "None")) {
                return Enum.Parse(parameterType, "None");
            }

            return Enum.ToObject(parameterType, 0);
        }

        /// <summary>
        /// Resolves a named artifact from an owner collection or a collection property.
        /// </summary>
        /// <param name="owner">Owner object or collection to inspect.</param>
        /// <param name="collectionProperty">Optional collection property name on the owner.</param>
        /// <param name="name">Artifact name to match.</param>
        /// <returns>The matching artifact, or <see langword="null"/> when not found.</returns>
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
                } catch (TargetInvocationException ex) {
                    ThrowInvocationException(
                        collection.GetType().FullName + ".Find",
                        ex,
                        DiagnosticData("OpennessCollectionType", collection.GetType().FullName),
                        DiagnosticData("OpennessMethodSignature", FormatMethodSignature(find)),
                        DiagnosticData("FindName", name),
                        DiagnosticData("CollectionProperty", collectionProperty ?? string.Empty));
                    throw;
                }

                if (found != null) {
                    return found;
                }
            }

            foreach (var candidate in collection as IEnumerable ?? Array.Empty<object>()) {
                if (string.Equals(GetStringProperty(candidate, "Name"), name, StringComparison.OrdinalIgnoreCase)) {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a generic group or folder path using a configurable child collection property.
        /// </summary>
        /// <param name="rootGroup">Root object used as the traversal starting point.</param>
        /// <param name="groupPath">Slash-delimited path relative to the root.</param>
        /// <param name="childCollectionProperty">Property exposing child groups or folders.</param>
        /// <returns>The resolved object, or <see langword="null"/> when any segment is missing.</returns>
        private static object ResolveGenericGroupByPath(object rootGroup, string groupPath, string childCollectionProperty) {
            if (rootGroup == null) {
                return null;
            }

            if (string.IsNullOrWhiteSpace(groupPath)) {
                return rootGroup;
            }

            object current = rootGroup;
            foreach (var part in groupPath.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                var next = GetEnumerableProperty(current, childCollectionProperty)
                    .Cast<object>()
                    .FirstOrDefault(g => string.Equals(GetStringProperty(g, "Name"), part, StringComparison.OrdinalIgnoreCase));
                if (next == null) {
                    return null;
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Resolves or creates a generic group or folder path when the child composition exposes Create(string).
        /// </summary>
        /// <param name="rootGroup">Root object used as the traversal starting point.</param>
        /// <param name="groupPath">Slash-delimited path relative to the root.</param>
        /// <param name="childCollectionProperty">Property exposing child groups or folders.</param>
        /// <returns>The resolved or newly created object.</returns>
        private static object ResolveOrCreateGenericGroupByPath(object rootGroup, string groupPath, string childCollectionProperty) {
            if (rootGroup == null) {
                throw new InvalidOperationException("Root group or folder is not available.");
            }

            if (string.IsNullOrWhiteSpace(groupPath)) {
                return rootGroup;
            }

            object current = rootGroup;
            foreach (var part in groupPath.Replace("\\", "/").Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                var next = GetEnumerableProperty(current, childCollectionProperty)
                    .Cast<object>()
                    .FirstOrDefault(g => string.Equals(GetStringProperty(g, "Name"), part, StringComparison.OrdinalIgnoreCase));
                if (next == null) {
                    var children = GetPropertyValue(current, childCollectionProperty);
                    next = InvokeCreate(children, part);
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Invokes Create(string) on a child group or folder collection.
        /// </summary>
        /// <param name="collection">Openness collection that should expose Create(string).</param>
        /// <param name="name">Name of the group or folder to create.</param>
        /// <returns>The created object returned by Openness.</returns>
        private static object InvokeCreate(object collection, string name) {
            if (collection == null) {
                throw new InvalidOperationException("Child collection is not available for group creation.");
            }

            var create = collection.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m =>
                    string.Equals(m.Name, "Create", StringComparison.OrdinalIgnoreCase) &&
                    m.GetParameters().Length == 1 &&
                    m.GetParameters()[0].ParameterType == typeof(string));
            if (create == null) {
                throw new MissingMethodException(collection.GetType().FullName, "Create");
            }

            try {
                var created = create.Invoke(collection, new object[] { name });
                if (created != null) {
                    return created;
                }
            } catch (TargetInvocationException ex) {
                ThrowInvocationException(
                    collection.GetType().FullName + ".Create",
                    ex,
                    DiagnosticData("OpennessCollectionType", collection.GetType().FullName),
                    DiagnosticData("OpennessMethodSignature", FormatMethodSignature(create)),
                    DiagnosticData("CreateName", name));
                throw;
            }

            var resolved = ResolveNamedItem(collection, null, name);
            if (resolved == null) {
                throw new InvalidOperationException("The group or folder was created but could not be resolved: " + name);
            }

            return resolved;
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
        /// Removes optional UI root labels so stored group paths match Openness root collections.
        /// </summary>
        /// <param name="kind">Artifact kind whose descriptor provides the repository root category.</param>
        /// <param name="groupPath">Stored group path from scan or repository metadata.</param>
        /// <returns>The group path relative to the actual Openness root.</returns>
        private static string NormalizeRootGroupPath(ExportKind kind, string groupPath) {
            var p = (groupPath ?? string.Empty).Trim().Replace("\\", "/");
            if (p.Length == 0) {
                return string.Empty;
            }

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

            foreach (var root in roots) {
                if (string.Equals(p, root, StringComparison.OrdinalIgnoreCase)) {
                    return string.Empty;
                }

                if (p.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) {
                    return p.Substring(root.Length + 1);
                }
            }

            return p;
        }

        // -------------------------
        // Resolution helpers
        // -------------------------

        /// <summary>
        /// Resolves PlcSoftware based on cpuPath in the form "&lt;DeviceName&gt;/&lt;CpuItemName&gt;".
        /// Splits at the last '/' because device names may contain '/'.
        /// </summary>
        /// <param name="project">Open TIA project that contains the target PLC device.</param>
        /// <param name="cpuPath">Device and CPU item path stored in the repository artifact.</param>
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
        /// Resolves the classic WinCC HMI target from a target path string.
        /// Expected path format: "&lt;DeviceName&gt;/&lt;DeviceItemName&gt;".
        /// </summary>
        /// <param name="project">Open TIA project that contains the target HMI device.</param>
        /// <param name="targetPath">Device and HMI item path stored in the repository artifact.</param>
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
        /// Resolves an HMI target by matching either device, device item, or HMI target names.
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
        /// Resolves an existing HMI screen folder without creating missing folders.
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
        /// Resolves or creates the destination HMI screen folder hierarchy.
        /// </summary>
        /// <param name="root">Root screen folder from the HMI target.</param>
        /// <param name="folderPath">Slash-delimited screen folder path to create when missing.</param>
        /// <returns>The resolved or newly created screen folder.</returns>
        private static ScreenFolder ResolveOrCreateScreenFolder(ScreenFolder root, string folderPath) {
            if (root == null) {
                throw new InvalidOperationException("HMI screen root folder is not available.");
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
                    next = current.Folders.Create(part);
                }

                current = next;
            }

            return current;
        }

        /// <summary>
        /// Enumerates all device items under a device (non-recursive traversal).
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

        /// <summary>
        /// Tries to get the root group for PLC tag tables using common property names across Openness versions.
        /// </summary>
        /// <param name="plc">PLC software instance to inspect for tag table root properties.</param>
        /// <returns>The tag table root group object, or <see langword="null"/> when unavailable.</returns>
        private static object TryGetTagTableRoot(PlcSoftware plc) {
            var prop = plc.GetType().GetProperty("PlcTagTableGroup", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) {
                var val = prop.GetValue(plc, null);
                if (val != null) {
                    return val;
                }
            }

            prop = plc.GetType().GetProperty("TagTableGroup", BindingFlags.Instance | BindingFlags.Public);
            if (prop != null) {
                var val = prop.GetValue(plc, null);
                if (val != null) {
                    return val;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves a generic Openness group hierarchy by walking "Groups" collections using reflection.
        /// This is used for type groups and tag table groups.
        /// </summary>
        /// <param name="rootGroup">Root Openness group object used as the traversal starting point.</param>
        /// <param name="groupPath">Slash-delimited relative group path.</param>
        /// <returns>The resolved group object, or <see langword="null"/> when any segment is missing.</returns>
        private static object ResolveGenericGroupByPath(object rootGroup, string groupPath) {
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
                var groups = GetEnumerableProperty(current, "Groups");
                object next = null;

                foreach (var g in groups) {
                    var name = GetStringProperty(g, "Name");
                    if (string.Equals(name, part, StringComparison.OrdinalIgnoreCase)) {
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
        /// Gets an object's property value using the shared reflection cache.
        /// </summary>
        /// <param name="obj">Object that may expose the requested public property.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The property value, or <see langword="null"/> when unavailable.</returns>
        private static object GetPropertyValue(object obj, string propertyName) {
            if (obj == null) {
                return null;
            }

            var p = ReflectionCache.GetProperty(obj.GetType(), propertyName);
            return p == null ? null : p.GetValue(obj, null);
        }

        /// <summary>
        /// Gets an IEnumerable property value (returns empty enumerable when missing/null).
        /// </summary>
        /// <param name="obj">Object that may expose the requested enumerable property.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The property value as <see cref="IEnumerable"/>, or an empty collection when unavailable.</returns>
        private static IEnumerable GetEnumerableProperty(object obj, string propertyName) {
            var v = GetPropertyValue(obj, propertyName);
            return v as IEnumerable ?? Array.Empty<object>();
        }

        /// <summary>
        /// Gets a string property value (returns null when missing/non-string).
        /// </summary>
        /// <param name="obj">Object that may expose the requested string property.</param>
        /// <param name="propertyName">Property name to read.</param>
        /// <returns>The string property value, or <see langword="null"/> when unavailable.</returns>
        private static string GetStringProperty(object obj, string propertyName) {
            var v = GetPropertyValue(obj, propertyName);
            return v as string;
        }
    }
}
