// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// OpennessWorkerClient.cs
// Owns a lazy worker process and inherited pipes, serializing commands and cooperative cancellation.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>Owns a lazy worker process and inherited pipes, serializing commands and cooperative cancellation.</summary>
    public sealed class OpennessWorkerClient : IOpennessAdapter {
        private readonly OpennessInstallation _installation;
        private readonly ILogger _logger;
        private readonly object _operationLock = new object();
        private readonly object _writeLock = new object();
        private Process _process;
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="OpennessWorkerClient"/> class without starting Siemens.</summary>
        /// <param name="installation">Validated API candidate, or null for offline operation.</param>
        /// <param name="logger">Sink for worker diagnostics and cleanup warnings.</param>
        public OpennessWorkerClient(OpennessInstallation installation, ILogger logger) {
            this._installation = installation;
            this._logger = logger;
        }

        /// <inheritdoc />
        public bool IsPortalOpen {
            get; private set;
        }

        /// <inheritdoc />
        public Task<List<TiaPortalInstanceInfo>> GetRunningPortalInstancesAsync() => this._installation == null
            ? Task.FromResult(new List<TiaPortalInstanceInfo>())
            : Task.Run(() => this.Execute(new WorkerMessage { Command = "Instances" }, CancellationToken.None).Instances);

        /// <inheritdoc />
        public Task AttachToRunningPortalAsync(int instanceIndex) => this.RunAsync(new WorkerMessage { Command = "Attach", Instance = instanceIndex });

        /// <inheritdoc />
        public Task StartPortalAsync(bool withUserInterface) => this.RunAsync(new WorkerMessage { Command = "Start", Flag = withUserInterface });

        /// <inheritdoc />
        public Task OpenProjectAsync(string projectPath) => this.RunAsync(new WorkerMessage { Command = "Open", Path = projectPath });

        /// <inheritdoc />
        public Task DisconnectAsync(bool closeOwnedPortal) => this.RunAsync(new WorkerMessage { Command = "Disconnect", Flag = closeOwnedPortal });

        /// <inheritdoc />
        public Task<TiaProjectInfo> GetProjectInfoAsync() => Task.Run(() => this.Execute(new WorkerMessage { Command = "Project" }, CancellationToken.None).Project);

        /// <inheritdoc />
        public WorkerMessage Execute(WorkerMessage request, CancellationToken token, Action<WorkerMessage> progress = null) {
            lock (this._operationLock) {
                if (this._disposed) {
                    throw new ObjectDisposedException(nameof(OpennessWorkerClient));
                }

                token.ThrowIfCancellationRequested();
                this.EnsureWorker();
                this.Send(request);
                using (token.Register(() => {
                    try {
                        this.Send(new WorkerMessage { Command = "Cancel" });
                    }
                    catch (IOException ex) {
                        this._logger.Warn("Cancellation delivery failed: " + ex.Message);
                    }
                    catch (InvalidOperationException ex) {
                        this._logger.Warn("Worker already exited: " + ex.Message);
                    }
                })) {
                    while (true) {
                        WorkerMessage reply;
                        try {
                            reply = WorkerMessage.Read(this._process.StandardOutput.BaseStream);
                        }
                        catch (IOException) {
                            this.IsPortalOpen = false;
                            throw;
                        }

                        if (reply.Command == "Log") {
                            if (reply.LogLevel == "Error") {
                                this._logger.Error(reply.LogText);
                            }
                            else if (reply.LogLevel == "Warning") {
                                this._logger.Warn(reply.LogText);
                            }
                            else {
                                this._logger.Info(reply.LogText);
                            }

                            continue;
                        }

                        if (reply.Command == "Progress") {
                            progress?.Invoke(reply);
                            continue;
                        }

                        if (reply.Project != null) {
                            this.IsPortalOpen = reply.Project.IsPortalOpen;
                        }

                        if (reply.Cancelled) {
                            throw new OperationCanceledException(token);
                        }

                        if (!string.IsNullOrWhiteSpace(reply.Error)) {
                            throw new InvalidOperationException(reply.Error);
                        }

                        return reply;
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() {
            lock (this._operationLock) {
                if (this._disposed) {
                    return;
                }

                this._disposed = true;
                if (this._process == null) {
                    return;
                }

                // EOF requests orderly session cleanup. Never terminate an in-flight Siemens call.
                this._process.StandardInput.Close();
                if (!this._process.WaitForExit(5000)) {
                    this._logger.Warn("Worker is still completing orderly Siemens cleanup.");
                }

                this._process.Dispose();
                this.IsPortalOpen = false;
            }
        }

        private Task RunAsync(WorkerMessage request) => Task.Run(() => this.Execute(request, CancellationToken.None));

        private void EnsureWorker() {
            if (this._process != null) {
                if (this._process.HasExited) {
                    throw new IOException("Openness worker exited. Disconnect and reselect the integration before retrying.");
                }

                return;
            }

            if (this._installation == null || !this._installation.CanUse) {
                throw new NotSupportedException("No usable Openness adapter selected. Configure TIA Portal Integration in Settings. Offline workspace validation remains available.");
            }

            var info = new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EidoTiaWorkbench.Worker.exe")) {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.EnvironmentVariables["EIDO_WORKER_API"] = this._installation.ApiPath;
            this._process = Process.Start(info);
            this._process.ErrorDataReceived += (s, e) => {
                if (e.Data != null) {
                    this._logger.Error(e.Data);
                }
            };
            this._process.BeginErrorReadLine();
            var ready = Task.Run(() => WorkerMessage.Read(this._process.StandardOutput.BaseStream));
            if (!ready.Wait(TimeSpan.FromSeconds(20))) {
                this._process.StandardInput.Close();
                throw new TimeoutException("Openness worker initialization timed out. No engineering command was sent.");
            }

            if (ready.Result.Command != "Ready") {
                throw new InvalidOperationException(ready.Result.Error ?? "Worker startup failed.");
            }
        }

        private void Send(WorkerMessage message) {
            lock (this._writeLock) {
                WorkerMessage.Write(this._process.StandardInput.BaseStream, message);
            }
        }
    }
}
