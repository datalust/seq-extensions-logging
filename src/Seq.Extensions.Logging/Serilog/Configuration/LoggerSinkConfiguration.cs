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

using System;
using System.ComponentModel;
using Serilog.Core;
using Serilog.Core.Sinks;
using Seq.Extensions.Logging;
using Serilog.Events;

namespace Serilog.Configuration
{
    /// <summary>
    /// Controls sink configuration.
    /// </summary>
    class LoggerSinkConfiguration
    {
        readonly LoggerConfiguration _loggerConfiguration;
        readonly Action<ILogEventSink> _addSink;
        readonly Action<LoggerConfiguration> _applyInheritedConfiguration;

        internal LoggerSinkConfiguration(LoggerConfiguration loggerConfiguration, Action<ILogEventSink> addSink, Action<LoggerConfiguration> applyInheritedConfiguration)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (addSink == null) throw new ArgumentNullException(nameof(addSink));
            if (applyInheritedConfiguration == null) throw new ArgumentNullException(nameof(applyInheritedConfiguration));
            _loggerConfiguration = loggerConfiguration;
            _addSink = addSink;
            _applyInheritedConfiguration = applyInheritedConfiguration;
        }

        /// <summary>
        /// Write log events to the specified <see cref="ILogEventSink"/>.
        /// </summary>
        /// <param name="logEventSink">The sink.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <remarks>Provided for binary compatibility for earlier versions,
        /// should be removed in 3.0. Not marked obsolete because warnings
        /// would be syntactically annoying to avoid.</remarks>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public LoggerConfiguration Sink(
            ILogEventSink logEventSink,
            LogEventLevel restrictedToMinimumLevel)
        {
            return Sink(logEventSink, restrictedToMinimumLevel, null);
        }

        /// <summary>
        /// Write log events to the specified <see cref="ILogEventSink"/>.
        /// </summary>
        /// <param name="logEventSink">The sink.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        public LoggerConfiguration Sink(
            ILogEventSink logEventSink,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            // ReSharper disable once MethodOverloadWithOptionalParameter
            LoggingLevelSwitch levelSwitch = null)
        {
            var sink = logEventSink;
            if (levelSwitch != null)
            {
                if (restrictedToMinimumLevel != LevelAlias.Minimum)
                    SelfLog.WriteLine("Sink {0} was configured with both a level switch and minimum level '{1}'; the minimum level will be ignored and the switch level used", sink, restrictedToMinimumLevel);

                sink = new RestrictedSink(sink, levelSwitch);
            }
            else if (restrictedToMinimumLevel > LevelAlias.Minimum)
            {
                sink = new RestrictedSink(sink, new LoggingLevelSwitch(restrictedToMinimumLevel));
            }

            _addSink(sink);
            return _loggerConfiguration;
        }

        /// <summary>
        /// Write log events to the specified <see cref="ILogEventSink"/>.
        /// </summary>
        /// <typeparam name="TSink">The sink.</typeparam>
        /// <param name="restrictedToMinimumLevel">The minimum level for
        /// events passed through the sink. Ignored when <paramref name="levelSwitch"/> is specified.</param>
        /// <param name="levelSwitch">A switch allowing the pass-through minimum level
        /// to be changed at runtime.</param>
        /// <returns>Configuration object allowing method chaining.</returns>
        public LoggerConfiguration Sink<TSink>(
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LoggingLevelSwitch levelSwitch = null)
            where TSink : ILogEventSink, new()
        {
            return Sink(new TSink(), restrictedToMinimumLevel, levelSwitch);
        }
    }
}
