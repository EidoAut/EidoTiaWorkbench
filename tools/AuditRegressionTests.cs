// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// AuditRegressionTests.cs
// Exercises audit regressions using isolated repositories and pipe fixtures, without Siemens.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EidoTiaWorkbench.Application;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Integration;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

/// <summary>Reproduces audit failures against isolated files, session doubles and child processes.</summary>
internal static class AuditRegressionTests {
    /// <summary>Runs regressions or handles a child pipe fixture without loading Siemens.</summary>
    /// <param name="args">Fixture mode, or an empty array for the regression suite.</param>
    public static void Run(string[] args) {
        if (args.Length > 0 && args[0] == "--fixture-worker") {
            RunFixtureWorker();
            return;
        }

        if (args.Length > 0 && args[0] == "--fixture-exit") {
            var request = WorkerMessage.Read(Console.OpenStandardInput());
            WorkerMessage.Write(Console.OpenStandardOutput(), new WorkerMessage {
                Command = "Result",
                RequestId = request.RequestId,
                Project = new TiaProjectInfo { IsPortalOpen = true },
            });
            return;
        }

        using (var legacy = new MemoryStream()) {
            WorkerMessage.Write(legacy, new WorkerMessage { Command = "Ready", Protocol = 1 });
            legacy.Position = 0;
            Expect<InvalidDataException>(() => WorkerMessage.Read(legacy));
        }

        var root = Path.Combine(Path.GetTempPath(), "EidoAudit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "PLC", "CPU", "Blocks"));
        Directory.CreateDirectory(Path.Combine(root, "meta"));
        Console.WriteLine("Fixture: " + root);
        var payload = Path.Combine(root, "PLC", "CPU", "Blocks", "FB1.xml");
        var xml = "<Document><Engineering version=\"V20\"/><SW.Blocks.FB ID=\"0\">" +
            "<AttributeList><Name>FB1</Name></AttributeList></SW.Blocks.FB></Document>";
        File.WriteAllText(payload, xml);
        File.WriteAllText(Path.Combine(root, "meta", "manifest.json"), "{broken");
        Expect<System.Runtime.Serialization.SerializationException>(() => new RepositoryInventoryService().Scan(root));
        File.Delete(Path.Combine(root, "meta", "manifest.json"));
        var inventory = new RepositoryInventoryService().Scan(root);
        var plan = new ImportDependencyPlanner().Build(inventory);
        Check(new ImportPreflightValidator().Validate(inventory, plan, 21).HasBlockingErrors, "V20 is blocked for V21");
        Check(new ImportPreflightValidator().Validate(inventory, plan).WarningCount > 0, "Unknown target warns");
        File.WriteAllText(payload, xml.Replace("V20", "V21"));
        Check(!new ImportPreflightValidator().Validate(inventory, plan, 21).HasBlockingErrors, "Matching version accepted");
        var entry = new ManifestService.ManifestEntry {
            PrimaryPath = "primary.xml",
            Files = new List<string> { "primary.xml", "secondary.xml" },
        };
        Check(!ManifestService.HasVerifiedPayload(root, entry), "Missing primary is not skipped");
        var primary = Path.Combine(root, "primary.xml");
        File.WriteAllText(primary, "original");
        entry.Sha256 = HashHelper.Sha256Hex(File.ReadAllBytes(primary));
        Check(!ManifestService.HasVerifiedPayload(root, entry), "Missing secondary is not skipped");
        File.WriteAllText(Path.Combine(root, "secondary.xml"), "secondary");
        Check(ManifestService.HasVerifiedPayload(root, entry), "Complete verified payload is skipped");
        File.WriteAllText(primary, "local edits");
        Expect<InvalidDataException>(() => ManifestService.HasVerifiedPayload(root, entry));
        Check(File.ReadAllText(primary) == "local edits", "Local edits preserved");
        var boundRoot = Path.Combine(root, "bound");
        Directory.CreateDirectory(boundRoot);
        var bound = new ManifestService.Manifest();
        ManifestService.BindProject(boundRoot, bound, Path.Combine(root, "A", "Project.ap21"));
        new ManifestService().Save(boundRoot, bound);
        bound = new ManifestService().Load(boundRoot);
        ManifestService.BindProject(boundRoot, bound, Path.Combine(root, "A", "Project.ap21"));
        Expect<InvalidDataException>(() => ManifestService.BindProject(boundRoot, bound, Path.Combine(root, "B", "Project.ap21")));
        Expect<InvalidDataException>(() => ManifestService.BindProject(root, new ManifestService.Manifest(), "A.ap21"));
        var missingRoot = Path.Combine(root, "missing-repository");
        var missingManifest = new ManifestService.Manifest();
        missingManifest.Entries.Add("lost", new ManifestService.ManifestEntry {
            PrimaryPath = "PLC/CPU/Blocks/FB1.xml",
            Files = new List<string> { "PLC/CPU/Blocks/FB1.xml" },
            ArtifactKind = "Fb",
            TargetPath = "CPU",
            Name = "FB1",
            LogicalKey = "lost",
            LastStatus = "Succeeded",
        });
        new ManifestService().Save(missingRoot, missingManifest);
        Expect<InvalidDataException>(() => new RepositoryInventoryService().Scan(missingRoot));
        missingManifest.Entries["lost"].PrimaryPath = "../outside.xml";
        new ManifestService().Save(missingRoot, missingManifest);
        Expect<InvalidDataException>(() => new RepositoryInventoryService().Scan(missingRoot));
        var state = new ApplicationState {
            SelectedRepository = root,
            ActiveProject = new ProjectDescriptor { Name = "Project A", Path = "A.ap21" },
            HasOpennessAdapter = true,
        };
        state.SetLastScan(new List<ExportItem> { new ExportItem("CPU", string.Empty, "FB1", ExportKind.Fb, "A||FB1") });
        var controller = (MainController)FormatterServices.GetUninitializedObject(typeof(MainController));
        SetField(controller, "_state", state);
        SetField(controller, "_session", new FakeAdapter());
        SetField(controller, "_scanProjectPath", "A.ap21");
        controller.RefreshSessionStateAsync().GetAwaiter().GetResult();
        Check(state.LastScan.Count == 0 && !new CommandStateService(state).CanExport, "Project switch invalidates scan");
        state.SetRepositoryInventory(inventory);
        SetField(controller, "_repositoryInventory", new RepositoryInventoryService());
        var absent = Path.Combine(root, "does-not-exist");
        try {
            controller.LoadRepositoryInventoryAsync(absent).GetAwaiter().GetResult();
        }
        catch (DirectoryNotFoundException) {
        }

        Check(
            state.SelectedRepository == root && state.RepositoryInventory.Count == inventory.Count,
            "Failed workspace load preserves binding");
        controller.SetSelectedRepository(absent);
        Check(state.RepositoryInventory.Count == 0, "Workspace change invalidates inventory");
        var start = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--fixture-worker") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
        };
        using (var client = new OpennessWorkerClient(null, new ThrowingLogger())) {
            var process = Process.Start(start);
            SetField(client, "_process", process);
            client.Execute(
                new WorkerMessage { Command = "First" },
                CancellationToken.None,
                message => { throw new IOException("Fixture callback failure"); });
            var response = client.Execute(new WorkerMessage { Command = "Second" }, CancellationToken.None);
            Check(response.Path == "Second", "Callback and logger failures do not desynchronize replies");
            Expect<InvalidDataException>(() => client.Execute(new WorkerMessage { Command = "WrongId" }, CancellationToken.None));
            Expect<IOException>(() => client.Execute(new WorkerMessage { Command = "Second" }, CancellationToken.None));
        }

        start.Arguments = "--fixture-exit";
        using (var client = new OpennessWorkerClient(null, NullLogger.Instance)) {
            var process = Process.Start(start);
            SetField(client, "_process", process);
            client.Execute(new WorkerMessage { Command = "Project" }, CancellationToken.None);
            if (!process.WaitForExit(5000)) {
                throw new Exception("Fixture exit timeout");
            }

            Check(!client.IsPortalOpen, "Exited worker clears portal state");
            client.DisconnectAsync(false).GetAwaiter().GetResult();
            Check(!client.GetProjectInfoAsync().GetAwaiter().GetResult().IsPortalOpen, "Exited worker disconnects offline");
        }
    }

    /// <summary>Injects controlled collaborators without starting a production controller or Siemens process.</summary>
    private static void SetField(object target, string name, object value) {
        target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    private static void Check(bool condition, string message) {
        if (!condition) {
            throw new InvalidOperationException(message);
        }

        Console.WriteLine("PASS: " + message);
    }

    private static void Expect<T>(Action action)
        where T : Exception {
        try {
            action();
        }
        catch (T) {
            Console.WriteLine("PASS: Rejected " + typeof(T).Name);
            return;
        }

        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static void RunFixtureWorker() {
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        try {
            while (true) {
                var request = WorkerMessage.Read(input);
                if (request.Command == "First") {
                    WorkerMessage.Write(output, new WorkerMessage { Command = "Progress", RequestId = request.RequestId });
                }

                WorkerMessage.Write(output, new WorkerMessage {
                    Command = "Log",
                    RequestId = request.RequestId,
                    LogText = "Fixture diagnostic",
                });
                WorkerMessage.Write(output, new WorkerMessage {
                    Command = "Result",
                    RequestId = request.Command == "WrongId" ? "wrong" : request.RequestId,
                    Path = request.Command,
                });
            }
        }
        catch (EndOfStreamException) {
        }
        catch (IOException) {
        }
    }

    private sealed class ThrowingLogger : ILogger {
        public void Info(string message) => throw new IOException("Fixture logger failure");

        public void Warn(string message) => throw new IOException("Fixture logger failure");

        public void Error(string message, Exception ex = null) => throw new IOException("Fixture logger failure");
    }

    private sealed class FakeAdapter : IOpennessAdapter {
        public bool IsPortalOpen {
            get {
                return true;
            }
        }

        public Task<TiaProjectInfo> GetProjectInfoAsync() {
            return Task.FromResult(new TiaProjectInfo {
                IsPortalOpen = true,
                IsProjectOpen = true,
                ProjectName = "Project B",
                ProjectPath = "B.ap21",
                TiaVersion = "V21",
            });
        }

        public Task<List<TiaPortalInstanceInfo>> GetRunningPortalInstancesAsync() {
            throw new NotImplementedException();
        }

        public Task AttachToRunningPortalAsync(int x) {
            throw new NotImplementedException();
        }

        public Task StartPortalAsync(bool x) {
            throw new NotImplementedException();
        }

        public Task OpenProjectAsync(string x) {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync(bool x) {
            throw new NotImplementedException();
        }

        public WorkerMessage Execute(WorkerMessage x, CancellationToken t, Action<WorkerMessage> p = null) {
            throw new NotImplementedException();
        }

        public void Dispose() {
        }
    }
}
