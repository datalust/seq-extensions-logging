// Copyright 2013-2015 Serilog Contributors
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
    readonly ActivityTraceId _traceId;
    readonly ActivitySpanId _spanId;

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
        Exception? exception,
        MessageTemplate messageTemplate,
        Dictionary<string, LogEventPropertyValue> properties,
        ActivityTraceId traceId,
        ActivitySpanId spanId)
    {
        _traceId = traceId;
        _spanId = spanId;
        Timestamp = timestamp;
        Level = level;
        Exception = exception;
        MessageTemplate = messageTemplate;
        _properties = properties;
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
    public void RenderMessage(TextWriter output, IFormatProvider? formatProvider = null)
    {
        MessageTemplate.Render(Properties, output, formatProvider);
    }

    /// <summary>
    /// Render the message template given the properties associated
    /// with the event, and return the result.
    /// </summary>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    public string RenderMessage(IFormatProvider? formatProvider = null)
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
    public Exception? Exception { get; }

    /// <summary>
    /// Add a property to the event if not already present, otherwise, update its value.
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddOrUpdateProperty(string propertyName, LogEventPropertyValue propertyValue)
    {
        _properties[propertyName] = propertyValue;
    }

    /// <summary>
    /// Add a property to the event if not already present.
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    public void AddPropertyIfAbsent(string propertyName, LogEventPropertyValue propertyValue)
    {
        if (!_properties.ContainsKey(propertyName))
            _properties.Add(propertyName, propertyValue);
    }
}
