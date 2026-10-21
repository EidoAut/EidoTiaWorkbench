// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// ModernizationTests.cs
// Regression tests for safe IPC, selection and non-destructive import decisions; no Siemens session needed.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Integration;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Tests {
    /// <summary>Regression tests for safe IPC, selection and non-destructive import decisions; no Siemens session needed.</summary>
    internal static class ModernizationTests {
        /// <summary>Verifies discovery, protocol, import policy and atomic-file contracts using temporary artifacts.</summary>
        public static void Run() {
            foreach (var version in new[] { 17, 18, 19, 20, 21 }) {
                Check(OpennessDiscovery.ParseVersion("V" + version + ".0") == version, "Parse API version");
                Check(OpennessDiscovery.ParseVersion(@"C:\Siemens\Portal V" + version + @"\PublicAPI") == version, "Parse installation path");
            }

            Check(OpennessDiscovery.ParseVersion("garbage") == 0, "Unknown versions stay unknown");
            Check(OpennessDiscovery.ParseVersion("21.0.0.0") == 21, "Assembly version provenance is parsed");
            var sta = new StaWorker();
            var queued = sta.InvokeAsync(() => 42);
            sta.Dispose();
            Check(queued.GetAwaiter().GetResult() == 42, "STA shutdown drains admitted work");
            var root = Path.Combine(Path.GetTempPath(), "EidoModernization-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                Check(!OpennessDiscovery.Validate(root).IsValid, "An empty directory is not an API");
                Check(!OpennessDiscovery.Validate(null).IsValid, "Missing path is reported without crashing discovery");
                File.WriteAllText(Path.Combine(root, "Siemens.Engineering.Base.dll"), "not an assembly");
                Check(!OpennessDiscovery.Validate(root).CanUse, "Invalid DLL rejected");
                var v20 = new OpennessInstallation { MajorVersion = 20, ApiPath = "legacy", IsValid = true };
                var v21 = new OpennessInstallation { MajorVersion = 21, ApiPath = "current", IsValid = true };
                var settings = new IntegrationSettings();
                Check(settings.Select(new[] { v20, v21 }) == v21, "Automatic chooses implemented adapter");
                settings.SelectedApiPath = "legacy";
                Check(settings.Select(new[] { v20, v21 }) == null, "Manual unsupported selection never falls back");
                settings.CustomPath = root;
                var configuration = Path.Combine(root, "settings.xml");
                settings.Save(configuration);
                settings.Save(configuration);
                Check(IntegrationSettings.Load(configuration).CustomPath == root, "Settings persist atomically");
                foreach (ImportConflictPolicy policy in Enum.GetValues(typeof(ImportConflictPolicy))) {
                    Check(ImportConflictDecision.ShouldImport(false, policy), "New objects allowed by explicit policies");
                }

                Check(!ImportConflictDecision.ShouldImport(true, ImportConflictPolicy.Skip), "Skip never replaces");
                Check(ImportConflictDecision.ShouldImport(true, ImportConflictPolicy.Replace), "Replace explicit");
                Throws<InvalidOperationException>(() => ImportConflictDecision.ShouldImport(true, ImportConflictPolicy.Ask));
                Throws<ArgumentOutOfRangeException>(() => ImportConflictDecision.ShouldImport(true, (ImportConflictPolicy)99));

                var item = new ExportItem("Device/CPU", "Blocks", "FB1", ExportKind.Fb, "test-key");
                item.Metadata["Origin"] = "V21";
                var artifact = new RepositoryArtifact(item, Path.Combine(root, "test.xml"), "test.xml", RepositoryArtifactState.New, "hash", string.Empty, string.Empty);
                var export = new ExportResult();
                export.Artifacts.Add(new ArtifactExportRecord(item, ArtifactExportStatus.Failed));
                export.Errors.Add(new ExportError(item, new IOException("Export failed", new Exception("inner detail"))));
                export.Warnings.Add(new ExportWarning(item, new InvalidOperationException("warning")));
                var import = new ImportResult();
                import.Errors.Add(new ImportError(item, new InvalidOperationException("Import failed")));
                var message = new WorkerMessage {
                    Command = "Result",
                    Items = new List<ExportItem> { item },
                    Artifacts = new List<RepositoryArtifact> { artifact },
                    Export = export,
                    Import = import,
                    Project = new TiaProjectInfo { ProjectName = "Test" },
                    Instances = new List<TiaPortalInstanceInfo> { new TiaPortalInstanceInfo(42, 42, "Test", string.Empty) },
                };
                message.Project.PlcTargets.Add("Device/CPU");
                using (var buffer = new MemoryStream()) {
                    WorkerMessage.Write(buffer, message);
                    buffer.Position = 0;
                    var reply = WorkerMessage.Read(buffer);
                    Check(reply.Items[0].LogicalKey == "test-key", "Immutable identity survives IPC");
                    Check(reply.Items[0].Metadata["Origin"] == "V21", "Metadata survives IPC");
                    Check(reply.Export.Errors[0].Exception.Message.Contains("inner detail"), "Diagnostic chain survives IPC");
                    Check(reply.Project.PlcTargets.Count == 1, "Target collections survive IPC");
                    Check(reply.Artifacts[0].RepositoryPath == artifact.RepositoryPath, "Import path survives IPC");
                }

                using (var buffer = new MemoryStream(BitConverter.GetBytes(-1))) {
                    Throws<InvalidDataException>(() => WorkerMessage.Read(buffer));
                }

                using (var buffer = new MemoryStream()) {
                    WorkerMessage.Write(buffer, new WorkerMessage { Protocol = 9 });
                    buffer.Position = 0;
                    Throws<InvalidDataException>(() => WorkerMessage.Read(buffer));
                }

                File.WriteAllText(artifact.RepositoryPath, "<Document><Engineering version=\"V20\" /></Document>");
                Throws<InvalidDataException>(() => ImportVersionGuard.Validate(artifact.RepositoryPath, 21));
                File.WriteAllText(artifact.RepositoryPath, "<Document><Engineering version=\"V21\" /></Document>");
                ImportVersionGuard.Validate(artifact.RepositoryPath, 21);
                var manifestService = new ManifestService();
                var manifest = new ManifestService.Manifest { TiaVersion = "V20" };
                manifestService.Save(root, manifest);
                Throws<InvalidDataException>(() => ImportVersionGuard.Validate(artifact.RepositoryPath, 21));
                var previous = File.ReadAllBytes(manifestService.GetManifestPath(root));
                using (var locked = new FileStream(manifestService.GetManifestPath(root), FileMode.Open, FileAccess.Read, FileShare.None)) {
                    Throws<IOException>(() => manifestService.Save(root, new ManifestService.Manifest()));
                }

                Check(previous.SequenceEqual(File.ReadAllBytes(manifestService.GetManifestPath(root))), "Failed promotion preserves previous manifest bytes");
                Check(!Directory.GetFiles(Path.Combine(root, "meta"), "*.tmp").Any(), "No manifest staging files remain");
                Check(!typeof(ExportItem).Assembly.GetReferencedAssemblies().Any(x => x.Name.StartsWith("Siemens.")), "Core has no Siemens dependency");
                using (var client = new OpennessWorkerClient(null, NullLogger.Instance)) {
                    Throws<NotSupportedException>(() => client.Execute(new WorkerMessage { Command = "Start" }, CancellationToken.None));
                }
            }
            finally {
                Directory.Delete(root, true);
            }
        }

        /// <summary>Verifies that a worker can load the installed API and report an empty session without loading Siemens in the caller.</summary>
        public static void WorkerSmoke() {
            var installation = new IntegrationSettings().Select(new OpennessDiscovery().Discover());
            Check(installation != null, "Installed V21 API is validated");
            using (var client = new OpennessWorkerClient(installation, NullLogger.Instance)) {
                var project = client.GetProjectInfoAsync().GetAwaiter().GetResult();
                Check(!project.IsPortalOpen, "New worker owns no portal");
            }

            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("Siemens.")), "Worker client never loads Siemens into the caller");
        }

        /// <summary>Starts and closes a fresh owned portal without opening any user project.</summary>
        public static void WorkerLifecycle() {
            var installation = new IntegrationSettings().Select(new OpennessDiscovery().Discover());
            using (var client = new OpennessWorkerClient(installation, new LifecycleLogger())) {
                Console.WriteLine("Starting a fresh owned TIA process without a project.");
                client.StartPortalAsync(false).GetAwaiter().GetResult();
                try {
                    var snapshot = client.GetProjectInfoAsync().GetAwaiter().GetResult();
                    Check(snapshot.IsPortalOpen && snapshot.OwnsPortal, "Worker owns the fresh portal");
                    Check(!snapshot.IsProjectOpen, "No production project was opened");
                }
                finally {
                    client.DisconnectAsync(true).GetAwaiter().GetResult();
                }

                Check(!client.IsPortalOpen, "Owned portal closed cleanly");
            }
        }

        private static void Check(bool condition, string message) {
            if (!condition) {
                throw new InvalidOperationException(message);
            }
        }

        private static void Throws<T>(Action action)
            where T : Exception {
            try {
                action();
            }
            catch (T) {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        private sealed class LifecycleLogger : ILogger {
            public void Info(string message) => Console.WriteLine(message);

            public void Warn(string message) => Console.WriteLine("WARN " + message);

            public void Error(string message, Exception ex = null) => Console.WriteLine(message + (ex == null ? string.Empty : " " + ex));
        }
    }
}
