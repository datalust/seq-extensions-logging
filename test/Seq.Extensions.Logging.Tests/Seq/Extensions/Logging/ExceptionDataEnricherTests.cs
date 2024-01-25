using Seq.Extensions.Logging;
using Serilog.Events;
using System;
using System.Linq;
using Tests.Support;
using Xunit;

namespace Tests.Seq.Extensions.Logging;

public class ExceptionDataEnricherTests
{
    [Fact]
    public void WhenNoDataIsPresentNoPropertyIsAdded()
    {
        var enricher = new ExceptionDataEnricher();
        var exception = new Exception();
        var evt = Some.ErrorEvent(exception);

        enricher.Enrich(evt, Some.PropertyFactory());

        Assert.Equal(0, evt.Properties.Count);
    }

    [Fact]
    public void WhenDataIsPresentThePropertyIsAdded()
    {
        var enricher = new ExceptionDataEnricher();
        var exception = new Exception()
        {
            Data =
            {
                ["A"] = 42,
                ["B"] = "Hello"
            }
        };
        var evt = Some.ErrorEvent(exception);

        enricher.Enrich(evt, Some.PropertyFactory());

        Assert.Equal(1, evt.Properties.Count);
        var data = evt.Properties["ExceptionData"];
        var value = Assert.IsType<StructureValue>(data);
        Assert.Equal(2, value.Properties.Count);
        var a = Assert.IsType<ScalarValue>(value.Properties.Single(p => p.Name == "A").Value);
        Assert.Equal(42, a.Value);
        var b = Assert.IsType<ScalarValue>(value.Properties.Single(p => p.Name == "B").Value);
        Assert.Equal("Hello", b.Value);
    }
}