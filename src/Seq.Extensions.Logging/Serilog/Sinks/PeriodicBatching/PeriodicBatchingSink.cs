// Copyright 2013-2020 Serilog Contributors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Serilog.Core;
using Serilog.Events;
using Seq.Extensions.Logging;

// ReSharper disable MemberCanBePrivate.Global, UnusedMember.Global, VirtualMemberNeverOverridden.Global, ClassWithVirtualMembersNeverInherited.Global

namespace Serilog.Sinks.PeriodicBatching;

/// <summary>
/// Base class for sinks that log events in batches. Batching is
/// triggered asynchronously on a timer.
/// </summary>
/// <remarks>
/// To avoid unbounded memory growth, events are discarded after attempting
/// to send a batch, regardless of whether the batch succeeded or not. Implementers
/// that want to change this behavior need to either implement from scratch, or
/// embed retry logic in the batch emitting functions.
/// </remarks>
sealed class PeriodicBatchingSink : ILogEventSink, IDisposable
{
    /// <summary>
    /// Constant used with legacy constructor to indicate that the internal queue shouldn't be limited.
    /// </summary>
    [Obsolete("Implement `IBatchedLogEventSink` and use the `PeriodicBatchingSinkOptions` constructor.")]
    public const int NoQueueLimit = -1;

    readonly IBatchedLogEventSink _batchedLogEventSink;
    readonly int _batchSizeLimit;
    readonly bool _eagerlyEmitFirstEvent;
    readonly BoundedConcurrentQueue<LogEvent> _queue;
    readonly BatchedConnectionStatus _status;
    readonly Queue<LogEvent> _waitingBatch = new();

    readonly object _stateLock = new();

    readonly PortableTimer _timer;

    bool _unloading;
    bool _started;

    /// <summary>
    /// Construct a <see cref="PeriodicBatchingSink"/>.
    /// </summary>
    /// <param name="batchedSink">A <see cref="IBatchedLogEventSink"/> to send log event batches to. Batches and empty
    /// batch notifications will not be sent concurrently. When the <see cref="PeriodicBatchingSink"/> is disposed,
    /// it will dispose this object if possible.</param>
    /// <param name="options">Options controlling behavior of the sink.</param>
    public PeriodicBatchingSink(IBatchedLogEventSink batchedSink, PeriodicBatchingSinkOptions options)
    {
        if (options.BatchSizeLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "The batch size limit must be greater than zero.");
        if (options.Period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "The period must be greater than zero.");

        _batchSizeLimit = options.BatchSizeLimit;
        _batchedLogEventSink = batchedSink ?? throw new ArgumentNullException(nameof(batchedSink));
        _queue = new BoundedConcurrentQueue<LogEvent>(options.QueueLimit);
        _status = new BatchedConnectionStatus(options.Period);
        _eagerlyEmitFirstEvent = options.EagerlyEmitFirstEvent;
        _timer = new PortableTimer(_ => OnTick());
    }

    void CloseAndFlush()
    {
        lock (_stateLock)
        {
            if (!_started || _unloading)
                return;

            _unloading = true;
        }

        _timer.Dispose();

        // This is the place where SynchronizationContext.Current is unknown and can be != null
        // so we prevent possible deadlocks here for sync-over-async downstream implementations 
        ResetSyncContextAndWait(OnTick);

        (_batchedLogEventSink as IDisposable)?.Dispose();
    }

    static void ResetSyncContextAndWait(Func<Task> taskFactory)
    {
        var prevContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            taskFactory().Wait();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prevContext);
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <filterpriority>2</filterpriority>
    public void Dispose()
    {
        CloseAndFlush();
    }

    async Task OnTick()
    {
        try
        {
            bool batchWasFull;
            do
            {
                while (_waitingBatch.Count < _batchSizeLimit &&
                       _queue.TryDequeue(out var next))
                {
                    _waitingBatch.Enqueue(next);
                }

                if (_waitingBatch.Count == 0)
                {
                    await _batchedLogEventSink.OnEmptyBatchAsync();
                    return;
                }

                await _batchedLogEventSink.EmitBatchAsync(_waitingBatch);

                batchWasFull = _waitingBatch.Count >= _batchSizeLimit;
                _waitingBatch.Clear();
                _status.MarkSuccess();
            }
            while (batchWasFull); // Otherwise, allow the period to elapse
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Exception while emitting periodic batch from {0}: {1}", this, ex);
            _status.MarkFailure();
        }
        finally
        {
            if (_status.ShouldDropBatch)
                _waitingBatch.Clear();

            if (_status.ShouldDropQueue)
            {
                while (_queue.TryDequeue(out _)) { }
            }

            lock (_stateLock)
            {
                if (!_unloading)
                    SetTimer(_status.NextInterval);
            }
        }
    }

    void SetTimer(TimeSpan interval)
    {
        _timer.Start(interval);
    }

    /// <summary>
    /// Emit the provided log event to the sink. If the sink is being disposed or
    /// the app domain unloaded, then the event is ignored.
    /// </summary>
    /// <param name="logEvent">Log event to emit.</param>
    /// <exception cref="ArgumentNullException">The event is null.</exception>
    /// <remarks>
    /// The sink implements the contract that any events whose Emit() method has
    /// completed at the time of sink disposal will be flushed (or attempted to,
    /// depending on app domain state).
    /// </remarks>
    public void Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        if (_unloading)
            return;

        if (!_started)
        {
            lock (_stateLock)
            {
                if (_unloading) return;
                if (!_started)
                {
                    _queue.TryEnqueue(logEvent);
                    _started = true;

                    if (_eagerlyEmitFirstEvent)
                    {
                        // Special handling to try to get the first event across as quickly
                        // as possible to show we're alive!
                        SetTimer(TimeSpan.Zero);
                    }
                    else
                    {
                        SetTimer(_status.NextInterval);
                    }

                    return;
                }
            }
        }

        _queue.TryEnqueue(logEvent);
    }
}