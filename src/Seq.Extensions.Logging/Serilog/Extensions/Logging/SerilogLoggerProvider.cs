// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
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
    [ProviderAlias("Seq")]
    class SerilogLoggerProvider : ILoggerProvider, ILogEventEnricher
    {
        internal const string OriginalFormatPropertyName = "{OriginalFormat}";
        internal const string ScopePropertyName = "Scope";

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

            _dispose = logger.Dispose;
            _logger = logger.ForContext(new[] { this });
        }

        /// <inheritdoc />
        public FrameworkLogger CreateLogger(string name)
        {
            return new SerilogLogger(this, _logger, name);
        }

        public IDisposable BeginScope<T>(T state)
        {
            return new SerilogLoggerScope(this, state);
        }

        /// <inheritdoc />
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            List<LogEventPropertyValue> scopeItems = null;
            for (var scope = CurrentScope; scope != null; scope = scope.Parent)
            {
                LogEventPropertyValue scopeItem;
                scope.EnrichAndCreateScopeItem(logEvent, propertyFactory, out scopeItem);

                if (scopeItem != null)
                {
                    scopeItems = scopeItems ?? new List<LogEventPropertyValue>();
                    scopeItems.Add(scopeItem);
                }
            }

            if (scopeItems != null)
            {
                scopeItems.Reverse();
                logEvent.AddPropertyIfAbsent(new LogEventProperty(ScopePropertyName, new SequenceValue(scopeItems)));
            }
        }

        readonly AsyncLocal<SerilogLoggerScope> _value = new AsyncLocal<SerilogLoggerScope>();

        internal SerilogLoggerScope CurrentScope
        {
            get => _value.Value;
            set => _value.Value = value;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _dispose();
        }
    }
}
