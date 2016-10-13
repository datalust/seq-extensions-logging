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

using System;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Enrichers;

namespace Serilog.Configuration
{
    /// <summary>
    /// Controls enrichment configuration.
    /// </summary>
    class LoggerEnrichmentConfiguration
    {
        readonly LoggerConfiguration _loggerConfiguration;
        readonly Action<ILogEventEnricher> _addEnricher;

        internal LoggerEnrichmentConfiguration(
            LoggerConfiguration loggerConfiguration,
            Action<ILogEventEnricher> addEnricher)
        {
            if (loggerConfiguration == null) throw new ArgumentNullException(nameof(loggerConfiguration));
            if (addEnricher == null) throw new ArgumentNullException(nameof(addEnricher));
            _loggerConfiguration = loggerConfiguration;
            _addEnricher = addEnricher;
        }

        /// <summary>
        /// Enrich log events with properties from <see cref="Context.LogContext"/>.
        /// </summary>
        /// <returns>Configuration object allowing method chaining.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public LoggerConfiguration FromLogContext()
        {
            _addEnricher(new LogContextEnricher());
            return _loggerConfiguration;
        }
    }
}
