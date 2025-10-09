using Xunit;
using Serilog.Parameters;
using Serilog.Events;
using Seq.Extensions.Logging;
using Tests.Support;

namespace Tests.Seq.Extensions.Logging;

public class EnrichingEventTests
{
    [Fact]
    public void AddPropertyIfAbsentAddsProperties()
    {
        var enriching = new EnrichingEvent(
            Some.EmptyLogEvent(),
            new PropertyValueConverter(int.MaxValue, int.MaxValue)
        );

        enriching.AddPropertyIfAbsent("A", false);
        enriching.AddPropertyIfAbsent("A", true);

        Assert.Equal(false, ((ScalarValue)enriching.LogEvent.Properties["A"]).Value);
    }

    [Fact]
    public void AddOrUpdatePropertyAddsProperties()
    {
        var enriching = new EnrichingEvent(
            Some.EmptyLogEvent(),
            new PropertyValueConverter(int.MaxValue, int.MaxValue)
        );

        enriching.AddOrUpdateProperty("A", false);
        enriching.AddOrUpdateProperty("A", true);

        Assert.Equal(true, ((ScalarValue)enriching.LogEvent.Properties["A"]).Value);
    }
}
