// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// Program.cs
// Loads one Siemens API and serves serialized engineering commands over inherited pipes.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Integration;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Worker {
    /// <summary>Hosts one selected Siemens API in a dedicated x64 process; engineering work stays on one STA.</summary>
    internal static class Program {
        private static readonly object OutputLock = new object();
        private static readonly object CancellationLock = new object();
        private static CancellationTokenSource _cancellation;
        private static Stream _output;

        // The pipe reader blocks in native ReadFile and cannot pump STA messages.
        // Keep it in MTA so Siemens shutdown can finish COM finalization; all
        // engineering calls still execute serially on the dedicated STA worker.
        [MTAThread]
        private static int Main() {
            _output = Console.OpenStandardOutput();
            try {
                var installation = OpennessDiscovery.Validate(Environment.GetEnvironmentVariable("EIDO_WORKER_API") ?? string.Empty);
                if (!installation.CanUse) {
                    throw new NotSupportedException(installation.Diagnostic);
                }

                var api = installation.ApiPath;
                var portal = new DirectoryInfo(api).Parent.Parent.Parent.FullName;
                var bin = Path.Combine(portal, "Bin");
                Environment.SetEnvironmentVariable("PATH", api + Path.PathSeparator + bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                    var requested = new AssemblyName(args.Name);
                    if (!requested.Name.StartsWith("Siemens.", StringComparison.Ordinal)) {
                        return null;
                    }

                    foreach (var directory in new[] { api, bin }) {
                        var file = Path.Combine(directory, requested.Name + ".dll");
                        if (!File.Exists(file)) {
                            continue;
                        }

                        var actual = AssemblyName.GetAssemblyName(file);
                        if (!AssemblyName.ReferenceMatchesDefinition(requested, actual)) {
                            continue;
                        }

                        if (requested.Name.StartsWith("Siemens.Engineering", StringComparison.Ordinal) && actual.Version.Major != 21) {
                            throw new FileLoadException("Mixed Siemens API versions are forbidden.");
                        }

                        return Assembly.LoadFrom(file);
                    }

                    return null;
                };
                foreach (var name in new[] { "Base", "Step7", "WinCC" }) {
                    Assembly.LoadFrom(Path.Combine(api, "Siemens.Engineering." + name + ".dll"));
                }

                return Run();
            }
            catch (Exception ex) {
                try {
                    Send(new WorkerMessage { Command = "Failed", Error = ex.ToString() });
                }
                catch (IOException) {
                    Console.Error.WriteLine(ex);
                }

                return 1;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int Run() {
            using (var sta = new StaWorker())
            using (var session = new TiaSessionService(sta, new PipeLogger())) {
                var logger = new PipeLogger();
                var export = new ExportService(session, new XmlNormalizeService(), new ManifestService(), logger);
                var import = new ImportService(session, logger);
                var input = Console.OpenStandardInput();
                Task operation = Task.CompletedTask;
                Send(new WorkerMessage { Command = "Ready" });
                try {
                    while (true) {
                        var request = WorkerMessage.Read(input);
                        if (request.Command == "Cancel") {
                            lock (CancellationLock) {
                                _cancellation?.Cancel();
                            }

                            continue;
                        }

                        // The client has one outstanding request; completing its response precedes the next command.
                        operation.GetAwaiter().GetResult();
                        lock (CancellationLock) {
                            _cancellation = new CancellationTokenSource();
                        }

                        operation = Task.Run(() => {
                            Console.Error.WriteLine(DateTime.UtcNow.ToString("O") + " Begin command " + request.Command);
                            var reply = new WorkerMessage { Command = "Result" };
                            try {
                                var token = _cancellation.Token;
                                switch (request.Command) {
                                    case "Instances":
                                        reply.Instances = session.GetRunningPortalInstancesAsync().GetAwaiter().GetResult();
                                        break;
                                    case "Attach":
                                        session.AttachToRunningPortalAsync(request.Instance).GetAwaiter().GetResult();
                                        break;
                                    case "Start":
                                        session.StartPortalAsync(request.Flag).GetAwaiter().GetResult();
                                        break;
                                    case "Open":
                                        session.OpenProjectAsync(request.Path).GetAwaiter().GetResult();
                                        break;
                                    case "Disconnect":
                                        session.DisconnectAsync(request.Flag).GetAwaiter().GetResult();
                                        break;
                                    case "Project":
                                        break;
                                    case "Scan":
                                        reply.Items = sta.InvokeAsync(() => new PlcInventoryService(session).ScanExportables(request.Profile, request.Flag).Concat(new HmiInventoryService(session).ScanScreens()).ToList(), token).GetAwaiter().GetResult();
                                        break;
                                    case "Export":
                                        reply.Export = sta.InvokeAsync(
                                            () => export.Export(
                                                request.Path,
                                                request.Items,
                                                request.Normalize,
                                                request.PreserveHmi,
                                                request.Incremental,
                                                (done, total, item, error) => Send(new WorkerMessage { Command = "Progress", Items = item == null ? null : new System.Collections.Generic.List<Core.Model.ExportItem> { item }, Progress = new ImportProgressInfo { Done = done, Total = total }, Error = error?.ToString() }),
                                                token),
                                            token).GetAwaiter().GetResult();
                                        break;
                                    case "Preview":
                                        reply.Import = sta.InvokeAsync(() => import.PreviewFiles(request.Artifacts, request.Policy), token).GetAwaiter().GetResult();
                                        break;
                                    case "Import":
                                        if (!Enum.IsDefined(typeof(Core.Model.ImportConflictPolicy), request.Policy)) {
                                            throw new ArgumentException("Explicit conflict policy required.");
                                        }

                                        foreach (var artifact in request.Artifacts) {
                                            ImportVersionGuard.Validate(artifact.RepositoryPath, 21);
                                        }

                                        var plan = new ImportDependencyPlanner().Build(request.Artifacts);
                                        var validation = new ImportPreflightValidator().Validate(request.Artifacts, plan);
                                        if (validation.HasBlockingErrors) {
                                            throw new InvalidDataException("Worker import preflight failed; no project changes applied.");
                                        }

                                        reply.Import = sta.InvokeAsync(
                                            () => import.ImportFiles(
                                                plan.OrderedArtifacts,
                                                request.Policy,
                                                request.Flag,
                                                request.Path,
                                                token,
                                                progress => Send(new WorkerMessage { Command = "Progress", Progress = progress })),
                                            token).GetAwaiter().GetResult();
                                        break;
                                    default:
                                        throw new NotSupportedException("Unknown engineering command: " + request.Command);
                                }

                                Console.Error.WriteLine(DateTime.UtcNow.ToString("O") + " Read session snapshot after " + request.Command);
                                reply.Project = session.GetProjectInfoAsync().GetAwaiter().GetResult();
                            }
                            catch (OperationCanceledException) {
                                reply.Cancelled = true;
                            }
                            catch (Exception ex) {
                                reply.Error = ExceptionDiagnosticFormatter.Format(ex);
                            }
                            finally {
                                lock (CancellationLock) {
                                    _cancellation.Dispose();
                                    _cancellation = null;
                                }
                            }

                            Console.Error.WriteLine(DateTime.UtcNow.ToString("O") + " Complete command " + request.Command);
                            Send(reply);
                        });
                    }
                }
                catch (EndOfStreamException) {
                    lock (CancellationLock) {
                        _cancellation?.Cancel();
                    }

                    try {
                        operation.GetAwaiter().GetResult();
                    }
                    catch (IOException) { /* Parent closed its response pipe; session still needs orderly cleanup. */
                    }
                }

                return 0;
            }
        }

        private static void Send(WorkerMessage message) {
            lock (OutputLock) {
                WorkerMessage.Write(_output, message);
            }
        }

        private sealed class PipeLogger : ILogger {
            public void Info(string message) => Write("Info", message);

            public void Warn(string message) => Write("Warning", message);

            public void Error(string message, Exception ex = null) => Write("Error", message + (ex == null ? string.Empty : "\n" + ex));

            private static void Write(string level, string text) {
                try {
                    Send(new WorkerMessage { Command = "Log", LogLevel = level, LogText = text });
                }
                catch (IOException) {
                    Console.Error.WriteLine(text);
                }
            }
        }
    }
}
