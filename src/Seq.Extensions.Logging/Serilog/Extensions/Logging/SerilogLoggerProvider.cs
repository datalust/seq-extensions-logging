// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;

namespace Serilog.Extensions.Logging;

/// <summary>
/// An <see cref="ILoggerProvider"/> that pipes events through Serilog.
/// </summary>
[ProviderAlias("Seq")]
class SerilogLoggerProvider : ILoggerProvider, ILogEventEnricher, ISupportExternalScope
{
    internal const string OriginalFormatPropertyName = "{OriginalFormat}";
    internal const string ScopePropertyName = "Scope";

    readonly Logger _logger;
    readonly Action _dispose;

    IExternalScopeProvider? _scopeProvider;

    /// <summary>
    /// Construct a <see cref="SerilogLoggerProvider"/>.
    /// </summary>
    /// <param name="logger">A Serilog logger to pipe events through.</param>
    public SerilogLoggerProvider(Logger logger)
    {
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));

        _dispose = logger.Dispose;
        _logger = logger.ForContext([this]);
    }

    /// <inheritdoc />
    public FrameworkLogger CreateLogger(string name)
    {
        return new SerilogLogger(this, _logger, name);
    }

    public IDisposable? BeginScope<T>(T state)
    {
        return _scopeProvider?.Push(state);
    }

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyValueFactory propertyFactory)
    {
        var scopeItems = new List<LogEventPropertyValue>();

        _scopeProvider?.ForEachScope((scopeState, state) =>
        {
            SerilogLoggerScope.EnrichAndCreateScopeItem(scopeState, state.logEvent, state.propertyFactory, out var scopeItem);

            if (scopeItem != null)
            {
                state.scopeItems.Add(scopeItem);
            }
        }, (logEvent, propertyFactory, scopeItems));

        if (scopeItems.Count > 0)
        {
            logEvent.AddPropertyIfAbsent(ScopePropertyName, new SequenceValue(scopeItems));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dispose();
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }
}