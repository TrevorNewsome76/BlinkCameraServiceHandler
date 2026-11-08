using BlinkCameraCall;
using BlinkCameraCall.Extensions;
using FluentAssertions;
using Xunit;

namespace BlinkCameraCallUnitTests.Extensions;

public class UnitTest_DeviceMediaListingExtension
{
    [Fact]
    public void Test_ToListingString_SortsCurrentFirstThenNewestDescending()
    {
        //assign
        var listing = new DeviceMediaListing(new List<DeviceMediaEntry>
        {
            new("2026-11-02_08-00-00", DeviceMediaKind.Local, new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc), null, null, null, null),
            new("2026-11-01_12-00-00", DeviceMediaKind.Cloud, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), null, null, null, null),
            new("current", DeviceMediaKind.Current, null, null, null, null, null),
        });
        var expected =
            "current  [current]" + Environment.NewLine +
            "2026-11-02_08-00-00  [local]" + Environment.NewLine +
            "2026-11-01_12-00-00  [cloud]";

        //act
        var actualResult = listing.ToListingString();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ToListingString_TiesKeepMergeOrder()
    {
        //assign
        var tiedTimestamp = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var listing = new DeviceMediaListing(new List<DeviceMediaEntry>
        {
            new("2026-11-01_12-00-00", DeviceMediaKind.Cloud, tiedTimestamp, null, null, null, null),
            new("2026-11-01_12-00-00_12", DeviceMediaKind.Local, tiedTimestamp, null, null, null, null),
        });
        var expected =
            "2026-11-01_12-00-00  [cloud]" + Environment.NewLine +
            "2026-11-01_12-00-00_12  [local]";

        //act
        var actualResult = listing.ToListingString();

        //assert
        actualResult.Should().Be(expected);
    }

    [Fact]
    public void Test_ToListingString_EmptyListing_ReturnsNoMediaMessage()
    {
        //assign
        var listing = new DeviceMediaListing(new List<DeviceMediaEntry>());

        //act
        var actualResult = listing.ToListingString();

        //assert
        actualResult.Should().Be("No media found for this device.");
    }
}
