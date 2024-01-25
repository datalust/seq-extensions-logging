// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Tests.Serilog.Extensions.Logging.Support;

class SerilogSink : ILogEventSink
{
    public List<LogEvent> Writes { get; } = [];
    public LogEvent SingleWrite => Assert.Single(Writes);

    public void Emit(LogEvent logEvent)
    {
        Writes.Add(logEvent);
    }
}