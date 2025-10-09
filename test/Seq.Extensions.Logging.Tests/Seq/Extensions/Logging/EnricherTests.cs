using Serilog.Parameters;
using Serilog.Events;
using Seq.Extensions.Logging;
using Tests.Support;
using Xunit;

namespace Tests.Seq.Extensions.Logging;

public class EnricherTests
{
    [Fact]
    public void EnrichersAreAppliedInOrder()
    {
        var evt = Some.EmptyLogEvent();

        new Enricher([
            enrichingEvent => enrichingEvent.AddPropertyIfAbsent("A", 1),
            enrichingEvent => enrichingEvent.AddPropertyIfAbsent("A", 2),
            enrichingEvent => enrichingEvent.AddOrUpdateProperty("B", 1),
            enrichingEvent => enrichingEvent.AddOrUpdateProperty("B", 2),
        ])
        .Enrich(
            evt,
            new PropertyValueConverter(int.MaxValue, int.MaxValue)
        );

        Assert.Equal(1, ((ScalarValue)evt.Properties["A"]).Value);
        Assert.Equal(2, ((ScalarValue)evt.Properties["B"]).Value);
    }

    [Fact]
    public void FailingEnricherIsHandled()
    {
        var evt = Some.EmptyLogEvent();

        new Enricher([
            enrichingEvent => enrichingEvent.AddOrUpdateProperty("A", 1),
            _ => throw new Exception("Enricher Failed"),
            enrichingEvent => enrichingEvent.AddOrUpdateProperty("A", 2),
        ])
        .Enrich(
            evt,
            new PropertyValueConverter(int.MaxValue, int.MaxValue)
        );

        Assert.Equal(2, ((ScalarValue)evt.Properties["A"]).Value);
    }
}