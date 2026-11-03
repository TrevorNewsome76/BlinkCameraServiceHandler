using BlinkCameraCall.Extensions;
using BlinkCommon.Interfaces.System;
using FluentAssertions;
using Shadow.Quack;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_CloudMediaExtension
{
    [Fact]
    public void Test_ExtractHours_Present_ReturnsValue()
    {
        var arguments = new[] { "h48" };

        arguments.ExtractHours().Should().Be(48);
    }

    [Fact]
    public void Test_ExtractHours_Missing_DefaultsTo24()
    {
        var arguments = new[] { "ifoo" };

        arguments.ExtractHours().Should().Be(24);
    }

    [Fact]
    public void Test_ExtractHours_NonNumeric_DefaultsTo24()
    {
        var arguments = new[] { "hnotanumber" };

        arguments.ExtractHours().Should().Be(24);
    }

    [Fact]
    public void Test_ExtractMediaId_Present_ReturnsValue()
    {
        var arguments = new[] { "i12345" };

        arguments.ExtractMediaId().Should().Be(12345L);
    }

    [Fact]
    public void Test_ExtractMediaId_Missing_Throws()
    {
        var arguments = new[] { "hfoo" };

        Assert.Throws<ArgumentException>(() => arguments.ExtractMediaId());
    }

    [Fact]
    public void Test_ExtractMediaId_NonNumeric_Throws()
    {
        var arguments = new[] { "inotanumber" };

        Assert.Throws<ArgumentException>(() => arguments.ExtractMediaId());
    }

    [Fact]
    public void Test_ToCloudMediaListString_NonEmptyList_PrintsNumberedTable()
    {
        var items = new List<ICloudMediaItem>
        {
            Duck.Implement<ICloudMediaItem>(new
            {
                Id = 2001L,
                Network_Id = 263178L,
                Device_Id = 321343L,
                Device_Name = "Pergoda",
                Created_At = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
                Media = "/media/clip1.mp4",
                Deleted = false,
            }),
            Duck.Implement<ICloudMediaItem>(new
            {
                Id = 2002L,
                Network_Id = 263178L,
                Device_Id = 321380L,
                Device_Name = "Garage",
                Created_At = new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc),
                Media = "/media/clip2.mp4",
                Deleted = false,
            }),
        };

        var expected =
            "[1] Id 2001  Device: Pergoda  Recorded: 2026-11-01 12:00:00 UTC" + Environment.NewLine +
            "[2] Id 2002  Device: Garage  Recorded: 2026-11-01 13:00:00 UTC";

        items.ToCloudMediaListString(24).Should().Be(expected);
    }

    [Fact]
    public void Test_ToCloudMediaListString_EmptyList_PrintsNoEventsMessage()
    {
        var items = new List<ICloudMediaItem>();

        items.ToCloudMediaListString(24).Should().Be("No cloud video events in the last 24 hours.");
    }
}
