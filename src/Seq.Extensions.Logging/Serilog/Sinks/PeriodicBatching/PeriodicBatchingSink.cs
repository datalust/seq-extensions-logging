﻿// Copyright 2013-2016 Serilog Contributors
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


using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using System.Threading;

namespace Serilog.Sinks.PeriodicBatching
{
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
    public abstract class PeriodicBatchingSink : ILogEventSink, IDisposable
    {
        readonly int _batchSizeLimit;
        readonly BoundedConcurrentQueue<LogEvent> _queue;
        readonly BatchedConnectionStatus _status;
        readonly Queue<LogEvent> _waitingBatch = new Queue<LogEvent>();

        readonly object _stateLock = new object();

        readonly PortableTimer _timer;

        bool _unloading;
        bool _started;

        /// <summary>
        /// Construct a sink posting to the specified database.
        /// </summary>
        /// <param name="batchSizeLimit">The maximum number of events to include in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        protected PeriodicBatchingSink(int batchSizeLimit, TimeSpan period)
        {
            _batchSizeLimit = batchSizeLimit;
            _queue = new BoundedConcurrentQueue<LogEvent>();
            _status = new BatchedConnectionStatus(period);
            
            _timer = new PortableTimer(cancel => OnTick());
        }

        /// <summary>
        /// Construct a sink posting to the specified database.
        /// </summary>
        /// <param name="batchSizeLimit">The maximum number of events to include in a single batch.</param>
        /// <param name="period">The time to wait between checking for event batches.</param>
        /// <param name="queueLimit">Maximum number of events in the queue.</param>
        protected PeriodicBatchingSink(int batchSizeLimit, TimeSpan period, int queueLimit)
            : this(batchSizeLimit, period)
        {
            _queue = new BoundedConcurrentQueue<LogEvent>(queueLimit);
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

            OnTick();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        /// <param name="disposing">If true, called because the object is being disposed; if false,
        /// the object is being disposed from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            CloseAndFlush();
        }

        /// <summary>
        /// Emit a batch of log events, running to completion synchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <remarks>Override either <see cref="EmitBatch"/> or <see cref="EmitBatchAsync"/>,
        /// not both.</remarks>
        protected virtual void EmitBatch(IEnumerable<LogEvent> events)
        {
            var prevContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                // Wait so that the timer thread stays busy and thus
                // we know we're working when flushing.
                EmitBatchAsync(events).Wait();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prevContext);
            }
        }

        /// <summary>
        /// Emit a batch of log events, running asynchronously.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        /// <remarks>Override either <see cref="EmitBatch"/> or <see cref="EmitBatchAsync"/>,
        /// not both. Overriding EmitBatch() is preferred.</remarks>
#pragma warning disable 1998
        protected virtual async Task EmitBatchAsync(IEnumerable<LogEvent> events)
#pragma warning restore 1998
        {
        }

        void OnTick()
        {
            try
            {
                bool batchWasFull;
                do
                {
                    LogEvent next;
                    while (_waitingBatch.Count < _batchSizeLimit &&
                        _queue.TryDequeue(out next))
                    {
                        if (CanInclude(next))
                            _waitingBatch.Enqueue(next);
                    }

                    if (_waitingBatch.Count == 0)
                    {
                        OnEmptyBatch();
                        return;
                    }

                    EmitBatch(_waitingBatch);

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
                    LogEvent evt;
                    while (_queue.TryDequeue(out evt)) { }
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
                        // Special handling to try to get the first event across as quickly
                        // as possible to show we're alive!
                        _queue.TryEnqueue(logEvent);
                        _started = true;
                        SetTimer(TimeSpan.Zero);
                        return;
                    }
                }
            }

            _queue.TryEnqueue(logEvent);
        }

        /// <summary>
        /// Determine whether a queued log event should be included in the batch. If
        /// an override returns false, the event will be dropped.
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        protected virtual bool CanInclude(LogEvent evt)
        {
            return true;
        }

        /// <summary>
        /// Allows derived sinks to perform periodic work without requiring additional threads
        /// or timers (thus avoiding additional flush/shut-down complexity).
        /// </summary>
        protected virtual void OnEmptyBatch()
        {
        }
    }
}
