// Copyright 2016-2020 Datalust and Contributors
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

using Serilog.Core;
using Serilog.Events;
using System.Collections;

namespace Seq.Extensions.Logging;

class ExceptionDataEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyValueFactory propertyFactory)
    {
        var exceptionData = logEvent.Exception?.GetBaseException().Data;
        if (exceptionData == null || exceptionData.Count == 0)
            return;

        var data = exceptionData
            .Cast<DictionaryEntry>()
            .Where(e => e.Key is string)
            .Select(e => new LogEventProperty((string)e.Key, propertyFactory.CreatePropertyValue(e.Value)));

        logEvent.AddPropertyIfAbsent("ExceptionData", new StructureValue(data));
    }
}
