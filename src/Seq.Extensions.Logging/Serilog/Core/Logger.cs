// Copyright 2013-2016 Serilog Contributors
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

using Serilog.Core.Enrichers;
using Seq.Extensions.Logging;
using Serilog.Events;
using Serilog.Parameters;
using Microsoft.Extensions.Logging;

#pragma warning disable Serilog004 // Constant MessageTemplate verifier

namespace Serilog.Core;

/// <summary>
/// The core Serilog logging pipeline. A <see cref="Logger"/> must
/// be disposed to flush any events buffered within it. Most application
/// code should depend on <see cref="Logger"/>, not this class.
/// </summary>
sealed class Logger : ILogEventSink, IDisposable
{
    static readonly object[] NoPropertyValues = new object[0];

    readonly MessageTemplateProcessor _messageTemplateProcessor = new MessageTemplateProcessor(new PropertyValueConverter(10, int.MaxValue));
    readonly ILogEventSink _sink;
    readonly Action _dispose;
    readonly ILogEventEnricher _enricher;

    readonly LoggingLevelSwitch _levelSwitch;
    readonly LevelOverrideMap _overrideMap;

    internal Logger(
        LoggingLevelSwitch levelSwitch,
        ILogEventSink sink,
        Action dispose = null,
        LevelOverrideMap overrideMap = null)
        : this(sink, new ExceptionDataEnricher(), dispose, levelSwitch, overrideMap)
    {
    }

    // The messageTemplateProcessor, sink and enricher are required. Argument checks are dropped because
    // throwing from here breaks the logger's no-throw contract, and callers are all in this file anyway.
    Logger(
        ILogEventSink sink,
        ILogEventEnricher enricher,
        Action dispose = null,
        LoggingLevelSwitch levelSwitch = null,
        LevelOverrideMap overrideMap = null)
    {
        _sink = sink;
        _dispose = dispose;
        _levelSwitch = levelSwitch;
        _overrideMap = overrideMap;
        _enricher = enricher;
    }

    /// <summary>
    /// Create a logger that enriches log events via the provided enrichers.
    /// </summary>
    /// <param name="enricher">Enricher that applies in the context.</param>
    /// <returns>A logger that will enrich log events as specified.</returns>
    public Logger ForContext(ILogEventEnricher enricher)
    {
        if (enricher == null)
            return this; // No context here, so little point writing to SelfLog.

        return new Logger(
            this,
            enricher,
            null,
            _levelSwitch,
            _overrideMap);
    }

    /// <summary>
    /// Create a logger that enriches log events via the provided enrichers.
    /// </summary>
    /// <param name="enrichers">Enrichers that apply in the context.</param>
    /// <returns>A logger that will enrich log events as specified.</returns>
    public Logger ForContext(IEnumerable<ILogEventEnricher> enrichers)
    {
        if (enrichers == null)
            return this; // No context here, so little point writing to SelfLog.

        return ForContext(new SafeAggregateEnricher(enrichers));
    }

    /// <summary>
    /// Create a logger that enriches log events with the specified property.
    /// </summary>
    /// <param name="propertyName">The name of the property. Must be non-empty.</param>
    /// <param name="value">The property value.</param>
    /// <param name="destructureObjects">If true, the value will be serialized as a structured
    /// object if possible; if false, the object will be recorded as a scalar or simple array.</param>
    /// <returns>A logger that will enrich log events as specified.</returns>
    public Logger ForContext(string propertyName, object value, bool destructureObjects = false)
    {
        if (!LogEventProperty.IsValidName(propertyName))
        {
            SelfLog.WriteLine("Attempt to call ForContext() with invalid property name `{0}` (value: `{1}`)", propertyName, value);
            return this;
        }

        // It'd be nice to do the destructuring lazily, but unfortunately `value` may be mutated between
        // now and the first log event written...
        // A future optimization opportunity may be to implement ILogEventEnricher on LogEventProperty to
        // remove one more allocation.
        var enricher = new FixedPropertyEnricher(_messageTemplateProcessor.CreateProperty(propertyName, value, destructureObjects));

        var levelSwitch = _levelSwitch;
        if (_overrideMap != null && propertyName == Constants.SourceContextPropertyName)
        {
            var context = value as string;
            if (context != null)
                _overrideMap.GetEffectiveLevel(context, out levelSwitch);
        }

        return new Logger(
            this,
            enricher,
            null,
            levelSwitch,
            _overrideMap);
    }

    /// <summary>
    /// Create a logger that marks log events as being from the specified
    /// source type.
    /// </summary>
    /// <param name="source">Type generating log messages in the context.</param>
    /// <returns>A logger that will enrich log events as specified.</returns>
    public Logger ForContext(Type source)
    {
        if (source == null)
            return this; // Little point in writing to SelfLog here because we don't have any contextual information

        return ForContext(Constants.SourceContextPropertyName, source.FullName);
    }

    /// <summary>
    /// Create a logger that marks log events as being from the specified
    /// source type.
    /// </summary>
    /// <typeparam name="TSource">Type generating log messages in the context.</typeparam>
    /// <returns>A logger that will enrich log events as specified.</returns>
    public Logger ForContext<TSource>()
    {
        return ForContext(typeof(TSource));
    }

    /// <summary>
    /// Determine if events at the specified level will be passed through
    /// to the log sinks.
    /// </summary>
    /// <param name="level">Level to check.</param>
    /// <returns>True if the level is enabled; otherwise, false.</returns>
    public bool IsEnabled(LogLevel level)
    {

        return _levelSwitch == null ||
               (int)level >= (int)_levelSwitch.MinimumLevel;
    }

    /// <summary>
    /// Write an event to the log.
    /// </summary>
    /// <param name="logEvent">The event to write.</param>
    public void Write(LogEvent logEvent)
    {
        if (logEvent == null) return;
        if (!IsEnabled(logEvent.Level)) return;
        Dispatch(logEvent);
    }

    void ILogEventSink.Emit(LogEvent logEvent)
    {
        if (logEvent == null) throw new ArgumentNullException(nameof(logEvent));

        // Bypasses the level check so that child loggers
        // using this one as a sink can increase verbosity.
        Dispatch(logEvent);
    }

    void Dispatch(LogEvent logEvent)
    {
        // The enricher may be a "safe" aggregate one, but is most commonly bare and so
        // the exception handling from SafeAggregateEnricher is duplicated here.
        try
        {
            _enricher.Enrich(logEvent, _messageTemplateProcessor);
        }
        catch (Exception ex)
        {
            SelfLog.WriteLine("Exception {0} caught while enriching {1} with {2}.", ex, logEvent, _enricher);
        }

        _sink.Emit(logEvent);
    }

    /// <summary>
    /// Uses configured scalar conversion and destructuring rules to bind a set of properties to a
    /// message template. Returns false if the template or values are invalid (<summary>Logger</summary>
    /// methods never throw exceptions).
    /// </summary>
    /// <param name="messageTemplate">Message template describing an event.</param>
    /// <param name="propertyValues">Objects positionally formatted into the message template.</param>
    /// <param name="parsedTemplate">The internal representation of the template, which may be used to
    /// render the <paramref name="boundProperties"/> as text.</param>
    /// <param name="boundProperties">Captured properties from the template and <paramref name="propertyValues"/>.</param>
    /// <example>
    /// MessageTemplate template;
    /// IEnumerable&lt;LogEventProperty&gt; properties>;
    /// if (Log.BindMessageTemplate("Hello, {Name}!", new[] { "World" }, out template, out properties)
    /// {
    ///     var propsByName = properties.ToDictionary(p => p.Name, p => p.Value);
    ///     Console.WriteLine(template.Render(propsByName, null));
    ///     // -> "Hello, World!"
    /// }
    /// </example>
    public bool BindMessageTemplate(string messageTemplate, object[] propertyValues, out MessageTemplate parsedTemplate, out IEnumerable<LogEventProperty> boundProperties)
    {
        if (messageTemplate == null)
        {
            parsedTemplate = null;
            boundProperties = null;
            return false;
        }

        _messageTemplateProcessor.Process(messageTemplate, propertyValues, out parsedTemplate, out boundProperties);
        return true;
    }

    /// <summary>
    /// Uses configured scalar conversion and destructuring rules to bind a property value to its captured
    /// representation.
    /// </summary>
    /// <returns>True if the property could be bound, otherwise false (<summary>Logger</summary>
    /// <param name="propertyName">The name of the property. Must be non-empty.</param>
    /// <param name="value">The property value.</param>
    /// <param name="destructureObjects">If true, the value will be serialized as a structured
    /// object if possible; if false, the object will be recorded as a scalar or simple array.</param>
    /// <param name="property">The resulting property.</param>
    /// methods never throw exceptions).</returns>
    public bool BindProperty(string propertyName, object value, bool destructureObjects, out LogEventProperty property)
    {
        if (!LogEventProperty.IsValidName(propertyName))
        {
            property = null;
            return false;
        }

        property = _messageTemplateProcessor.CreateProperty(propertyName, value, destructureObjects);
        return true;
    }

    /// <summary>
    /// Close and flush the logging pipeline.
    /// </summary>
    public void Dispose()
    {
        _dispose?.Invoke();
    }
}