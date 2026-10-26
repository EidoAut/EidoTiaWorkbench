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
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using EidoTiaWorkbench.Core.Openness;
using EidoTiaWorkbench.Core.Utilities;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>
    /// Owns a lazy worker process and inherited pipes, serializing commands and cooperative cancellation.
    /// </summary>
    public sealed class OpennessWorkerClient : IOpennessAdapter {
        private readonly OpennessInstallation _installation;
        private readonly ILogger _logger;
        private readonly object _operationLock = new object();
        private readonly object _writeLock = new object();
        private Process _process;
        private bool _disposed;
        private bool _channelFaulted;
        private bool _isPortalOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpennessWorkerClient"/> class without starting Siemens.
        /// </summary>
        /// <param name="installation">Validated API candidate, or null for offline operation.</param>
        /// <param name="logger">Sink for worker diagnostics and cleanup warnings.</param>
        public OpennessWorkerClient(OpennessInstallation installation, ILogger logger) {
            this._installation = installation;
            this._logger = logger;
        }

        /// <inheritdoc />
        public bool IsPortalOpen {
            get {
                if (this._process != null && this._process.HasExited) {
                    this._isPortalOpen = false;
                    this._channelFaulted = true;
                }

                return this._isPortalOpen;
            }

            private set {
                this._isPortalOpen = value;
            }
        }

        /// <inheritdoc />
        public Task<List<TiaPortalInstanceInfo>> GetRunningPortalInstancesAsync() => this._installation == null
            ? Task.FromResult(new List<TiaPortalInstanceInfo>())
            : Task.Run(
                () => this.Execute(new WorkerMessage { Command = "Instances" }, CancellationToken.None).Instances);

        /// <inheritdoc />
        public Task AttachToRunningPortalAsync(int instanceIndex) =>
            this.RunAsync(new WorkerMessage { Command = "Attach", Instance = instanceIndex });

        /// <inheritdoc />
        public Task StartPortalAsync(bool withUserInterface) =>
            this.RunAsync(new WorkerMessage { Command = "Start", Flag = withUserInterface });

        /// <inheritdoc />
        public Task OpenProjectAsync(string projectPath) =>
            this.RunAsync(new WorkerMessage { Command = "Open", Path = projectPath });

        /// <inheritdoc />
        public Task DisconnectAsync(bool closeOwnedPortal) => Task.Run(() => {
            lock (this._operationLock) {
                if (this._process != null && this._process.HasExited) {
                    this.IsPortalOpen = false;
                    this._channelFaulted = true;
                    return;
                }

                this.Execute(new WorkerMessage { Command = "Disconnect", Flag = closeOwnedPortal }, CancellationToken.None);
            }
        });

        /// <inheritdoc />
        public Task<TiaProjectInfo> GetProjectInfoAsync() => Task.Run(() => {
            lock (this._operationLock) {
                if (this._process != null && this._process.HasExited) {
                    this.IsPortalOpen = false;
                    this._channelFaulted = true;
                    return new TiaProjectInfo();
                }

                return this.Execute(new WorkerMessage { Command = "Project" }, CancellationToken.None).Project;
            }
        });

        /// <inheritdoc />
        public WorkerMessage Execute(
            WorkerMessage request,
            CancellationToken token,
            Action<WorkerMessage> progress = null) {
            lock (this._operationLock) {
                if (this._disposed) {
                    throw new ObjectDisposedException(nameof(OpennessWorkerClient));
                }

                token.ThrowIfCancellationRequested();
                this.EnsureWorker();
                request.RequestId = Guid.NewGuid().ToString("N");
                try {
                    this.Send(request);
                }
                catch (IOException) {
                    this._channelFaulted = true;
                    this.IsPortalOpen = false;
                    throw;
                }

                using (token.Register(() => {
                    try {
                        this.Send(new WorkerMessage { Command = "Cancel", RequestId = request.RequestId });
                    }
                    catch (IOException ex) {
                        this.Observe(() => this._logger.Warn("Cancellation delivery failed: " + ex.Message));
                    }
                    catch (InvalidOperationException ex) {
                        this.Observe(() => this._logger.Warn("Worker already exited: " + ex.Message));
                    }
                })) {
                    try {
                        var reply = ReadResponse(
                            this._process.StandardOutput.BaseStream, request.RequestId, this._logger, progress);
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
                    catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is XmlException || ex is SerializationException) {
                        this._channelFaulted = true;
                        this.IsPortalOpen = false;
                        throw;
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
                try {
                    this.Observe(() => this._process.StandardInput.Close());
                    if (!this._process.WaitForExit(5000)) {
                        this.Observe(() => this._logger.Warn("Worker is still completing orderly Siemens cleanup."));
                    }
                }
                finally {
                    this._process.Dispose();
                    this._process = null;
                    this.IsPortalOpen = false;
                }
            }
        }

        /// <summary>Drains one correlated response even if diagnostic or progress observers fail.</summary>
        /// <param name="stream">Caller-owned worker output stream.</param>
        /// <param name="requestId">Identifier of the one outstanding operation.</param>
        /// <param name="logger">Diagnostic observer; failures must not interrupt protocol consumption.</param>
        /// <param name="progress">Optional progress observer.</param>
        /// <returns>The terminal result belonging to this request.</returns>
        /// <exception cref="InvalidDataException">A response is uncorrelated or has an unknown command.</exception>
        internal static WorkerMessage ReadResponse(
            Stream stream, string requestId, ILogger logger, Action<WorkerMessage> progress) {
            while (true) {
                var reply = WorkerMessage.Read(stream);
                if (reply.RequestId != requestId || string.IsNullOrWhiteSpace(requestId)) {
                    throw new InvalidDataException("Worker response does not belong to the outstanding command.");
                }

                if (reply.Command == "Result") {
                    return reply;
                }

                if (reply.Command != "Progress" && reply.Command != "Log") {
                    throw new InvalidDataException("Unknown worker response: " + reply.Command);
                }

                try {
                    if (reply.Command == "Progress") {
                        progress?.Invoke(reply);
                    }
                    else if (reply.LogLevel == "Error") {
                        logger.Error(reply.LogText);
                    }
                    else if (reply.LogLevel == "Warning") {
                        logger.Warn(reply.LogText);
                    }
                    else {
                        logger.Info(reply.LogText);
                    }
                }
                catch (Exception ex) {
                    // Observers are not engineering operations. Drain the result before allowing another command.
                    Trace.TraceError("Worker observer failed: " + ex);
                }
            }
        }

        /// <summary>Contains observer failures at the process boundary without interrupting cleanup.</summary>
        /// <param name="action">Best-effort notification or pipe cleanup action.</param>
        private void Observe(Action action) {
            try {
                action();
            }
            catch (Exception ex) {
                Trace.TraceError("Worker notification failed: " + ex);
            }
        }

        /// <summary>Schedules a serialized worker command without blocking the calling UI thread.</summary>
        /// <param name="request">Command envelope sent after the worker is ready.</param>
        /// <returns>A task that completes after the final worker response has been consumed.</returns>
        private Task RunAsync(WorkerMessage request) => Task.Run(() => this.Execute(request, CancellationToken.None));

        /// <summary>Starts the selected worker lazily and waits for its protocol readiness frame.</summary>
        /// <remarks>Requires the operation lock. An exited worker is never restarted implicitly.</remarks>
        /// <exception cref="IOException">The previously started worker has exited.</exception>
        /// <exception cref="NotSupportedException">No usable installation is selected.</exception>
        /// <exception cref="TimeoutException">Readiness was not received within the startup timeout.</exception>
        /// <exception cref="InvalidOperationException">The worker rejected initialization.</exception>
        private void EnsureWorker() {
            if (this._channelFaulted) {
                throw new IOException("Worker channel is unavailable. Reselect the integration before retrying.");
            }

            if (this._process != null) {
                if (this._process.HasExited) {
                    this.IsPortalOpen = false;
                    this._channelFaulted = true;
                    throw new IOException(
                        "Openness worker exited. Disconnect and reselect the integration before retrying.");
                }

                return;
            }

            if (this._installation == null || !this._installation.CanUse) {
                throw new NotSupportedException(
                    "No usable Openness adapter selected. Configure TIA Portal Integration in Settings. " +
                    "Offline workspace validation remains available.");
            }

            var info = new ProcessStartInfo(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EidoTiaWorkbench.Worker.exe")) {
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
                    this.Observe(() => this._logger.Error(e.Data));
                }
            };
            this._process.BeginErrorReadLine();
            var output = this._process.StandardOutput.BaseStream;
            try {
                var ready = Task.Run(() => WorkerMessage.Read(output));
                if (!ready.Wait(TimeSpan.FromSeconds(20))) {
                    this._channelFaulted = true;
                    this._process.StandardInput.Close();
                    throw new TimeoutException(
                        "Openness worker initialization timed out. No engineering command was sent.");
                }

                if (ready.Result.Command != "Ready") {
                    this._channelFaulted = true;
                    throw new InvalidOperationException(ready.Result.Error ?? "Worker startup failed.");
                }
            }
            catch {
                this._channelFaulted = true;
                this.IsPortalOpen = false;
                this.Observe(() => this._process.StandardInput.Close());
                throw;
            }
        }

        /// <summary>Serializes pipe writes so cancellation cannot interleave bytes with a command.</summary>
        /// <param name="message">Frame to send to the initialized worker.</param>
        private void Send(WorkerMessage message) {
            lock (this._writeLock) {
                WorkerMessage.Write(this._process.StandardInput.BaseStream, message);
            }
        }
    }
}
