// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Extensions.Logging
{
    readonly struct SerilogLoggerScope
    {
        const string NoName = "None";

        readonly object _state;

        public SerilogLoggerScope(object state)
        {
            _state = state;
        }

        public void EnrichAndCreateScopeItem(LogEvent logEvent, ILogEventPropertyFactory propertyFactory, out LogEventPropertyValue scopeItem)
        {
            if (_state == null)
            {
                scopeItem = null;
                return;
            }

            var stateProperties = _state as IEnumerable<KeyValuePair<string, object>>;
            if (stateProperties != null)
            {
                scopeItem = null; // Unless it's `FormattedLogValues`, these are treated as property bags rather than scope items.

                foreach (var stateProperty in stateProperties)
                {
                    if (stateProperty.Key == SerilogLoggerProvider.OriginalFormatPropertyName && stateProperty.Value is string)
                    {
                        scopeItem = new ScalarValue(_state.ToString());
                        continue;
                    }

                    var key = stateProperty.Key;
                    var destructureObject = false;

                    if (key.StartsWith("@"))
                    {
                        key = key.Substring(1);
                        destructureObject = true;
                    }

                    var property = propertyFactory.CreateProperty(key, stateProperty.Value, destructureObject);
                    logEvent.AddOrUpdateProperty(property);
                }
            }
            else
            {
                scopeItem = propertyFactory.CreateProperty(NoName, _state).Value;
            }
        }
    }
}
