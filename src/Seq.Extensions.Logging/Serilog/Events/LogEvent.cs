﻿// Copyright 2013-2015 Serilog Contributors
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

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Serilog.Events;

/// <summary>
/// A log event.
/// </summary>
class LogEvent
{
    readonly Dictionary<string, LogEventPropertyValue> _properties;
    ActivityTraceId _traceId;
    ActivitySpanId _spanId;

    /// <summary>
    /// Construct a new <seealso cref="LogEvent"/>.
    /// </summary>
    /// <param name="timestamp">The time at which the event occurred.</param>
    /// <param name="level">The level of the event.</param>
    /// <param name="exception">An exception associated with the event, or null.</param>
    /// <param name="messageTemplate">The message template describing the event.</param>
    /// <param name="properties">Properties associated with the event, including those presented in <paramref name="messageTemplate"/>.</param>
    /// <param name="traceId">The id of the trace that was active when the event was created, if any.</param>
    /// <param name="spanId">The id of the span that was active when the event was created, if any.</param>
    public LogEvent(
        DateTimeOffset timestamp,
        LogLevel level,
        Exception exception,
        MessageTemplate messageTemplate,
        IEnumerable<LogEventProperty> properties,
        ActivityTraceId traceId,
        ActivitySpanId spanId)
    {
        if (properties == null) throw new ArgumentNullException(nameof(properties));
        _traceId = traceId;
        _spanId = spanId;
        Timestamp = timestamp;
        Level = level;
        Exception = exception;
        MessageTemplate = messageTemplate ?? throw new ArgumentNullException(nameof(messageTemplate));
        _properties = new Dictionary<string, LogEventPropertyValue>();
        foreach (var p in properties)
            AddOrUpdateProperty(p);
    }

    /// <summary>
    /// The time at which the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The level of the event.
    /// </summary>
    public LogLevel Level { get; }

    /// <summary>
    /// The id of the trace that was active when the event was created, if any.
    /// </summary>
    public ActivityTraceId? TraceId => _traceId == default ? null : _traceId;

    /// <summary>
    /// The id of the span that was active when the event was created, if any.
    /// </summary>
    public ActivitySpanId? SpanId => _spanId == default ? null : _spanId;

    /// <summary>
    /// The message template describing the event.
    /// </summary>
    public MessageTemplate MessageTemplate { get; }

    /// <summary>
    /// Render the message template to the specified output, given the properties associated
    /// with the event.
    /// </summary>
    /// <param name="output">The output.</param>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    public void RenderMessage(TextWriter output, IFormatProvider formatProvider = null)
    {
        MessageTemplate.Render(Properties, output, formatProvider);
    }

    /// <summary>
    /// Render the message template given the properties associated
    /// with the event, and return the result.
    /// </summary>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    public string RenderMessage(IFormatProvider formatProvider = null)
    {
        return MessageTemplate.Render(Properties, formatProvider);
    }

    /// <summary>
    /// Properties associated with the event, including those presented in <see cref="LogEvent.MessageTemplate"/>.
    /// </summary>
    public IReadOnlyDictionary<string, LogEventPropertyValue> Properties => _properties;

    /// <summary>
    /// An exception associated with the event, or null.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Add a property to the event if not already present, otherwise, update its value.
    /// </summary>
    /// <param name="property">The property to add or update.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddOrUpdateProperty(LogEventProperty property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        _properties[property.Name] = property.Value;
    }

    /// <summary>
    /// Add a property to the event if not already present.
    /// </summary>
    /// <param name="property">The property to add.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddPropertyIfAbsent(LogEventProperty property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        if (!_properties.ContainsKey(property.Name))
            _properties.Add(property.Name, property.Value);
    }

    /// <summary>
    /// Remove a property from the event, if present. Otherwise no action
    /// is performed.
    /// </summary>
    /// <param name="propertyName">The name of the property to remove.</param>
    public void RemovePropertyIfPresent(string propertyName)
    {
        _properties.Remove(propertyName);
    }
}
