using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_MediaCommandExtensions
{
    [Fact]
    public void Test_ExtractDeviceName_Successful()
    {
        var arguments = new[] { "nPergoda" };

        arguments.ExtractDeviceName().Should().Be("Pergoda");
    }

    [Fact]
    public void Test_ExtractDeviceName_MultiWordName_Successful()
    {
        var arguments = new[] { "nFront Door Bell" };

        arguments.ExtractDeviceName().Should().Be("Front Door Bell");
    }

    [Fact]
    public void Test_ExtractDeviceName_MissingArgument_Throws()
    {
        var arguments = new[] { "ifoo" };

        Assert.Throws<ArgumentException>(() => arguments.ExtractDeviceName());
    }

    [Fact]
    public void Test_ExtractDeviceName_NullArguments_Throws()
    {
        string[]? arguments = null;

        Assert.Throws<ArgumentException>(() => arguments.ExtractDeviceName());
    }
}
