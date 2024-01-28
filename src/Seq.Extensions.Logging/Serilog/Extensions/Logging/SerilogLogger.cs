// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using FrameworkLogger = Microsoft.Extensions.Logging.ILogger;
using System.Reflection;
using Serilog.Parsing;

#nullable enable
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable ConstantNullCoalescingCondition

namespace Serilog.Extensions.Logging;

class SerilogLogger : FrameworkLogger
{
    readonly SerilogLoggerProvider _provider;
    readonly Logger _logger;

    static readonly MessageTemplateParser MessageTemplateParser = new();

    public SerilogLogger(
        SerilogLoggerProvider provider,
        Logger logger,
        string? name = null)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));

        _logger = logger.ForContext(new[] { provider });

        if (name != null)
        {
            _logger = _logger.ForContext(Constants.SourceContextPropertyName, name);
        }
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _logger.IsEnabled(logLevel);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _provider.BeginScope(state);
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var logger = _logger;
        string? messageTemplate = null;

        var properties = new List<LogEventProperty>();

        if (state is IEnumerable<KeyValuePair<string, object>> structure)
        {
            foreach (var property in structure)
            {
                if (property is { Key: SerilogLoggerProvider.OriginalFormatPropertyName, Value: string })
                {
                    messageTemplate = (string)property.Value;
                }
                else if (property.Key.StartsWith("@"))
                {
                    if (logger.BindProperty(property.Key.Substring(1), property.Value, true, out var destructured))
                        properties.Add(destructured);
                }
                else
                {
                    if (logger.BindProperty(property.Key, property.Value, false, out var bound))
                        properties.Add(bound);
                }
            }

            var stateType = state.GetType();
            var stateTypeInfo = stateType.GetTypeInfo();
            // Imperfect, but at least eliminates `1 names
            if (messageTemplate == null && !stateTypeInfo.IsGenericType)
            {
                messageTemplate = "{" + stateType.Name + ":l}";
                if (logger.BindProperty(stateType.Name, AsLoggableValue(state, formatter), false, out var stateTypeProperty))
                    properties.Add(stateTypeProperty);
            }
        }

        if (messageTemplate == null)
        {
            string? propertyName = null;
            if (state != null)
            {
                propertyName = "State";
                messageTemplate = "{State:l}";
            }
            else if (formatter != null)
            {
                propertyName = "Message";
                messageTemplate = "{Message:l}";
            }

            if (propertyName != null)
            {
                if (logger.BindProperty(propertyName, AsLoggableValue(state, formatter!), false, out var property))
                    properties.Add(property);
            }
        }

        if (eventId.Id != 0 || eventId.Name != null)
            properties.Add(CreateEventIdProperty(eventId));

        var parsedTemplate = MessageTemplateParser.Parse(messageTemplate ?? "");
        var currentActivity = Activity.Current;
        var evt = new LogEvent(DateTimeOffset.Now, logLevel, exception, parsedTemplate, properties, currentActivity?.TraceId ?? default, currentActivity?.SpanId ?? default);
        logger.Write(evt);
    }

    static object? AsLoggableValue<TState>(TState state, Func<TState, Exception?, string> formatter)
    {
        object? stateObject = state;
        if (formatter != null)
            stateObject = formatter(state, null);
        return stateObject;
    }

    static LogEventProperty CreateEventIdProperty(EventId eventId)
    {
        var properties = new List<LogEventProperty>(2);

        if (eventId.Id != 0)
        {
            properties.Add(new LogEventProperty("Id", new ScalarValue(eventId.Id)));
        }

        if (eventId.Name != null)
        {
            properties.Add(new LogEventProperty("Name", new ScalarValue(eventId.Name)));
        }

        return new LogEventProperty("EventId", new StructureValue(properties));
    }
}