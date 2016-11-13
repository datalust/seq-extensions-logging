// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
#if ASYNCLOCAL
using System.Threading;
#else
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
#endif
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;
using System.Collections.Generic;

namespace Serilog.Extensions.Logging
{
    /// <summary>
    /// An <see cref="ILoggerProvider"/> that pipes events through Serilog.
    /// </summary>
    class SerilogLoggerProvider : ILoggerProvider, ILogEventEnricher
    {
        internal const string OriginalFormatPropertyName = "{OriginalFormat}";
        internal const string ScopePropertyName = "Scope";

        // May be null; if it is, Log.Logger will be lazily used
        readonly Logger _logger;
        readonly Action _dispose;

        /// <summary>
        /// Construct a <see cref="SerilogLoggerProvider"/>.
        /// </summary>
        /// <param name="logger">A Serilog logger to pipe events through.</param>
        public SerilogLoggerProvider(Logger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger.ForContext(new[] { this });
            _dispose = () => (logger as IDisposable)?.Dispose();
        }

        /// <inheritdoc />
        public FrameworkLogger CreateLogger(string name)
        {
            return new SerilogLogger(this, _logger, name);
        }

        /// <inheritdoc />
        public IDisposable BeginScope<T>(T state)
        {
            return new SerilogLoggerScope(this, state);
        }

        /// <inheritdoc />
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            List<ScalarValue> names = null;
            for (var scope = CurrentScope; scope != null; scope = scope.Parent)
            {
                scope.Enrich(logEvent, propertyFactory);

                string name;
                if (scope.TryGetName(out name))
                {
                    names = names ?? new List<ScalarValue>();
                    names.Add(new ScalarValue(name));
                }
            }
            if (names != null)
            {
                names.Reverse();
                logEvent.AddPropertyIfAbsent(new LogEventProperty(ScopePropertyName, new SequenceValue(names)));
            }
        }

#if ASYNCLOCAL
        readonly AsyncLocal<SerilogLoggerScope> _value = new AsyncLocal<SerilogLoggerScope>();

        internal SerilogLoggerScope CurrentScope
        {
            get
            {
                return _value.Value;
            }
            set
            {
                _value.Value = value;
            }
        }
#else
        readonly string _currentScopeKey = nameof(SerilogLoggerScope) + "#" + Guid.NewGuid().ToString("n");

        internal SerilogLoggerScope CurrentScope
        {
            get
            {
                var objectHandle = CallContext.LogicalGetData(_currentScopeKey) as ObjectHandle;
                return objectHandle?.Unwrap() as SerilogLoggerScope;
            }
            set
            {
                CallContext.LogicalSetData(_currentScopeKey, new ObjectHandle(value));
            }
        }
#endif

        /// <inheritdoc />
        public void Dispose()
        {
            _dispose?.Invoke();
        }
    }
}
