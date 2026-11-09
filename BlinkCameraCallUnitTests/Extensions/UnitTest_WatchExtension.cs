using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_WatchExtension
{
    [Fact]
    public void Test_ExtractWatchIntervalSeconds_Present_ReturnsValue()
    {
        var arguments = new[] { "s45" };

        arguments.ExtractWatchIntervalSeconds().Should().Be(45);
    }

    [Fact]
    public void Test_ExtractWatchIntervalSeconds_Missing_DefaultsTo20()
    {
        var arguments = new[] { "nfoo" };

        arguments.ExtractWatchIntervalSeconds().Should().Be(20);
    }

    [Fact]
    public void Test_ExtractWatchIntervalSeconds_NonNumeric_DefaultsTo20()
    {
        var arguments = new[] { "snotanumber" };

        arguments.ExtractWatchIntervalSeconds().Should().Be(20);
    }

    [Fact]
    public void Test_ExtractWatchIntervalSeconds_NullArguments_DefaultsTo20()
    {
        string[]? arguments = null;

        arguments.ExtractWatchIntervalSeconds().Should().Be(20);
    }
}
