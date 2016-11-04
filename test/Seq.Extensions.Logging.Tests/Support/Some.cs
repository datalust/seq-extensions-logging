using System;
using System.Collections.Generic;
using Serilog.Events;
using Xunit.Sdk;
using Serilog.Core;
using Microsoft.Extensions.Logging;

namespace Tests.Support
{
    static class Some
    {
        public static LogEvent LogEvent(string messageTemplate, params object[] propertyValues)
        {
            return LogEvent(null, messageTemplate, propertyValues);
        }

        public static LogEvent LogEvent(Exception exception, string messageTemplate, params object[] propertyValues)
        {
            return LogEvent(LogLevel.Information, exception, messageTemplate, propertyValues);
        }

        public static LogEvent LogEvent(LogLevel level, Exception exception, string messageTemplate, params object[] propertyValues)
        {
            var log = new Logger(null, null, null);
            MessageTemplate template;
            IEnumerable<LogEventProperty> properties;
#pragma warning disable Serilog004 // Constant MessageTemplate verifier
            if (!log.BindMessageTemplate(messageTemplate, propertyValues, out template, out properties))
#pragma warning restore Serilog004 // Constant MessageTemplate verifier
            {
                throw new XunitException("Template could not be bound.");
            }
            return new LogEvent(DateTimeOffset.Now, level, exception, template, properties);
        }

        public static LogEvent DebugEvent()
        {
            return LogEvent(LogLevel.Debug, null, "Debug event");
        }

        public static LogEvent InformationEvent()
        {
            return LogEvent(LogLevel.Information, null, "Information event");
        }

        public static LogEvent ErrorEvent()
        {
            return LogEvent(LogLevel.Error, null, "Error event");
        }
    }
}
