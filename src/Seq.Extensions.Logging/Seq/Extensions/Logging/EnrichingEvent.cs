using Serilog.Core;
using Serilog.Events;

namespace Seq.Extensions.Logging;

/// <summary>
/// The input to an enricher.
/// </summary>
public readonly struct EnrichingEvent
{
    internal EnrichingEvent(LogEvent logEvent, ILogEventPropertyValueFactory propertyFactory)
    {
        _propertyFactory = propertyFactory;
        LogEvent = logEvent;
    }

    readonly ILogEventPropertyValueFactory _propertyFactory;

    internal LogEvent LogEvent { get; }

    /// <summary>
    /// Add a property to the event if not already present, otherwise, update its value.
    /// </summary>
    public void AddOrUpdateProperty(string propertyName, object propertyValue, bool serialize = false)
    {
        LogEvent.AddOrUpdateProperty(propertyName, _propertyFactory.CreatePropertyValue(propertyValue, serialize));
    }

    /// <summary>
    /// Add a property to the event if not already present.
    /// </summary>
    public void AddPropertyIfAbsent(string propertyName, object propertyValue, bool serialize = false)
    {
        if (LogEvent.Properties.ContainsKey(propertyName))
            return;

        AddOrUpdateProperty(propertyName, propertyValue, serialize);
    }
}
