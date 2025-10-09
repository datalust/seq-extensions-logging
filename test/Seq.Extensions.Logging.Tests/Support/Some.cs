using Serilog.Events;
using Xunit.Sdk;
using Serilog.Core;
using Microsoft.Extensions.Logging;
using Serilog.Parameters;
// ReSharper disable MemberCanBePrivate.Global

namespace Tests.Support;

static class Some
{
    public static LogEvent EmptyLogEvent()
    {
        return new LogEvent(
            default,
            default,
            null,
            new MessageTemplate("", []),
            new(),
            default,
            default
        );
    }
    
    public static LogEvent LogEvent(string messageTemplate, params object?[] propertyValues)
    {
        return LogEvent(null, messageTemplate, propertyValues);
    }

    public static LogEvent LogEvent(Exception? exception, string messageTemplate, params object?[] propertyValues)
    {
        return LogEvent(LogLevel.Information, exception, messageTemplate, propertyValues);
    }

    public static ILogEventPropertyValueFactory PropertyValueFactory()
    {
        return new PropertyValueConverter(10, 1024);
    }

    public static LogEvent LogEvent(LogLevel level, Exception? exception, string messageTemplate, params object?[] propertyValues)
    {
        var log = new Logger(null!, null!);
#pragma warning disable Serilog004 // Constant MessageTemplate verifier
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (!log.BindMessageTemplate(messageTemplate, propertyValues, out var template, out var properties))
#pragma warning restore Serilog004 // Constant MessageTemplate verifier
        {
            throw new XunitException("Template could not be bound.");
        }
        return new LogEvent(DateTimeOffset.Now, level, exception, template, properties.ToDictionary(p => p.Name, p => p.Value), default, default);
    }

    public static LogEvent DebugEvent()
    {
        return LogEvent(LogLevel.Debug, null, "Debug event");
    }

    public static LogEvent InformationEvent()
    {
        return LogEvent(LogLevel.Information, null, "Information event");
    }

    public static LogEvent ErrorEvent(Exception? exception = null)
    {
        return LogEvent(LogLevel.Error, exception, "Error event");
    }

    static int _next;

    public static int Int()
    {
        return Interlocked.Increment(ref _next);
    }

    public static string String()
    {
        return $"S_{Int()}";
    }
}