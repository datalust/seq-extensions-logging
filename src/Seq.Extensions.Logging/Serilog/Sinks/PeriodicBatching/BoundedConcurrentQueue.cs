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

using System.Collections.Concurrent;

namespace Serilog.Sinks.PeriodicBatching;

class BoundedConcurrentQueue<T>
{
    const int Unbounded = -1;

    readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
    readonly int _queueLimit;

    int _counter;

    public BoundedConcurrentQueue(int? queueLimit = null)
    {
        if (queueLimit.HasValue && queueLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(queueLimit), "Queue limit must be positive, or `null` to indicate unbounded.");

        _queueLimit = queueLimit ?? Unbounded;
    }

    public int Count => _queue.Count;

    public bool TryDequeue(out T item)
    {
        if (_queueLimit == Unbounded)
            return _queue.TryDequeue(out item);

        var result = false;
        try
        { }
        finally // prevent state corrupt while aborting
        {
            if (_queue.TryDequeue(out item))
            {
                Interlocked.Decrement(ref _counter);
                result = true;
            }
        }

        return result;
    }

    public bool TryEnqueue(T item)
    {
        if (_queueLimit == Unbounded)
        {
            _queue.Enqueue(item);
            return true;
        }

        var result = true;
        try
        { }
        finally
        {
            if (Interlocked.Increment(ref _counter) <= _queueLimit)
            {
                _queue.Enqueue(item);
            }
            else
            {
                Interlocked.Decrement(ref _counter);
                result = false;
            }
        }

        return result;
    }
}