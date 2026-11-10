using BlinkCameraCall.Driver;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Driver;

public class UnitTest_AdaptivePollInterval
{
    [Fact]
    public void Test_ReportRound_FasterThanCurrent_ReturnsNullAndLeavesCurrentUnchanged()
    {
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(20));

        var result = interval.ReportRound(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        result.Should().BeNull();
        interval.Current.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Test_ReportRound_ExactlyCurrent_ReturnsNull()
    {
        // totalRoundElapsed == Current (not greater) must not retune — boundary check.
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(20));

        var result = interval.ReportRound(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));

        result.Should().BeNull();
        interval.Current.Should().Be(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Test_ReportRound_SlowerThanCurrent_ReturnsSlowestItemPlusOneSecondAndUpdatesCurrent()
    {
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(5));

        var result = interval.ReportRound(TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(12));

        result.Should().Be(TimeSpan.FromSeconds(13));
        interval.Current.Should().Be(TimeSpan.FromSeconds(13));
    }

    [Fact]
    public void Test_ReportRound_SingleItemRound_TotalAndSlowestAreTheSameValue()
    {
        // Mirrors how WATCH calls this (one check per round: both arguments are the same value).
        var interval = new AdaptivePollInterval(TimeSpan.FromSeconds(20));

        var result = interval.ReportRound(TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25));

        result.Should().Be(TimeSpan.FromSeconds(26));
    }
}
