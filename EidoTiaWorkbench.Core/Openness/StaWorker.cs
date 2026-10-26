// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// StaWorker.cs
// Runs all TIA Openness calls on a dedicated STA thread.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EidoTiaWorkbench.Core.Openness {
    /// <summary>
    /// Runs all TIA Openness calls on a dedicated STA thread.
    /// </summary>
    /// <remarks>
    /// Why STA?
    /// - TIA Openness objects are not thread-safe and behave similarly to COM components.
    /// - Keeping all Openness access on a single STA thread prevents cross-thread exceptions.
    ///
    /// Design notes:
    /// - Work is queued as delegates and executed serially in FIFO order.
    /// - Exceptions thrown by work items are marshalled back to the caller via TaskCompletionSource.
    /// - Cancellation is cooperative: it can prevent queueing or skip queued work before it starts, but it cannot abort an in-progress Openness call.
    /// </remarks>
    public sealed class StaWorker : IDisposable {
        /// <summary>
        /// Queue of work items to execute on the STA thread.
        /// BlockingCollection provides thread-safe producer/consumer semantics.
        /// </summary>
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();

        /// <summary>
        /// The dedicated background STA thread.
        /// </summary>
        private readonly Thread _thread;

        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaWorker"/> class and starts its STA thread.
        /// </summary>
        /// <param name="name">Thread name (useful for debugging).</param>
        public StaWorker(string name = "TIA Openness STA Worker") {
            this._thread = new Thread(this.Run) {
                IsBackground = true,
                Name = name,
            };

            // Openness expects STA access (COM-like behavior).
            this._thread.SetApartmentState(ApartmentState.STA);
            this._thread.Start();
        }

        /// <summary>
        /// Executes a function on the STA thread and returns its result.
        /// </summary>
        /// <typeparam name="T">Type returned by the queued function.</typeparam>
        /// <param name="func">Function to execute on the STA thread.</param>
        /// <returns>A task that completes with the function result.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="func"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown when the worker has been disposed.</exception>
        public Task<T> InvokeAsync<T>(Func<T> func) {
            if (func == null) {
                throw new ArgumentNullException(nameof(func));
            }

            this.EnsureNotDisposed();

            // RunContinuationsAsynchronously avoids running continuations inline on the STA thread.
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            this._queue.Add(() => {
                try {
                    tcs.SetResult(func());
                }
                catch (Exception ex) {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Executes a function on the STA thread and returns its result, honoring cancellation.
        /// </summary>
        /// <remarks>
        /// Note:
        /// - Cancellation cannot abort an Openness call already executing.
        /// - Cancellation can prevent queueing if it was already requested.
        /// - Once queued, the caller stays busy until the STA worker reaches the item and either skips it
        ///   or completes it. This prevents the UI from starting another Openness operation while queued
        ///   Siemens work may still run.
        /// </remarks>
        /// <typeparam name="T">Type returned by the queued function.</typeparam>
        /// <param name="func">Function to execute on the STA thread.</param>
        /// <param name="cancellationToken">
        /// Token that can cancel waiting or prevent queueing before execution starts.
        /// </param>
        /// <returns>A task that completes with the function result or cancellation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="func"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown when the worker has been disposed.</exception>
        public Task<T> InvokeAsync<T>(Func<T> func, CancellationToken cancellationToken) {
            if (func == null) {
                throw new ArgumentNullException(nameof(func));
            }

            this.EnsureNotDisposed();

            // If already cancelled, avoid queueing any work.
            if (cancellationToken.IsCancellationRequested) {
                return Task.FromCanceled<T>(cancellationToken);
            }

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            this._queue.Add(() => {
                try {
                    // If cancelled before execution begins, do not run user code.
                    if (cancellationToken.IsCancellationRequested) {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }

                    tcs.TrySetResult(func());
                }
                catch (Exception ex) {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Executes an action on the STA thread.
        /// </summary>
        /// <param name="action">Action to execute on the STA thread.</param>
        /// <returns>A task that completes when the action has run.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="action"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown when the worker has been disposed.</exception>
        public Task InvokeAsync(Action action) {
            if (action == null) {
                throw new ArgumentNullException(nameof(action));
            }

            // Reuse the generic path to keep queueing and exception behavior consistent.
            return this.InvokeAsync(() => {
                action();
                return true;
            });
        }

        /// <summary>
        /// Stops accepting work, drains queued actions and releases the thread and queue.
        /// </summary>
        /// <remarks>Blocks the owning thread until all accepted work has finished.</remarks>
        /// <exception cref="InvalidOperationException">Disposal was requested from the STA thread itself.</exception>
        public void Dispose() {
            if (this._disposed) {
                return;
            }

            if (Thread.CurrentThread == this._thread) {
                throw new InvalidOperationException("The owner must dispose the STA worker from another thread.");
            }

            this._disposed = true;
            this._queue.CompleteAdding();
            this._thread.Join();
            this._queue.Dispose();
        }

        /// <summary>
        /// Executes accepted actions in FIFO order until the queue is completed and drained.
        /// </summary>
        private void Run() {
            foreach (var action in this._queue.GetConsumingEnumerable()) {
                action();
            }
        }

        /// <summary>
        /// Throws if this instance has been disposed.
        /// </summary>
        private void EnsureNotDisposed() {
            if (this._disposed) {
                throw new ObjectDisposedException(nameof(StaWorker));
            }
        }
    }
}
