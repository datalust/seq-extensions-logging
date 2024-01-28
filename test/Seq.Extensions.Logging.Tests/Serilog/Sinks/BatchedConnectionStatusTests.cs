// Copyright 2017 Serilog Contributors
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
using System.Globalization;
using Xunit;
using Serilog.Sinks.PeriodicBatching;

namespace Seq.Extensions.Logging.Tests.Serilog.Sinks;

public class BatchedConnectionStatusTests
{
    readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(2);

    [Fact]
    public void WhenNoFailuresHaveOccurredTheRegularIntervalIsUsed()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        Assert.Equal(DefaultPeriod, bcs.NextInterval);
    }

    [Fact]
    public void WhenOneFailureHasOccurredTheRegularIntervalIsUsed()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        bcs.MarkFailure();
        Assert.Equal(DefaultPeriod, bcs.NextInterval);
    }

    [Fact]
    public void WhenTwoFailuresHaveOccurredTheIntervalBacksOff()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        bcs.MarkFailure();
        bcs.MarkFailure();
        Assert.Equal(TimeSpan.FromSeconds(10), bcs.NextInterval);
    }

    [Fact]
    public void WhenABatchSucceedsTheStatusResets()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        bcs.MarkFailure();
        bcs.MarkFailure();
        bcs.MarkSuccess();
        Assert.Equal(DefaultPeriod, bcs.NextInterval);
    }

    [Fact]
    public void WhenThreeFailuresHaveOccurredTheIntervalBacksOff()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        bcs.MarkFailure();
        bcs.MarkFailure();
        bcs.MarkFailure();
        Assert.Equal(TimeSpan.FromSeconds(20), bcs.NextInterval);
        Assert.False(bcs.ShouldDropBatch);
    }

    [Fact]
    public void When8FailuresHaveOccurredTheIntervalBacksOffAndBatchIsDropped()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        for (var i = 0; i < 8; ++i)
        {
            Assert.False(bcs.ShouldDropBatch);
            bcs.MarkFailure();
        }
        Assert.Equal(TimeSpan.FromMinutes(10), bcs.NextInterval);
        Assert.True(bcs.ShouldDropBatch);
        Assert.False(bcs.ShouldDropQueue);
    }

    [Fact]
    public void When10FailuresHaveOccurredTheQueueIsDropped()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        for (var i = 0; i < 10; ++i)
        {
            Assert.False(bcs.ShouldDropQueue);
            bcs.MarkFailure();
        }
        Assert.True(bcs.ShouldDropQueue);
    }

    [Fact]
    public void AtTheDefaultIntervalRetriesFor10MinutesBeforeDroppingBatch()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        var cumulative = TimeSpan.Zero;
        do
        {
            bcs.MarkFailure();

            if (!bcs.ShouldDropBatch)
                cumulative += bcs.NextInterval;
        } while (!bcs.ShouldDropBatch);

        Assert.False(bcs.ShouldDropQueue);
        Assert.Equal(TimeSpan.Parse("00:10:32", CultureInfo.InvariantCulture), cumulative);
    }

    [Fact]
    public void AtTheDefaultIntervalRetriesFor30MinutesBeforeDroppingQueue()
    {
        var bcs = new BatchedConnectionStatus(DefaultPeriod);
        var cumulative = TimeSpan.Zero;
        do
        {
            bcs.MarkFailure();

            if (!bcs.ShouldDropQueue)
                cumulative += bcs.NextInterval;
        } while (!bcs.ShouldDropQueue);

        Assert.Equal(TimeSpan.Parse("00:30:32", CultureInfo.InvariantCulture), cumulative);
    }
}