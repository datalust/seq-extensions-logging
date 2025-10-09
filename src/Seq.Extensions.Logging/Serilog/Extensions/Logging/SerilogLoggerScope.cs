// Copyright (c) .NET Foundation. All rights reserved.
// Modifications Copyright (c) Datalust and Contributors
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Serilog.Core;
using Serilog.Events;

namespace Serilog.Extensions.Logging;

static class SerilogLoggerScope
{
    public static void EnrichAndCreateScopeItem(object? state, LogEvent logEvent, ILogEventPropertyValueFactory propertyFactory, out LogEventPropertyValue? scopeItem)
    {
        if (state == null)
        {
            scopeItem = null;
        }

        if (state is IEnumerable<KeyValuePair<string, object>> stateProperties)
        {
            scopeItem = null; // Unless it's `FormattedLogValues`, these are treated as property bags rather than scope items.

            foreach (var stateProperty in stateProperties)
            {
                if (stateProperty.Key == SerilogLoggerProvider.OriginalFormatPropertyName && stateProperty.Value is string)
                {
                    scopeItem = new ScalarValue(state.ToString());
                    continue;
                }

                var key = stateProperty.Key;
                var destructureObject = false;

                if (key.StartsWith("@"))
                {
                    key = key.Substring(1);
                    destructureObject = true;
                }

                var property = propertyFactory.CreatePropertyValue(stateProperty.Value, destructureObject);
                logEvent.AddOrUpdateProperty(key, property);
            }
        }
        else
        {
            scopeItem = propertyFactory.CreatePropertyValue(state);
        }
    }
}