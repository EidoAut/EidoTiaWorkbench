// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Siemens.Engineering;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Tests {
    /// <summary>
    /// Executes offline regression tests for repository, path, manifest, and model logic.
    /// </summary>
    internal static class Program {
        /// <summary>
        /// Runs all tests and returns a non-zero process exit code when any assertion fails.
        /// </summary>
        /// <returns>Zero when all tests pass; otherwise one.</returns>
        private static int Main() {
            var tests = new List<Tuple<string, Action>>
            {
                Tuple.Create("Sanitize protects Windows paths", new Action(TestSanitize)),
                Tuple.Create("Artifact catalog and adapter registry are consistent", new Action(TestCatalogConsistency)),
                Tuple.Create("PLC output path uses PLC root", new Action(TestPlcOutputPath)),
                Tuple.Create("HMI output path uses HMI screen layout", new Action(TestHmiOutputPath)),
                Tuple.Create("New artifact routes use catalog categories and formats", new Action(TestNewArtifactRoutes)),
                Tuple.Create("SHA-256 is deterministic", new Action(TestSha256)),
                Tuple.Create("Manifest saves and loads entries", new Action(TestManifestRoundTrip)),
                Tuple.Create("Manifest preserves entries without payload", new Action(TestManifestNoPayload)),
                Tuple.Create("Manifest retry selector uses final status", new Action(TestManifestRetrySelector)),
                Tuple.Create("Repository inventory differentiates PLC and HMI", new Action(TestRepositoryInventory)),
                Tuple.Create("Repository inventory supports non-XML formats", new Action(TestRepositoryInventoryFormats)),
                Tuple.Create("Repository inventory reads schema 3 logical manifest keys", new Action(TestRepositoryInventoryLogicalManifestKey)),
                Tuple.Create("Repository inventory hides manifest auxiliary files", new Action(TestCompoundManifestPayload)),
                Tuple.Create("Repository inventory detects invalid XML", new Action(TestInvalidXml)),
                Tuple.Create("Import dependency planner orders HMI dependencies first", new Action(TestImportDependencyPlanner)),
                Tuple.Create("Import preflight detects duplicate XML IDs", new Action(TestImportPreflightDuplicateIds)),
                Tuple.Create("Exception diagnostics preserve reflection inner exceptions", new Action(TestExceptionDiagnostics)),
                Tuple.Create("HMI screen export omits defaults for round trip", new Action(TestHmiScreenExportOptionPolicy)),
                Tuple.Create("Export order is independent from import order", new Action(TestExportOrderScreensBeforeGraphicLists)),
                Tuple.Create("Retry export order schedules previous session-loss root last", new Action(TestRetryExportOrderSchedulesPreviousRootLast)),
                Tuple.Create("Reflective export invokes exact options overload", new Action(TestReflectiveExportExactOverloadAndOption)),
                Tuple.Create("Export result derives global statuses", new Action(TestExportResultStatusModel)),
                Tuple.Create("Final GraphicList session loss keeps safe payload status", new Action(TestFinalGraphicListSessionLossCompletesWithIssues)),
                Tuple.Create("Manifest preserves prior payload on failed final artifact", new Action(TestManifestPreservesPayloadOnFailedFinalArtifact)),
                Tuple.Create("Export preflight selects explicit HMI graphic list handler", new Action(TestExportPreflightGraphicListHandler)),
                Tuple.Create("Export failure classifier preserves restrictions", new Action(TestFailureClassifierRestricted)),
                Tuple.Create("Export failure classifier isolates HMI graphic list failures", new Action(TestFailureClassifierGraphicListIsolated)),
                Tuple.Create("Export failure classifier detects session loss", new Action(TestFailureClassifierSessionLost)),
                Tuple.Create("Export failure isolation policy recovers once", new Action(TestFailureIsolationPolicyRecoveryOnce)),
                Tuple.Create("STA worker cancellation does not release queued Openness work early", new Action(TestStaWorkerCancellationWaitsForQueuedWork)),
                Tuple.Create("Transactional writer preserves previous payload on failure", new Action(TestTransactionalWriterPreservesPreviousPayload)),
                Tuple.Create("Coverage regression fixture has no exportable unsupported kinds", new Action(TestCoverageRegressionFixture)),
                Tuple.Create("Conflict policy enum remains explicit", new Action(TestConflictPolicy)),
            };

            var failed = 0;
            foreach (var test in tests) {
                try {
                    test.Item2();
                    Console.WriteLine("PASS " + test.Item1);
                } catch (Exception ex) {
                    failed++;
                    Console.WriteLine("FAIL " + test.Item1);
                    Console.WriteLine(ex);
                }
            }

            Console.WriteLine("Executed " + tests.Count + " test(s), failures: " + failed + ".");
            return failed == 0 ? 0 : 1;
        }

        /// <summary>
        /// Verifies repository file-name sanitization for reserved, invalid, and traversal names.
        /// </summary>
        private static void TestSanitize() {
            AssertEqual("_CON", PathHelper.Sanitize("CON"), "Reserved device names must be prefixed.");
            AssertEqual("A_B_C", PathHelper.Sanitize("A:B<C"), "Invalid filename characters must be replaced.");
            AssertEqual("_", PathHelper.Sanitize(".."), "Traversal tokens must not survive sanitization.");
        }

        /// <summary>
        /// Verifies that descriptors and adapters agree on declared capabilities.
        /// </summary>
        private static void TestCatalogConsistency() {
            var messages = ArtifactAdapterRegistry.Validate();
            AssertEqual(0, messages.Count, "Catalog validation must not produce errors: " + string.Join("; ", messages));

            foreach (ExportKind kind in Enum.GetValues(typeof(ExportKind))) {
                var descriptor = ArtifactCatalog.Get(kind);
                AssertEqual(kind, descriptor.Kind, "Every ExportKind must have an explicit descriptor.");

                if (descriptor.CanExport) {
                    AssertTrue(ArtifactAdapterRegistry.Get(kind).CanExport, "Export-capable kind must have adapter support: " + kind);
                }

                if (descriptor.CanImport) {
                    AssertTrue(ArtifactAdapterRegistry.Get(kind).CanImport, "Import-capable kind must have adapter support: " + kind);
                }
            }
        }

        /// <summary>
        /// Verifies that PLC artifacts are written below the PLC repository root.
        /// </summary>
        private static void TestPlcOutputPath() {
            var item = new ExportItem("Device 1/CPU 1516", "Motion/Axes", "FB_Main", ExportKind.Fb, "k1");
            var path = PathHelper.GetOutputFilePath("C:\\Repo", item).Replace("\\", "/");
            AssertContains(path, "C:/Repo/PLC/Device 1/CPU 1516/Blocks/Motion/Axes/");
            AssertContains(path, "FB_Main__");
        }

        /// <summary>
        /// Verifies that HMI screens use the classic HMI screen repository layout.
        /// </summary>
        private static void TestHmiOutputPath() {
            var item = new ExportItem("Panel 1/HMI_RT", "Plant/Overview", "Main Screen", ExportKind.HmiScreen, "k2");
            var path = PathHelper.GetOutputFilePath("C:\\Repo", item).Replace("\\", "/");
            AssertEqual("C:/Repo/HMI/Panel 1/HMI_RT/Screens/Plant/Overview/Main Screen.xml", path, "HMI screens must keep readable names without PLC hash suffixes.");
        }

        /// <summary>
        /// Verifies repository paths for artifact types added beyond the original PLC/HMI subset.
        /// </summary>
        private static void TestNewArtifactRoutes() {
            AssertRoute(ExportKind.HmiTemplate, "HMI/Panel/HMI_RT/Templates/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiPopup, "HMI/Panel/HMI_RT/Popups/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiSlideIn, "HMI/Panel/HMI_RT/SlideIns/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiTagTable, "HMI/Panel/HMI_RT/Tags/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiConnection, "HMI/Panel/HMI_RT/Connections/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiTextList, "HMI/Panel/HMI_RT/TextLists/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiGraphicList, "HMI/Panel/HMI_RT/GraphicLists/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiCycle, "HMI/Panel/HMI_RT/Cycles/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiVBScript, "HMI/Panel/HMI_RT/Scripts/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiGlobalElements, "HMI/Panel/HMI_RT/GlobalElements/Group/Artifact.xml");
            AssertRoute(ExportKind.HmiScreenOverview, "HMI/Panel/HMI_RT/ScreenOverview/Group/Artifact.xml");
            AssertRoute(ExportKind.PlcWatchTable, "PLC/Device/CPU/WatchTables/Group/Artifact__");
            AssertRoute(ExportKind.PlcForceTable, "PLC/Device/CPU/ForceTables/Group/Artifact__");
            AssertRoute(ExportKind.PlcExternalSource, "PLC/Device/CPU/ExternalSources/Group/Artifact__");
            AssertRoute(ExportKind.PlcAlarmTextList, "PLC/Device/CPU/AlarmTextLists/Group/Artifact__");

            var source = new ExportItem("Device/CPU", "Group", "Artifact", ExportKind.PlcExternalSource, "scl-key");
            AssertTrue(PathHelper.GetOutputFilePath("C:\\Repo", source).EndsWith(".scl", StringComparison.OrdinalIgnoreCase), "External sources must use the SCL extension.");

            var alarm = new ExportItem("Device/CPU", "Group", "Artifact", ExportKind.PlcAlarmTextList, "xlsx-key");
            AssertTrue(PathHelper.GetOutputFilePath("C:\\Repo", alarm).EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase), "PLC alarm text lists must use the XLSX extension.");
        }

        /// <summary>
        /// Verifies the SHA-256 helper against a known test vector.
        /// </summary>
        private static void TestSha256() {
            var hash = HashHelper.Sha256Hex("abc");
            AssertEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash, "Known SHA-256 vector must match.");
        }

        /// <summary>
        /// Verifies that manifest metadata survives save/load round-trips.
        /// </summary>
        private static void TestManifestRoundTrip() {
            WithTempDirectory(temp => {
                var manifest = new ManifestService.Manifest();
                manifest.Entries["HMI/Panel/Screens/Main.xml"] = new ManifestService.ManifestEntry {
                    Sha256 = "ABC",
                    LastExportUtc = "2026-08-10T00:00:00Z",
                    ArtifactKind = ExportKind.HmiScreen.ToString(),
                    TargetPath = "Panel/HMI",
                    TargetType = "Classic HMI",
                    GroupPath = string.Empty,
                    Name = "Main",
                };

                var service = new ManifestService();
                service.Save(temp, manifest);
                var loaded = service.Load(temp);
                AssertTrue(loaded.Entries.ContainsKey("HMI/Panel/Screens/Main.xml"), "Saved manifest entry must load.");
                AssertEqual("ABC", loaded.Entries["HMI/Panel/Screens/Main.xml"].Sha256, "Manifest hash must round-trip.");
            });
        }

        /// <summary>
        /// Verifies that unavailable/restricted entries without payload survive save/load and pruning.
        /// </summary>
        private static void TestManifestNoPayload() {
            WithTempDirectory(temp => {
                var item = new ExportItem(
                    "Device/CPU",
                    "Sources",
                    "Source1",
                    ExportKind.PlcExternalSource,
                    "Device/CPU||PlcExternalSource||Sources||Source1");
                var manifest = new ManifestService.Manifest();
                manifest.Entries[ManifestService.GetArtifactKey(item)] = new ManifestService.ManifestEntry {
                    LogicalKey = item.LogicalKey,
                    ArtifactKind = item.Kind.ToString(),
                    LastStatus = ArtifactExportStatus.Unavailable.ToString(),
                    Message = "No public export operation.",
                    PrimaryPath = string.Empty,
                    Files = new List<string>(),
                    FileHashes = new Dictionary<string, string>(),
                };

                var service = new ManifestService();
                service.Save(temp, manifest);
                var loaded = service.Load(temp);
                service.PruneMissingEntries(temp, loaded);

                AssertTrue(loaded.Entries.ContainsKey(item.LogicalKey), "No-payload manifest entries must not be pruned.");
                AssertEqual(ArtifactExportStatus.Unavailable.ToString(), loaded.Entries[item.LogicalKey].LastStatus, "Final unavailable status must survive.");
            });
        }

        /// <summary>
        /// Verifies the retry filter selects failed and aborted states but not restrictions.
        /// </summary>
        private static void TestManifestRetrySelector() {
            var failed = new ManifestService.ManifestEntry {
                LastStatus = ArtifactExportStatus.Failed.ToString(),
            };
            var aborted = new ManifestService.ManifestEntry {
                LastStatus = ArtifactExportStatus.AbortedSessionLost.ToString(),
            };
            var restricted = new ManifestService.ManifestEntry {
                LastStatus = ArtifactExportStatus.Restricted.ToString(),
            };

            AssertTrue(ManifestService.IsRetryableEntry(failed), "Failed entries must be retryable.");
            AssertTrue(ManifestService.IsRetryableEntry(aborted), "Session-loss entries must be retryable.");
            AssertEqual(false, ManifestService.IsRetryableEntry(restricted), "Restricted entries must not be retried automatically.");
        }

        /// <summary>
        /// Verifies that repository scanning reconstructs PLC and HMI item identities.
        /// </summary>
        private static void TestRepositoryInventory() {
            WithTempDirectory(temp => {
                WriteXml(Path.Combine(temp, "PLC", "Device", "CPU", "Blocks", "FB_Main__12345678.xml"));
                WriteXml(Path.Combine(temp, "HMI", "Panel", "HMI_RT", "Screens", "Plant", "Main.xml"));

                var artifacts = new RepositoryInventoryService().Scan(temp);
                var plc = artifacts.Single(a => a.Item.Kind == ExportKind.Fb);
                var hmi = artifacts.Single(a => a.Item.Kind == ExportKind.HmiScreen);

                AssertEqual("Device/CPU", plc.Item.CpuPath, "PLC target path must be parsed from the PLC layout.");
                AssertEqual("Panel/HMI_RT", hmi.Item.CpuPath, "HMI target path must be parsed from the HMI layout.");
                AssertEqual("Plant", hmi.Item.GroupPath, "HMI folder hierarchy must be preserved.");
            });
        }

        /// <summary>
        /// Verifies that repository scanning handles catalog formats beyond XML.
        /// </summary>
        private static void TestRepositoryInventoryFormats() {
            WithTempDirectory(temp => {
                WriteText(Path.Combine(temp, "PLC", "Device", "CPU", "ExternalSources", "Source__12345678.scl"), "FUNCTION FC1 : Void\r\nEND_FUNCTION\r\n");
                WriteText(Path.Combine(temp, "PLC", "Device", "CPU", "AlarmTextLists", "AlarmTexts__12345678.xlsx"), "placeholder");

                var artifacts = new RepositoryInventoryService().Scan(temp);
                var source = artifacts.Single(a => a.Item.Kind == ExportKind.PlcExternalSource);
                var alarms = artifacts.Single(a => a.Item.Kind == ExportKind.PlcAlarmTextList);

                AssertEqual(RepositoryArtifactState.New, source.State, "SCL payloads must not be validated as XML.");
                AssertEqual(RepositoryArtifactState.New, alarms.State, "XLSX payloads must not be validated as XML.");
                AssertEqual(false, source.Item.CanExport, "External source fallback items must remain non-exportable.");
                AssertEqual(false, alarms.Item.CanImport, "Provider-level alarm text lists must remain non-importable.");
            });
        }

        /// <summary>
        /// Verifies repository inventory can associate a payload with a schema 3 logical-key manifest entry.
        /// </summary>
        private static void TestRepositoryInventoryLogicalManifestKey() {
            WithTempDirectory(temp => {
                var rel = "HMI/Panel/HMI_RT/GraphicLists/Machine.xml";
                var full = Path.Combine(temp, rel.Replace("/", Path.DirectorySeparatorChar.ToString()));
                WriteXml(full);
                var hash = HashHelper.Sha256Hex(File.ReadAllBytes(full));
                var manifest = new ManifestService.Manifest();
                manifest.Entries["Panel/HMI_RT||HmiGraphicList||||Machine"] = new ManifestService.ManifestEntry {
                    Sha256 = hash,
                    ArtifactKind = ExportKind.HmiGraphicList.ToString(),
                    TargetPath = "Panel/HMI_RT",
                    GroupPath = string.Empty,
                    Name = "Machine",
                    LogicalKey = "Panel/HMI_RT||HmiGraphicList||||Machine",
                    PrimaryPath = rel,
                    Files = new List<string> { rel },
                    LastStatus = ArtifactExportStatus.Succeeded.ToString(),
                };
                new ManifestService().Save(temp, manifest);

                var artifact = new RepositoryInventoryService().Scan(temp).Single();
                AssertEqual(RepositoryArtifactState.Identical, artifact.State, "Schema 3 logical manifest keys must still match payload files.");
                AssertEqual(ExportKind.HmiGraphicList, artifact.Item.Kind, "Manifest kind must drive artifact reconstruction.");
            });
        }

        /// <summary>
        /// Verifies that manifest auxiliary payload files are not exposed as standalone artifacts.
        /// </summary>
        private static void TestCompoundManifestPayload() {
            WithTempDirectory(temp => {
                var primary = Path.Combine(temp, "HMI", "Panel", "HMI_RT", "GraphicLists", "Machine.xml");
                var auxiliary = Path.Combine(temp, "HMI", "Panel", "HMI_RT", "GraphicLists", "Machine.assets.xml");
                WriteXml(primary);
                WriteXml(auxiliary);

                var manifest = new ManifestService.Manifest();
                manifest.Entries["HMI/Panel/HMI_RT/GraphicLists/Machine.xml"] = new ManifestService.ManifestEntry {
                    Sha256 = "PRIMARY",
                    ArtifactKind = ExportKind.HmiGraphicList.ToString(),
                    TargetPath = "Panel/HMI_RT",
                    GroupPath = string.Empty,
                    Name = "Machine",
                    PrimaryPath = "HMI/Panel/HMI_RT/GraphicLists/Machine.xml",
                    Files = new List<string>
                    {
                        "HMI/Panel/HMI_RT/GraphicLists/Machine.xml",
                        "HMI/Panel/HMI_RT/GraphicLists/Machine.assets.xml",
                    },
                };
                new ManifestService().Save(temp, manifest);

                var artifacts = new RepositoryInventoryService().Scan(temp);
                AssertEqual(1, artifacts.Count, "Auxiliary files declared by the manifest must not become standalone artifacts.");
                AssertEqual(ExportKind.HmiGraphicList, artifacts[0].Item.Kind, "The primary payload must still be scanned.");
            });
        }

        /// <summary>
        /// Verifies that malformed XML files are reported as invalid repository artifacts.
        /// </summary>
        private static void TestInvalidXml() {
            WithTempDirectory(temp => {
                var path = Path.Combine(temp, "HMI", "Panel", "Screens", "Broken.xml");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "<Screen>", System.Text.Encoding.UTF8);

                var artifact = new RepositoryInventoryService().Scan(temp).Single();
                AssertEqual(RepositoryArtifactState.InvalidXml, artifact.State, "Malformed XML must be marked invalid.");
            });
        }

        /// <summary>
        /// Verifies HMI tag tables and templates are planned before screens for the same target.
        /// </summary>
        private static void TestImportDependencyPlanner() {
            WithTempDirectory(temp => {
                var screen = CreateRepositoryArtifact(temp, "Panel/HMI_RT", string.Empty, "Main", ExportKind.HmiScreen, RepositoryArtifactState.New, "<Screen />");
                var template = CreateRepositoryArtifact(temp, "Panel/HMI_RT", string.Empty, "Base", ExportKind.HmiTemplate, RepositoryArtifactState.New, "<ScreenTemplate />");
                var tags = CreateRepositoryArtifact(temp, "Panel/HMI_RT", string.Empty, "Tags", ExportKind.HmiTagTable, RepositoryArtifactState.New, "<TagTable />");

                var plan = new ImportDependencyPlanner().Build(new[] { screen, template, tags });
                var orderedKinds = plan.OrderedArtifacts.Select(a => a.Item.Kind).ToList();
                AssertTrue(orderedKinds.IndexOf(ExportKind.HmiTagTable) < orderedKinds.IndexOf(ExportKind.HmiScreen), "HMI tag tables must import before screens.");
                AssertTrue(orderedKinds.IndexOf(ExportKind.HmiTemplate) < orderedKinds.IndexOf(ExportKind.HmiScreen), "HMI templates must import before screens.");
            });
        }

        /// <summary>
        /// Verifies preflight reports duplicate ID-like attributes before Siemens import is called.
        /// </summary>
        private static void TestImportPreflightDuplicateIds() {
            WithTempDirectory(temp => {
                var artifact = CreateRepositoryArtifact(
                    temp,
                    "Panel/HMI_RT",
                    string.Empty,
                    "Main",
                    ExportKind.HmiScreen,
                    RepositoryArtifactState.New,
                    "<Screen><Item ID=\"Duplicate\" /><Item ID=\"Duplicate\" /></Screen>");
                var plan = new ImportDependencyPlanner().Build(new[] { artifact });
                var result = new ImportPreflightValidator().Validate(new[] { artifact }, plan);

                AssertTrue(result.Issues.Any(i =>
                    i.Severity == ImportValidationSeverity.Error &&
                    i.Message.IndexOf("Duplicate XML ID", StringComparison.OrdinalIgnoreCase) >= 0), "Duplicate IDs must be reported as preflight errors.");
            });
        }

        /// <summary>
        /// Verifies exception diagnostics include wrapper and inner exception details.
        /// </summary>
        private static void TestExceptionDiagnostics() {
            try {
                typeof(Program).GetMethod("ThrowInnerForFormatter", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[0]);
                throw new InvalidOperationException("Reflection invocation should have thrown.");
            } catch (TargetInvocationException ex) {
                var diagnostic = ExceptionDiagnosticFormatter.Format(ex);
                AssertContains(diagnostic, "TargetInvocationException.InnerException");
                AssertContains(diagnostic, "System.InvalidOperationException");
                AssertContains(diagnostic, "inner formatter message");
                AssertContains(diagnostic, "sample = value");

                var probeDiagnostic = ExceptionDiagnosticFormatter.Format(new FormatterProbeException("probe message"));
                AssertContains(probeDiagnostic, "PublicProperties:");
                AssertContains(probeDiagnostic, "SiemensDetail = openness detail");
                AssertContains(probeDiagnostic, "SiemensErrorCode = 7001");
            }
        }

        /// <summary>
        /// Verifies screen export uses the Openness option that avoids serializing dependent defaults.
        /// </summary>
        private static void TestHmiScreenExportOptionPolicy() {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveTestOpennessAssembly;

            var policyType = typeof(PathHelper).Assembly.GetType("EidoTiaWorkbench.Core.Utilities.OpennessExportOptionPolicy");
            AssertTrue(policyType != null, "Export option policy type must exist.");

            var method = policyType.GetMethod("ForArtifact", BindingFlags.Public | BindingFlags.Static);
            AssertTrue(method != null, "Export option policy must expose ForArtifact.");

            var screenOption = Convert.ToString(method.Invoke(null, new object[] { ExportKind.HmiScreen }));
            var templateOption = Convert.ToString(method.Invoke(null, new object[] { ExportKind.HmiTemplate }));
            AssertEqual("None", screenOption, "HMI screens must not be exported with defaults.");
            AssertEqual("WithDefaults", templateOption, "Other HMI artifacts keep the previous default export mode.");
        }

        /// <summary>
        /// Verifies export can produce screen payloads before volatile graphic-list exports are attempted.
        /// </summary>
        private static void TestExportOrderScreensBeforeGraphicLists() {
            var policyType = GetExportOrderPolicyType();
            var getOrder = policyType.GetMethod("GetOrder", BindingFlags.Public | BindingFlags.Static);
            var isFinal = policyType.GetMethod("IsFinalIsolated", BindingFlags.Public | BindingFlags.Static);
            AssertTrue(getOrder != null, "Export order policy must expose GetOrder.");
            AssertTrue(isFinal != null, "Export order policy must expose IsFinalIsolated.");

            var screenOrder = (int)getOrder.Invoke(null, new object[] { ExportKind.HmiScreen });
            var graphicListExportOrder = (int)getOrder.Invoke(null, new object[] { ExportKind.HmiGraphicList });
            AssertTrue(screenOrder < graphicListExportOrder, "Screen export must run before HMI graphic-list export.");
            AssertTrue(
                ArtifactAdapterRegistry.Get(ExportKind.HmiGraphicList).ImportOrder < ArtifactAdapterRegistry.Get(ExportKind.HmiScreen).ImportOrder,
                "Import order must still keep graphic lists before screens.");
            AssertEqual(true, (bool)isFinal.Invoke(null, new object[] { ExportKind.HmiGraphicList }), "HMI graphic lists must be final isolated exports.");
            AssertEqual(false, (bool)isFinal.Invoke(null, new object[] { ExportKind.HmiScreen }), "HMI screens must remain in the safe export phase.");
        }

        /// <summary>
        /// Verifies retry queues attempt safe pending artifacts before retrying the previous session-loss root.
        /// </summary>
        private static void TestRetryExportOrderSchedulesPreviousRootLast() {
            var screen = new ExportItem("Panel/HMI_RT", string.Empty, "00_Startscreen", ExportKind.HmiScreen, "screen-key");
            var otherGraphicList = new ExportItem("Panel/HMI_RT", string.Empty, "Other", ExportKind.HmiGraphicList, "other-graphic-key");
            var rootGraphicList = new ExportItem("Panel/HMI_RT", string.Empty, "DiagState_3", ExportKind.HmiGraphicList, "root-graphic-key");

            var ordered = OrderForExport(new[] { rootGraphicList, otherGraphicList, screen }, rootGraphicList.LogicalKey);

            AssertEqual(screen.LogicalKey, ordered[0].LogicalKey, "Safe retry items must be attempted before final risky artifacts.");
            AssertEqual(rootGraphicList.LogicalKey, ordered[ordered.Count - 1].LogicalKey, "Previous session-loss root must be retried last.");
        }

        /// <summary>
        /// Verifies the reflective export helper selects the exact FileInfo/ExportOptions overload and policy option.
        /// </summary>
        private static void TestReflectiveExportExactOverloadAndOption() {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveTestOpennessAssembly;

            WithTempDirectory(temp => {
                var probe = new ReflectiveExportProbe();
                var logger = new CaptureLogger();
                var destination = new FileInfo(Path.Combine(temp, "probe.xml"));
                var method = typeof(ExportService).GetMethod("InvokeExport", BindingFlags.NonPublic | BindingFlags.Static);
                AssertTrue(method != null, "Reflective export helper must exist.");

                method.Invoke(null, new object[] { probe, destination, ExportKind.HmiScreen, logger });

                AssertEqual("exact", probe.SelectedMethod, "Exact Export(FileInfo, ExportOptions) overload must be selected.");
                AssertEqual(ExportOptions.None, probe.SelectedOption, "Reflective export must use the per-artifact export option policy.");
                AssertTrue(File.Exists(destination.FullName), "Reflective export probe must receive the destination file.");
                AssertTrue(logger.Infos.Any(m => m.IndexOf("ExportKind=HmiScreen", StringComparison.OrdinalIgnoreCase) >= 0), "Reflective export log must include the artifact kind.");
                AssertTrue(logger.Infos.Any(m => m.IndexOf("Siemens.Engineering.ExportOptions", StringComparison.OrdinalIgnoreCase) >= 0), "Reflective export log must include the exact method signature.");
                AssertTrue(logger.Infos.Any(m => m.IndexOf("ExportOption=None", StringComparison.OrdinalIgnoreCase) >= 0), "Reflective export log must include the actual option.");
            });
        }

        /// <summary>
        /// Resolves Siemens Openness assemblies for tests that execute methods returning Openness enum values.
        /// </summary>
        /// <param name="sender">Assembly resolve sender.</param>
        /// <param name="args">Assembly resolution arguments.</param>
        /// <returns>The resolved assembly, or null when this resolver does not handle it.</returns>
        private static Assembly ResolveTestOpennessAssembly(object sender, ResolveEventArgs args) {
            var assemblyName = new AssemblyName(args.Name).Name;
            if (assemblyName == null ||
                !assemblyName.StartsWith("Siemens.Engineering", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            var opennessDir = Environment.GetEnvironmentVariable("TIA_OPENNESS_V21");
            if (string.IsNullOrWhiteSpace(opennessDir)) {
                opennessDir = @"C:\Program Files\Siemens\Automation\Portal V21\PublicAPI\V21\net48";
            }

            var candidate = Path.Combine(opennessDir, assemblyName + ".dll");
            return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
        }

        /// <summary>
        /// Verifies export result counters and global run status are derived from artifact records.
        /// </summary>
        private static void TestExportResultStatusModel() {
            var item = new ExportItem("Device/CPU", "Blocks", "FB1", ExportKind.Fb, "fb1");
            var result = new ExportResult();
            result.Artifacts.Add(new ArtifactExportRecord(item, ArtifactExportStatus.Succeeded));
            result.Artifacts.Add(new ArtifactExportRecord(item, ArtifactExportStatus.Restricted));
            result.Recalculate();
            AssertEqual(ExportRunStatus.CompletedWithIssues, result.Status, "Restrictions must produce a completed run with issues.");

            result.Artifacts.Add(new ArtifactExportRecord(item, ArtifactExportStatus.AbortedSessionLost));
            result.Recalculate();
            AssertEqual(ExportRunStatus.Aborted, result.Status, "Non-final session loss must dominate the global status.");
        }

        /// <summary>
        /// Verifies final isolated GraphicList session loss does not recast already exported safe payloads as a failed run.
        /// </summary>
        private static void TestFinalGraphicListSessionLossCompletesWithIssues() {
            var screen = new ExportItem("Panel/HMI_RT", string.Empty, "00_Startscreen", ExportKind.HmiScreen, "screen-key");
            var graphicList = new ExportItem("Panel/HMI_RT", string.Empty, "DiagState_3", ExportKind.HmiGraphicList, "graphic-key");
            var screenRecord = new ArtifactExportRecord(screen, ArtifactExportStatus.Succeeded) {
                PrimaryPath = "HMI/Panel/HMI_RT/Screens/00_Startscreen.xml",
                Hash = "HASH",
            };
            var graphicRecord = new ArtifactExportRecord(graphicList, ArtifactExportStatus.AbortedSessionLost) {
                Message = "The TIA Portal session or project was lost while exporting this artifact.",
                SessionFailureKey = graphicList.LogicalKey,
            };
            var result = new ExportResult();
            result.Artifacts.Add(screenRecord);
            result.Artifacts.Add(graphicRecord);
            result.FirstSessionFailure = graphicRecord;

            result.Recalculate();

            AssertEqual(ExportRunStatus.CompletedWithIssues, result.Status, "Final isolated GraphicList session loss must leave the safe export phase complete with issues.");
            AssertEqual(1, result.Exported, "Previously exported screen payload must remain counted as exported.");
            AssertEqual("HMI/Panel/HMI_RT/Screens/00_Startscreen.xml", screenRecord.PrimaryPath, "Screen payload metadata must remain attached to the success record.");
        }

        /// <summary>
        /// Verifies a failed final artifact updates status metadata without erasing its previous valid payload entry.
        /// </summary>
        private static void TestManifestPreservesPayloadOnFailedFinalArtifact() {
            var item = new ExportItem("Panel/HMI_RT", string.Empty, "DiagState_3", ExportKind.HmiGraphicList, "graphic-key");
            var key = ManifestService.GetArtifactKey(item);
            var existing = new ManifestService.ManifestEntry {
                LogicalKey = item.LogicalKey,
                ArtifactKind = item.Kind.ToString(),
                Sha256 = "OLDHASH",
                PrimaryPath = "HMI/Panel/HMI_RT/GraphicLists/DiagState_3.xml",
                Files = new List<string> { "HMI/Panel/HMI_RT/GraphicLists/DiagState_3.xml" },
                FileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "HMI/Panel/HMI_RT/GraphicLists/DiagState_3.xml", "OLDHASH" },
                },
                AggregateHash = "OLDHASH",
                LastStatus = ArtifactExportStatus.Succeeded.ToString(),
            };
            var manifest = new ManifestService.Manifest();
            manifest.Entries[key] = existing;
            var failed = new ArtifactExportRecord(item, ArtifactExportStatus.AbortedSessionLost) {
                Message = "The TIA Portal session or project was lost while exporting this artifact.",
                IsRetryable = true,
                IsTransient = true,
                SessionFailureKey = item.LogicalKey,
            };
            var method = typeof(ExportService).GetMethod("UpsertManifestEntry", BindingFlags.NonPublic | BindingFlags.Static);
            AssertTrue(method != null, "Manifest upsert helper must exist.");

            method.Invoke(null, new object[] { manifest, item, failed, existing, key });

            var updated = manifest.Entries[key];
            AssertEqual("OLDHASH", updated.Sha256, "Failed final export must not erase the previous payload hash.");
            AssertEqual("HMI/Panel/HMI_RT/GraphicLists/DiagState_3.xml", updated.PrimaryPath, "Failed final export must not erase the previous payload path.");
            AssertEqual(ArtifactExportStatus.AbortedSessionLost.ToString(), updated.LastStatus, "Failed final export must still update the last status.");
        }

        /// <summary>
        /// Verifies preflight exposes the dedicated HMI GraphicList handler and capability trace.
        /// </summary>
        private static void TestExportPreflightGraphicListHandler() {
            var item = new ExportItem("Panel/HMI_RT", "GraphicLists", "DiagState_3", ExportKind.HmiGraphicList, "hmi-graphic-list");
            var descriptor = ArtifactCatalog.Get(item.Kind);
            var adapter = ArtifactAdapterRegistry.Get(item.Kind);
            var preflight = ArtifactExportPreflight.Evaluate(item, descriptor, adapter);

            AssertTrue(preflight.CanAttemptExport, "Graphic lists remain export-attemptable when catalog and adapter agree.");
            AssertEqual("HmiGraphicListExporter", preflight.ExporterName, "Graphic lists must use the explicit handler.");
            AssertContains(preflight.CapabilityDetail, "adapter.CanExport=True");
        }

        /// <summary>
        /// Verifies protected block messages remain restrictions instead of generic failures.
        /// </summary>
        private static void TestFailureClassifierRestricted() {
            var classification = ExportFailureClassifier.Classify(
                new InvalidOperationException("The export of block 'FOB_RTG1' is not permitted."),
                TiaSessionHealth.Healthy("Project"));
            AssertEqual(ArtifactExportStatus.Restricted, classification.Status, "Protected exports must be classified as restricted.");
            AssertEqual(false, classification.IsRetryable, "Restricted artifacts must not be retryable by default.");
        }

        /// <summary>
        /// Verifies HMI GraphicList export exceptions stay artifact-local when session health is valid.
        /// </summary>
        private static void TestFailureClassifierGraphicListIsolated() {
            var item = new ExportItem("Panel/HMI_RT", "GraphicLists", "DiagState_3", ExportKind.HmiGraphicList, "graphic-list-key");
            var exportException = new InvalidOperationException("Siemens.Engineering.Hmi.TextGraphicList.GraphicList.Export failed.");
            var classification = ExportFailureClassifier.Classify(exportException, TiaSessionHealth.Healthy("Project"), item);

            AssertEqual(ArtifactExportStatus.Failed, classification.Status, "GraphicList failure with healthy session must not abort the queue.");
            AssertEqual(true, classification.IsRetryable, "Artifact-local GraphicList failures may be retried.");
            AssertContains(classification.Message, "GraphicList exporter");
            AssertEqual(false, ExportFailureIsolationPolicy.ShouldAbortQueue(classification), "Artifact-local GraphicList failures must not cascade.");
        }

        /// <summary>
        /// Verifies a failed artifact followed by unhealthy session is classified as session-loss abort.
        /// </summary>
        private static void TestFailureClassifierSessionLost() {
            var health = TiaSessionHealth.Unhealthy(
                TiaSessionHealthStatus.ProjectReferenceInvalid,
                "The project reference could not be validated.",
                new InvalidOperationException("No project is open."));
            var classification = ExportFailureClassifier.Classify(new InvalidOperationException("Error when calling method 'Export'."), health);
            AssertEqual(ArtifactExportStatus.AbortedSessionLost, classification.Status, "Unhealthy session after failure must abort the run.");
            AssertEqual(true, classification.IsRetryable, "Session-loss aborts are retryable after reopening the project.");
        }

        /// <summary>
        /// Verifies the queue-level policy attempts recovery at most once for confirmed session loss.
        /// </summary>
        private static void TestFailureIsolationPolicyRecoveryOnce() {
            var health = TiaSessionHealth.Unhealthy(
                TiaSessionHealthStatus.ProjectReferenceInvalid,
                "The project reference could not be validated.",
                new InvalidOperationException("No project is open."));
            var classification = ExportFailureClassifier.Classify(new InvalidOperationException("Error when calling method 'Export'."), health);

            AssertTrue(ExportFailureIsolationPolicy.ShouldAttemptControlledRecovery(classification, false), "First session-loss classification should spend recovery.");
            AssertEqual(false, ExportFailureIsolationPolicy.ShouldAttemptControlledRecovery(classification, true), "Recovery must not be attempted twice.");
            AssertTrue(ExportFailureIsolationPolicy.ShouldAbortQueue(classification), "Unrecovered session loss must abort the queue.");
        }

        /// <summary>
        /// Verifies cancelling a queued STA work item does not complete the caller before the STA worker reaches it.
        /// </summary>
        private static void TestStaWorkerCancellationWaitsForQueuedWork() {
            using (var worker = new StaWorker("TIA test STA worker"))
            using (var blockerEntered = new ManualResetEventSlim(false))
            using (var releaseBlocker = new ManualResetEventSlim(false))
            using (var cancellation = new CancellationTokenSource()) {
                var blocker = worker.InvokeAsync(() => {
                    blockerEntered.Set();
                    releaseBlocker.Wait();
                    return true;
                });
                AssertTrue(blockerEntered.Wait(5000), "Blocking STA work item must start.");

                var executed = false;
                var queued = worker.InvokeAsync(() => {
                    executed = true;
                    return 1;
                }, cancellation.Token);

                cancellation.Cancel();
                AssertEqual(false, queued.IsCompleted, "Queued cancellation must not complete before STA reaches the item.");

                releaseBlocker.Set();
                try {
                    queued.GetAwaiter().GetResult();
                    throw new InvalidOperationException("Cancelled queued work should not run.");
                } catch (OperationCanceledException) {
                    // Expected.
                }

                blocker.GetAwaiter().GetResult();
                AssertEqual(false, executed, "Cancelled queued STA work must be skipped before executing user code.");
            }
        }

        /// <summary>
        /// Verifies transactional promotion does not overwrite a valid previous payload when validation fails.
        /// </summary>
        private static void TestTransactionalWriterPreservesPreviousPayload() {
            WithTempDirectory(temp => {
                var path = Path.Combine(temp, "payload.xml");
                File.WriteAllText(path, "<?xml version=\"1.0\"?><Document />", System.Text.Encoding.UTF8);
                var before = File.ReadAllText(path);
                try {
                    TransactionalFileWriter.PromoteUtf8Text(path, "<Broken>", ArtifactFormat.Xml);
                    throw new InvalidOperationException("Invalid XML promotion must fail.");
                } catch (Exception ex) {
                    AssertContains(ex.GetType().FullName, "XmlException");
                }

                AssertEqual(before, File.ReadAllText(path), "Previous payload must remain intact after failed promotion.");
                AssertEqual(0, Directory.GetFiles(temp, "*.tmp").Length, "Temporary files must be cleaned after failed promotion.");
            });
        }

        /// <summary>
        /// Verifies anonymized report counts against the catalog support model.
        /// </summary>
        private static void TestCoverageRegressionFixture() {
            var counts = new Dictionary<ExportKind, int>
            {
                { ExportKind.Fb, 110 },
                { ExportKind.Udt, 83 },
                { ExportKind.PlcTagTable, 28 },
                { ExportKind.HmiScreen, 44 },
                { ExportKind.HmiCycle, 19 },
                { ExportKind.HmiTemplate, 9 },
                { ExportKind.HmiTagTable, 8 },
                { ExportKind.HmiPopup, 7 },
                { ExportKind.HmiTextList, 4 },
                { ExportKind.HmiGraphicList, 3 },
                { ExportKind.HmiVBScript, 1 },
                { ExportKind.HmiGlobalElements, 1 },
                { ExportKind.HmiScreenOverview, 1 },
                { ExportKind.PlcWatchTable, 1 },
                { ExportKind.PlcForceTable, 1 },
                { ExportKind.PlcExternalSource, 2 },
                { ExportKind.Ob, 1 },
            };

            AssertEqual(323, counts.Values.Sum(), "Coverage fixture must match the operation report inventory size.");

            foreach (var pair in counts) {
                var descriptor = ArtifactCatalog.Get(pair.Key);
                var adapter = ArtifactAdapterRegistry.Get(pair.Key);
                if (descriptor.CanExport) {
                    AssertTrue(adapter.CanExport, "Coverage exportable kind must have adapter support: " + pair.Key);
                }
            }

            AssertEqual(false, ArtifactCatalog.Get(ExportKind.PlcExternalSource).CanExport, "External sources must be classified as unavailable unless public API export is confirmed.");
            AssertEqual(true, ArtifactCatalog.Get(ExportKind.PlcWatchTable).CanImport, "Watch tables must be import-capable.");
            AssertEqual(true, ArtifactCatalog.Get(ExportKind.PlcForceTable).CanExport, "Force tables must be export-capable.");
        }

        /// <summary>
        /// Verifies that conflict policy names remain stable for persisted UI choices.
        /// </summary>
        private static void TestConflictPolicy() {
            var values = Enum.GetNames(typeof(ImportConflictPolicy));
            AssertTrue(values.SequenceEqual(new[] { "Ask", "Skip", "Replace" }), "Conflict policies must remain explicit and ordered.");
        }

        /// <summary>
        /// Reflects the internal export order policy type from the core assembly.
        /// </summary>
        /// <returns>The export order policy type.</returns>
        private static Type GetExportOrderPolicyType() {
            var policyType = typeof(ExportService).Assembly.GetType("EidoTiaWorkbench.Core.Export.ExportOrderPolicy");
            AssertTrue(policyType != null, "Export order policy type must exist.");
            return policyType;
        }

        /// <summary>
        /// Orders export items through the internal policy for regression tests.
        /// </summary>
        /// <param name="items">Items to order.</param>
        /// <param name="previousSessionFailureKey">Previous session-loss root key.</param>
        /// <returns>Ordered export items.</returns>
        private static List<ExportItem> OrderForExport(IEnumerable<ExportItem> items, string previousSessionFailureKey) {
            var method = GetExportOrderPolicyType().GetMethod("OrderForExport", BindingFlags.Public | BindingFlags.Static);
            AssertTrue(method != null, "Export order policy must expose OrderForExport.");
            return ((IEnumerable<ExportItem>)method.Invoke(null, new object[] { items, previousSessionFailureKey })).ToList();
        }

        /// <summary>
        /// Verifies that one artifact kind maps to the expected repository route fragment.
        /// </summary>
        /// <param name="kind">Artifact kind to route through PathHelper.</param>
        /// <param name="expectedFragment">Expected normalized path fragment.</param>
        private static void AssertRoute(ExportKind kind, string expectedFragment) {
            var target = ArtifactCatalog.Get(kind).Domain == ArtifactDomain.HmiClassic
                ? "Panel/HMI_RT"
                : "Device/CPU";
            var item = new ExportItem(target, "Group", "Artifact", kind, "route-" + kind);
            var path = PathHelper.GetOutputFilePath("C:\\Repo", item).Replace("\\", "/");
            AssertContains(path, expectedFragment);
        }

        /// <summary>
        /// Writes a minimal XML file for repository inventory tests.
        /// </summary>
        /// <param name="path">File path to create.</param>
        private static void WriteXml(string path) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?><Document />", System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Creates a repository artifact backed by a temporary XML file.
        /// </summary>
        /// <param name="root">Temporary repository root.</param>
        /// <param name="target">Artifact target path.</param>
        /// <param name="group">Artifact group path.</param>
        /// <param name="name">Artifact name.</param>
        /// <param name="kind">Artifact kind.</param>
        /// <param name="state">Repository comparison state.</param>
        /// <param name="xml">XML payload to write.</param>
        /// <returns>A repository artifact model.</returns>
        private static RepositoryArtifact CreateRepositoryArtifact(
            string root,
            string target,
            string group,
            string name,
            ExportKind kind,
            RepositoryArtifactState state,
            string xml) {
            var relative = kind + "/" + name + ".xml";
            var full = Path.Combine(root, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, xml, System.Text.Encoding.UTF8);
            var key = target + "||" + kind + "||" + group + "||" + name;
            var item = new ExportItem(target, group, name, kind, key);
            return new RepositoryArtifact(item, full, relative, state, HashHelper.Sha256Hex(File.ReadAllBytes(full)), null, null);
        }

        /// <summary>
        /// Throws an inner exception through reflection for diagnostic formatter coverage.
        /// </summary>
        private static void ThrowInnerForFormatter() {
            var exception = new InvalidOperationException("inner formatter message");
            exception.Data["sample"] = "value";
            throw exception;
        }

        /// <summary>
        /// Provides extra public properties similar to Siemens Openness exception types.
        /// </summary>
        private sealed class FormatterProbeException : Exception {
            /// <summary>
            /// Initializes a new instance of the <see cref="FormatterProbeException"/> class.
            /// </summary>
            /// <param name="message">Exception message.</param>
            public FormatterProbeException(string message)
                : base(message) {
            }

            /// <summary>
            /// Gets a sample Siemens-style diagnostic detail.
            /// </summary>
            public string SiemensDetail {
                get { return "openness detail"; }
            }

            /// <summary>
            /// Gets a sample Siemens-style diagnostic code.
            /// </summary>
            public int SiemensErrorCode {
                get { return 7001; }
            }
        }

        private enum ProbeExportOptions {
            /// <summary>
            /// Probe-only option that must not match the Siemens overload.
            /// </summary>
            WithDefaults = 1,
        }

        /// <summary>
        /// Probe object with competing Export overloads for the reflective export helper.
        /// </summary>
        private sealed class ReflectiveExportProbe {
            /// <summary>
            /// Gets a Siemens-like public artifact name for diagnostics.
            /// </summary>
            public string Name {
                get { return "ReflectiveProbe"; }
            }

            /// <summary>
            /// Gets the overload that was selected by reflection.
            /// </summary>
            public string SelectedMethod {
                get; private set;
            }

            /// <summary>
            /// Gets the Openness export option passed to the exact overload.
            /// </summary>
            public ExportOptions SelectedOption {
                get; private set;
            }

            /// <summary>
            /// Wrong two-argument overload that must not be selected.
            /// </summary>
            /// <param name="destination">Export destination.</param>
            /// <param name="options">Probe-specific options.</param>
            public void Export(FileInfo destination, ProbeExportOptions options) {
                this.SelectedMethod = "wrong";
            }

            /// <summary>
            /// Exact Openness overload that must be selected.
            /// </summary>
            /// <param name="destination">Export destination.</param>
            /// <param name="options">Openness export options.</param>
            public void Export(FileInfo destination, ExportOptions options) {
                this.SelectedMethod = "exact";
                this.SelectedOption = options;
                File.WriteAllText(destination.FullName, "<?xml version=\"1.0\"?><Document />", System.Text.Encoding.UTF8);
            }

            /// <summary>
            /// File-only fallback that must not be preferred over the exact overload.
            /// </summary>
            /// <param name="destination">Export destination.</param>
            public void Export(FileInfo destination) {
                this.SelectedMethod = "file-only";
            }
        }

        /// <summary>
        /// Captures log lines emitted by core services during tests.
        /// </summary>
        private sealed class CaptureLogger : ILogger {
            /// <summary>
            /// Gets informational messages written by the system under test.
            /// </summary>
            public List<string> Infos { get; } = new List<string>();

            /// <inheritdoc />
            public void Info(string message) {
                this.Infos.Add(message);
            }

            /// <inheritdoc />
            public void Warn(string message) {
            }

            /// <inheritdoc />
            public void Error(string message, Exception ex = null) {
            }
        }

        /// <summary>
        /// Writes a text file for repository inventory tests.
        /// </summary>
        /// <param name="path">File path to create.</param>
        /// <param name="content">Text content to write.</param>
        private static void WriteText(string path, string content) {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Creates a temporary directory, runs a test action, and removes the directory afterwards.
        /// </summary>
        /// <param name="action">Test action that receives the temporary directory path.</param>
        private static void WithTempDirectory(Action<string> action) {
            var temp = Path.Combine(Path.GetTempPath(), "EidoTiaWorkbench.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            try {
                action(temp);
            } finally {
                if (Directory.Exists(temp)) {
                    Directory.Delete(temp, recursive: true);
                }
            }
        }

        /// <summary>
        /// Throws when a condition is false.
        /// </summary>
        /// <param name="condition">Condition expected to be true.</param>
        /// <param name="message">Failure message.</param>
        private static void AssertTrue(bool condition, string message) {
            if (!condition) {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Throws when two values are not equal.
        /// </summary>
        /// <typeparam name="T">Compared value type.</typeparam>
        /// <param name="expected">Expected value.</param>
        /// <param name="actual">Actual value.</param>
        /// <param name="message">Failure message.</param>
        private static void AssertEqual<T>(T expected, T actual, string message) {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
                throw new InvalidOperationException(message + " Expected: " + expected + ". Actual: " + actual + ".");
            }
        }

        /// <summary>
        /// Throws when a text value does not contain the expected fragment.
        /// </summary>
        /// <param name="text">Text to inspect.</param>
        /// <param name="expectedFragment">Fragment expected to be present.</param>
        private static void AssertContains(string text, string expectedFragment) {
            if (text == null || text.IndexOf(expectedFragment, StringComparison.OrdinalIgnoreCase) < 0) {
                throw new InvalidOperationException("Expected fragment was not found. Fragment: " + expectedFragment + ". Text: " + text + ".");
            }
        }
    }
}
