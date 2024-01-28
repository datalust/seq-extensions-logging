using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Sinks.Seq;
using Tests.Support;
using Xunit;

namespace Tests.Serilog.Sinks.Seq;

public class ControlledLevelSwitchTests
{
    [Fact]
    public void WhenTheServerSendsALevelTheSwitchIsAdjusted()
    {
        var lls = new LoggingLevelSwitch(LogLevel.Warning);
        var cls = new ControlledLevelSwitch(lls);
        cls.Update(LogLevel.Debug);
        Assert.Equal(LogLevel.Debug, lls.MinimumLevel);
    }

    [Fact]
    public void WhenTheServerSendsNoLevelTheSwitchIsNotInitiallyAdjusted()
    {
        var lls = new LoggingLevelSwitch(LogLevel.Warning);
        lls.MinimumLevel = LogLevel.Critical;
        var cls = new ControlledLevelSwitch(lls);
        cls.Update(null);
        Assert.Equal(LogLevel.Critical, lls.MinimumLevel);
    }

    [Fact]
    public void WhenTheServerSendsNoLevelTheSwitchIsResetIfPreviouslyAdjusted()
    {
        var lls = new LoggingLevelSwitch(LogLevel.Warning);
        var cls = new ControlledLevelSwitch(lls);
        cls.Update(LogLevel.Information);
        cls.Update(null);
        Assert.Equal(LogLevel.Warning, lls.MinimumLevel);
    }

    [Fact]
    public void WithNoSwitchToControlAllEventsAreIncluded()
    {
        var cls = new ControlledLevelSwitch(null);
        Assert.True(cls.IsIncluded(Some.DebugEvent()));
    }

    [Fact]
    public void WithNoSwitchToControlEventsAreStillFiltered()
    {
        var cls = new ControlledLevelSwitch(null);
        cls.Update(LogLevel.Warning);
        Assert.True(cls.IsIncluded(Some.ErrorEvent()));
        Assert.False(cls.IsIncluded(Some.InformationEvent()));
    }

    [Fact]
    public void WithNoSwitchToControlAllEventsAreIncludedAfterReset()
    {
        var cls = new ControlledLevelSwitch(null);
        cls.Update(LogLevel.Warning);
        cls.Update(null);
        Assert.True(cls.IsIncluded(Some.DebugEvent()));
    }

    [Fact]
    public void WhenControllingASwitchTheControllerIsActive()
    {
        var cls = new ControlledLevelSwitch(new LoggingLevelSwitch());
        Assert.True(cls.IsActive);
    }

    [Fact]
    public void WhenNotControllingASwitchTheControllerIsNotActive()
    {
        var cls = new ControlledLevelSwitch();
        Assert.False(cls.IsActive);
    }

    [Fact]
    public void AfterServerControlhTheControllerIsAlwaysActive()
    {
        var cls = new ControlledLevelSwitch();

        cls.Update(LogLevel.Information);
        Assert.True(cls.IsActive);

        cls.Update(null);
        Assert.True(cls.IsActive);
    }
}