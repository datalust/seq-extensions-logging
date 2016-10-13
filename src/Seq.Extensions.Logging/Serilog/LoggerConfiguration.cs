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

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Core.Sinks;
using Serilog.Events;
using Serilog.Parameters;
using Microsoft.Extensions.Logging;

namespace Serilog
{
    /// <summary>
    /// Configuration object for creating <see cref="Logger"/> instances.
    /// </summary>
    class LoggerConfiguration
    {
        readonly List<ILogEventSink> _logEventSinks = new List<ILogEventSink>();
        readonly List<ILogEventEnricher> _enrichers = new List<ILogEventEnricher>();
        readonly Dictionary<string, LoggingLevelSwitch> _overrides = new Dictionary<string, LoggingLevelSwitch>();
        LogLevel _minimumLevel = LogLevel.Information;
        LoggingLevelSwitch _levelSwitch;
        int _maximumDestructuringDepth = 10;
        int _maximumStringLength = int.MaxValue;
        bool _loggerCreated;

        void ApplyInheritedConfiguration(LoggerConfiguration child)
        {
            if (_levelSwitch != null)
                child.MinimumLevel.ControlledBy(_levelSwitch);
            else
                child.MinimumLevel.Is(_minimumLevel);
        }

        /// <summary>
        /// Configures the sinks that log events will be emitted to.
        /// </summary>
        public LoggerSinkConfiguration WriteTo => new LoggerSinkConfiguration(this, s => _logEventSinks.Add(s), ApplyInheritedConfiguration);
        
        /// <summary>
        /// Configures the minimum level at which events will be passed to sinks. If
        /// not specified, only events at the Information level and above will be passed through.
        /// </summary>
        /// <returns>Configuration object allowing method chaining.</returns>
        public LoggerMinimumLevelConfiguration MinimumLevel
        {
            get
            {
                return new LoggerMinimumLevelConfiguration(this,
                    l => {
                        _minimumLevel = l;
                        _levelSwitch = null;
                    },
                    sw => _levelSwitch = sw,
                    (s, lls) => _overrides[s] = lls);
            }
        }

        /// <summary>
        /// Configures enrichment of <see cref="LogEvent"/>s. Enrichers can add, remove and
        /// modify the properties associated with events.
        /// </summary>
        public LoggerEnrichmentConfiguration Enrich => new LoggerEnrichmentConfiguration(this, e => _enrichers.Add(e));

        /// <summary>
        /// Create a logger using the configured sinks, enrichers and minimum level.
        /// </summary>
        /// <returns>The logger.</returns>
        /// <remarks>To free resources held by sinks ahead of program shutdown,
        /// the returned logger may be cast to <see cref="IDisposable"/> and
        /// disposed.</remarks>
        public Logger CreateLogger()
        {
            if (_loggerCreated)
                throw new InvalidOperationException("CreateLogger() was previously called and can only be called once.");
            _loggerCreated = true;

            Action dispose = () =>
            {
                foreach (var disposable in _logEventSinks.OfType<IDisposable>())
                    disposable.Dispose();
            };

            ILogEventSink sink = new SafeAggregateSink(_logEventSinks);

            var converter = new PropertyValueConverter(
                _maximumDestructuringDepth, 
                _maximumStringLength);

            var processor = new MessageTemplateProcessor(converter);

            ILogEventEnricher enricher;
            switch (_enrichers.Count)
            {
                case 0:
                    // Should be a rare case, so no problem making that extra interface dispatch.
                    enricher = new EmptyEnricher();
                    break;
                case 1:
                    enricher = _enrichers[0];
                    break;
                default:
                    enricher = new SafeAggregateEnricher(_enrichers);
                    break;
            }

            LevelOverrideMap overrideMap = null;
            if (_overrides.Count != 0)
            {
                overrideMap = new LevelOverrideMap(_overrides, _minimumLevel, _levelSwitch);
            }

            return _levelSwitch == null ?
                new Logger(processor, _minimumLevel, sink, enricher, dispose, overrideMap) :
                new Logger(processor, _levelSwitch, sink, enricher, dispose, overrideMap);
        }
    }
}
